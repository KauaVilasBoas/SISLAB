using System.Reflection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Identity.Application;
using SISLAB.Modules.Inventory.Application;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Infraestrutura compartilhada (mediator leve, IClock, DbConnectionFactory)
// ---------------------------------------------------------------------------
builder.Services.AddSislabInfrastructure();

// ---------------------------------------------------------------------------
// Composition Root — descoberta e registro de módulos por assembly scanning.
// A ordem de carregamento é determinada por IModule.Order (menor = primeiro).
// Convenção: Identity=10, Inventory=20.
// ---------------------------------------------------------------------------
Assembly[] moduleAssemblies =
[
    typeof(IdentityModule).Assembly,
    typeof(InventoryModule).Assembly
];

ModuleLoader.RegisterModules(builder.Services, builder.Configuration, moduleAssemblies);

// ---------------------------------------------------------------------------
// Swagger / OpenAPI (apenas em desenvolvimento)
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "SISLAB API",
        Version = "v1",
        Description = "Sistema de gestão de laboratório — monólito modular .NET"
    });
});

// ---------------------------------------------------------------------------
// Health Checks
// NOTA: O health check do PostgreSQL reportará 'Unhealthy' se o banco não estiver
// disponível, mas o startup da API NÃO será bloqueado. Comportamento intencional
// para o E0 — banco real será exigido a partir do E1.
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
// CORS base (origens refinadas no E7 — SPA React)
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
            policy.AllowAnyOrigin(); // Apenas em dev sem configuração explícita

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
// Pipeline HTTP
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

// AuthN/AuthZ da Lumen (JWT Bearer) — registrados via AddLumenIdentity no módulo Identity.
// UseAuthentication resolve o principal a partir do JWT; UseAuthorization aplica policies
// (a Lumen registra um FallbackPolicy que exige usuário autenticado em endpoints sem AllowAnonymous).
app.UseAuthentication();
app.UseAuthorization();

// Resolução de tenant (company ativa via cookie httpOnly) — DEPOIS da AuthN/AuthZ,
// pois valida a company contra company_user usando o usuário já autenticado.
app.UseSislabTenantResolution();

// Health check endpoint — público (AllowAnonymous escapa o FallbackPolicy da Lumen).
app.MapHealthChecks("/health").AllowAnonymous();

// Módulos mapeiam seus próprios endpoints (Minimal API)
ModuleLoader.MapModuleEndpoints(app);

// Controllers MVC dos módulos (ex.: endpoints administrativos decorados com [RequirePermission]).
// Os módulos registram seus ApplicationParts em RegisterServices (AddControllers().AddApplicationPart).
// O FallbackPolicy da Lumen exige autenticação; [Authorize]/[RequirePermission] refinam por ação.
app.MapControllers();

app.Run();

// Necessário para xUnit WebApplicationFactory em testes de integração futuros
public partial class Program { }
