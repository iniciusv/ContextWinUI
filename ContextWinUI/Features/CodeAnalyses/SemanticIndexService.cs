using ColorCode.Styling;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Features.CodeEditor.Highlight;
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

	private CSharpCompilation? _cachedCompilation;
	private CSharpParseOptions _parseOptions = new(LanguageVersion.Latest, DocumentationMode.None);

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
				.Where(f => !f.Contains("obj") && !f.Contains("bin"));

		var syntaxTrees = new ConcurrentBag<SyntaxTree>();

		// Usar as opções salvas no campo da classe
		await Parallel.ForEachAsync(filePaths, async (path, ct) =>
		{
			var text = await File.ReadAllTextAsync(path, ct);
			var tree = CSharpSyntaxTree.ParseText(text, _parseOptions, path: path, cancellationToken: ct);
			syntaxTrees.Add(tree);
		});

		// Criar e armazenar a compilação no campo privado
		_cachedCompilation = CSharpCompilation.Create("ContextAnalysis_Session")
			.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddSyntaxTrees(syntaxTrees);

		var graph = new DependencyGraph();

		await Task.Run(() =>
		{
			// Nota: Para acesso concorrente seguro, é melhor iterar a lista fixa da compilação
			Parallel.ForEach(_cachedCompilation.SyntaxTrees, tree =>
			{
				var model = _cachedCompilation.GetSemanticModel(tree);
				var walker = new GraphBuilderWalker(graph, model, tree.FilePath);
				var root = tree.GetRoot();
				walker.Visit(root);
			});
		});

		_cachedGraph = graph;
		_cachedRootPath = rootPath;
		return graph;
	}

	public DependencyGraph GetCurrentGraph() => _cachedGraph;

	public async Task<string?> GetSourceContentAsync(string filePath)
	{
		if (_cachedCompilation == null) return null;

		// Busca a árvore de sintaxe correspondente ao caminho do arquivo
		var tree = _cachedCompilation.SyntaxTrees.FirstOrDefault(t =>
			string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

		if (tree != null)
		{
			// Retorna o texto que está na memória da compilação
			var sourceText = await tree.GetTextAsync();
			return sourceText.ToString();
		}

		return null;
	}

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
	public async Task UpdateSourceFileAsync(string filePath, string newContent)
	{
		if (_cachedCompilation == null || _cachedGraph == null) return;

		// 1. Identificar a SyntaxTree antiga
		var oldTree = _cachedCompilation.SyntaxTrees.FirstOrDefault(t =>
			string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

		// 2. Criar a nova SyntaxTree
		var newTree = CSharpSyntaxTree.ParseText(newContent, _parseOptions, path: filePath);

		// 3. Atualizar a Compilação (Imutável: retorna uma nova instância)
		if (oldTree != null)
		{
			_cachedCompilation = _cachedCompilation.ReplaceSyntaxTree(oldTree, newTree);
		}
		else
		{
			_cachedCompilation = _cachedCompilation.AddSyntaxTrees(newTree);
		}

		// 4. Limpar dados antigos do Grafo para este arquivo
		int fileId = _cachedGraph.GetOrAddFileId(filePath);
		_cachedGraph.RemoveNodesForFile(fileId);

		// 5. Re-analisar apenas este arquivo usando a NOVA compilação
		await Task.Run(() =>
		{
			var model = _cachedCompilation.GetSemanticModel(newTree);
			var walker = new GraphBuilderWalker(_cachedGraph, model, filePath);
			var root = newTree.GetRoot();
			walker.Visit(root);
		});
	}
	public async Task ReloadFilesAsync(IEnumerable<string> filePaths)
	{
		if (_cachedCompilation == null || _cachedGraph == null) return;

		foreach (var path in filePaths)
		{
			try
			{
				// Verifica a existência física do arquivo para decidir a ação
				if (File.Exists(path))
				{
					// ARQUIVO EXISTE -> ATUALIZA (Update)
					var newContent = await File.ReadAllTextAsync(path);
					await UpdateSourceFileAsync(path, newContent);
				}
				else
				{
					// ARQUIVO NÃO EXISTE -> REMOVE (Delete)
					await RemoveSourceFileAsync(path);
				}
			}
			catch (Exception ex)
			{
				// Log de erro (pode ser ajustado conforme sua infra de log)
				System.Diagnostics.Debug.WriteLine($"Erro ao processar arquivo {path}: {ex.Message}");
			}
		}
	}

	// Método privado para remover o arquivo do Roslyn e do Grafo
	private async Task RemoveSourceFileAsync(string filePath)
	{
		if (_cachedCompilation == null || _cachedGraph == null) return;

		// 1. Encontrar e remover a árvore de sintaxe do Compilation (Roslyn)
		var oldTree = _cachedCompilation.SyntaxTrees.FirstOrDefault(t =>
			string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

		if (oldTree != null)
		{
			// RemoveSyntaxTrees cria uma nova compilação imutável sem aquela árvore
			_cachedCompilation = _cachedCompilation.RemoveSyntaxTrees(oldTree);
		}

		// 2. Remover do Grafo de Dependências Customizado
		// Obtém o ID do arquivo (mesmo que não exista mais no disco, o ID existe no cache)
		int fileId = _cachedGraph.GetOrAddFileId(filePath);

		// Remove todos os nós associados a este arquivo
		_cachedGraph.RemoveNodesForFile(fileId);

		// Opcional: Remover o ID do índice de arquivos do grafo para não crescer indefinidamente
		// _cachedGraph.FileIndex.TryRemove(fileId, out _); 
		// Nota: Dependendo da implementação do IDependencyGraph, pode ser necessário um método específico
		// para remover a entrada do dicionário de caminhos se quiser limpar totalmente.

		await Task.CompletedTask; // Mantém a assinatura async para consistência
	}
	public SymbolType? GetSymbolType(string word)
	{
		if (_cachedGraph == null) return null;

		if (_cachedGraph.GlobalSymbolCache.TryGetValue(word, out SymbolType type))
			return type;

		return null;
	}

}