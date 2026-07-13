using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners.Commands;

/// <summary>Puts a deactivated partner back in service. Idempotent at the aggregate level.</summary>
public sealed record ReactivatePartnerCommand(Guid PartnerId) : ICommand;

internal sealed class ReactivatePartnerCommandValidator : AbstractValidator<ReactivatePartnerCommand>
{
    public ReactivatePartnerCommandValidator()
        => RuleFor(command => command.PartnerId).NotEmpty();
}

internal sealed class ReactivatePartnerCommandHandler : ICommandHandler<ReactivatePartnerCommand>
{
    private readonly IPartnerRepository _partners;

    public ReactivatePartnerCommandHandler(IPartnerRepository partners) => _partners = partners;

    public async Task<Unit> HandleAsync(
        ReactivatePartnerCommand request,
        CancellationToken cancellationToken = default)
    {
        Partner partner = await _partners.FindByIdAsync(request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        partner.Reactivate();

        await _partners.UpdateAsync(partner, cancellationToken);

        return Unit.Value;
    }
}
