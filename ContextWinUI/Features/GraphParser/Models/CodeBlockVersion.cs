using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ARQUIVO: CodeBlockVersion.cs
namespace ContextWinUI.Features.GraphParser.Models;


public class CodeBlockVersion
{
	public string Content { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime Timestamp { get; set; }
	public bool IsOriginal { get; set; }

	public CodeBlockVersion Clone()
	{
		return new CodeBlockVersion
		{
			Content = this.Content,
			Description = this.Description,
			Timestamp = this.Timestamp,
			IsOriginal = this.IsOriginal
		};
	}
}