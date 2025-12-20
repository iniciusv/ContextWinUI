using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Services;

public class RoslynCsharpStrategy : ILanguageStrategy
{
	private readonly Dictionary<string, string> _projectTypeMap;

	public RoslynCsharpStrategy(Dictionary<string, string> projectTypeMap)
	{
		_projectTypeMap = projectTypeMap;
	}

	public bool CanHandle(string extension) => extension.ToLower() == ".cs";

	public async Task<RoslynAnalyzerService.FileAnalysisResult> AnalyzeAsync(string filePath)
	{
		var result = new RoslynAnalyzerService.FileAnalysisResult();
		try
		{
			var code = await File.ReadAllTextAsync(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = await tree.GetRootAsync();

			// 1. Extração de Métodos (Mantido)
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				var paramsList = method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
				var signature = $"{method.Identifier.Text}({string.Join(", ", paramsList)})";
				result.Methods.Add(signature);
			}

			// 2. Extração de Dependências (Aprimorado)
			var potentialTypes = new HashSet<string>();

			// A) Herança (Classes base e Interfaces)
			var baseLists = root.DescendantNodes().OfType<BaseListSyntax>();
			foreach (var baseList in baseLists)
			{
				foreach (var type in baseList.Types)
				{
					potentialTypes.Add(type.Type.ToString());
				}
			}

			// B) Injeção de Dependência (Parâmetros de Construtor)
			var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
			foreach (var ctor in constructors)
			{
				foreach (var param in ctor.ParameterList.Parameters)
				{
					if (param.Type != null)
						potentialTypes.Add(param.Type.ToString());
				}
			}

			// C) Campos e Propriedades (Composição)
			var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
			foreach (var prop in properties)
			{
				if (prop.Type != null)
					potentialTypes.Add(prop.Type.ToString());
			}

			var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
			foreach (var field in fields)
			{
				if (field.Declaration.Type != null)
					potentialTypes.Add(field.Declaration.Type.ToString());
			}

			// 3. Validação Cruzada com o Mapa do Projeto
			// Verifica se os tipos encontrados realmente pertencem ao projeto
			foreach (var typeName in potentialTypes)
			{
				// Remove genéricos (ex: List<MainViewModel> -> MainViewModel)
				var cleanName = CleanTypeName(typeName);

				// Tenta encontrar no mapa global do projeto
				if (_projectTypeMap.TryGetValue(cleanName, out var depPath))
				{
					// Evita auto-referência
					if (depPath != filePath)
					{
						result.Dependencies.Add(depPath);
					}
				}
				// Tenta encontrar interfaces (ex: IFileSystemService)
				else if (_projectTypeMap.TryGetValue("I" + cleanName, out var interfacePath))
				{
					if (interfacePath != filePath) result.Dependencies.Add(interfacePath);
				}
			}

			result.Dependencies = result.Dependencies.Distinct().ToList();
		}
		catch { }

		return result;
	}

	private string CleanTypeName(string typeName)
	{
		// Limpa List<T>, IEnumerable<T>, nullable?, arrays[]
		var name = typeName.Trim();

		if (name.Contains("<"))
			name = name.Split('<')[1].Split('>')[0]; // Pega o T de List<T>

		return name.Replace("?", "").Replace("[]", "").Trim();
	}
}