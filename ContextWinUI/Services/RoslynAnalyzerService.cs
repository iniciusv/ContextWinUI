using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class RoslynAnalyzerService
{
	// Mapa de NomeDoTipo -> CaminhoDoArquivo
	private Dictionary<string, string> _projectTypeMap = new();

	// DTO para retorno da análise
	public class FileAnalysisResult
	{
		public List<string> Methods { get; set; } = new();
		public List<string> Dependencies { get; set; } = new();
	}

	// 1. Indexa o projeto inteiro (Mapeia Onde está cada Classe/Interface)
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

				// Pega declarações de classes, interfaces, enums, structs
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
				// Em caso de duplicata, o último vence
				_projectTypeMap[typeName] = result.FilePath;
			}
		}
	}

	// 2. Analisa a estrutura de um arquivo específico
	public async Task<FileAnalysisResult> AnalyzeFileStructureAsync(string filePath)
	{
		var result = new FileAnalysisResult();

		try
		{
			var code = await File.ReadAllTextAsync(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = await tree.GetRootAsync();

			// A. Extrair Métodos
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				// Formata: Nome(TipoParam1, TipoParam2)
				var paramsList = method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
				var signature = $"{method.Identifier.Text}({string.Join(", ", paramsList)})";
				result.Methods.Add(signature);
			}

			// B. Encontrar Dependências (Tipos usados no arquivo)
			var identifiers = root.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Select(id => id.Identifier.Text)
				.Distinct();

			var dependenciesFound = new HashSet<string>();

			foreach (var id in identifiers)
			{
				// Se o identificador é um tipo conhecido no projeto E não é o próprio arquivo
				if (_projectTypeMap.TryGetValue(id, out var depPath))
				{
					// Evita auto-referência
					if (!string.Equals(depPath, filePath, StringComparison.OrdinalIgnoreCase))
					{
						dependenciesFound.Add(depPath);
					}
				}
			}

			result.Dependencies = dependenciesFound.ToList();
		}
		catch
		{
			// Ignora erros de parse para não travar a UI
		}

		return result;
	}
}