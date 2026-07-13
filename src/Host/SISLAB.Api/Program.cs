using System.Reflection;
using SISLAB.Api.Csrf;
using SISLAB.Api.Middleware;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Jobs.DependencyInjection;
using SISLAB.Modules.Identity.Application;
using SISLAB.Modules.Inventory.Application;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Shared infrastructure (lightweight mediator, IClock, DbConnectionFactory)
// ---------------------------------------------------------------------------
builder.Services.AddSislabInfrastructure();

// ---------------------------------------------------------------------------
// Composition Root — module discovery and registration via assembly scanning.
// Load order is determined by IModule.Order (lower = first).
// Convention: Identity = 10, Inventory = 20.
// ---------------------------------------------------------------------------
Assembly[] moduleAssemblies =
[
    typeof(IdentityModule).Assembly,
    typeof(InventoryModule).Assembly
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
// Build
// ---------------------------------------------------------------------------
WebApplication app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------

// First in the pipeline so it wraps every downstream middleware and endpoint,
// translating domain/application exceptions into the uniform ApiResult envelope.
app.UseMiddleware<ExceptionHandlingMiddleware>();

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
