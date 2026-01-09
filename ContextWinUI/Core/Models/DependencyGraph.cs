// ARQUIVO: DependencyGraph.cs
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ContextWinUI.Features.CodeAnalyses;

public class DependencyGraph : IDependencyGraph
{
	// Gerador de IDs Thread-Safe
	private int _nextNodeId = 0;
	private int _nextFileId = 0;

	// OTIMIZAÇÃO #4: Dicionário principal agora usa int como chave
	public ConcurrentDictionary<int, SymbolNode> Nodes { get; } = new();

	// OTIMIZAÇÃO #1: Lookup bidirecional de arquivos
	// Mapeia "C:\Users\...\File.cs" -> 1
	private ConcurrentDictionary<string, int> _pathToFileId = new(StringComparer.OrdinalIgnoreCase);
	// Mapeia 1 -> "C:\Users\...\File.cs"
	private ConcurrentDictionary<int, string> _fileIdToPath = new();

	// Index agora agrupa por FileId (int), não mais pela string do caminho
	public ConcurrentDictionary<int, List<SymbolNode>> FileIndex { get; } = new();

	public ConcurrentDictionary<string, List<SymbolNode>> NameIndex { get; } = new();

	// OTIMIZAÇÃO #1 e #4: Interfaces agora mapeiam Strings (nomes) para IDs de nós (int)
	public ConcurrentDictionary<string, List<int>> InterfaceImplementations { get; } = new();

	public ConcurrentDictionary<string, SymbolType> GlobalSymbolCache { get; } = new();

	private ConcurrentDictionary<string, int> _symbolStringToId = new();

	public int GetOrAddNodeId(string symbolKey)
	{
		if (string.IsNullOrEmpty(symbolKey)) return 0;

		return _symbolStringToId.GetOrAdd(symbolKey, _ => GenerateNodeId());
	}

	public int GenerateNodeId()
	{
		return Interlocked.Increment(ref _nextNodeId);
	}

	public int GetOrAddFileId(string filePath)
	{
		if (string.IsNullOrEmpty(filePath)) return -1;

		return _pathToFileId.GetOrAdd(filePath, (path) =>
		{
			int newId = Interlocked.Increment(ref _nextFileId);
			_fileIdToPath.TryAdd(newId, path);
			return newId;
		});
	}

	public string GetFilePath(int fileId)
	{
		return _fileIdToPath.TryGetValue(fileId, out var path) ? path : string.Empty;
	}


	public void AddNode(SymbolNode node)
	{
		if (node == null) return;

		// Adiciona ao dicionário principal (int -> Node)
		Nodes[node.Id] = node;

		// Atualiza índice de arquivos usando FileId
		if (node.FileId > 0)
		{
			FileIndex.AddOrUpdate(
				node.FileId,
				new List<SymbolNode> { node },
				(key, list) =>
				{
					lock (list) { list.Add(node); return list; }
				}
			);
		}

		// Atualiza índice de nomes (permanece string -> lista de nós)
		if (!string.IsNullOrEmpty(node.Name))
		{
			NameIndex.AddOrUpdate(
				node.Name,
				new List<SymbolNode> { node },
				(key, list) =>
				{
					lock (list) { list.Add(node); return list; }
				}
			);

			if (IsGlobalType(node.Type))
			{
				GlobalSymbolCache.TryAdd(node.Name, node.Type);
			}
		}
	}

	public void Clear()
	{
		_nextNodeId = 0;
		_nextFileId = 0;
		Nodes.Clear();
		FileIndex.Clear();
		_pathToFileId.Clear();
		_fileIdToPath.Clear();
		InterfaceImplementations.Clear();
		NameIndex.Clear();
		GlobalSymbolCache.Clear();
	}

	private bool IsGlobalType(SymbolType type)
	{
		return type == SymbolType.Class ||
			   type == SymbolType.Interface ||
			   type == SymbolType.Enum ||
			   type == SymbolType.Struct ||
			   type == SymbolType.Constructor;
	}

	public void RemoveNodesForFile(int fileId)
	{
		if (fileId <= 0) return;

		// 1. Remover do FileIndex e obter os nós afetados
		if (FileIndex.TryRemove(fileId, out var nodesToRemove))
		{
			foreach (var node in nodesToRemove)
			{
				// 2. Remover do Dicionário principal de Nós
				Nodes.TryRemove(node.Id, out _);

				// 3. Remover do NameIndex (pode ser custoso se houver colisão de nomes, mas necessário)
				if (!string.IsNullOrEmpty(node.Name) && NameIndex.TryGetValue(node.Name, out var nameList))
				{
					lock (nameList)
					{
						nameList.RemoveAll(n => n.Id == node.Id);
					}
				}

				// 4. Remover do GlobalSymbolCache se necessário
				if (IsGlobalType(node.Type))
				{
					// Nota: Isso é uma simplificação. Em um cenário ideal, verificaríamos se outra definição existe.
					// Para incremental, deixamos o cache sobrescrever depois.
				}

				// 5. Remover implementações de interface
				if (node.Type == SymbolType.Class)
				{
					foreach (var key in InterfaceImplementations.Keys)
					{
						if (InterfaceImplementations.TryGetValue(key, out var implList))
						{
							lock (implList) { implList.Remove(node.Id); }
						}
					}
				}
			}
		}
	}
}