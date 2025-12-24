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

	// Regex para capturar cabeçalhos como:
	// // ARQUIVO: Folder/File.cs
	// // FILE: File.cs
	// // PATH: ...
	private static readonly Regex FileHeaderRegex = new Regex(
		@"^//\s*(?:ARQUIVO|FILE|PATH):\s*(?<path>.+)$",
		RegexOptions.Multiline | RegexOptions.IgnoreCase);

	/// <summary>
	/// Analisa o texto bruto e retorna uma lista de alterações propostas.
	/// Usa Regex para cabeçalhos explícitos ou Roslyn para inferir arquivos pelo conteúdo.
	/// </summary>
	public List<ProposedFileChange> ParseInput(string rawInput, string projectRoot, DependencyGraph? dependencyGraph = null)
	{
		var changes = new List<ProposedFileChange>();
		if (string.IsNullOrWhiteSpace(rawInput)) return changes;

		// Normaliza quebras de linha para facilitar regex e split
		rawInput = rawInput.Replace("\r\n", "\n");

		var matches = FileHeaderRegex.Matches(rawInput);

		// --- ESTRATÉGIA A: Cabeçalhos Explícitos ---
		if (matches.Count > 0)
		{
			for (int i = 0; i < matches.Count; i++)
			{
				var match = matches[i];
				var relativePath = match.Groups["path"].Value.Trim();

				// Remove textos extras que a IA possa ter colocado no cabeçalho (ex: " (novo)")
				if (relativePath.Contains('('))
					relativePath = relativePath.Split('(')[0].Trim();

				// Calcula onde começa e termina o código deste arquivo
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
		// --- ESTRATÉGIA B: Detecção Inteligente (Sem cabeçalhos) ---
		else if (dependencyGraph != null)
		{
			// O usuário colou um bloco de código solto. Vamos tentar descobrir de quem ele é.
			string cleanedContent = CleanMarkdown(rawInput);

			// Tenta achar via Roslyn (procura classes/métodos no grafo do projeto)
			string? detectedPath = TryFindFileByCodeStructure(cleanedContent, dependencyGraph);

			if (!string.IsNullOrEmpty(detectedPath))
			{
				changes.Add(new ProposedFileChange
				{
					FilePath = detectedPath,
					NewContent = cleanedContent,
					Status = "Detectado Automaticamente" // ViewModel decidirá se precisa de Merge depois
				});
			}
			else
			{
				// Fallback: Se não achou nada, cria como "Novo Arquivo Sem Nome" para o usuário decidir
				changes.Add(new ProposedFileChange
				{
					FilePath = Path.Combine(projectRoot, "NovoArquivo_IA.cs"),
					NewContent = cleanedContent,
					Status = "Novo Arquivo (Caminho incerto)"
				});
			}
		}

		// --- ENRIQUECIMENTO: Análise de Comentários e Intenção ---
		foreach (var change in changes)
		{
			_analyzer.AnalyzeAndEnrich(change);

			// Se o analisador detectou "// ...", ajusta o status
			if (change.IsSnippet)
			{
				change.Status = "Snippet (Requer Merge)";
			}
		}

		return changes;
	}

	/// <summary>
	/// Remove blocos markdown (```csharp ... ```).
	/// </summary>
	private string CleanMarkdown(string text)
	{
		text = text.Trim();
		var lines = text.Split('\n').ToList();

		// Remove primeira linha se for ```
		if (lines.Count > 0 && lines[0].Trim().StartsWith("```"))
			lines.RemoveAt(0);

		// Remove última linha se for ```
		if (lines.Count > 0 && lines[lines.Count - 1].Trim().StartsWith("```"))
			lines.RemoveAt(lines.Count - 1);

		return string.Join("\n", lines).Trim();
	}

	private string NormalizePath(string root, string relative)
	{
		// Garante separadores corretos do SO
		relative = relative.Replace('/', Path.DirectorySeparatorChar)
						   .Replace('\\', Path.DirectorySeparatorChar);

		// Se o caminho relativo já vier com o root (alucinação da IA), corrige
		if (relative.StartsWith(root)) return relative;

		return Path.Combine(root, relative);
	}

	/// <summary>
	/// Tenta identificar o arquivo alvo analisando a estrutura sintática (Classe/Métodos)
	/// e comparando com o Grafo de Dependências do projeto.
	/// </summary>
	private string? TryFindFileByCodeStructure(string code, DependencyGraph graph)
	{
		try
		{
			// Envelopa em classe temporária caso sejam métodos soltos (evita erro CS0106)
			string wrappedCode = $"public class __Wrapper__ {{ {code} }}";
			var tree = CSharpSyntaxTree.ParseText(wrappedCode);
			var root = tree.GetRoot();

			// 1. Procura Construtores (Pista mais forte)
			var ctor = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
			if (ctor != null)
			{
				// Se achou "public MainViewModel()", busca a classe MainViewModel no grafo
				var classNode = graph.Nodes.Values.FirstOrDefault(n => n.Name == ctor.Identifier.Text && n.Type == SymbolType.Class);
				return classNode?.FilePath;
			}

			// 2. Procura Classes explícitas (se o usuário colou a classe inteira)
			// Parse do código original (sem wrapper)
			var treeRaw = CSharpSyntaxTree.ParseText(code);
			var classDecl = treeRaw.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classDecl != null)
			{
				var classNode = graph.Nodes.Values.FirstOrDefault(n => n.Name == classDecl.Identifier.Text && n.Type == SymbolType.Class);
				return classNode?.FilePath;
			}

			// 3. Procura Métodos únicos (Pista média)
			// Se o código tem um método que só existe em UM lugar no projeto todo
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				var candidates = graph.Nodes.Values
					.Where(n => n.Name == method.Identifier.Text && n.Type == SymbolType.Method)
					.ToList();

				if (candidates.Count == 1)
				{
					return candidates[0].FilePath;
				}
			}
		}
		catch
		{
			// Falha silenciosa no parse, retorna null
		}
		return null;
	}
}