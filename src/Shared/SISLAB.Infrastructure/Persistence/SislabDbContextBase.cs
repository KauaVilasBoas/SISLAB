using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for all SISLAB modules.
/// Each module inherits this and registers its configurations via OnModelCreating.
///
/// Provides two cross-cutting concerns for every module:
/// <list type="bullet">
///   <item>snake_case naming for all tables, columns, keys and indexes;</item>
///   <item>write-side multi-tenancy for <see cref="ITenantEntity"/> — a global query filter
///   that narrows reads to the active company, plus a save interceptor
///   (<see cref="TenantStampingInterceptor"/>) that stamps and guards <c>company_id</c>.</item>
/// </list>
///
/// The tenant services are optional: at design time (migrations) and in system/background
/// contexts the context may be built without them, in which case the tenant filter is not
/// applied and every row is visible — safe for migrations and explicit bypass scenarios.
/// </summary>
public abstract class SislabDbContextBase : DbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly ITenantBypass? _tenantBypass;

    protected SislabDbContextBase(DbContextOptions options) : base(options) { }

    protected SislabDbContextBase(
        DbContextOptions options,
        ITenantContext? tenantContext,
        ITenantBypass? tenantBypass) : base(options)
    {
        _tenantContext = tenantContext;
        _tenantBypass = tenantBypass;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplyTenantQueryFilter(modelBuilder);
        ApplySnakeCaseNamingConvention(modelBuilder);
    }

    /// <summary>
    /// Active company used by the global query filter. Reading it through an instance member of
    /// the context (instead of capturing the service directly) is the EF Core-recommended pattern:
    /// EF evaluates the member per context instance, so the compiled model is cached once yet each
    /// request/scope filters by its own tenant.
    /// </summary>
    protected Guid TenantFilterCompanyId => _tenantContext?.CompanyId ?? Guid.Empty;

    /// <summary>
    /// Whether an auditable <see cref="ITenantBypass"/> scope is currently open. When <c>true</c>
    /// the tenant filter short-circuits and every company's rows are visible (system/background work).
    /// </summary>
    protected bool TenantFilterBypassed => _tenantBypass?.IsActive ?? false;

    /// <summary>
    /// Applies a global query filter to every mapped <see cref="ITenantEntity"/> so that
    /// EF Core reads only return rows for the active company. The predicate is
    /// <c>ctx.TenantFilterBypassed || e.CompanyId == ctx.TenantFilterCompanyId</c>, referencing the
    /// context instance so EF re-evaluates it per request. No filter is applied at design time
    /// (migrations) when the tenant context is absent.
    /// </summary>
    private void ApplyTenantQueryFilter(ModelBuilder modelBuilder)
    {
        if (_tenantContext is null)
            return;

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Skip derived types in TPH hierarchies — EF Core only allows HasQueryFilter on the
            // root entity type. The filter applied to the root automatically covers all subtypes.
            if (entityType.BaseType is not null)
                continue;

            LambdaExpression filter = BuildTenantFilter(entityType.ClrType);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    /// <summary>
    /// Builds <c>e =&gt; this.TenantFilterBypassed || e.CompanyId == this.TenantFilterCompanyId</c>
    /// strongly typed to <paramref name="entityClrType"/>. The context members are read against a
    /// constant reference to <c>this</c>, which EF Core resolves per context instance.
    /// </summary>
    private LambdaExpression BuildTenantFilter(Type entityClrType)
    {
        ParameterExpression parameter = Expression.Parameter(entityClrType, "e");
        ConstantExpression self = Expression.Constant(this);

        // e.CompanyId == this.TenantFilterCompanyId
        MemberExpression entityCompanyId =
            Expression.Property(parameter, nameof(ITenantEntity.CompanyId));
        MemberExpression activeCompanyId =
            Expression.Property(self, nameof(TenantFilterCompanyId));
        BinaryExpression matchesTenant = Expression.Equal(entityCompanyId, activeCompanyId);

        // this.TenantFilterBypassed
        MemberExpression bypassActive = Expression.Property(self, nameof(TenantFilterBypassed));

        BinaryExpression body = Expression.OrElse(bypassActive, matchesTenant);
        return Expression.Lambda(body, parameter);
    }

    private static void ApplySnakeCaseNamingConvention(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } tableName)
                entity.SetTableName(ToSnakeCase(tableName));

            // Owned entity types (value objects mapped to the owner's table via table splitting)
            // share the principal's primary key column. Their non-key columns are already named
            // explicitly by each configuration (HasColumnName), and their shared key/FK columns must
            // keep the principal's column name — renaming them here would split the shared column into
            // a divergent name (e.g. stock_item_id vs id) and break the mapping. So the whole
            // snake_case pass is skipped for owned types.
            if (entity.IsOwned())
                continue;

            foreach (IMutableProperty property in entity.GetProperties())
            {
                if (property.GetColumnName() is { } columnName)
                    property.SetColumnName(ToSnakeCase(columnName));
            }

            foreach (IMutableKey key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));

            foreach (IMutableForeignKey fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName() ?? string.Empty));

            foreach (IMutableIndex index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
        }
    }

    /// <summary>
    /// Registers the tenant-stamping save interceptor when the tenant services are available.
    /// Applied here (rather than at registration) so every module DbContext inherits the
    /// behavior without extra wiring.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        if (_tenantContext is not null && _tenantBypass is not null)
            optionsBuilder.AddInterceptors(new TenantStampingInterceptor(_tenantContext, _tenantBypass));
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;

        var snakeCaseChars = new List<char>(name.Length + 4);

        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];

            if (char.IsUpper(current))
            {
                bool isFirstChar = i == 0;
                bool previousIsLower = i > 0 && char.IsLower(name[i - 1]);
                bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (!isFirstChar && (previousIsLower || nextIsLower))
                    snakeCaseChars.Add('_');

                snakeCaseChars.Add(char.ToLowerInvariant(current));
            }
            else
            {
                snakeCaseChars.Add(current);
            }
        }

        return new string(snakeCaseChars.ToArray());
    }
}
