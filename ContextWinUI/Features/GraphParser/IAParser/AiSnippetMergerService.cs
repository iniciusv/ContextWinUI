// ARQUIVO: AiSnippetMergerService.cs
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
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

namespace ContextWinUI.Features.GraphParser.Services;

public class AiSnippetMergerService : IAiSnippetMergerService
{
	private readonly ISemanticIndexService _indexService;
	private readonly ICodeBlockParserService _parserService;
	private readonly IFileSystemService _fileSystemService;

	public AiSnippetMergerService(
		ISemanticIndexService indexService,
		ICodeBlockParserService parserService,
		IFileSystemService fileSystemService)
	{
		_indexService = indexService;
		_parserService = parserService;
		_fileSystemService = fileSystemService;
	}

	public async Task<MergeResult> MergeSnippetAsync(string snippetContent)
	{
		var result = new MergeResult();

		if (string.IsNullOrWhiteSpace(snippetContent))
		{
			result.Message = "Snippet vazio.";
			return result;
		}

		// 1. Identificar o alvo usando Roslyn e o Modelo Semântico
		var targetFile = await IdentifyTargetFileAsync(snippetContent);

		if (string.IsNullOrEmpty(targetFile))
		{
			result.Message = "Não foi possível identificar a classe ou arquivo de origem deste snippet no projeto atual.";
			return result;
		}

		result.TargetFilePath = targetFile;

		// 2. Carregar o estado ATUAL do arquivo original
		var originalFileContent = await _fileSystemService.ReadFileContentAsync(targetFile);
		var originalBlocks = await _parserService.ParseFileAsync(targetFile, originalFileContent);

		// 3. Processar o SNIPPET como se fosse um arquivo, para gerar blocos estruturados
		// Nota: Passamos o targetFile apenas para manter a extensão correta, o conteúdo é o snippet
		var snippetBlocks = await _parserService.ParseFileAsync(targetFile, snippetContent);

		// 4. Executar o Merge
		ApplyMerge(originalBlocks, snippetBlocks, result);

		result.UpdatedBlocks = originalBlocks;
		result.Success = true;
		result.Message = $"Merge concluído em {Path.GetFileName(targetFile)}. {result.BlocksModified} modificados, {result.NewBlocksAdded} novos.";

		return result;
	}

	private async Task<string?> IdentifyTargetFileAsync(string snippet)
	{
		var tree = CSharpSyntaxTree.ParseText(snippet);
		var root = await tree.GetRootAsync();

		// Estratégia A: O snippet contém declaração de classe?
		var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
		if (classDecl != null)
		{
			var className = classDecl.Identifier.Text;
			var symbolType = _indexService.GetSymbolType(className);

			// Se achou o símbolo, precisamos saber onde ele está definido.
			// O SemanticIndexService atual retorna SymbolNode via InferSymbolFromGraph, 
			// mas precisamos de uma busca reversa pelo nome.
			// Assumindo que podemos consultar o grafo:
			var graph = _indexService.GetCurrentGraph();
			var node = graph.Nodes.Values.FirstOrDefault(n => n.Name == className && n.Type == SymbolType.Class);

			if (node != null)
			{
				return graph.GetFilePath(node.FileId);
			}
		}

		// Estratégia B: O snippet contém métodos soltos? Tentar votar pelo arquivo mais provável.
		var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
		var fileVotes = new Dictionary<int, int>(); // FileId -> Contagem

		var graphInstance = _indexService.GetCurrentGraph();

		foreach (var method in methods)
		{
			var methodName = method.Identifier.Text;
			// Busca nós no grafo com esse nome e tipo Método
			var candidates = graphInstance.Nodes.Values
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
			var winnerFileId = fileVotes.MaxBy(kvp => kvp.Value).Key;
			return graphInstance.GetFilePath(winnerFileId);
		}

		return null;
	}

	private void ApplyMerge(List<CodeBlockItem> originalBlocks, List<CodeBlockItem> snippetBlocks, MergeResult result)
	{
		var timestamp = DateTime.Now;
		string versionDesc = "Sugestão IA";

		foreach (var snippetBlock in snippetBlocks)
		{
			// Ignorar blocos triviais (chaves, espaços) ou usings na comparação principal 
			// para evitar duplicidade de estrutura, focando em membros lógicos.
			if (!snippetBlock.IsGranular && snippetBlock.SegmentType != SegmentType.Using)
				continue;

			// Tenta encontrar correspondência exata de Nome e Tipo
			var match = originalBlocks.FirstOrDefault(b =>
				b.Name == snippetBlock.Name &&
				b.SegmentType == snippetBlock.SegmentType);

			if (match != null)
			{
				// Verifica se o conteúdo realmente mudou antes de criar versão
				if (match.Content.Trim() != snippetBlock.Content.Trim())
				{
					match.CreateNewVersion(snippetBlock.Content, versionDesc, timestamp);
					result.BlocksModified++;
				}
			}
			else
			{
				// É um bloco NOVO (ex: a IA sugeriu um novo método helper)
				// Precisamos inseri-lo em um lugar lógico.
				// Heurística simples: Inserir antes do último fechamento de classe.

				InsertNewBlock(originalBlocks, snippetBlock);
				result.NewBlocksAdded++;
			}
		}
	}

	private void InsertNewBlock(List<CodeBlockItem> originalBlocks, CodeBlockItem newBlock)
	{
		// Define o bloco como "Novo" visualmente
		newBlock.InitializeVersions(string.Empty); // Versão original vazia
		newBlock.CreateNewVersion(newBlock.Content, "Novo Item (IA)", DateTime.Now);

		// Encontra o último fechamento de classe ("}")
		var lastCloseBrace = originalBlocks.LastOrDefault(b => b.Content.Contains("}"));

		if (lastCloseBrace != null)
		{
			var index = originalBlocks.IndexOf(lastCloseBrace);
			originalBlocks.Insert(index, newBlock);
		}
		else
		{
			// Fallback: adiciona no final
			originalBlocks.Add(newBlock);
		}
	}
}