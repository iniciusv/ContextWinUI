using ColorCode.Styling;
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

		// 1. Busca Local: Identificar nós dentro do arquivo atual
		// Normalizamos a chave para garantir o match no dicionário
		string fileKey = filePath.ToLowerInvariant();

		if (!_cachedGraph.FileIndex.TryGetValue(fileKey, out var fileNodes))
		{
			return null; // Arquivo não indexado ou novo
		}

		// 2. Identificar Contexto: Em qual container (Método/Classe) o cursor está?
		// Buscamos o nó mais "apertado" (menor Length) que contém a posição.
		// Isso garante que se estivermos num Método dentro de uma Classe, pegaremos o Método primeiro.
		var containerNode = fileNodes
			.Where(n => n.StartPosition <= absolutePosition &&
						(n.StartPosition + n.Length) >= absolutePosition)
			.OrderBy(n => n.Length) // Menor para o maior (Método -> Classe -> Namespace)
			.FirstOrDefault();

		// 3. TENTATIVA A: É um membro da mesma classe/arquivo?
		// Procuramos por: Propriedades, Métodos, Campos definidos neste mesmo arquivo
		// que tenham o nome exato da palavra.
		var localMember = fileNodes.FirstOrDefault(n => n.Name == word);

		// Se achamos e não é o próprio container onde estamos (ex: recursão), retornamos.
		if (localMember != null)
		{
			return localMember;
		}

		// 4. TENTATIVA B: É um Tipo (Classe/Interface) definido em outro lugar do projeto?
		// Se a palavra não está no arquivo, pode ser uma referência a outra classe (ex: "DependencyGraph graph")
		// Esta busca varre todos os nós. Se o projeto for MUITO grande, idealmente o DependencyGraph teria um "NameIndex".
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