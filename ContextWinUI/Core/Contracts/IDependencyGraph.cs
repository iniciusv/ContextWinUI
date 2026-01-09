using ContextWinUI.Core.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace ContextWinUI.Core.Contracts;

public interface IDependencyGraph
{
	ConcurrentDictionary<int, SymbolNode> Nodes { get; }
	ConcurrentDictionary<int, List<SymbolNode>> FileIndex { get; }

	string GetFilePath(int fileId);
	int GetOrAddFileId(string filePath);
	int GetOrAddNodeId(string symbolKey);

	// Manipulação
	void AddNode(SymbolNode node);
	void Clear();
	void RemoveNodesForFile(int fileId);
}
