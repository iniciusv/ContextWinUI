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

	// ARQUIVO: AiCodeMerger.cs

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
			// 1. Identifica o arquivo de destino
			var targetFilePath = await IdentifyTargetFileAsync(snippetCode);

			// Se não achou por identificação automática, tenta usar o que está aberto na UI (se sua arquitetura permitisse passar esse contexto)
			// Por enquanto, mantemos a lógica original de erro se não achar
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

			// 2. Parse do arquivo original (Esse funciona bem pois é um arquivo válido)
			var originalBlocks = await _parserService.ParseFileAsync(targetFilePath, originalContent);

			// 3. Parse do Snippet (AQUI ESTÁ A CORREÇÃO)
			// Usamos um método especial que embrulha o código se necessário
			var snippetBlocks = await ParseSnippetWithFallbackAsync(targetFilePath, snippetCode);

			// 4. Executa o Merge
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

	// NOVO MÉTODO AUXILIAR
	private async Task<List<CodeBlockItem>> ParseSnippetWithFallbackAsync(string filePath, string snippetCode)
	{
		// Tentativa 1: Parse direto (funciona se for uma classe completa)
		var blocks = await _parserService.ParseFileAsync(filePath, snippetCode);

		// Verifica se achou algo útil (Métodos, Propriedades, etc)
		// Se só achou Trivia/Using, provavelmente o parse falhou em reconhecer a estrutura
		bool hasMeaningfulBlocks = blocks.Any(b => b.IsGranular || b.SegmentType == SegmentType.Class);

		if (hasMeaningfulBlocks)
		{
			return blocks;
		}

		// Tentativa 2: Embrulhar em uma classe fictícia para forçar o Roslyn a reconhecer métodos
		var wrapperCode = $"public class AiSnippetWrapper_Temp {{ \n{snippetCode}\n }}";
		var wrappedBlocks = await _parserService.ParseFileAsync(filePath, wrapperCode);

		// Agora precisamos extrair o que está DENTRO do Wrapper e descartar o Wrapper em si
		var extractedBlocks = wrappedBlocks
			.Where(b => b.Name != "AiSnippetWrapper_Temp" && b.Name != "Corpo de AiSnippetWrapper_Temp") // Filtra a classe wrapper
			.Select(b => {
				// Ajuste opcional de profundidade se necessário, mas para o merge o que importa é Nome e Tipo
				return b;
			})
			.ToList();

		return extractedBlocks;
	}

	private async Task<string?> IdentifyTargetFileAsync(string snippet)
	{
		// 1. Tentativa Direta (caso o usuário tenha colado uma classe inteira)
		var tree = CSharpSyntaxTree.ParseText(snippet);
		var root = await tree.GetRootAsync();

		// Verifica se é uma classe
		var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
		if (classNode != null)
		{
			var className = classNode.Identifier.Text;
			var graph = _indexService.GetCurrentGraph();
			var symbolNode = graph.Nodes.Values
				.FirstOrDefault(n => n.Name == className &&
								   (n.Type == SymbolType.Class || n.Type == SymbolType.Interface));

			if (symbolNode != null)
			{
				return graph.GetFilePath(symbolNode.FileId);
			}
		}

		// 2. TÉCNICA DO WRAPPER: Se não achou classe, pode ser um método solto.
		// O Roslyn tem dificuldade de identificar 'MethodDeclarationSyntax' solto na raiz 
		// se tiver modificadores (private/public). Vamos embrulhar numa classe falsa.

		var textToParse = snippet;
		if (classNode == null)
		{
			// Embrulha o código para garantir que o parse identifique os métodos corretamente
			textToParse = $"class DummyWrapper {{ {snippet} }}";
			tree = CSharpSyntaxTree.ParseText(textToParse);
			root = await tree.GetRootAsync();
		}

		// Agora buscamos os métodos (seja no root original ou dentro do Wrapper)
		var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
		var fileVotes = new Dictionary<int, int>();
		var graphInstance = _indexService.GetCurrentGraph();

		foreach (var method in methods)
		{
			var methodName = method.Identifier.Text;

			// Busca quem tem esse método no projeto
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
			var winnerId = fileVotes.MaxBy(kv => kv.Value).Key;
			return graphInstance.GetFilePath(winnerId);
		}

		// Fallback: Se o método for NOVO (não existe no grafo), a votação retornará 0.
		// Nesse caso, o ideal seria o ViewModel passar o arquivo atualmente aberto 
		// como sugestão padrão, mas aqui no serviço retornamos null.
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