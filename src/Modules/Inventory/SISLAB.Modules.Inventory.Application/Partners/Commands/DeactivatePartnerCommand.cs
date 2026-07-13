using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners.Commands;

/// <summary>
/// Takes a partner out of service. A deactivated partner is kept for the traceability of past stock
/// entries but can no longer be selected as the origin of a new one. Idempotent at the aggregate level.
/// </summary>
public sealed record DeactivatePartnerCommand(Guid PartnerId) : ICommand;

internal sealed class DeactivatePartnerCommandValidator : AbstractValidator<DeactivatePartnerCommand>
{
    public DeactivatePartnerCommandValidator()
        => RuleFor(command => command.PartnerId).NotEmpty();
}

internal sealed class DeactivatePartnerCommandHandler : ICommandHandler<DeactivatePartnerCommand>
{
    private readonly IPartnerRepository _partners;

    public DeactivatePartnerCommandHandler(IPartnerRepository partners) => _partners = partners;

    public async Task<Unit> HandleAsync(
        DeactivatePartnerCommand request,
        CancellationToken cancellationToken = default)
    {
        Partner partner = await _partners.FindByIdAsync(request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        partner.Deactivate();

        await _partners.UpdateAsync(partner, cancellationToken);

        return Unit.Value;
    }
}
