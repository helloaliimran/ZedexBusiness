using Microsoft.AspNetCore.StaticFiles;

namespace Zedex.Web.Services;

/// <summary>
/// Stores uploads OUTSIDE wwwroot (under &lt;ContentRoot&gt;/App_Data/uploads) so they are
/// never reachable by a direct URL. Files are streamed back only through controller
/// actions that check the user's module permission — used for employee photos / CNIC
/// scans and expense receipts.
/// </summary>
public interface IPrivateFileStore
{
    /// <summary>Validates and saves <paramref name="file"/> into <paramref name="folder"/>.
    /// Returns (relativePath, null) on success, (null, errorMessage) on validation failure,
    /// or (null, null) when no file was supplied.</summary>
    Task<(string? Path, string? Error)> SaveAsync(IFormFile? file, string folder, bool allowPdf = false);

    /// <summary>Resolves a stored relative path to (physicalPath, contentType), or null if the
    /// path is invalid / missing. Guards against path traversal.</summary>
    (string PhysicalPath, string ContentType)? Resolve(string? relativePath);
}

public class PrivateFileStore : IPrivateFileStore
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxBytes = 3 * 1024 * 1024;

    private readonly string _root;
    private readonly FileExtensionContentTypeProvider _types = new();

    public PrivateFileStore(IWebHostEnvironment env)
    {
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "uploads"));
    }

    public async Task<(string? Path, string? Error)> SaveAsync(IFormFile? file, string folder, bool allowPdf = false)
    {
        if (file is not { Length: > 0 })
            return (null, null);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = allowPdf ? ImageExtensions.Append(".pdf").ToArray() : ImageExtensions;
        if (!allowed.Contains(ext))
            return (null, $"Allowed file types: {string.Join(", ", allowed.Select(a => a.TrimStart('.')))}.");
        if (file.Length > MaxBytes)
            return (null, "File must be 3 MB or smaller.");

        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using (var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
            await file.CopyToAsync(stream);

        return ($"{folder}/{fileName}", null);
    }

    public (string PhysicalPath, string ContentType)? Resolve(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !System.IO.File.Exists(full))
            return null;

        if (!_types.TryGetContentType(full, out var contentType))
            contentType = "application/octet-stream";
        return (full, contentType);
    }
}
