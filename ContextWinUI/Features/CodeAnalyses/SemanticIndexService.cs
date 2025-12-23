using ContextWinUI.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class SemanticIndexService
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

	// ARQUIVO: SemanticIndexService.cs
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
}