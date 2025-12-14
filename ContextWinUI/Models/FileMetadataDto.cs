using System.Collections.Generic;

namespace ContextWinUI.Models;

public class FileMetadataDto
{
	public string RelativePath { get; set; } = string.Empty;
	public bool IsIgnored { get; set; } // Novo campo
	public List<string> Tags { get; set; } = new();
}