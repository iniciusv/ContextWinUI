using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.ContextWinUI.Models;
public class ProjectCacheDto
{
	public string RootPath { get; set; } = string.Empty;
	public List<FileMetadataDto> Files { get; set; } = new();
}