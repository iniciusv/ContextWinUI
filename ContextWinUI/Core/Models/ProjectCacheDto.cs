using System.Collections.Generic;

namespace ContextWinUI.Models;

public class ProjectCacheDto
{
	public string RootPath { get; set; } = string.Empty;
	public string PrePrompt { get; set; } = string.Empty;

	// Configurações de Código
	public bool OmitUsings { get; set; }
	public bool OmitComments { get; set; }

	// --- NOVAS CONFIGURAÇÕES DE ESTRUTURA ---
	public bool IncludeStructure { get; set; }
	public bool StructureOnlyFolders { get; set; }

	public List<FileMetadataDto> Files { get; set; } = new();
}
