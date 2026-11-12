namespace ACA.Application.DTOs;

public class BatchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Session { get; set; } = string.Empty;
    public int BatchNo { get; set; }
    public int MemberCount { get; set; }
}
