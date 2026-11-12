using ACA.Domain.Enums;

namespace ACA.Application.DTOs;

public class ImageUpload
{
    public Stream Content { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
}

public class PostImageDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class PostDto
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorPhotoUrl { get; set; }
    public string? BatchName { get; set; }
    public MemberType MemberType { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsEdited { get; set; }
    public IReadOnlyList<PostImageDto> Images { get; set; } = Array.Empty<PostImageDto>();
    public int ReactionCount { get; set; }
    public int CommentCount { get; set; }
    public ReactionType? MyReaction { get; set; }
}

public class CreatePostDto
{
    public string Content { get; set; } = string.Empty;
    public IReadOnlyList<ImageUpload> Images { get; set; } = Array.Empty<ImageUpload>();
}

public class UpdatePostDto
{
    public string Content { get; set; } = string.Empty;
}
