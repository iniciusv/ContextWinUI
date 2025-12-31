using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Models;

public class GlobalVersion
{
	public string Id { get; set; } = Guid.NewGuid().ToString();
	public string Description { get; set; } = string.Empty;
	public DateTime Timestamp { get; set; } = DateTime.Now;
	public bool IsOriginal { get; set; }
}
