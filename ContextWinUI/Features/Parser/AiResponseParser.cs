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
	private static readonly Regex FileHeaderRegex = new Regex(
		@"^//\s*(?:ARQUIVO|FILE|PATH):\s*(?<path>.+)$",
		RegexOptions.Multiline | RegexOptions.IgnoreCase);

	// Agora aceita o grafo para fazer a busca inteligente
	public List<ProposedFileChange> ParseInput(string rawInput, string projectRoot, DependencyGraph? dependencyGraph = null)
	{
		var changes = new List<ProposedFileChange>();
		if (string.IsNullOrWhiteSpace(rawInput)) return changes;

		rawInput = rawInput.Replace("\r\n", "\n");
		var matches = FileHeaderRegex.Matches(rawInput);

		// CENÁRIO 1: A IA usou os cabeçalhos // ARQUIVO: ... (Prioridade Alta)
		if (matches.Count > 0)
		{
			for (int i = 0; i < matches.Count; i++)
			{
				var match = matches[i];
				var relativePath = match.Groups["path"].Value.Trim();
				int startIndex = match.Index + match.Length + 1;
				int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : rawInput.Length;

				if (startIndex >= endIndex) continue;

				string content = CleanMarkdown(rawInput.Substring(startIndex, endIndex - startIndex));
				string fullPath = NormalizePath(projectRoot, relativePath);

				changes.Add(new ProposedFileChange { FilePath = fullPath, NewContent = content });
			}
		}
		// CENÁRIO 2: Sem cabeçalhos. Tentar detecção via Roslyn (Smart Detect)
		else if (dependencyGraph != null)
		{
			// Assume que o texto colado é um arquivo inteiro ou um bloco de classe
			string content = CleanMarkdown(rawInput);
			string? detectedPath = TryFindFileByCodeStructure(content, dependencyGraph);

			if (!string.IsNullOrEmpty(detectedPath))
			{
				changes.Add(new ProposedFileChange
				{
					FilePath = detectedPath,
					NewContent = content,
					// Adiciona um aviso visual se foi detectado automaticamente
					Status = "Detectado via Roslyn"
				});
			}
		}

		return changes;
	}

	private string? TryFindFileByCodeStructure(string code, DependencyGraph graph)
	{
		try
		{
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = tree.GetRoot();

			// Procura pela primeira declaração de Classe, Interface ou Struct
			var typeDecl = root.DescendantNodes()
							   .OfType<TypeDeclarationSyntax>()
							   .FirstOrDefault();

			if (typeDecl != null)
			{
				string typeName = typeDecl.Identifier.Text;

				// Busca no Grafo de Dependências existente
				// O grafo mapeia IDs/Nomes para SymbolNodes, que contêm o FilePath
				var node = graph.Nodes.Values
								.FirstOrDefault(n => n.Name == typeName && !string.IsNullOrEmpty(n.FilePath));

				if (node != null)
				{
					return node.FilePath;
				}
			}
		}
		catch
		{
			// Ignora erros de parse, pois o código pode estar incompleto
		}
		return null;
	}

	private string CleanMarkdown(string text)
	{
		text = text.Trim();
		if (text.StartsWith("```"))
		{
			var lines = text.Split('\n').ToList();
			if (lines.Count > 0 && lines[0].StartsWith("```")) lines.RemoveAt(0);
			if (lines.Count > 0 && lines[lines.Count - 1].StartsWith("```")) lines.RemoveAt(lines.Count - 1);
			return string.Join("\n", lines);
		}
		return text;
	}

	private string NormalizePath(string root, string relative)
	{
		return Path.Combine(root, relative)
				   .Replace('/', Path.DirectorySeparatorChar)
				   .Replace('\\', Path.DirectorySeparatorChar);
	}
}