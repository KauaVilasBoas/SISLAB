using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.Modules.Configuration.Domain.Units;
using SISLAB.Modules.Configuration.Infrastructure.Messaging;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;
using SISLAB.Modules.Configuration.Infrastructure.Provisioning;
using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Tests.Infrastructure.Messaging;

/// <summary>
/// Covers the cross-module reaction to <see cref="CompanyCreatedIntegrationEvent"/> (card [E12] #75b): the
/// handler provisions a brand-new tenant's baseline configuration by delegating to the idempotent
/// <see cref="TenantConfigurationProvisioner"/>. Exercised against a real <see cref="ConfigurationDbContext"/>
/// on the EF Core InMemory provider — no live database.
///
/// <para>Idempotency is the load-bearing property for the Outbox retry semantics ("if it fails, it comes
/// back"): the background dispatcher redelivers the event on failure, so handling the same company twice must
/// NOT duplicate the seeded rows. The last test proves exactly that.</para>
/// </summary>
public sealed class ProvisionTenantOnCompanyCreatedHandlerTests
{
    private static readonly Guid Company = Guid.NewGuid();

    [Fact]
    public async Task Handling_the_event_seeds_the_baseline_configuration_for_the_company()
    {
        using ConfigurationDbContext context = CreateInMemoryContext();
        ProvisionTenantOnCompanyCreatedHandler handler = CreateHandler(context);

        await handler.HandleAsync(EventFor(Company));

        Assert.Equal(DefaultItemCategories.Catalogue.Count, await CountCategories(context));
        Assert.Equal(DefaultUnits.Catalogue.Count, await CountUnits(context));
        Assert.Equal(1, await CountExpiryPolicies(context));
    }

    [Fact]
    public async Task Handling_the_same_event_twice_does_not_duplicate_the_seeds()
    {
        using ConfigurationDbContext context = CreateInMemoryContext();
        ProvisionTenantOnCompanyCreatedHandler handler = CreateHandler(context);

        // Simulates an Outbox redelivery (at-least-once): the second call must be a no-op on already-seeded data.
        await handler.HandleAsync(EventFor(Company));
        await handler.HandleAsync(EventFor(Company));

        Assert.Equal(DefaultItemCategories.Catalogue.Count, await CountCategories(context));
        Assert.Equal(DefaultUnits.Catalogue.Count, await CountUnits(context));
        Assert.Equal(1, await CountExpiryPolicies(context));
    }

    [Fact]
    public async Task Two_companies_are_provisioned_independently()
    {
        using ConfigurationDbContext context = CreateInMemoryContext();
        ProvisionTenantOnCompanyCreatedHandler handler = CreateHandler(context);
        Guid other = Guid.NewGuid();

        await handler.HandleAsync(EventFor(Company));
        await handler.HandleAsync(EventFor(other));

        // Each company gets its own full catalogue at its own deterministic ids.
        Assert.Equal(DefaultItemCategories.Catalogue.Count * 2, await CountCategories(context));
        Assert.Equal(2, await CountExpiryPolicies(context));
    }

    private static CompanyCreatedIntegrationEvent EventFor(Guid companyId)
        => new(Guid.NewGuid(), DateTime.UtcNow, companyId, "Acme Labs", Guid.NewGuid());

    private static ProvisionTenantOnCompanyCreatedHandler CreateHandler(ConfigurationDbContext context)
    {
        var bypass = new TenantBypass(NullLogger<TenantBypass>.Instance);
        var provisioner = new TenantConfigurationProvisioner(
            context, bypass, NullLogger<TenantConfigurationProvisioner>.Instance);

        return new ProvisionTenantOnCompanyCreatedHandler(
            provisioner, NullLogger<ProvisionTenantOnCompanyCreatedHandler>.Instance);
    }

    // Built without tenant services (design-time ctor path), so no query filter is applied — the counts below
    // see every seeded row across companies. The provisioner opens its own ITenantBypass for the write.
    private static ConfigurationDbContext CreateInMemoryContext()
    {
        DbContextOptions<ConfigurationDbContext> options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ConfigurationDbContext(options);
    }

    private static Task<int> CountCategories(ConfigurationDbContext context)
        => context.ItemCategories.AsNoTracking().CountAsync();

    private static Task<int> CountUnits(ConfigurationDbContext context)
        => context.Units.AsNoTracking().CountAsync();

    private static Task<int> CountExpiryPolicies(ConfigurationDbContext context)
        => context.ExpiryPolicies.AsNoTracking().CountAsync();
}
