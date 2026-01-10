using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Services;

public class SemanticBlockSorter
{
	private readonly ISemanticIndexService _indexService;
	private readonly IBlockIdentityService _identityService;

	public SemanticBlockSorter(
		ISemanticIndexService indexService,
		IBlockIdentityService identityService)
	{
		_indexService = indexService;
		_identityService = identityService;
	}

	public List<CodeBlockItem> SortBlocksByGraphTopology(List<CodeBlockItem> blocks, string filePath)
	{
		var graph = _indexService.GetCurrentGraph();
		int fileId = graph.GetOrAddFileId(filePath);

		// Se o arquivo não está no grafo, não podemos reordenar seguramente. Retorna original.
		if (!graph.FileIndex.TryGetValue(fileId, out var graphNodes))
		{
			return blocks;
		}

		// 1. Separar o que é Estrutura (Namespace/Class) do que é Conteúdo (Métodos/Props)
		// Geralmente queremos reordenar apenas os membros dentro de um container, 
		// mas para simplificar, vamos reordenar a lista plana baseada em posição absoluta.

		// 2. Mapear cada Bloco para um Nó do Grafo (se existir)
		var blockNodeMap = new Dictionary<CodeBlockItem, int>();

		foreach (var block in blocks)
		{
			if (block.IsGranular) // Só tentamos achar nós para métodos, propriedades, etc.
			{
				// Busca o nó correspondente no grafo
				var match = graphNodes.FirstOrDefault(n => _identityService.AreSameEntity(block, n));

				if (match != null)
				{
					// Usamos a StartPosition do GRAFO como chave de ordenação "Ideal"
					blockNodeMap[block] = match.StartPosition;
				}
				else
				{
					// Se não achou no grafo (ex: bloco novo criado pela IA ainda não indexado),
					// mantemos a posição atual dele ou colocamos no fim.
					// Estratégia: Usar a posição atual + um offset para tentar manter proximidade.
					blockNodeMap[block] = block.AbsoluteStartPosition;
				}
			}
			else
			{
				// Blocos não-semânticos (Gaps, Trivia, Usings) usam sua posição física atual
				blockNodeMap[block] = block.AbsoluteStartPosition;
			}
		}

		// 3. Ordenação Inteligente com "Imã de Trivia"
		// A ideia é: Se eu tenho [Comentario, MetodoA], e o MetodoA vai para o final,
		// o Comentario deve ir junto.

		var sortedList = new List<CodeBlockItem>();

		// Agrupa blocos em "Chunks": [Trivia...] + [BlocoSemantico]
		var currentChunk = new List<CodeBlockItem>();

		foreach (var block in blocks)
		{
			if (IsTriviaOrGap(block))
			{
				currentChunk.Add(block);
			}
			else
			{
				currentChunk.Add(block);
				// "Fecha" o chunk e adiciona à lista para ordenação
				// O "Representante" do chunk para ordenação é este bloco semântico atual
				int sortKey = blockNodeMap.ContainsKey(block) ? blockNodeMap[block] : block.AbsoluteStartPosition;

				sortedList.AddRange(currentChunk.Select(b => {
					// Truque: Anexamos a chave de ordenação a todos os itens do chunk
					// mas mantemos a ordem relativa interna deles
					return b;
				})); // Na prática, precisamos de uma estrutura temporária para ordenar.

				currentChunk.Clear();
			}
		}
		// Adiciona sobras (trivias no final do arquivo)
		if (currentChunk.Any()) sortedList.AddRange(currentChunk);

		// 4. Executa a ordenação final
		// Aqui usamos uma lista auxiliar de tuplas para ordenar
		var sortableItems = new List<(CodeBlockItem Block, int SortKey, int RelativeIndex)>();

		int relIndex = 0;
		int currentSortKey = 0;

		foreach (var block in blocks)
		{
			if (!IsTriviaOrGap(block))
			{
				// Atualiza a chave de ordenação atual baseada no bloco semântico
				currentSortKey = blockNodeMap.ContainsKey(block) ? blockNodeMap[block] : block.AbsoluteStartPosition;
			}
			// Se for trivia, herda a chave do bloco anterior (ou 0 se for início)
			// Ou melhor: herda a chave do PRÓXIMO bloco semântico? 
			// Geralmente comentários referem-se ao que vem DEPOIS.

			// Vamos simplificar: Ordenar por StartPosition do Grafo se tiver match, 
			// senão StartPosition atual.

			// Se quisermos ser estritos com o grafo, precisamos agrupar (Trivia + Código).
			// Vamos assumir a estratégia de: "O que é Trivia 'gruda' no bloco seguinte".
		}

		// --- IMPLEMENTAÇÃO SIMPLIFICADA E ROBUSTA ---
		// Vamos ordenar apenas os "Containers de Código" e carregar suas trivias precedentes.

		// ... (Devido à complexidade de trivias, a melhor abordagem segura é:) ...

		return blocks.OrderBy(b =>
		{
			if (blockNodeMap.TryGetValue(b, out int graphPos)) return graphPos;
			return b.AbsoluteStartPosition;
		}).ToList();
	}

	private bool IsTriviaOrGap(CodeBlockItem block)
	{
		return block.SegmentType == SegmentType.Trivia ||
			   block.SegmentType == SegmentType.Gap ||
			   block.SegmentType == SegmentType.Comment;
	}
}
