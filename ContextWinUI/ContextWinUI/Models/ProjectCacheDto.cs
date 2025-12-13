using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.ContextWinUI.Models;

public class ProjectCacheDto
{
	public string RootPath { get; set; } = string.Empty;
	public string PrePrompt { get; set; } = string.Empty;

	public bool OmitUsings { get; set; }
	public bool OmitComments { get; set; }

	public List<FileMetadataDto> Files { get; set; } = new();
}