using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using SISLAB.Api.Csrf;
using SISLAB.Api.Middleware;
using SISLAB.Api.Observability;
using SISLAB.Api.Security;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Jobs.DependencyInjection;
using SISLAB.Modules.Audit.Application;
using SISLAB.Modules.Configuration.Application;
using SISLAB.Modules.Identity.Application;
using SISLAB.Modules.Inventory.Application;
using SISLAB.Modules.Notifications.Application;
using SISLAB.SharedKernel.Observability;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Structured logging (card [E9] #56) — Serilog with the CorrelationId enricher.
// Console JSON always; ships to Coralogix over HTTP when Coralogix:ApiKey is set
// (absent in dev → Console only). Replaces the default Microsoft logger.
// ---------------------------------------------------------------------------
builder.Host.UseSerilog(SerilogConfiguration.Configure);

// ---------------------------------------------------------------------------
// Shared infrastructure (lightweight mediator, IClock, DbConnectionFactory)
// ---------------------------------------------------------------------------
builder.Services.AddSislabInfrastructure();

// ---------------------------------------------------------------------------
// Request correlation (card [E9] #56). The Host owns the request-aware accessor,
// populated by CorrelationIdMiddleware from the X-Correlation-Id header; it
// overrides the ambient fallback registered by AddSislabInfrastructure. Registered
// as the concrete type too so the middleware can set the resolved id.
// ---------------------------------------------------------------------------
builder.Services.AddScoped<CorrelationIdAccessor>();
builder.Services.Replace(
    ServiceDescriptor.Scoped<ICorrelationIdAccessor>(sp => sp.GetRequiredService<CorrelationIdAccessor>()));

// ---------------------------------------------------------------------------
// Composition Root — module discovery and registration via assembly scanning.
// Load order is determined by IModule.Order (lower = first).
// Convention: Identity = 10, Inventory = 20, Notifications = 30, Audit = 40, Configuration = 50.
// ---------------------------------------------------------------------------
Assembly[] moduleAssemblies =
[
    typeof(IdentityModule).Assembly,
    typeof(InventoryModule).Assembly,
    typeof(NotificationsModule).Assembly,
    typeof(AuditModule).Assembly,
    typeof(ConfigurationModule).Assembly
];

ModuleLoader.RegisterModules(builder.Services, builder.Configuration, moduleAssemblies);

// ---------------------------------------------------------------------------
// Background jobs (E6 #39) — run in-process with the API (Fork #1 → C).
// Registered AFTER the modules because the Outbox dispatcher job depends on the
// module-contributed IOutboxDbContext / write-side DbContext. Registers the
// scheduled IHostedService(s), the SISLAB IEventBus, the Outbox dispatcher and
// the settable ambient tenant context for background work.
// ---------------------------------------------------------------------------
builder.Services.AddSislabJobs(builder.Configuration);

// ---------------------------------------------------------------------------
// Swagger / OpenAPI (development only)
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "SISLAB API",
        Version = "v1",
        Description = "Laboratory management system — .NET 8 modular monolith"
    });
});

// ---------------------------------------------------------------------------
// Health Checks
// The PostgreSQL health check reports 'Unhealthy' when the database is
// unavailable, but it does NOT block API startup — intentional for E0.
// ---------------------------------------------------------------------------
string? pgConnectionString = builder.Configuration.GetConnectionString("SislabDb");

IHealthChecksBuilder healthChecks = builder.Services
    .AddHealthChecks();

if (!string.IsNullOrWhiteSpace(pgConnectionString))
{
    healthChecks.AddNpgSql(
        connectionString: pgConnectionString,
        name: "postgresql",
        tags: ["db", "ready"]);
}

// ---------------------------------------------------------------------------
// CORS base (origins tightened in E7 when the React SPA comes online)
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("SislabCorsPolicy", policy =>
    {
        string[] allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins);
        else
            policy.AllowAnyOrigin(); // dev only — no explicit origin configured

        policy
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// CSRF protection (ASP.NET Core antiforgery) for the cookie-authenticated SPA.
// The SPA arms it via GET /api/auth/csrf; CsrfValidationMiddleware enforces it
// on state-changing requests that carry the XSRF-TOKEN cookie.
// ---------------------------------------------------------------------------
builder.Services.AddSislabAntiforgery();

// ---------------------------------------------------------------------------
// ProblemDetails (card [E9] #59) — RFC 7807 error contract. Registers the
// ProblemDetailsService used for framework-generated problems; SISLAB's own
// ExceptionHandlingMiddleware writes the domain/application error shapes.
// ---------------------------------------------------------------------------
builder.Services.AddProblemDetails();

// ---------------------------------------------------------------------------
// Rate limiting (card [E9] #58) — per-IP fixed windows: 10/min on /api/auth/*
// ("login") and 300/min elsewhere ("api"). Rejected requests get 429 with the
// uniform ApiResult envelope.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(RateLimitingConfiguration.Configure);

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------
WebApplication app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------

// First in the pipeline so it wraps every downstream middleware and endpoint,
// translating domain/application exceptions into the RFC 7807 ProblemDetails response.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Correlation id (card [E9] #56) — resolves X-Correlation-Id (or generates one), stores it on the
// scoped accessor for the LoggingBehavior/ProblemDetails traceId, and echoes it on the response. Runs
// right after the exception boundary so every downstream log line and error carries the id.
app.UseMiddleware<CorrelationIdMiddleware>();

// Security headers (card [E9] #58) — a baseline of browser-hardening headers on every response.
// Placed early so it also covers Swagger, health checks and error responses.
app.UseMiddleware<SecurityHeadersMiddleware>();

// HSTS outside development only (never over plain HTTP in local dev). Complements the security headers
// above with Strict-Transport-Security, pinning HTTPS for future requests.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Serilog request logging — one structured summary line per request (method, path, status, elapsed),
// enriched with the CorrelationId pushed onto the LogContext below.
app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        diagnosticContext.Set(
            ObservabilityConstants.CorrelationIdProperty,
            httpContext.RequestServices.GetRequiredService<ICorrelationIdAccessor>().CorrelationId));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SISLAB API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// Rate limiting (card [E9] #58) — enforce the per-IP windows before authentication so abusive traffic is
// shed cheaply, ahead of any DB-touching auth/tenant work.
app.UseRateLimiter();
app.UseCors("SislabCorsPolicy");

// Lumen AuthN — resolves the principal from the JWT Bearer token.
// Lumen registers a FallbackPolicy that requires authentication on all
// endpoints that do not carry [AllowAnonymous].
app.UseAuthentication();

// Tenant resolution — MUST run BETWEEN UseAuthentication and UseAuthorization.
// Needs the authenticated principal (to validate company membership in the DB),
// and must populate ITenantScopeAccessor BEFORE UseAuthorization so that
// Lumen's PermissionAuthorizationHandler ([RequirePermission]) finds the
// active company scope. Running it after UseAuthorization = 403 on every
// tenant-scoped endpoint even when the user has the correct permission.
app.UseSislabTenantResolution();

// CSRF enforcement — runs after authentication (so exemptions can reason about the
// request) and before authorization, short-circuiting forged state-changing requests
// with 403 before they reach any endpoint.
app.UseMiddleware<CsrfValidationMiddleware>();

app.UseAuthorization();

// Health check endpoint — public (AllowAnonymous bypasses Lumen's FallbackPolicy).
app.MapHealthChecks("/health").AllowAnonymous();

// CSRF token endpoint — public. Issues the readable XSRF-TOKEN cookie for the SPA.
app.MapCsrfEndpoints();

// Modules map their own Minimal API endpoints
ModuleLoader.MapModuleEndpoints(app);

// MVC controllers from modules (e.g. admin endpoints decorated with [RequirePermission]).
// Modules register their ApplicationParts in RegisterServices (AddControllers().AddApplicationPart).
// Lumen's FallbackPolicy requires authentication; [Authorize]/[RequirePermission] refine per action.
app.MapControllers();

app.Run();

// Required for xUnit WebApplicationFactory in future integration tests
public partial class Program { }
