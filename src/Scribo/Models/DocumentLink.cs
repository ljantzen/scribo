namespace Scribo.Models;

public class DocumentLink
{
    public string LinkText { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public string? TargetDocumentId { get; set; }
    public bool IsResolved { get; set; }
}
