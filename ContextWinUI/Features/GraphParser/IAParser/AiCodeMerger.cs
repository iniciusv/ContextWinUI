using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models; // Para SymbolType
using ContextWinUI.Features.CodeAnalyses; // Para DependencyGraph (se necessário)
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.Services; // Namespace do novo serviço
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.IAParser;

public class AiCodeMerger : IAiCodeMerger
{
	private readonly ISemanticIndexService _indexService;
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeBlockParserService _parserService;
	private readonly IBlockIdentityService _identityService; // <--- A Chave da Solução

	public AiCodeMerger(
		ISemanticIndexService indexService,
		IFileSystemService fileSystemService,
		ICodeBlockParserService parserService,
		IBlockIdentityService identityService) // <--- Injeção via Construtor
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_parserService = parserService;
		_identityService = identityService;
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
			// 1. Identifica a qual arquivo este snippet pertence
			var targetFilePath = await IdentifyTargetFileAsync(snippetCode);

			if (string.IsNullOrEmpty(targetFilePath) || !File.Exists(targetFilePath))
			{
				result.Message = "Não foi possível identificar o arquivo de origem correspondente no projeto.";
				return result;
			}

			result.FilePath = targetFilePath;
			var originalContent = await _fileSystemService.ReadFileContentAsync(targetFilePath);

			if (string.IsNullOrEmpty(originalContent))
			{
				result.Message = "O arquivo identificado está vazio ou não pôde ser lido.";
				return result;
			}

			// 2. Parse do Arquivo Original (do Disco)
			var originalBlocks = await _parserService.ParseFileAsync(targetFilePath, originalContent);

			// 3. Parse do Snippet da IA (com fallback para embrulhar métodos soltos)
			var snippetBlocks = await ParseSnippetWithFallbackAsync(targetFilePath, snippetCode);

			// 4. Executa o Merge usando o IdentityService
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

	private async Task<List<CodeBlockItem>> ParseSnippetWithFallbackAsync(string filePath, string snippetCode)
	{
		// Tentativa 1: Parse direto (ideal se a IA mandou a classe completa)
		var blocks = await _parserService.ParseFileAsync(filePath, snippetCode);

		// Verifica se o resultado tem "sustância" (Métodos, Propriedades ou Classe)
		bool hasMeaningfulBlocks = blocks.Any(b => b.IsGranular || b.SegmentType == SegmentType.Class);

		if (hasMeaningfulBlocks)
		{
			return blocks;
		}

		// Tentativa 2: A IA mandou apenas um método solto "public void Foo()..."
		// O Roslyn pode se perder com modificadores de acesso na raiz. Embrulhamos numa classe temporária.
		var wrapperCode = $"public class AiSnippetWrapper_Temp {{ \n{snippetCode}\n }}";
		var wrappedBlocks = await _parserService.ParseFileAsync(filePath, wrapperCode);

		// Extraímos o conteúdo de dentro do Wrapper
		var extractedBlocks = wrappedBlocks
			.Where(b => b.Name != "AiSnippetWrapper_Temp" && b.Name != "Corpo de AiSnippetWrapper_Temp")
			.ToList();

		return extractedBlocks;
	}

	private async Task<string?> IdentifyTargetFileAsync(string snippet)
	{
		// Cenário A: O Snippet é uma Classe completa
		var tree = CSharpSyntaxTree.ParseText(snippet);
		var root = await tree.GetRootAsync();

		var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
		if (classNode != null)
		{
			var className = classNode.Identifier.Text;
			var graph = _indexService.GetCurrentGraph();
			// Busca no grafo global por essa classe
			var symbolNode = graph.Nodes.Values
				.FirstOrDefault(n => n.Name == className &&
								   (n.Type == SymbolType.Class || n.Type == SymbolType.Interface));

			if (symbolNode != null)
			{
				return graph.GetFilePath(symbolNode.FileId);
			}
		}

		// Cenário B: O Snippet são métodos soltos (Smart Paste comum)
		var textToParse = snippet;
		if (classNode == null)
		{
			// Embrulha para garantir que o Roslyn veja os métodos
			textToParse = $"class DummyWrapper {{ {snippet} }}";
			tree = CSharpSyntaxTree.ParseText(textToParse);
			root = await tree.GetRootAsync();
		}

		var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
		var fileVotes = new Dictionary<int, int>();
		var graphInstance = _indexService.GetCurrentGraph();

		// Faz uma "votação": para cada método no snippet, quem é o dono dele no projeto?
		foreach (var method in methods)
		{
			var methodName = method.Identifier.Text;

			var candidates = graphInstance.Nodes.Values
				.Where(n => n.Name == methodName && n.Type == SymbolType.Method);

			// Aprimoramento: Aqui poderíamos usar assinatura também se o grafo suportasse,
			// mas por nome já é suficiente para identificar o ARQUIVO na maioria dos casos.
			foreach (var candidate in candidates)
			{
				if (!fileVotes.ContainsKey(candidate.FileId))
					fileVotes[candidate.FileId] = 0;
				fileVotes[candidate.FileId]++;
			}
		}

		if (fileVotes.Any())
		{
			// O arquivo com mais "hits" ganha
			var winnerId = fileVotes.MaxBy(kv => kv.Value).Key;
			return graphInstance.GetFilePath(winnerId);
		}

		return null;
	}

	private void PerformMerge(List<CodeBlockItem> originalBlocks, List<CodeBlockItem> snippetBlocks, AiMergeResult result)
	{
		var timestamp = DateTime.Now;
		string versionDescription = "Sugestão IA";

		foreach (var snippetBlock in snippetBlocks)
		{
			if (!snippetBlock.IsGranular) continue;

			// --- USO CENTRALIZADO DA LÓGICA DE IDENTIDADE ---
			// Aqui usamos o serviço que sabe ignorar namespaces e checar assinaturas corretamente
			var originalBlock = originalBlocks.FirstOrDefault(b => _identityService.AreSameEntity(b, snippetBlock));
			// ------------------------------------------------

			if (originalBlock != null)
			{
				// Normaliza (Trim) para evitar falsos positivos de mudanças apenas de espaço
				if (originalBlock.Content.Trim() != snippetBlock.Content.Trim())
				{
					originalBlock.CreateNewVersion(snippetBlock.Content, versionDescription, timestamp);
					result.ModifiedCount++;
				}
			}
			else
			{
				// Se o IdentityService disse que não existe, é realmente novo (ou sobrecarga nova)
				InsertNewBlockIdeally(originalBlocks, snippetBlock, result);
				result.AddedCount++;
			}
		}
	}

	private void InsertNewBlockIdeally(List<CodeBlockItem> originalBlocks, CodeBlockItem newBlock, AiMergeResult result)
	{
		newBlock.Id = Guid.NewGuid().ToString();
		newBlock.InitializeVersions(string.Empty);
		newBlock.CreateNewVersion(newBlock.Content, "Novo Item (IA)", DateTime.Now);
		newBlock.TypeDescription += " (Novo)";

		// Tenta encontrar o melhor lugar para inserir (antes do fechamento da classe)
		var closingBlockIndex = -1;

		for (int i = originalBlocks.Count - 1; i >= 0; i--)
		{
			var block = originalBlocks[i];
			// Procura pelo fechamento "}"
			if (block.Content.Contains("}"))
			{
				closingBlockIndex = i;
				break;
			}
		}

		if (closingBlockIndex >= 0)
		{
			// Herda identação do bloco anterior ao fechamento para ficar bonito
			if (closingBlockIndex > 0)
			{
				newBlock.DepthLevel = originalBlocks[closingBlockIndex - 1].DepthLevel;
			}

			originalBlocks.Insert(closingBlockIndex, newBlock);

			// Adiciona um espaçamento (Gap) visual
			var gapBlock = new CodeBlockItem
			{
				Content = "\n\t",
				SegmentType = SegmentType.Gap,
				DepthLevel = newBlock.DepthLevel,
				Name = "Gap (Auto)"
			};
			gapBlock.InitializeVersions("\n\t");
			originalBlocks.Insert(closingBlockIndex, gapBlock);
		}
		else
		{
			originalBlocks.Add(newBlock);
		}
	}
}