using System;
using System.Collections.Generic;

namespace Scribo.Models;

public class Project
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<Document> Documents { get; set; } = new();
    public ProjectMetadata Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
}
