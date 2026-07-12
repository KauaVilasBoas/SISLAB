using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners;

/// <summary>Removes a previously recorded sample from a partner, identified by its reference.</summary>
public sealed record RemovePartnerSampleCommand(
    Guid PartnerId,
    string Reference) : ICommand;

internal sealed class RemovePartnerSampleCommandValidator : AbstractValidator<RemovePartnerSampleCommand>
{
    public RemovePartnerSampleCommandValidator()
    {
        RuleFor(command => command.PartnerId).NotEmpty();
        RuleFor(command => command.Reference).NotEmpty();
    }
}

internal sealed class RemovePartnerSampleCommandHandler : ICommandHandler<RemovePartnerSampleCommand>
{
    private readonly IPartnerRepository _partners;

    public RemovePartnerSampleCommandHandler(IPartnerRepository partners) => _partners = partners;

    public async Task<Unit> HandleAsync(
        RemovePartnerSampleCommand request,
        CancellationToken cancellationToken = default)
    {
        Partner partner = await _partners.FindByIdAsync(request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        partner.RemoveSample(request.Reference);

        await _partners.UpdateAsync(partner, cancellationToken);

        return Unit.Value;
    }
}
