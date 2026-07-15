using Microsoft.Extensions.Logging;
using SISLAB.Modules.Configuration.Infrastructure.Provisioning;
using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Infrastructure.Messaging;

/// <summary>
/// Reacts to the Identity module's <see cref="CompanyCreatedIntegrationEvent"/> to provision a brand-new
/// tenant's baseline configuration — the default 30-day expiry policy, the base item categories and the base
/// units (card [E12] #75b). It is the cross-module consumer the <c>CompanyCreated</c> fact was raised for.
///
/// <para><b>Eventual + retried (Option A — Outbox).</b> The event arrives from the Identity Outbox via
/// <see cref="IEventBus"/>, published by the background Outbox dispatcher AFTER the signup transaction has
/// committed. Because provisioning runs off the signup transaction, a failure here does not roll back (or
/// block) signup: it simply propagates so the Outbox dispatcher leaves the message unprocessed and retries it
/// on the next tick until it succeeds (at-least-once).</para>
///
/// <para><b>Idempotent.</b> Safe to run more than once — which the retry semantics require. The work is fully
/// delegated to <see cref="TenantConfigurationProvisioner"/>, which seeds at deterministic ids derived from
/// <c>(company, code/symbol)</c> and checks existence first, so a re-delivery never duplicates a seed. The
/// provisioner also opens its own auditable tenant-bypass scope (this is cross-tenant system work targeting a
/// specific company), so this handler needs no ambient tenant of its own.</para>
/// </summary>
internal sealed class ProvisionTenantOnCompanyCreatedHandler
    : IIntegrationEventHandler<CompanyCreatedIntegrationEvent>
{
    private readonly TenantConfigurationProvisioner _provisioner;
    private readonly ILogger<ProvisionTenantOnCompanyCreatedHandler> _logger;

    public ProvisionTenantOnCompanyCreatedHandler(
        TenantConfigurationProvisioner provisioner,
        ILogger<ProvisionTenantOnCompanyCreatedHandler> logger)
    {
        _provisioner = provisioner;
        _logger = logger;
    }

    public async Task HandleAsync(
        CompanyCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Provisioning baseline configuration for newly created company {CompanyId} ('{CompanyName}').",
            integrationEvent.CompanyId, integrationEvent.Name);

        // Let any failure propagate: the Outbox dispatcher must NOT mark the message processed, so it is
        // retried on the next tick. The provisioner is idempotent, so a retry after a partial run is safe.
        await _provisioner.ProvisionAsync(integrationEvent.CompanyId, cancellationToken);
    }
}
