namespace SISLAB.SharedKernel.Storage;

/// <summary>
/// Transversal port for binary file storage (SISLAB-09): the seam through which the application persists and later
/// retrieves evidence files (hemogram photos, external-reader laudos, PDFs) without knowing <i>where</i> or <i>how</i>
/// the bytes live. It is a pure abstraction — no filesystem, no cloud SDK — so the domain and application layers depend
/// only on this contract and stay decoupled from the concrete backing store.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why here.</b> Like <c>IClock</c> and <c>ITenantContext</c>, file storage is a cross-cutting capability every module
/// may need, so its <i>contract</i> lives in the pure SharedKernel while its <i>implementation</i> lives in
/// Infrastructure. The SharedKernel purity rule (no infra) is what keeps this an interface plus plain value types only.
/// </para>
/// <para>
/// <b>Domain never holds the file.</b> A <see cref="SaveAsync"/> call returns an opaque <see cref="StoredFileKey"/>; the
/// aggregate persists only that key (plus its own metadata) — never a stream, a path or a URL. The upload itself is
/// performed in the Application/Infrastructure layer through this port, so swapping the local adapter for S3 (card #53)
/// changes nothing in the domain: only the DI registration of the implementation changes.
/// </para>
/// <para>
/// <b>Adapters.</b> The default registered implementation is a local placeholder (filesystem/in-memory) explicitly
/// marked as such until the S3 adapter arrives. Any adapter must round-trip a saved stream: what
/// <see cref="OpenReadAsync"/> returns for a key must be byte-identical to what <see cref="SaveAsync"/> was given.
/// </para>
/// </remarks>
public interface IFileStorage
{
    /// <summary>
    /// Persists the <paramref name="content"/> stream under a freshly generated, opaque key and returns that key. The
    /// caller keeps only the returned <see cref="StoredFileKey"/>; the stream is consumed (read to end) but not disposed
    /// by the store. The supplied <paramref name="metadata"/> (original file name, content type) lets an adapter store
    /// side-car information (e.g. S3 object metadata) but is never required to reconstruct the key.
    /// </summary>
    /// <param name="content">The bytes to store; read from its current position to the end.</param>
    /// <param name="metadata">Descriptive metadata for the stored object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<StoredFileKey> SaveAsync(
        Stream content, FileStorageMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable stream over the object previously stored under <paramref name="key"/>. The caller owns and must
    /// dispose the returned stream. Throws when the key does not resolve to a stored object.
    /// </summary>
    /// <param name="key">The key returned by a prior <see cref="SaveAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Stream> OpenReadAsync(StoredFileKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a retrieval URL for the object stored under <paramref name="key"/>. For a cloud adapter this is a
    /// (possibly pre-signed) object URL; for the local placeholder it is an application-relative reference the API can
    /// serve. The URL is opaque to callers — they surface it to the client, they do not parse it.
    /// </summary>
    /// <param name="key">The key returned by a prior <see cref="SaveAsync"/>.</param>
    string GetUrl(StoredFileKey key);
}
