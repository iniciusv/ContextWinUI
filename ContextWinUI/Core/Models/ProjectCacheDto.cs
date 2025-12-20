using ContextWinUI.Models;
using System.Collections.Generic;

namespace ContextWinUI.Core.Models;

public class ProjectCacheDto
{
	public string RootPath { get; set; } = string.Empty;
	public string PrePrompt { get; set; } = string.Empty;

	public bool OmitUsings { get; set; }
	public bool OmitNamespaces { get; set; }
	public bool OmitComments { get; set; }
	public bool OmitEmptyLines { get; set; }
	public bool IncludeStructure { get; set; }
	public bool StructureOnlyFolders { get; set; }

	public List<FileMetadataDto> Files { get; set; } = new();
}