namespace SISLAB.SharedKernel.Storage;

/// <summary>
/// Descriptive metadata handed to <see cref="IFileStorage.SaveAsync"/> when persisting a file (SISLAB-09). It carries
/// only the two facts a store may want to record alongside the bytes — the original <see cref="FileName"/> and the
/// <see cref="ContentType"/> (MIME type) — and never anything tenant- or domain-specific: the storage port is
/// deliberately unaware of animals, analyses or companies. Those linkages live on the domain aggregate, not on the file.
/// </summary>
/// <param name="FileName">The original client file name (e.g. "hemograma-A1.jpg"), for display and download.</param>
/// <param name="ContentType">The MIME content type (e.g. "image/jpeg", "application/pdf").</param>
public sealed record FileStorageMetadata(string FileName, string ContentType);
