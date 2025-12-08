// ==================== C:\Users\vinic\source\repos\ContextWinUI\ContextWinUI\Services\RoslynAnalyzerService.cs ====================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class RoslynAnalyzerService
{
	private Dictionary<string, string> _projectTypeMap = new();

	public class FileAnalysisResult
	{
		public List<string> Methods { get; set; } = new();
		public List<string> Dependencies { get; set; } = new();
	}

	public async Task IndexProjectAsync(string rootPath)
	{
		_projectTypeMap.Clear();

		var csFiles = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains(".g.cs"));

		var tasks = csFiles.Select(async file =>
		{
			try
			{
				var code = await File.ReadAllTextAsync(file);
				var tree = CSharpSyntaxTree.ParseText(code);
				var root = await tree.GetRootAsync();

				var typeDeclarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
				var typesInFile = typeDeclarations.Select(t => t.Identifier.Text).ToList();

				return (FilePath: file, Types: typesInFile);
			}
			catch
			{
				return (FilePath: file, Types: new List<string>());
			}
		});

		var results = await Task.WhenAll(tasks);

		foreach (var result in results)
		{
			foreach (var typeName in result.Types)
			{
				_projectTypeMap[typeName] = result.FilePath;
			}
		}
	}

	// --- CORREÇÃO APLICADA AQUI EMBAIXO ---
	public async Task<FileAnalysisResult> AnalyzeFileStructureAsync(string filePath)
	{
		var result = new FileAnalysisResult();

		try
		{
			var code = await File.ReadAllTextAsync(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = await tree.GetRootAsync();

			// 1. Extrair Métodos
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				var paramsList = method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
				var signature = $"{method.Identifier.Text}({string.Join(", ", paramsList)})";
				result.Methods.Add(signature);
			}

			// 2. Extrair Dependências (Tipos usados) - LÓGICA REFINADA
			var identifiers = root.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(id => IsValidDependency(id)) // Filtro novo
				.Select(id => id.Identifier.Text)
				.Distinct();

			var dependencies = new HashSet<string>();
			foreach (var id in identifiers)
			{
				// Se o tipo existe no mapa do projeto e não é o próprio arquivo
				if (_projectTypeMap.TryGetValue(id, out var depPath) && depPath != filePath)
				{
					dependencies.Add(depPath);
				}
			}
			result.Dependencies = dependencies.ToList();
		}
		catch { /* Ignorar erros de parse */ }

		return result;
	}

	/// <summary>
	/// Verifica se o identificador é uma dependência válida de código (propriedade, variável, retorno)
	/// e ignora usings e declarações de namespace.
	/// </summary>
	private bool IsValidDependency(IdentifierNameSyntax id)
	{
		// 1. Ignora se faz parte de um 'using SBrain.Inventory...;'
		// O FirstAncestorOrSelf sobe na árvore até achar (ou não) um nó desse tipo.
		if (id.FirstAncestorOrSelf<UsingDirectiveSyntax>() != null)
		{
			return false;
		}

		// 2. Ignora se faz parte da declaração do namespace (ex: namespace Baker.Domain...)
		// Nota: Não podemos ignorar apenas por ter um pai NamespaceDeclarationSyntax, 
		// pois todo o código está dentro dele. Temos que ver se o ID faz parte do NOME do namespace.
		var namespaceDecl = id.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
		if (namespaceDecl != null)
		{
			// Se o nó identificador estiver dentro do nó "Name" da declaração do namespace, ignoramos.
			if (namespaceDecl.Name.Contains(id))
			{
				return false;
			}
		}

		return true;
	}
}