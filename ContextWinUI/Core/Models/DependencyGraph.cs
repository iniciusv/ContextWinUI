using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ContextWinUI.Core.Models;

public class DependencyGraph
{
	// Lookup principal (ID -> Nó)
	public ConcurrentDictionary<string, SymbolNode> Nodes { get; } = new();


	public ConcurrentDictionary<string, List<SymbolNode>> FileIndex { get; } = new();
	public ConcurrentDictionary<string, List<string>> InterfaceImplementations { get; } = new();

	public void AddNode(SymbolNode node)
	{
		// Adiciona no índice principal
		Nodes[node.Id] = node;

		// Adiciona no índice de arquivos (com normalização de chave para evitar bugs de path)
		if (!string.IsNullOrEmpty(node.FilePath))
		{
			// Normaliza para minúsculo para garantir o match
			string fileKey = node.FilePath.ToLowerInvariant();

			FileIndex.AddOrUpdate(
				fileKey,
				new List<SymbolNode> { node },
				(key, list) => { lock (list) { list.Add(node); return list; } }
			);
		}
	}
}