using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.ContextWinUI.Models;

public class FileMetadataDto
{
	public string RelativePath { get; set; } = string.Empty;
	public List<string> Tags { get; set; } = new();
}
