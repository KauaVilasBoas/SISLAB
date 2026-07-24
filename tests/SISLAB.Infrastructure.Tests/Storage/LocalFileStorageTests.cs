using System.Text;
using Microsoft.Extensions.Options;
using SISLAB.Infrastructure.Storage;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Infrastructure.Tests.Storage;

/// <summary>
/// Tests for the LOCAL placeholder <see cref="IFileStorage"/> adapter (SISLAB-09). They pin the adapter's contract —
/// save returns an opaque key, read round-trips the exact bytes, an unknown key is a not-found, and a URL is built from
/// the key — under a temp root that is cleaned up after each test. When the S3 adapter (card #53) arrives it must honour
/// the same contract, so these double as the storage-port acceptance suite.
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sislab-file-storage-tests", Guid.NewGuid().ToString("N"));

    private LocalFileStorage NewStorage()
        => new(Options.Create(new LocalFileStorageOptions { RootPath = _root, PublicBaseUrl = "/api/files" }));

    [Fact]
    public async Task Save_then_read_round_trips_the_exact_bytes()
    {
        LocalFileStorage storage = NewStorage();
        byte[] payload = Encoding.UTF8.GetBytes("hemograma-laudo-bytes");

        StoredFileKey key = await storage.SaveAsync(
            new MemoryStream(payload), new FileStorageMetadata("laudo.jpg", "image/jpeg"));

        await using Stream read = await storage.OpenReadAsync(key);
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);

        Assert.Equal(payload, buffer.ToArray());
    }

    [Fact]
    public async Task Save_preserves_the_original_extension_in_the_opaque_key()
    {
        LocalFileStorage storage = NewStorage();

        StoredFileKey key = await storage.SaveAsync(
            new MemoryStream(new byte[] { 1, 2, 3 }), new FileStorageMetadata("photo.png", "image/png"));

        Assert.EndsWith(".png", key.Value);
    }

    [Fact]
    public async Task Read_of_an_unknown_key_throws_not_found()
    {
        LocalFileStorage storage = NewStorage();

        await Assert.ThrowsAsync<NotFoundException>(
            () => storage.OpenReadAsync(StoredFileKey.Of("does-not-exist.jpg")));
    }

    [Fact]
    public async Task Read_refuses_a_path_traversal_key()
    {
        LocalFileStorage storage = NewStorage();

        await Assert.ThrowsAsync<NotFoundException>(
            () => storage.OpenReadAsync(StoredFileKey.Of("../escape.txt")));
    }

    [Fact]
    public async Task GetUrl_builds_a_public_reference_from_the_key()
    {
        LocalFileStorage storage = NewStorage();
        StoredFileKey key = await storage.SaveAsync(
            new MemoryStream(new byte[] { 9 }), new FileStorageMetadata("x.pdf", "application/pdf"));

        Assert.Equal($"/api/files/{key.Value}", storage.GetUrl(key));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
