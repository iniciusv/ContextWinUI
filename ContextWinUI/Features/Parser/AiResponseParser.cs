using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis;
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

		// 1. Tenta achar cabeçalhos explícitos
		if (matches.Count > 0)
		{
			for (int i = 0; i < matches.Count; i++)
			{
				var match = matches[i];
				var relativePath = match.Groups["path"].Value.Trim();
				if (relativePath.Contains('(')) relativePath = relativePath.Split('(')[0].Trim();

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
					Status = "Identificado por Cabeçalho"
				});
			}
		}
		// 2. Se não tem cabeçalho, tenta detectar pelo código (Revertido para lógica estrutural)
		else if (dependencyGraph != null)
		{
			string cleanedContent = CleanMarkdown(rawInput);

			// Usa a lógica direta: Acha o método no grafo e retorna o arquivo dele
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
					Status = "Novo Arquivo (Não detectado)"
				});
			}
		}

		foreach (var change in changes)
		{
			_analyzer.AnalyzeAndEnrich(change);
		}

		return changes;
	}

	// LÓGICA REVERTIDA E MELHORADA
	public static string? TryFindFileByCodeStructure(string code, DependencyGraph graph)
	{
		try
		{
			// Envolvemos o código em uma classe wrapper para garantir que o Roslyn consiga 
			// parsear métodos soltos (snippets) sem erro de sintaxe.
			string wrappedCode = $"public class __Wrapper__ {{ \n{code}\n }}";
			var tree = CSharpSyntaxTree.ParseText(wrappedCode);
			var root = tree.GetRoot();

			// 1. Procura por Construtores
			var ctor = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (ctor != null)
			{
				// Se achou construtor 'MainViewModel', procura a classe 'MainViewModel' no grafo
				var classNode = graph.Nodes.Values.FirstOrDefault(n => n.Name == ctor.Identifier.Text && n.Type == SymbolType.Class);
				if (classNode != null) return classNode.FilePath;
			}

			// 2. Procura por Classes completas
			// Nota: Usamos parse do código original (sem wrapper) para checar se é uma classe inteira
			var treeRaw = CSharpSyntaxTree.ParseText(code);
			var classDecl = treeRaw.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classDecl != null)
			{
				var classNode = graph.Nodes.Values.FirstOrDefault(n => n.Name == classDecl.Identifier.Text && n.Type == SymbolType.Class);
				if (classNode != null) return classNode.FilePath;
			}

			// 3. Procura por Métodos (O caso mais comum de snippet)
			// Voltamos a usar o 'root' do wrappedCode
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				// Busca EXATA pelo nome do método no grafo
				var candidates = graph.Nodes.Values
					.Where(n => n.Name == method.Identifier.Text && n.Type == SymbolType.Method)
					.ToList();

				if (candidates.Count == 1)
				{
					// Match perfeito (só existe um método com esse nome no projeto todo)
					return candidates[0].FilePath;
				}
				else if (candidates.Count > 1)
				{
					// Ambíguo (ex: 'Execute'). Tenta desempatar pelos parametros se possível, 
					// ou simplesmente retorna o primeiro por enquanto.
					// (Melhoria: verificar qual arquivo tem os 'usings' ou tipos compatíveis)
					return candidates[0].FilePath;
				}
			}

			// 4. Procura por Propriedades (se for snippet só de prop)
			var props = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
			foreach (var prop in props)
			{
				var candidate = graph.Nodes.Values
					.FirstOrDefault(n => n.Name == prop.Identifier.Text && n.Type == SymbolType.Property);

				if (candidate != null) return candidate.FilePath;
			}
		}
		catch
		{
			// Ignora erros de parse
		}
		return null;
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
}