using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Models;

// Local: ContextWinUI.Core.Models.Graph
public enum SymbolType { Class, Interface, Method, Property, Field, Constructor }
public enum LinkType { Calls, Accesses, UsesType, Implements, Inherits }

public class SymbolNode
{
	// ID Único: "Namespace.Class.Method(int)" gerado pelo Roslyn
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string FilePath { get; set; } = string.Empty;
	public SymbolType Type { get; set; }

	public int StartPosition { get; set; }
	public int Length { get; set; }

	public HashSet<SymbolLink> OutgoingLinks { get; set; } = new();
}

public record SymbolLink(string TargetId, LinkType Type);

public class DependencyGraph
{
	public ConcurrentDictionary<string, SymbolNode> Nodes { get; } = new();

	public ConcurrentDictionary<string, List<string>> InterfaceImplementations { get; } = new();

	public void AddNode(SymbolNode node) => Nodes[node.Id] = node;
}