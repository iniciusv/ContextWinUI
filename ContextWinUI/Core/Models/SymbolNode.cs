using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Models;

// Local: ContextWinUI.Core.Models.Graph
public enum SymbolType { Class, Interface, Method, Property, Field, Constructor }
public enum LinkType { Calls, Accesses, UsesType, Implements, Inherits }
public record SymbolLink(string TargetId, LinkType Type, int Start, int Length);

public class SymbolNode
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string FilePath { get; set; } = string.Empty;
	public SymbolType Type { get; set; }
	public int StartPosition { get; set; }
	public int Length { get; set; }

	public HashSet<SymbolLink> OutgoingLinks { get; set; } = new();
}
