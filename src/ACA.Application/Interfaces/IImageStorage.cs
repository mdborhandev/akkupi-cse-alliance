using ACA.Application.DTOs;

namespace ACA.Application.Interfaces;

public interface IImageStorage
{
    /// <summary>Validates and stores an image; returns a public URL. Throws InvalidOperationException when invalid.</summary>
    Task<string> SaveAsync(ImageUpload image, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}
