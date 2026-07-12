using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners;

/// <summary>
/// Records a sample/compound a partner sent for testing (for example "GDA-92 · pendente"). This is the
/// incremental registration of samples on an existing partner; duplicate references are rejected by the
/// aggregate.
/// </summary>
public sealed record RecordPartnerSampleCommand(
    Guid PartnerId,
    string Reference,
    string? Status) : ICommand;

internal sealed class RecordPartnerSampleCommandValidator : AbstractValidator<RecordPartnerSampleCommand>
{
    public RecordPartnerSampleCommandValidator()
    {
        RuleFor(command => command.PartnerId).NotEmpty();
        RuleFor(command => command.Reference).NotEmpty().MaximumLength(120);
    }
}

internal sealed class RecordPartnerSampleCommandHandler : ICommandHandler<RecordPartnerSampleCommand>
{
    private readonly IPartnerRepository _partners;

    public RecordPartnerSampleCommandHandler(IPartnerRepository partners) => _partners = partners;

    public async Task<Unit> HandleAsync(
        RecordPartnerSampleCommand request,
        CancellationToken cancellationToken = default)
    {
        Partner partner = await _partners.FindByIdAsync(request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        partner.RecordSample(SampleNote.Create(request.Reference, request.Status));

        await _partners.UpdateAsync(partner, cancellationToken);

        return Unit.Value;
    }
}
