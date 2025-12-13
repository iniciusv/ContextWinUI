using System.Collections.Generic;

namespace ContextWinUI.ContextWinUI.Models;

public class FileTagDto
{
	public string RelativePath { get; set; } = string.Empty;
	public List<string> Tags { get; set; } = new();
}