using Microsoft.Extensions.Options;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Infrastructure.Storage;

/// <summary>
/// <b>Placeholder</b> local-filesystem implementation of <see cref="IFileStorage"/> (SISLAB-09). It stores each file as
/// a flat object on disk under a configured root, keyed by a freshly generated GUID (the original file name is kept only
/// as a suffix for human readability). It is the default adapter registered by DI <b>until the S3 adapter arrives</b>
/// (card #53): swapping to S3 is a one-line DI change — replace this registration with the S3 one — because callers
/// depend on the <see cref="IFileStorage"/> port, never on this type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope of the placeholder.</b> This adapter is intentionally minimal: no bucketing, no lifecycle, no signed URLs.
/// It exists so the SISLAB-09 evidence flow (attach → persist key → retrieve) is fully wired and testable without any
/// cloud dependency. Do not build durable/production expectations on it.
/// </para>
/// <para>
/// <b>Key opacity.</b> The minted <see cref="StoredFileKey"/> is a relative object name under the root; callers treat it
/// as opaque. On read, the key is resolved back to a path, guarding against traversal outside the root.
/// </para>
/// </remarks>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options)
    {
        LocalFileStorageOptions value = options.Value;

        _rootPath = string.IsNullOrWhiteSpace(value.RootPath)
            ? Path.Combine(Path.GetTempPath(), "sislab-file-storage")
            : value.RootPath;

        _publicBaseUrl = value.PublicBaseUrl.TrimEnd('/');

        Directory.CreateDirectory(_rootPath);
    }

    /// <inheritdoc />
    public async Task<StoredFileKey> SaveAsync(
        Stream content, FileStorageMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        // The key is a GUID plus a sanitized extension of the original name — opaque to callers, unique on disk.
        string extension = SafeExtension(metadata.FileName);
        string objectName = $"{Guid.NewGuid():N}{extension}";
        string fullPath = Path.Combine(_rootPath, objectName);

        await using (FileStream target = File.Create(fullPath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        return StoredFileKey.Of(objectName);
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(StoredFileKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        string fullPath = ResolveWithinRoot(key);
        if (!File.Exists(fullPath))
            throw new NotFoundException($"No stored file was found for key '{key.Value}'.");

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public string GetUrl(StoredFileKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return $"{_publicBaseUrl}/{key.Value}";
    }

    // Resolves a key to an absolute path and refuses anything that escapes the root (path traversal guard).
    private string ResolveWithinRoot(StoredFileKey key)
    {
        string candidate = Path.GetFullPath(Path.Combine(_rootPath, key.Value));
        string root = Path.GetFullPath(_rootPath);

        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(candidate, root, StringComparison.Ordinal))
        {
            throw new NotFoundException($"No stored file was found for key '{key.Value}'.");
        }

        return candidate;
    }

    private static string SafeExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        string extension = Path.GetExtension(fileName);
        // Keep only a short, dot-prefixed alphanumeric extension; drop anything unusual.
        if (string.IsNullOrEmpty(extension) || extension.Length > 12 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
            return string.Empty;

        return extension.ToLowerInvariant();
    }
}
