using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners;

/// <summary>
/// Registers a new partner (supplier/client/both) for the active company. The contact e-mail and
/// document are optional; the partner starts active. The company is taken from <c>ITenantContext</c> and
/// stamped by the tenant save interceptor, never from the payload.
/// </summary>
public sealed record RegisterPartnerCommand(
    string Name,
    PartnerType Type,
    string? Document,
    string? ContactEmail,
    string? Description) : ICommand<Guid>;

internal sealed class RegisterPartnerCommandValidator : AbstractValidator<RegisterPartnerCommand>
{
    public RegisterPartnerCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Type).IsInEnum();
    }
}

internal sealed class RegisterPartnerCommandHandler : ICommandHandler<RegisterPartnerCommand, Guid>
{
    private readonly IPartnerRepository _partners;

    public RegisterPartnerCommandHandler(IPartnerRepository partners) => _partners = partners;

    public async Task<Guid> HandleAsync(
        RegisterPartnerCommand request,
        CancellationToken cancellationToken = default)
    {
        Partner partner = Partner.Register(
            request.Name,
            request.Type,
            request.Document,
            request.ContactEmail,
            request.Description);

        await _partners.AddAsync(partner, cancellationToken);

        return partner.Id;
    }
}
