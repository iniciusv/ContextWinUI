using System.Collections.Generic;

namespace ContextWinUI.Models;

public class ProjectCacheDto
{
	public string RootPath { get; set; } = string.Empty;
	public string PrePrompt { get; set; } = string.Empty;

	public bool OmitUsings { get; set; }
	public bool OmitNamespaces { get; set; } // [NOVO]
	public bool OmitComments { get; set; }
	public bool OmitEmptyLines { get; set; } // [NOVO]

	public bool IncludeStructure { get; set; }
	public bool StructureOnlyFolders { get; set; }
	public Dictionary<string, string> TagColors { get; set; } = new();

	public List<FileMetadataDto> Files { get; set; } = new();
}