// ARQUIVO: DependencyGraph.cs
using ContextWinUI.Core.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ContextWinUI.Features.CodeAnalyses;

public class DependencyGraph
{
	/// <summary>
	/// O índice principal. Mapeia ID único do símbolo -> Objeto SymbolNode.
	/// Fonte da verdade absoluta.
	/// </summary>
	public ConcurrentDictionary<string, SymbolNode> Nodes { get; } = new();

	/// <summary>
	/// Índice espacial por arquivo. Mapeia Caminho do Arquivo (lower) -> Lista de nós contidos nele.
	/// Usado para saber "o que tem neste arquivo" sem varrer tudo.
	/// </summary>
	public ConcurrentDictionary<string, List<SymbolNode>> FileIndex { get; } = new();

	/// <summary>
	/// Rastreia relacionamentos de herança/implementação.
	/// Mapeia ID da Interface/Base -> Lista de IDs das Classes que a implementam.
	/// </summary>
	public ConcurrentDictionary<string, List<string>> InterfaceImplementations { get; } = new();

	/// <summary>
	/// NOVO: Índice de Nomes Simples. 
	/// Mapeia "CustomerService" -> [Node(Class), Node(Prop), Node(Field)].
	/// Essencial para a Heurística de Resolução (descobrir o que é uma palavra).
	/// </summary>
	public ConcurrentDictionary<string, List<SymbolNode>> NameIndex { get; } = new();

	/// <summary>
	/// NOVO: Cache O(1) para Highlight.
	/// Mapeia "DependencyGraph" -> SymbolType.Class.
	/// Usado exclusivamente para colorir o editor rapidamente.
	/// Armazena apenas Tipos Globais (Classes, Interfaces, Enums, Structs).
	/// </summary>
	public ConcurrentDictionary<string, SymbolType> GlobalSymbolCache { get; } = new();

	/// <summary>
	/// Adiciona um nó ao grafo e atualiza automaticamente todos os índices de performance.
	/// </summary>
	public void AddNode(SymbolNode node)
	{
		if (node == null) return;

		// 1. Adiciona no índice principal (ID)
		Nodes[node.Id] = node;

		// 2. Adiciona no índice de arquivos
		if (!string.IsNullOrEmpty(node.FilePath))
		{
			// Normaliza para minúsculo para garantir o match independente do casing do Windows
			string fileKey = node.FilePath.ToLowerInvariant();

			FileIndex.AddOrUpdate(
				fileKey,
				new List<SymbolNode> { node },
				(key, list) =>
				{
					lock (list)
					{
						list.Add(node);
						return list;
					}
				}
			);
		}

		// 3. Adiciona no índice de Nomes e Cache Global
		if (!string.IsNullOrEmpty(node.Name))
		{
			// A. Índice de Nomes (para Resolução de Mouse Hover)
			NameIndex.AddOrUpdate(
				node.Name,
				new List<SymbolNode> { node },
				(key, list) =>
				{
					lock (list)
					{
						list.Add(node);
						return list;
					}
				}
			);

			// B. Cache Global (apenas para Tipos Relevantes para Highlight)
			// Não queremos cachear nomes de métodos comuns (ex: "ToString", "Add") 
			// pois poluiria o highlight global.
			if (IsGlobalType(node.Type))
			{
				// TryAdd é suficiente. Em caso de conflito de nomes de classes (mesmo nome, namespaces diferentes),
				// o highlight terá a mesma cor (ex: Teal), então não importa qual ganha.
				GlobalSymbolCache.TryAdd(node.Name, node.Type);
			}
		}
	}

	/// <summary>
	/// Limpa todos os dados. Usado ao reindexar o projeto.
	/// </summary>
	public void Clear()
	{
		Nodes.Clear();
		FileIndex.Clear();
		InterfaceImplementations.Clear();
		NameIndex.Clear();
		GlobalSymbolCache.Clear();
	}

	/// <summary>
	/// Verifica se o tipo do símbolo deve ser considerado globalmente para highlight.
	/// </summary>
	private bool IsGlobalType(SymbolType type)
	{
		return type == SymbolType.Class ||
			   type == SymbolType.Interface ||
			   type == SymbolType.Enum ||
			   type == SymbolType.Struct ||
			   type == SymbolType.Constructor; // Opcional: Colorir construtores como a classe
	}
}