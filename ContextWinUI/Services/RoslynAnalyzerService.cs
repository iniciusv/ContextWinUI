using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class RoslynAnalyzerService
{
	public class MethodDependency
	{
		public string MethodName { get; set; } = string.Empty;
		public string FullName { get; set; } = string.Empty;
		public string FilePath { get; set; } = string.Empty;
		public List<string> CalledMethods { get; set; } = new();
		public List<string> UsedTypes { get; set; } = new();
		public List<string> UsedNamespaces { get; set; } = new();
		public List<string> AccessedMembers { get; set; } = new();
		public string SourceCode { get; set; } = string.Empty;
	}

	public async Task<List<MethodDependency>> AnalyzeMethodDependenciesAsync(string filePath)
	{
		var code = await File.ReadAllTextAsync(filePath);
		var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
		var root = await tree.GetRootAsync();

		var compilation = CSharpCompilation.Create("Analysis")
			.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddSyntaxTrees(tree);

		var semanticModel = compilation.GetSemanticModel(tree);

		var methods = root.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.ToList();

		var dependencies = new List<MethodDependency>();

		foreach (var method in methods)
		{
			var dependency = await AnalyzeMethodAsync(method, semanticModel, filePath);
			dependencies.Add(dependency);
		}

		return dependencies;
	}

	private async Task<MethodDependency> AnalyzeMethodAsync(
		MethodDeclarationSyntax method,
		SemanticModel semanticModel,
		string filePath)
	{
		var dependency = new MethodDependency
		{
			MethodName = method.Identifier.Text,
			FilePath = filePath,
			SourceCode = method.ToString()
		};

		var methodSymbol = semanticModel.GetDeclaredSymbol(method);
		if (methodSymbol != null)
		{
			dependency.FullName = methodSymbol.ToDisplayString();
		}

		// Encontrar invocações de métodos
		var invocations = method.DescendantNodes()
			.OfType<InvocationExpressionSyntax>();

		foreach (var invocation in invocations)
		{
			var symbolInfo = semanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is IMethodSymbol invokedMethod)
			{
				var fullMethodName = invokedMethod.ToDisplayString();
				if (!dependency.CalledMethods.Contains(fullMethodName))
				{
					dependency.CalledMethods.Add(fullMethodName);
				}

				// Adicionar namespace usado
				var ns = invokedMethod.ContainingNamespace?.ToDisplayString();
				if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>" &&
					!dependency.UsedNamespaces.Contains(ns))
				{
					dependency.UsedNamespaces.Add(ns);
				}
			}
		}

		// Encontrar tipos utilizados
		var typeNodes = method.DescendantNodes()
			.Where(n => n is TypeSyntax || n is ObjectCreationExpressionSyntax);

		foreach (var typeNode in typeNodes)
		{
			var typeInfo = semanticModel.GetTypeInfo(typeNode);
			if (typeInfo.Type != null)
			{
				var typeName = typeInfo.Type.ToDisplayString();
				if (!dependency.UsedTypes.Contains(typeName))
				{
					dependency.UsedTypes.Add(typeName);
				}

				// Adicionar namespace do tipo
				var ns = typeInfo.Type.ContainingNamespace?.ToDisplayString();
				if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>" &&
					!dependency.UsedNamespaces.Contains(ns))
				{
					dependency.UsedNamespaces.Add(ns);
				}
			}
		}

		// Encontrar acesso a propriedades e campos
		var memberAccesses = method.DescendantNodes()
			.OfType<MemberAccessExpressionSyntax>();

		foreach (var memberAccess in memberAccesses)
		{
			var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
			if (symbolInfo.Symbol != null)
			{
				var memberName = symbolInfo.Symbol.ToDisplayString();
				if (!dependency.AccessedMembers.Contains(memberName))
				{
					dependency.AccessedMembers.Add(memberName);
				}
			}
		}

		return dependency;
	}

	public async Task<HashSet<string>> GetAllDependentFilesAsync(
		string projectPath,
		string methodName,
		string className)
	{
		var dependentFiles = new HashSet<string>();
		var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
			.ToList();

		foreach (var file in csFiles)
		{
			try
			{
				var code = await File.ReadAllTextAsync(file);
				var tree = CSharpSyntaxTree.ParseText(code, path: file);
				var root = await tree.GetRootAsync();

				var compilation = CSharpCompilation.Create("DependencyAnalysis")
					.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
					.AddSyntaxTrees(tree);

				var semanticModel = compilation.GetSemanticModel(tree);

				// Procurar invocações do método
				var invocations = root.DescendantNodes()
					.OfType<InvocationExpressionSyntax>();

				foreach (var invocation in invocations)
				{
					var symbolInfo = semanticModel.GetSymbolInfo(invocation);
					if (symbolInfo.Symbol is IMethodSymbol method)
					{
						if (method.Name == methodName &&
							method.ContainingType?.Name == className)
						{
							dependentFiles.Add(file);
							break;
						}
					}
				}
			}
			catch
			{
				// Ignora erros de análise
			}
		}

		return dependentFiles;
	}
}