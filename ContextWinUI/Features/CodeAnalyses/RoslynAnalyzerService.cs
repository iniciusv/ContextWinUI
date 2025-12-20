using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class RoslynAnalyzerService : IRoslynAnalyzerService
{
	private Dictionary<string, string> _projectTypeMap = new();
	private readonly List<ILanguageStrategy> _strategies;

	public class FileAnalysisResult
	{
		public List<string> Methods { get; set; } = new();
		public List<string> Dependencies { get; set; } = new();
	}

	public RoslynAnalyzerService()
	{
		_strategies = new List<ILanguageStrategy>
		{
			new RoslynCsharpStrategy(_projectTypeMap),
			new RegexScriptStrategy(_projectTypeMap)
		};
	}

	public async Task IndexProjectAsync(string rootPath)
	{
		_projectTypeMap.Clear();

		if (Directory.Exists(rootPath))
		{
			var csFiles = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
				.Where(f => !IsIgnoredFolder(f));

			foreach (var file in csFiles)
			{
				try
				{
					var code = await File.ReadAllTextAsync(file);
					var tree = CSharpSyntaxTree.ParseText(code);
					var root = await tree.GetRootAsync();
					var typeDeclarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
					foreach (var t in typeDeclarations)
					{
						_projectTypeMap[t.Identifier.Text] = file;
					}
				}
				catch { }
			}

			var jsExtensions = new[] { "*.js", "*.jsx", "*.ts", "*.tsx", "*.vue" };
			foreach (var ext in jsExtensions)
			{
				var jsFiles = Directory.GetFiles(rootPath, ext, SearchOption.AllDirectories)
					.Where(f => !IsIgnoredFolder(f));

				foreach (var file in jsFiles)
				{
					var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
					if (!_projectTypeMap.ContainsKey(fileNameNoExt))
					{
						_projectTypeMap[fileNameNoExt] = file;
					}
				}
			}
		}
	}

	private bool IsIgnoredFolder(string path)
	{
		return path.Contains("\\bin\\") ||
			   path.Contains("\\obj\\") ||
			   path.Contains("\\node_modules\\") ||
			   path.Contains(".g.cs") ||
			   path.Contains(".min.js");
	}

	public async Task<FileAnalysisResult> AnalyzeFileStructureAsync(string filePath)
	{
		var ext = Path.GetExtension(filePath).ToLower();
		var strategy = _strategies.FirstOrDefault(s => s.CanHandle(ext));

		if (strategy != null)
		{
			return await strategy.AnalyzeAsync(filePath);
		}

		return new FileAnalysisResult();
	}

	public async Task<string> FilterClassContentAsync(
		string filePath,
		IEnumerable<string>? keptMethodSignatures,
		bool removeUsings,
		bool removeNamespaces,
		bool removeComments,
		bool removeEmptyLines)
	{
		if (!File.Exists(filePath)) return string.Empty;

		try
		{
			var code = await File.ReadAllTextAsync(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = await tree.GetRootAsync();

			// O Rewriter trata da estrutura sintática (remover métodos não selecionados, usings, comentários triviais)
			var rewriter = new MethodFilterRewriter(keptMethodSignatures, removeUsings, removeComments);

			var newRoot = rewriter.Visit(root);

			if (newRoot != null)
			{
				var processedCode = newRoot.ToFullString();
				// O Helper trata das limpezas textuais finais (Namespaces e Linhas vazias)
				return Helpers.CodeCleanupHelper.ProcessCode(processedCode, ".cs", false, removeNamespaces, false, removeEmptyLines);
			}
		}
		catch { }

		return string.Empty;
	}

	public async Task<List<string>> GetMethodCallsAsync(string filePath, string methodSignature)
	{
		if (!filePath.EndsWith(".cs")) return new List<string>();

		var calls = new List<string>();
		try
		{
			var code = await File.ReadAllTextAsync(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = await tree.GetRootAsync();

			var classVariableMap = BuildClassVariableMap(root);
			var methodName = methodSignature.Split('(')[0].Trim();

			var methodNode = root.DescendantNodes()
									 .OfType<MethodDeclarationSyntax>()
									 .FirstOrDefault(m => m.Identifier.Text == methodName);

			if (methodNode != null && methodNode.Body != null)
			{
				var invocations = methodNode.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();

				foreach (var invocation in invocations)
				{
					string callName = "";

					if (invocation.Expression is IdentifierNameSyntax simpleName)
					{
						callName = simpleName.Identifier.Text;
					}
					else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
					{
						var objectName = memberAccess.Expression.ToString();
						var methodCalled = memberAccess.Name.Identifier.Text;

						if (classVariableMap.TryGetValue(objectName, out var typeName))
						{
							callName = $"{typeName}.{methodCalled}";
						}
						else
						{
							callName = $"{objectName}.{methodCalled}";
						}
					}

					if (!string.IsNullOrEmpty(callName))
					{
						calls.Add(callName);
					}
				}
			}
		}
		catch { }

		return calls.Distinct().ToList();
	}

	private Dictionary<string, string> BuildClassVariableMap(SyntaxNode root)
	{
		var map = new Dictionary<string, string>();
		var props = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
		foreach (var prop in props)
		{
			map[prop.Identifier.Text] = ExtractSimpleName(prop.Type.ToString());
		}
		var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
		foreach (var field in fields)
		{
			var typeName = ExtractSimpleName(field.Declaration.Type.ToString());
			foreach (var variable in field.Declaration.Variables)
			{
				map[variable.Identifier.Text] = typeName;
			}
		}
		return map;
	}

	private string ExtractSimpleName(string typeName)
	{
		if (typeName.Contains("<"))
		{
			return typeName.Substring(0, typeName.IndexOf("<"));
		}
		return typeName;
	}
}