// ARQUIVO: AiCodeMerger.cs
namespace ContextWinUI.Features.GraphParser.IAParser;

using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser; // Para ICodeBlockParserService
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class AiCodeMerger : IAiCodeMerger
{
	private readonly ISemanticIndexService _indexService;
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeBlockParserService _parserService;

	public AiCodeMerger(
		ISemanticIndexService indexService,
		IFileSystemService fileSystemService,
		ICodeBlockParserService parserService)
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_parserService = parserService;
	}

	public async Task<AiMergeResult> MergeAiSnippetAsync(string snippetCode)
	{
		var result = new AiMergeResult();

		if (string.IsNullOrWhiteSpace(snippetCode))
		{
			result.Message = "O código fornecido está vazio.";
			return result;
		}

		try
		{
			// 1. Identificar a qual arquivo este snippet pertence
			var targetFilePath = await IdentifyTargetFileAsync(snippetCode);

			if (string.IsNullOrEmpty(targetFilePath) || !File.Exists(targetFilePath))
			{
				result.Message = "Não foi possível identificar o arquivo de origem correspondente no projeto.";
				return result;
			}

			result.FilePath = targetFilePath;

			// 2. Carregar o conteúdo atual do arquivo (do disco)
			var originalContent = await _fileSystemService.ReadFileContentAsync(targetFilePath);
			if (string.IsNullOrEmpty(originalContent))
			{
				result.Message = "O arquivo identificado está vazio ou não pôde ser lido.";
				return result;
			}

			// 3. Transformar ambos (Original e Snippet) em Blocos
			// Nota: Parseamos o snippet passando o targetFilePath apenas para manter a extensão correta
			var originalBlocks = await _parserService.ParseFileAsync(targetFilePath, originalContent);
			var snippetBlocks = await _parserService.ParseFileAsync(targetFilePath, snippetCode);

			// 4. Executar o Merge Lógico
			PerformMerge(originalBlocks, snippetBlocks, result);

			result.MergedBlocks = originalBlocks;
			result.Success = true;
			result.Message = $"Processamento concluído: {result.ModifiedCount} modificados, {result.AddedCount} novos itens.";
		}
		catch (Exception ex)
		{
			result.Success = false;
			result.Message = $"Erro crítico ao processar snippet: {ex.Message}";
		}

		return result;
	}

	private async Task<string?> IdentifyTargetFileAsync(string snippet)
	{
		var tree = CSharpSyntaxTree.ParseText(snippet);
		var root = await tree.GetRootAsync();
		var graph = _indexService.GetCurrentGraph(); // Acessa o grafo atual

		// Estratégia A: Busca por Declaração de Classe
		var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
		if (classNode != null)
		{
			var className = classNode.Identifier.Text;

			// Busca no grafo um nó que seja Classe e tenha esse nome
			var symbolNode = graph.Nodes.Values
				.FirstOrDefault(n => n.Name == className &&
								   (n.Type == SymbolType.Class || n.Type == SymbolType.Interface));

			if (symbolNode != null)
			{
				return graph.GetFilePath(symbolNode.FileId);
			}
		}

		// Estratégia B: Heurística por Métodos (caso o snippet seja apenas métodos soltos)
		var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
		var fileVotes = new Dictionary<int, int>();

		foreach (var method in methods)
		{
			var methodName = method.Identifier.Text;
			// Encontra todos os arquivos que possuem um método com este nome
			var candidates = graph.Nodes.Values
				.Where(n => n.Name == methodName && n.Type == SymbolType.Method);

			foreach (var candidate in candidates)
			{
				if (!fileVotes.ContainsKey(candidate.FileId))
					fileVotes[candidate.FileId] = 0;

				fileVotes[candidate.FileId]++;
			}
		}

		if (fileVotes.Any())
		{
			// O arquivo com mais "hits" vence
			var winnerId = fileVotes.MaxBy(kv => kv.Value).Key;
			return graph.GetFilePath(winnerId);
		}

		return null;
	}

	private void PerformMerge(List<CodeBlockItem> originalBlocks, List<CodeBlockItem> snippetBlocks, AiMergeResult result)
	{
		var timestamp = DateTime.Now;
		string versionDescription = "Sugestão IA";

		foreach (var snippetBlock in snippetBlocks)
		{
			// Ignoramos usings, namespace e estrutura de classe wrapper na iteração de merge
			// Focamos em membros granulares (Métodos, Propriedades, Campos)
			if (!snippetBlock.IsGranular) continue;

			// Tenta encontrar correspondência exata na lista original
			var originalBlock = originalBlocks.FirstOrDefault(b =>
				b.Name == snippetBlock.Name &&
				b.SegmentType == snippetBlock.SegmentType);

			if (originalBlock != null)
			{
				// CASO 1: Bloco Existente -> Cria Nova Versão
				// Só cria versão se o conteúdo for diferente (normalizando espaços básicos)
				if (originalBlock.Content.Trim() != snippetBlock.Content.Trim())
				{
					originalBlock.CreateNewVersion(snippetBlock.Content, versionDescription, timestamp);
					result.ModifiedCount++;
				}
			}
			else
			{
				// CASO 2: Bloco Novo -> Inserir na Lista
				InsertNewBlockIdeally(originalBlocks, snippetBlock, result);
				result.AddedCount++;
			}
		}
	}

	private void InsertNewBlockIdeally(List<CodeBlockItem> originalBlocks, CodeBlockItem newBlock, AiMergeResult result)
	{
		// Prepara o bloco novo para ser um "cidadão de primeira classe" na lista
		newBlock.Id = Guid.NewGuid().ToString();
		// A versão inicial dele é vazia, a versão atual é o conteúdo da IA
		newBlock.InitializeVersions(string.Empty);
		newBlock.CreateNewVersion(newBlock.Content, "Novo Item (IA)", DateTime.Now);
		newBlock.TypeDescription += " (Novo)";

		// Lógica de inserção:
		// Precisamos achar o fim da classe para não inserir depois do "}" final ou fora do namespace.
		// Procuramos o último bloco que contém "}" e que seja do tipo Trivia ou CloseBrace.

		var closingBlockIndex = -1;

		// Procura de trás para frente o fechamento da classe
		for (int i = originalBlocks.Count - 1; i >= 0; i--)
		{
			var block = originalBlocks[i];
			if (block.Content.Contains("}"))
			{
				// Assumimos que o último } do arquivo é namespace, o penúltimo é classe
				// Mas no parser linear, isso pode variar. 
				// Vamos tentar inserir logo antes do último bloco de fechamento que encontrarmos
				closingBlockIndex = i;
				break;
			}
		}

		if (closingBlockIndex >= 0)
		{
			// Ajusta identação do novo bloco baseado no bloco anterior ou profundidade esperada
			if (closingBlockIndex > 0)
			{
				newBlock.DepthLevel = originalBlocks[closingBlockIndex - 1].DepthLevel;
			}

			originalBlocks.Insert(closingBlockIndex, newBlock);

			// Adiciona uma quebra de linha (Gap) antes, se necessário, para não ficar colado
			var gapBlock = new CodeBlockItem
			{
				Content = "\n\t", // Identação básica
				SegmentType = SegmentType.Gap,
				DepthLevel = newBlock.DepthLevel,
				Name = "Gap (Auto)"
			};
			gapBlock.InitializeVersions("\n\t");
			originalBlocks.Insert(closingBlockIndex, gapBlock);
		}
		else
		{
			// Fallback: Se não achou estrutura clara, joga no final
			originalBlocks.Add(newBlock);
		}
	}
}