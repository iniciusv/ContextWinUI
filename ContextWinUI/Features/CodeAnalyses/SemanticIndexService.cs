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

	public async Task<DependencyGraph> IndexProjectAsync(string rootPath)
	{
		var filePaths = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
			 .Where(f => !f.Contains("obj") && !f.Contains("bin")); // Filtro básico

		// 1. Parse em Paralelo (CPU Bound)
		var syntaxTrees = new ConcurrentBag<SyntaxTree>();
		await Parallel.ForEachAsync(filePaths, async (path, ct) =>
		{
			var text = await File.ReadAllTextAsync(path, ct);
			var tree = CSharpSyntaxTree.ParseText(text, path: path, cancellationToken: ct);
			syntaxTrees.Add(tree);
		});

		// 2. Compilação Única (Resolve símbolos entre arquivos)
		var compilation = CSharpCompilation.Create("ContextAnalysis_Session")
			.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)) // mscorlib
			.AddSyntaxTrees(syntaxTrees);

		// 3. Povoar Grafo
		var graph = new DependencyGraph();

		// Processamento sequencial aqui é mais seguro para o SemanticModel, 
		// mas pode ser paralelizado se criarmos um SemanticModel por árvore
		foreach (var tree in syntaxTrees)
		{
			var model = compilation.GetSemanticModel(tree);
			var walker = new GraphBuilderWalker(graph, model, tree.FilePath);
			var root = await tree.GetRootAsync();
			walker.Visit(root);
		}

		_cachedGraph = graph;
		return graph;
	}

	public DependencyGraph GetCurrentGraph() => _cachedGraph;
}