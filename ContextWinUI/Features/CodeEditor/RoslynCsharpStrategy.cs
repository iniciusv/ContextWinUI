using ContextWinUI.Core.Contracts;
using ContextWinUI.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class RoslynCsharpStrategy : ILanguageStrategy
{
	// Referência ao mapa de tipos compartilhado pelo serviço principal
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

			// 1. Extrair Métodos
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				var paramsList = method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
				var signature = $"{method.Identifier.Text}({string.Join(", ", paramsList)})";
				result.Methods.Add(signature);
			}

			// 2. Extrair Identificadores para Dependências
			var identifiers = root.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(id => IsValidDependency(id))
				.Select(id => id.Identifier.Text)
				.Distinct();

			// 3. Extrair Tipos Base (Herança)
			var baseTypesList = new List<string>();
			var baseDecls = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();

			foreach (var baseDecl in baseDecls)
			{
				if (baseDecl.BaseList != null)
				{
					foreach (var baseType in baseDecl.BaseList.Types)
					{
						var typesInBase = baseType.DescendantNodesAndSelf()
												  .OfType<IdentifierNameSyntax>()
												  .Select(id => id.Identifier.Text);
						baseTypesList.AddRange(typesInBase);
					}
				}
			}

			// 4. Cruzar dados com o Mapa do Projeto
			var allPotentialDeps = identifiers.Concat(baseTypesList).Distinct();

			foreach (var id in allPotentialDeps)
			{
				if (_projectTypeMap.TryGetValue(id, out var depPath) && depPath != filePath)
				{
					result.Dependencies.Add(depPath);
				}
			}

			// Remove duplicatas finais
			result.Dependencies = result.Dependencies.Distinct().ToList();
		}
		catch { }

		return result;
	}

	private bool IsValidDependency(IdentifierNameSyntax id)
	{
		if (id.FirstAncestorOrSelf<UsingDirectiveSyntax>() != null)
			return false;

		var namespaceDecl = id.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
		if (namespaceDecl != null && namespaceDecl.Name.ToString().Contains(id.Identifier.Text))
			return false;

		return true;
	}
}