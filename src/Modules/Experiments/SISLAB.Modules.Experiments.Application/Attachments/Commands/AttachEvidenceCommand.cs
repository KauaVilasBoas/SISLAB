using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Storage;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Attachments.Commands;

/// <summary>
/// Attaches a piece of evidence (photo/PDF of a hemogram laudo or external-reader result) to an animal's
/// reading/analysis (SISLAB-09). It is the two-step flow the operators do by hand today, made atomic: the file bytes go
/// to the storage port (local placeholder now, S3 later — card #53), the returned opaque key plus the descriptive
/// metadata are persisted as an <see cref="Attachment"/> aggregate, and the anexo↔animal↔análise link is validated
/// against the owning aggregate so evidence can never hang off a mismatched animal. Returns the new attachment id.
/// </summary>
/// <remarks>
/// <para>
/// <b>Target validation.</b> <see cref="OwnerId"/> is the owning aggregate that proves the link: for a
/// <see cref="AttachmentTargetKind.SampleAnalysis"/> it is the biobank sample id (the analysis must belong to it and the
/// sample must belong to the animal); for a <see cref="AttachmentTargetKind.ExperimentReading"/> it is the behavioural
/// experiment id (the measurement must belong to it and to the animal). A mismatch is a <see cref="NotFoundException"/> —
/// the handler never persists an orphan/cross-animal attachment.
/// </para>
/// <para>
/// <b>Boundaries.</b> The command carries a plain <see cref="Stream"/> (the controller adapts the ASP.NET
/// <c>IFormFile</c> into it), never an ASP.NET type, keeping the CQRS request infra-agnostic. The company is stamped by
/// the write-side tenant machinery on save; the uploader comes from the audit actor accessor, the instant from the
/// clock. <see cref="Origin"/> is a free-text provenance label (e.g. "Fiocruz") — never hardcoded.
/// </para>
/// </remarks>
public sealed record AttachEvidenceCommand(
    Guid AnimalId,
    AttachmentTargetKind TargetKind,
    Guid OwnerId,
    Guid TargetId,
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Origin) : ICommand<Guid>;

internal sealed class AttachEvidenceCommandValidator : AbstractValidator<AttachEvidenceCommand>
{
    public AttachEvidenceCommandValidator()
    {
        RuleFor(command => command.AnimalId).NotEmpty();
        RuleFor(command => command.TargetKind).IsInEnum();
        RuleFor(command => command.OwnerId).NotEmpty();
        RuleFor(command => command.TargetId).NotEmpty();
        RuleFor(command => command.Content).NotNull();
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(260);
        RuleFor(command => command.ContentType).NotEmpty().MaximumLength(120);
        RuleFor(command => command.SizeBytes).GreaterThan(0);
        RuleFor(command => command.Origin).MaximumLength(120);
    }
}

internal sealed class AttachEvidenceCommandHandler : ICommandHandler<AttachEvidenceCommand, Guid>
{
    private readonly IAttachmentRepository _attachments;
    private readonly ISampleRepository _samples;
    private readonly IExperimentRepository _experiments;
    private readonly IFileStorage _fileStorage;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public AttachEvidenceCommandHandler(
        IAttachmentRepository attachments,
        ISampleRepository samples,
        IExperimentRepository experiments,
        IFileStorage fileStorage,
        IAuditActorAccessor actorAccessor,
        ITenantContext tenantContext,
        IClock clock)
    {
        _attachments = attachments;
        _samples = samples;
        _experiments = experiments;
        _fileStorage = fileStorage;
        _actorAccessor = actorAccessor;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(AttachEvidenceCommand request, CancellationToken cancellationToken = default)
    {
        // Validate the anexo↔animal↔análise link BEFORE spending storage, so a mismatch never leaves an orphan file.
        AttachmentTarget target = await ResolveValidatedTargetAsync(request, cancellationToken);

        StoredFileKey storageKey = await _fileStorage.SaveAsync(
            request.Content,
            new FileStorageMetadata(request.FileName, request.ContentType),
            cancellationToken);

        Attachment attachment = Attachment.Register(
            _tenantContext.CompanyId,
            request.AnimalId,
            target,
            storageKey,
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            request.Origin,
            _actorAccessor.GetCurrentActor(),
            _clock.UtcNow);

        await _attachments.AddAsync(attachment, cancellationToken);

        return attachment.Id;
    }

    // Loads the owning aggregate and asserts the target reading/analysis exists on it AND belongs to the animal.
    private async Task<AttachmentTarget> ResolveValidatedTargetAsync(
        AttachEvidenceCommand request, CancellationToken cancellationToken)
    {
        switch (request.TargetKind)
        {
            case AttachmentTargetKind.SampleAnalysis:
            {
                Sample sample = await _samples.FindByIdAsync(request.OwnerId, cancellationToken)
                    ?? throw new NotFoundException($"Sample '{request.OwnerId}' was not found.");

                if (sample.AnimalId != request.AnimalId)
                    throw new NotFoundException(
                        $"Sample '{request.OwnerId}' does not belong to animal '{request.AnimalId}'.");

                bool analysisExists = sample.Analyses.Any(analysis => analysis.Id == request.TargetId);
                if (!analysisExists)
                    throw new NotFoundException(
                        $"Analysis '{request.TargetId}' was not found on sample '{request.OwnerId}'.");

                return AttachmentTarget.ForSampleAnalysis(request.TargetId);
            }

            case AttachmentTargetKind.ExperimentReading:
            {
                BehavioralExperiment experiment =
                    await _experiments.FindBehavioralExperimentAsync(request.OwnerId, cancellationToken)
                    ?? throw new NotFoundException($"Behavioural experiment '{request.OwnerId}' was not found.");

                bool readingMatchesAnimal = experiment.Measurements.Any(measurement =>
                    measurement.Id == request.TargetId && measurement.AnimalId == request.AnimalId);
                if (!readingMatchesAnimal)
                    throw new NotFoundException(
                        $"Reading '{request.TargetId}' for animal '{request.AnimalId}' was not found on " +
                        $"experiment '{request.OwnerId}'.");

                return AttachmentTarget.ForExperimentReading(request.TargetId);
            }

            default:
                throw new DomainException($"Unsupported attachment target kind '{request.TargetKind}'.");
        }
    }
}
