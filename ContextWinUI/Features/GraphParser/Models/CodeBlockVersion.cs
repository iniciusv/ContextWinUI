using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ARQUIVO: CodeBlockVersion.cs
namespace ContextWinUI.Features.GraphParser.Models
{
	public class CodeBlockVersion
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public DateTime Timestamp { get; set; } = DateTime.Now;
		public string Content { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public bool IsOriginal { get; set; }

		public CodeBlockVersion Clone()
		{
			return new CodeBlockVersion
			{
				Id = Guid.NewGuid().ToString(),
				Timestamp = DateTime.Now,
				Content = Content,
				Description = Description,
				IsOriginal = false
			};
		}
	}
}
