using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners;

/// <summary>
/// Updates a partner's descriptive data (name, type, document, contact e-mail, description). Passing a
/// null/blank document, e-mail or description clears the corresponding field. Does not change the active
/// status nor the recorded samples; those have their own operations.
/// </summary>
public sealed record UpdatePartnerCommand(
    Guid PartnerId,
    string Name,
    PartnerType Type,
    string? Document,
    string? ContactEmail,
    string? Description) : ICommand;

internal sealed class UpdatePartnerCommandValidator : AbstractValidator<UpdatePartnerCommand>
{
    public UpdatePartnerCommandValidator()
    {
        RuleFor(command => command.PartnerId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Type).IsInEnum();
    }
}

internal sealed class UpdatePartnerCommandHandler : ICommandHandler<UpdatePartnerCommand>
{
    private readonly IPartnerRepository _partners;

    public UpdatePartnerCommandHandler(IPartnerRepository partners) => _partners = partners;

    public async Task<Unit> HandleAsync(
        UpdatePartnerCommand request,
        CancellationToken cancellationToken = default)
    {
        Partner partner = await _partners.FindByIdAsync(request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        partner.Rename(request.Name);
        partner.ChangeType(request.Type);
        partner.UpdateDocument(request.Document);
        partner.UpdateContactEmail(request.ContactEmail);
        partner.DescribeAs(request.Description);

        await _partners.UpdateAsync(partner, cancellationToken);

        return Unit.Value;
    }
}
