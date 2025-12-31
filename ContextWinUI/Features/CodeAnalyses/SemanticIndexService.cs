using ColorCode.Styling;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class SemanticIndexService : ISemanticIndexService
{
	private DependencyGraph _cachedGraph = new();
	private string _cachedRootPath = string.Empty;

	// Adicione este método que estava faltando
	public async Task<DependencyGraph> GetOrIndexProjectAsync(string rootPath)
	{
		if (_cachedRootPath == rootPath && _cachedGraph.Nodes.Any())
		{
			return _cachedGraph;
		}

		return await Task.Run(async () => await IndexProjectAsync(rootPath));
	}

	public async Task<DependencyGraph> IndexProjectAsync(string rootPath)
	{
		// 1. Leitura rápida dos arquivos (mantém como estava)
		var filePaths = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
				.Where(f => !f.Contains("obj") && !f.Contains("bin"));

		var syntaxTrees = new ConcurrentBag<SyntaxTree>();

		// Configuração de parse otimizada
		var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None);

		await Parallel.ForEachAsync(filePaths, async (path, ct) =>
		{
			var text = await File.ReadAllTextAsync(path, ct);
			var tree = CSharpSyntaxTree.ParseText(text, parseOptions, path: path, cancellationToken: ct);
			syntaxTrees.Add(tree);
		});

		// 2. Criação da Compilação (Isso é Single Threaded por natureza do Roslyn, não tem jeito)
		var compilation = CSharpCompilation.Create("ContextAnalysis_Session")
			.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddSyntaxTrees(syntaxTrees);

		var graph = new DependencyGraph();

		// 3. OTIMIZAÇÃO CRÍTICA: Processamento Paralelo dos Modelos Semânticos
		// Em vez de foreach simples, usamos Parallel para visitar as árvores simultaneamente
		await Task.Run(() =>
		{
			Parallel.ForEach(syntaxTrees, tree =>
			{
				// GetSemanticModel pode ser chamado concorrentemente
				var model = compilation.GetSemanticModel(tree);
				var walker = new GraphBuilderWalker(graph, model, tree.FilePath);

				// GetRoot é rápido pois a árvore já está em memória
				var root = tree.GetRoot();
				walker.Visit(root);
			});
		});

		_cachedGraph = graph;
		_cachedRootPath = rootPath; // Cache o path para evitar re-indexação desnecessária
		return graph;
	}

	public DependencyGraph GetCurrentGraph() => _cachedGraph;

	public SymbolNode? InferSymbolFromGraph(string word, string filePath, int absolutePosition)
	{
		if (_cachedGraph == null || string.IsNullOrWhiteSpace(word))
			return null;

		// OTIMIZAÇÃO: Primeiro pegamos o ID do arquivo a partir da string
		int fileId = _cachedGraph.GetOrAddFileId(filePath);

		// Se o arquivo não está no índice, retornamos null
		if (!_cachedGraph.FileIndex.TryGetValue(fileId, out var fileNodes))
		{
			return null;
		}

		// Busca local (dentro do arquivo)
		var containerNode = fileNodes
			.Where(n => n.StartPosition <= absolutePosition &&
						(n.StartPosition + n.Length) >= absolutePosition)
			.OrderBy(n => n.Length)
			.FirstOrDefault();

		var localMember = fileNodes.FirstOrDefault(n => n.Name == word);
		if (localMember != null) // Node é struct/class, verificação de null pode precisar de ajuste dependendo do uso
		{
			// Nota: Se SymbolNode for ref type, null check é valido. 
			// Se mudou para struct, use 'default'. Aqui assumi que SymbolNode é class.
			return localMember;
		}

		// Busca global
		// Aqui não mudou muito, pois NameIndex ainda é string
		var globalType = _cachedGraph.Nodes.Values
			.FirstOrDefault(n => n.Name == word &&
								(n.Type == SymbolType.Class ||
								 n.Type == SymbolType.Interface ||
								 n.Type == SymbolType.Enum ||
								 n.Type == SymbolType.Struct));

		return globalType;
	}

	public SymbolType? GetSymbolType(string word)
	{
		if (_cachedGraph == null) return null;

		if (_cachedGraph.GlobalSymbolCache.TryGetValue(word, out SymbolType type))
			return type;

		return null;
	}

}