using ACA.Application.DTOs;
using ACA.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace ACA.Infrastructure.Services;

/// <summary>
/// Stores post images on the local file system under wwwroot/uploads/posts/{yyyy}/.
/// Validation is byte-level: declared content type is checked, then the file signature (magic bytes)
/// must really be JPEG, PNG, or WebP.
/// </summary>
public class LocalImageStorage : IImageStorage
{
    public const long MaxImageBytes = 5 * 1024 * 1024;

    private const string UploadsPrefix = "/uploads/posts/";

    private readonly IWebHostEnvironment _environment;

    public LocalImageStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(ImageUpload image, CancellationToken ct = default)
    {
        if (image is null)
        {
            throw new InvalidOperationException("No image was provided.");
        }

        if (image.Content is null || !image.Content.CanRead)
        {
            throw new InvalidOperationException("The uploaded image could not be read.");
        }

        if (image.Length <= 0)
        {
            throw new InvalidOperationException("The uploaded image is empty.");
        }

        if (image.Length > MaxImageBytes)
        {
            throw new InvalidOperationException($"Each image must be {FormatLimit()} or smaller.");
        }

        var contentType = (image.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            throw new InvalidOperationException("Only JPEG, PNG, and WebP images are allowed.");
        }

        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            var chunk = new byte[81920];
            int read;
            while ((read = await image.Content.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)) > 0)
            {
                if (buffer.Length + read > MaxImageBytes)
                {
                    throw new InvalidOperationException($"Each image must be {FormatLimit()} or smaller.");
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }

            bytes = buffer.ToArray();
        }

        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("The uploaded image is empty.");
        }

        var extension = DetectExtension(bytes)
            ?? throw new InvalidOperationException("The file is not a valid JPEG, PNG, or WebP image.");

        var webRoot = ResolveWebRoot();
        var folder = DateTime.UtcNow.ToString("yyyy");
        var directory = Path.Combine(webRoot, "uploads", "posts", folder);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(directory, fileName);

        await File.WriteAllBytesAsync(path, bytes, ct);

        return $"{UploadsPrefix}{folder}/{fileName}";
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.CompletedTask;
        }

        var normalized = url.Replace('\\', '/').Trim();
        if (!normalized.StartsWith(UploadsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Never touch paths this store does not own (e.g. external image URLs).
            return Task.CompletedTask;
        }

        var webRoot = ResolveWebRoot();
        var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "posts"));
        var relative = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(webRoot, relative));

        // Guard against path traversal in stored URLs.
        if (!fullPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Another request may be deleting the same file; a missing file is fine.
        }
        catch (UnauthorizedAccessException)
        {
            // Nothing more we can safely do here.
        }

        return Task.CompletedTask;
    }

    private string ResolveWebRoot()
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = string.IsNullOrWhiteSpace(_environment.ContentRootPath)
                ? Directory.GetCurrentDirectory()
                : Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        Directory.CreateDirectory(webRoot);
        return webRoot;
    }

    private static string FormatLimit() => $"{MaxImageBytes / (1024 * 1024)} MB";

    /// <summary>Returns the extension implied by the file signature, or null when it is not an allowed image.</summary>
    private static string? DetectExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ".png";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return ".webp";
        }

        return null;
    }
}
