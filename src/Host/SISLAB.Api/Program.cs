using System.Reflection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
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
// Build
// ---------------------------------------------------------------------------
WebApplication app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
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

app.UseAuthorization();

// Health check endpoint — public (AllowAnonymous bypasses Lumen's FallbackPolicy).
app.MapHealthChecks("/health").AllowAnonymous();

// Modules map their own Minimal API endpoints
ModuleLoader.MapModuleEndpoints(app);

// MVC controllers from modules (e.g. admin endpoints decorated with [RequirePermission]).
// Modules register their ApplicationParts in RegisterServices (AddControllers().AddApplicationPart).
// Lumen's FallbackPolicy requires authentication; [Authorize]/[RequirePermission] refine per action.
app.MapControllers();

app.Run();

// Required for xUnit WebApplicationFactory in future integration tests
public partial class Program { }
