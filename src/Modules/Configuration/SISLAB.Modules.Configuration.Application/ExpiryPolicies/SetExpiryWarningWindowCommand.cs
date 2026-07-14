using FluentValidation;
using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.ExpiryPolicies;

/// <summary>
/// Sets the active company's expiry warning window (card [E12] #76) — how many days ahead of a batch's last
/// valid day stock is flagged as "expiring soon". Write-side: it upserts the tenant's singleton
/// <see cref="ExpiryPolicy"/> (creating it on first configuration, updating it thereafter) through the domain
/// behaviour, and lets the unit of work commit. The window's positive/sensible-range invariant lives in the
/// aggregate.
/// </summary>
public sealed record SetExpiryWarningWindowCommand(int WarningWindowDays) : ICommand;

internal sealed class SetExpiryWarningWindowCommandValidator : AbstractValidator<SetExpiryWarningWindowCommand>
{
    public SetExpiryWarningWindowCommandValidator()
        => RuleFor(command => command.WarningWindowDays).GreaterThan(0);
}

internal sealed class SetExpiryWarningWindowCommandHandler : ICommandHandler<SetExpiryWarningWindowCommand>
{
    private readonly IExpiryPolicyRepository _policies;

    public SetExpiryWarningWindowCommandHandler(IExpiryPolicyRepository policies) => _policies = policies;

    public async Task<Unit> HandleAsync(
        SetExpiryWarningWindowCommand request,
        CancellationToken cancellationToken = default)
    {
        ExpiryPolicy? policy = await _policies.GetAsync(cancellationToken);

        if (policy is null)
        {
            // First time the tenant configures a window: create the singleton policy for the active company.
            policy = ExpiryPolicy.Create(request.WarningWindowDays);
            await _policies.AddAsync(policy, cancellationToken);
        }
        else
        {
            policy.ChangeWarningWindow(request.WarningWindowDays);
            await _policies.UpdateAsync(policy, cancellationToken);
        }

        return Unit.Value;
    }
}
