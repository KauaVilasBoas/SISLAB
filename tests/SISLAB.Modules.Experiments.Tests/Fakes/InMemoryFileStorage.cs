using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IFileStorage"/> for handler unit tests — the bytes live in a dictionary, no disk. It mints an
/// opaque GUID key on save (like the real adapters) and round-trips the exact bytes on read, so a test can assert the
/// evidence flow (attach → persist key → retrieve) end to end without the filesystem or a cloud SDK.
/// </summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _objects = new();

    /// <summary>The metadata captured on the last save, so a test can assert what crossed the port.</summary>
    public FileStorageMetadata? LastMetadata { get; private set; }

    public async Task<StoredFileKey> SaveAsync(
        Stream content, FileStorageMetadata metadata, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        string key = Guid.NewGuid().ToString("N");
        _objects[key] = buffer.ToArray();
        LastMetadata = metadata;

        return StoredFileKey.Of(key);
    }

    public Task<Stream> OpenReadAsync(StoredFileKey key, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(key.Value, out byte[]? bytes))
            throw new NotFoundException($"No stored file was found for key '{key.Value}'.");

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public string GetUrl(StoredFileKey key) => $"memory://{key.Value}";
}
