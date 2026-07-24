namespace SISLAB.Infrastructure.Storage;

/// <summary>
/// Options for the <see cref="LocalFileStorage"/> placeholder adapter (SISLAB-09). Bound from configuration section
/// <c>FileStorage:Local</c>; a missing configuration falls back to a temp-based root so the app still boots in
/// development without extra setup.
/// </summary>
public sealed class LocalFileStorageOptions
{
    /// <summary>Configuration section the options bind from.</summary>
    public const string SectionName = "FileStorage:Local";

    /// <summary>
    /// Absolute (or app-relative) directory the adapter writes objects under. When blank, the adapter uses a
    /// <c>sislab-file-storage</c> folder in the system temp directory — a development-only default.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Base URL path the adapter prefixes stored keys with when building a retrieval URL (see
    /// <see cref="LocalFileStorage.GetUrl"/>). Defaults to the local files endpoint the API can serve.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "/api/files";
}
