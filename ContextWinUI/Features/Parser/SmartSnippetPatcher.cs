using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class SmartSnippetPatcher
{
	private readonly SemanticIndexService _indexService;

	public SmartSnippetPatcher(SemanticIndexService indexService)
	{
		_indexService = indexService;
	}

	public async Task<ProposedFileChange?> PatchAsync(string snippetCode, string projectRoot)
	{
		// Tenta parsear normalmente primeiro
		var tree = CSharpSyntaxTree.ParseText(snippetCode);
		var root = tree.GetRoot();

		// Tenta achar a classe declarada explicitamente
		var snippetClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

		// --- NOVO: LÓGICA DE ROBUSTEZ (Envelopamento) ---
		if (snippetClass == null)
		{
			// Se não achou classe, envelopa o código para validar membros soltos
			string wrappedCode = $"public class __WrapperTemp__ {{ \n{snippetCode}\n }}";
			var wrappedTree = CSharpSyntaxTree.ParseText(wrappedCode);
			var wrappedRoot = wrappedTree.GetRoot();

			var wrapperClass = wrappedRoot.DescendantNodes()
										  .OfType<ClassDeclarationSyntax>()
										  .FirstOrDefault(c => c.Identifier.Text == "__WrapperTemp__");

			if (wrapperClass != null && wrapperClass.Members.Any())
			{
				// Tenta descobrir o nome da classe real baseando-se nos membros
				string? inferredClassName = InferClassNameFromMembers(wrapperClass);

				if (inferredClassName != null)
				{
					// Recria uma classe sintática com o nome correto para o Rewriter usar
					snippetClass = SyntaxFactory.ClassDeclaration(inferredClassName)
												.WithMembers(wrapperClass.Members);
				}
			}
		}
		// ------------------------------------------------

		if (snippetClass == null) return null; // Desistimos se não conseguimos inferir nada

		// Busca no Grafo (agora temos um nome de classe, seja explícito ou inferido)
		var graph = _indexService.GetCurrentGraph();
		var targetNode = graph.Nodes.Values.FirstOrDefault(n => n.Name == snippetClass.Identifier.Text && n.Type == SymbolType.Class);

		if (targetNode == null || !File.Exists(targetNode.FilePath)) return null;

		string originalFilePath = targetNode.FilePath;
		string originalCode = await File.ReadAllTextAsync(originalFilePath);

		// Executar o Merge
		var originalTree = CSharpSyntaxTree.ParseText(originalCode);
		var rewriter = new SnippetMergerRewriter(snippetClass);

		var newRoot = rewriter.Visit(originalTree.GetRoot());

		if (newRoot.ToFullString() == originalTree.GetRoot().ToFullString()) return null;

		return new ProposedFileChange
		{
			FilePath = originalFilePath,
			OriginalContent = originalCode,
			NewContent = newRoot.ToFullString(),
			Status = "Smart Patch (Inferido)"
		};
	}

	private string? InferClassNameFromMembers(ClassDeclarationSyntax wrapperClass)
	{
		// PISTA 1: Construtores (Certeza de 100%)
		// Se tem "public MainViewModel()", a classe TEM que ser MainViewModel
		var constructor = wrapperClass.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
		if (constructor != null)
		{
			return constructor.Identifier.Text;
		}

		// PISTA 2: Métodos únicos (Busca no Grafo)
		// Se não tem construtor, olhamos os métodos e tentamos achar qual classe no projeto tem esse método
		var methods = wrapperClass.Members.OfType<MethodDeclarationSyntax>();
		var graph = _indexService.GetCurrentGraph();

		foreach (var method in methods)
		{
			string methodName = method.Identifier.Text;

			// Procura no grafo quem tem esse método
			// O Grafo mapeia nós. Precisamos achar um nó do tipo Method com esse nome, 
			// e pegar o "Parent" (que é a classe).

			// Como seu SymbolNode atual não tem link direto "Parent", usamos o FileIndex ou conveção
			// Vamos procurar nós com esse nome e ver a qual arquivo pertencem

			var methodNodes = graph.Nodes.Values.Where(n => n.Name == methodName && n.Type == SymbolType.Method).ToList();

			if (methodNodes.Count == 1)
			{
				// Achamos um método único no projeto inteiro! 
				// Vamos descobrir o nome da classe desse arquivo.
				var fileClasses = graph.FileIndex.GetValueOrDefault(methodNodes[0].FilePath.ToLowerInvariant());
				var parentClass = fileClasses?.FirstOrDefault(n => n.Type == SymbolType.Class);

				if (parentClass != null) return parentClass.Name;
			}
		}

		return null;
	}
	// Adicione dentro da classe SmartSnippetPatcher

	public string InspectSnippetStructure(string codeSnippet)
	{
		var sb = new System.Text.StringBuilder();
		sb.AppendLine("=== INÍCIO DA INSPEÇÃO ROSLYN ===");
		sb.AppendLine($"Tamanho do Input: {codeSnippet.Length} caracteres");

		// 1. Parse
		var tree = CSharpSyntaxTree.ParseText(codeSnippet);
		var root = tree.GetRoot();

		// 2. Verificar Erros de Sintaxe (Diagnostics)
		// O Roslyn é tolerante, mas diagnósticos mostram se ele "se perdeu"
		var diagnostics = tree.GetDiagnostics();
		if (diagnostics.Any())
		{
			sb.AppendLine("\n[ALERTA] Diagnósticos encontrados (Syntax Errors):");
			foreach (var diag in diagnostics)
			{
				// Ignora erros comuns de snippets incompletos, mas lista os graves
				sb.AppendLine($" - {diag.Id}: {diag.GetMessage()} (Linha: {diag.Location.GetLineSpan().StartLinePosition.Line})");
			}
		}
		else
		{
			sb.AppendLine("\n[OK] Nenhuma erro de sintaxe grave detectado.");
		}

		// 3. Testar a Linha Crítica de Detecção
		var detectedClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

		sb.AppendLine("\n--- ANÁLISE DE ESTRUTURA ---");

		if (detectedClass != null)
		{
			sb.AppendLine($"[SUCESSO] Classe Detectada: '{detectedClass.Identifier.Text}'");

			// Listar o que tem dentro da classe
			sb.AppendLine("Membros encontrados dentro da classe:");
			foreach (var member in detectedClass.Members)
			{
				string tipoMembro = member.Kind().ToString(); // Ex: MethodDeclaration, PropertyDeclaration
				string nome = GetMemberName(member);
				sb.AppendLine($"   -> {tipoMembro}: {nome}");
			}
		}
		else
		{
			sb.AppendLine("[FALHA] Nenhuma 'ClassDeclarationSyntax' encontrada.");
			sb.AppendLine("O Roslyn vê este código como Top-Level Statements ou apenas métodos soltos.");

			// O que ele encontrou então?
			var firstNode = root.DescendantNodes().FirstOrDefault();
			sb.AppendLine($"Primeiro nó encontrado: {firstNode?.Kind().ToString() ?? "Nenhum"}");
		}

		sb.AppendLine("=================================");
		return sb.ToString();
	}

	// Helper para pegar nome de métodos/propriedades genericamente
	private string GetMemberName(MemberDeclarationSyntax member)
	{
		if (member is MethodDeclarationSyntax m) return m.Identifier.Text;
		if (member is PropertyDeclarationSyntax p) return p.Identifier.Text;
		if (member is ConstructorDeclarationSyntax c) return c.Identifier.Text;
		if (member is FieldDeclarationSyntax f) return f.Declaration.Variables.First().Identifier.Text;
		return "(sem nome)";
	}
}