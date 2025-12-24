using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextWinUI.Services;

public class AiResponseParser
{
	private readonly AiCommentAnalyzer _analyzer = new();
	private static readonly Regex FileHeaderRegex = new Regex(
		@"^(?:\/\/\s*ARQUIVO:|\/\/\s*FILE:|\*\*\s*ARQUIVO:|\*\*\s*FILE:)\s*(?<path>[\w\.\-\\\/\s]+?)(?:\s*$|\s*\n)",
		RegexOptions.Multiline | RegexOptions.IgnoreCase);

	public List<ProposedFileChange> ParseInput(string rawInput, string projectRoot, DependencyGraph? dependencyGraph = null)
	{
		var changes = new List<ProposedFileChange>();
		if (string.IsNullOrWhiteSpace(rawInput)) return changes;

		rawInput = rawInput.Replace("\r\n", "\n");
		var matches = FileHeaderRegex.Matches(rawInput);

		if (matches.Count > 0)
		{
			for (int i = 0; i < matches.Count; i++)
			{
				var match = matches[i];
				var relativePath = match.Groups["path"].Value.Trim();

				if (relativePath.Contains('('))
					relativePath = relativePath.Split('(')[0].Trim();

				int startIndex = match.Index + match.Length + 1;
				int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : rawInput.Length;

				if (startIndex >= endIndex) continue;

				string contentBlock = rawInput.Substring(startIndex, endIndex - startIndex);
				string cleanedContent = CleanMarkdown(contentBlock);
				string fullPath = NormalizePath(projectRoot, relativePath);

				changes.Add(new ProposedFileChange
				{
					FilePath = fullPath,
					NewContent = cleanedContent,
					Status = "Pendente"
				});
			}
		}
		else if (dependencyGraph != null)
		{
			string cleanedContent = CleanMarkdown(rawInput);

			// ALTERAÇÃO: Usando o método estático centralizado
			string? detectedPath = TryFindFileByCodeStructure(cleanedContent, dependencyGraph);

			if (!string.IsNullOrEmpty(detectedPath))
			{
				changes.Add(new ProposedFileChange
				{
					FilePath = detectedPath,
					NewContent = cleanedContent,
					Status = "Detectado Automaticamente"
				});
			}
			else
			{
				changes.Add(new ProposedFileChange
				{
					FilePath = Path.Combine(projectRoot, "NovoArquivo_IA.cs"),
					NewContent = cleanedContent,
					Status = "Novo Arquivo (Caminho incerto)"
				});
			}
		}

		foreach (var change in changes)
		{
			_analyzer.AnalyzeAndEnrich(change);
			if (change.IsSnippet)
			{
				change.Status = "Snippet (Requer Merge)";
			}
		}

		return changes;
	}

	private string CleanMarkdown(string text)
	{
		text = text.Trim();
		var lines = text.Split('\n').ToList();
		if (lines.Count > 0 && lines[0].Trim().StartsWith("```"))
			lines.RemoveAt(0);
		if (lines.Count > 0 && lines[lines.Count - 1].Trim().StartsWith("```"))
			lines.RemoveAt(lines.Count - 1);
		return string.Join("\n", lines).Trim();
	}

	private string NormalizePath(string root, string relative)
	{
		relative = relative.Replace('/', Path.DirectorySeparatorChar)
						   .Replace('\\', Path.DirectorySeparatorChar);
		if (relative.StartsWith(root)) return relative;
		return Path.Combine(root, relative);
	}

	// ALTERAÇÃO: Método promovido a 'public static' para reutilização
	public static string? TryFindFileByCodeStructure(string code, DependencyGraph graph)
	{
		try
		{
			// Tenta envolver em classe caso seja apenas um método solto
			string wrappedCode = $"public class __Wrapper__ {{ {code} }}";
			var tree = CSharpSyntaxTree.ParseText(wrappedCode);
			var root = tree.GetRoot();

			// 1. Procura construtores no wrapper (indica nome da classe)
			var ctor = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (ctor != null)
			{
				var classNode = graph.Nodes.Values.FirstOrDefault(n => n.Name == ctor.Identifier.Text && n.Type == SymbolType.Class);
				if (classNode != null) return classNode.FilePath;
			}

			// 2. Procura classe direta (caso o código já seja a classe inteira)
			var treeRaw = CSharpSyntaxTree.ParseText(code);
			var classDecl = treeRaw.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classDecl != null)
			{
				var classNode = graph.Nodes.Values.FirstOrDefault(n => n.Name == classDecl.Identifier.Text && n.Type == SymbolType.Class);
				if (classNode != null) return classNode.FilePath;
			}

			// 3. Procura métodos únicos (heurística forte)
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				var candidates = graph.Nodes.Values
					.Where(n => n.Name == method.Identifier.Text && n.Type == SymbolType.Method)
					.ToList();

				// Se só existe UM método com esse nome no projeto inteiro, achamos o arquivo
				if (candidates.Count == 1)
				{
					return candidates[0].FilePath;
				}
			}
		}
		catch
		{
			// Falha silenciosa na inferência
		}
		return null;
	}
}