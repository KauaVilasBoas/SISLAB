using SISLAB.Modules.Experiments.Domain.Attachments.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Modules.Experiments.Domain.Attachments;

/// <summary>
/// A piece of evidence attached to a reading/analysis, by animal (SISLAB-09): the digital replacement for the manual
/// "tira foto das análises, cada animal é uma foto, pra colocar na planilha" flow. It is the photo/PDF the operator
/// uploads for a hemogram laudo or an external-reader result — kept here as its own aggregate that holds only the
/// <see cref="StorageKey"/> (the opaque handle to the bytes in <see cref="IFileStorage"/>) plus the metadata needed to
/// display, download and trace it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own aggregate, ids and file by value.</b> The attachment references the animal it belongs to and the
/// analysis/reading it documents only by value (an <see cref="AttachmentTarget"/>); it owns neither the biobank
/// <c>Sample</c> aggregate nor the experiment — no cross-aggregate FK/navigation, consistent with the rest of the
/// module. It never holds the file bytes, a path or a URL: only the <see cref="StorageKey"/>. The actual upload runs in
/// the Application layer through the storage port, so the domain is unaware of <i>where</i> the file lives (local
/// placeholder today, S3 tomorrow — card #53).
/// </para>
/// <para>
/// <b>Origin is a label, not a constant.</b> <see cref="Origin"/> (e.g. "Fiocruz", "Leitora externa") is a free-text
/// provenance label captured per attachment — never a value hardcoded in the code. A lab records whatever external
/// source produced the evidence.
/// </para>
/// </remarks>
public sealed class Attachment : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxFileNameLength = 260;
    private const int MaxContentTypeLength = 120;
    private const int MaxOriginLength = 120;
    private const int MaxActorLength = 200;

    // Parameterless constructor for EF Core materialization.
    private Attachment() : base(Guid.Empty)
    {
        StorageKey = default!;
        Target = default!;
        FileName = default!;
        ContentType = default!;
        UploadedBy = default!;
    }

    private Attachment(
        Guid id,
        Guid companyId,
        Guid animalId,
        AttachmentTarget target,
        StoredFileKey storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string? origin,
        string uploadedBy,
        DateTime uploadedAtUtc)
        : base(id)
    {
        CompanyId = companyId;
        AnimalId = animalId;
        Target = target;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Origin = origin;
        UploadedBy = uploadedBy;
        UploadedAtUtc = uploadedAtUtc;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>The study animal the evidence belongs to, referenced by value.</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>The reading/analysis the evidence documents (sample analysis or experiment reading), by value.</summary>
    public AttachmentTarget Target { get; private set; }

    /// <summary>The opaque storage handle to the file bytes (never a path/URL). Resolved via <see cref="IFileStorage"/>.</summary>
    public StoredFileKey StorageKey { get; private set; }

    /// <summary>The original client file name, for display and download.</summary>
    public string FileName { get; private set; }

    /// <summary>The MIME content type of the file (e.g. "image/jpeg", "application/pdf").</summary>
    public string ContentType { get; private set; }

    /// <summary>The size of the stored file in bytes.</summary>
    public long SizeBytes { get; private set; }

    /// <summary>Free-text provenance label (e.g. "Fiocruz", "Leitora externa"), or null when not recorded. Never hardcoded.</summary>
    public string? Origin { get; private set; }

    /// <summary>Actor who uploaded the evidence (identity claim).</summary>
    public string UploadedBy { get; private set; }

    /// <summary>Instant (UTC) the evidence was uploaded.</summary>
    public DateTime UploadedAtUtc { get; private set; }

    /// <summary>
    /// Registers a piece of evidence for an animal's reading/analysis, after its bytes were persisted to storage and
    /// the resulting <paramref name="storageKey"/> obtained. Validates the descriptive metadata and raises the created
    /// event. The company is stamped by the write-side tenant machinery on save; the "who"/"when" come from the caller.
    /// </summary>
    public static Attachment Register(
        Guid companyId,
        Guid animalId,
        AttachmentTarget target,
        StoredFileKey storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string? origin,
        string uploadedBy,
        DateTime uploadedAtUtc)
    {
        Guard.AgainstEmptyGuid(companyId, nameof(companyId));
        Guard.AgainstEmptyGuid(animalId, nameof(animalId));
        Guard.AgainstNull(target, nameof(target));
        Guard.AgainstNull(storageKey, nameof(storageKey));

        string trimmedFileName = Guard.AgainstNullOrWhiteSpace(fileName, nameof(fileName)).Trim();
        Guard.AgainstMaxLength(trimmedFileName, MaxFileNameLength, nameof(fileName));

        string trimmedContentType = Guard.AgainstNullOrWhiteSpace(contentType, nameof(contentType)).Trim();
        Guard.AgainstMaxLength(trimmedContentType, MaxContentTypeLength, nameof(contentType));

        Guard.AgainstNegative(sizeBytes, nameof(sizeBytes));

        string trimmedUploadedBy = Guard.AgainstNullOrWhiteSpace(uploadedBy, nameof(uploadedBy)).Trim();
        Guard.AgainstMaxLength(trimmedUploadedBy, MaxActorLength, nameof(uploadedBy));

        string? trimmedOrigin = NormalizeOrigin(origin);

        var attachment = new Attachment(
            Guid.NewGuid(),
            companyId,
            animalId,
            target,
            storageKey,
            trimmedFileName,
            trimmedContentType,
            sizeBytes,
            trimmedOrigin,
            trimmedUploadedBy,
            uploadedAtUtc);

        attachment.RaiseDomainEvent(new AttachmentRegisteredEvent(
            companyId, attachment.Id, animalId, target.Kind, target.TargetId));

        return attachment;
    }

    private static string? NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return null;

        string trimmed = origin.Trim();
        Guard.AgainstMaxLength(trimmed, MaxOriginLength, nameof(origin));
        return trimmed;
    }
}
