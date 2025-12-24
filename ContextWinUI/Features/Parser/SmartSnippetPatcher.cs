using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class SmartSnippetPatcher
{
	private readonly SemanticIndexService _indexService;

	// Regex para capturar o caminho, ignorando textos extras como "(método adicionado)"
	private static readonly Regex HeaderRegex = new Regex(
		@"^//\s*(?:ARQUIVO|FILE|PATH):\s*(?<path>[\w\.\\/\-]+)",
		RegexOptions.Multiline | RegexOptions.IgnoreCase);

	public SmartSnippetPatcher(SemanticIndexService indexService)
	{
		_indexService = indexService;
	}

	public async Task<ProposedFileChange?> PatchAsync(string snippetCode, string projectRoot)
	{
		// 1. Tenta identificar o arquivo alvo pelo cabeçalho (Estratégia mais forte)
		string? targetFilePath = DetectFileFromHeader(snippetCode, projectRoot);

		// 2. Prepara o Snippet para análise (Envelopamento Virtual)
		// Isso resolve o erro CS0106 (public não permitido em script)
		string wrappedCode = $"public class __WrapperTemp__ {{ \n{snippetCode}\n }}";
		var wrappedTree = CSharpSyntaxTree.ParseText(wrappedCode);
		var wrappedRoot = wrappedTree.GetRoot();

		var wrapperClass = wrappedRoot.DescendantNodes()
									  .OfType<ClassDeclarationSyntax>()
									  .FirstOrDefault(c => c.Identifier.Text == "__WrapperTemp__");

		if (wrapperClass == null) return null; // Snippet ininteligível

		// 3. Se não achou pelo cabeçalho, tenta inferir pelo conteúdo (Construtores/Métodos existentes)
		if (string.IsNullOrEmpty(targetFilePath))
		{
			targetFilePath = InferTargetFile(wrapperClass);
		}

		// Se falhou em tudo, desiste
		if (string.IsNullOrEmpty(targetFilePath) || !File.Exists(targetFilePath)) return null;

		// 4. Executa o Merge (Inserção ou Substituição)
		string originalCode = await File.ReadAllTextAsync(targetFilePath);
		var originalTree = CSharpSyntaxTree.ParseText(originalCode);

		// Descobre o nome da classe principal no arquivo original para saber onde injetar
		var mainClassInOriginal = originalTree.GetRoot().DescendantNodes()
											  .OfType<ClassDeclarationSyntax>()
											  .FirstOrDefault(); // Pega a primeira classe (assumindo 1 classe por arquivo)

		if (mainClassInOriginal == null) return null;

		// Configura o Rewriter com os membros extraídos do Wrapper
		var rewriter = new RobustMergerRewriter(mainClassInOriginal.Identifier.Text, wrapperClass.Members);

		var newRoot = rewriter.Visit(originalTree.GetRoot());

		// Formata o código para ficar bonito (arruma identação das injeções)
		newRoot = newRoot.NormalizeWhitespace();

		return new ProposedFileChange
		{
			FilePath = targetFilePath,
			OriginalContent = originalCode,
			NewContent = newRoot.ToFullString(),
			Status = "Smart Merge (Inserção/Edição)"
		};
	}

	private string? DetectFileFromHeader(string snippet, string projectRoot)
	{
		var match = HeaderRegex.Match(snippet);
		if (match.Success)
		{
			string relativePath = match.Groups["path"].Value.Trim();
			// Normaliza barras
			relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

			// Tenta encontrar o arquivo exato
			string fullPath = Path.Combine(projectRoot, relativePath);
			if (File.Exists(fullPath)) return fullPath;

			// Busca recursiva caso o caminho seja parcial (ex: ScenariosService.cs sem pasta)
			var files = Directory.GetFiles(projectRoot, Path.GetFileName(relativePath), SearchOption.AllDirectories);
			return files.FirstOrDefault();
		}
		return null;
	}

	private string? InferTargetFile(ClassDeclarationSyntax wrapperClass)
	{
		var graph = _indexService.GetCurrentGraph();

		// Pista 1: Construtor
		var constructor = wrapperClass.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
		if (constructor != null)
		{
			var node = graph.Nodes.Values.FirstOrDefault(n => n.Name == constructor.Identifier.Text && n.Type == SymbolType.Class);
			return node?.FilePath;
		}

		// Pista 2: Método Único (Só funciona para métodos que JÁ existem no projeto)
		// Para métodos novos, isso falha, por isso dependemos do HeaderRegex acima.
		foreach (var method in wrapperClass.Members.OfType<MethodDeclarationSyntax>())
		{
			var nodes = graph.Nodes.Values.Where(n => n.Name == method.Identifier.Text && n.Type == SymbolType.Method).ToList();
			if (nodes.Count == 1) return nodes[0].FilePath;
		}

		return null;
	}
}

// O Rewriter Robusto que sabe INSERIR e SUBSTITUIR
public class RobustMergerRewriter : CSharpSyntaxRewriter
{
	private readonly string _targetClassName;
	private readonly List<MemberDeclarationSyntax> _snippetMembers;

	public RobustMergerRewriter(string targetClassName, SyntaxList<MemberDeclarationSyntax> snippetMembers)
	{
		_targetClassName = targetClassName;
		_snippetMembers = snippetMembers.ToList();
	}

	public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		// Só mexe na classe alvo
		if (node.Identifier.Text != _targetClassName) return base.VisitClassDeclaration(node);

		var updatedNode = node;
		var membersToAdd = new List<MemberDeclarationSyntax>();

		// Para cada membro que veio do snippet (o novo código)
		foreach (var newMember in _snippetMembers)
		{
			bool isReplaced = false;

			// Tenta achar um correspondente no original
			var existingMember = FindMatchingMember(updatedNode, newMember);

			if (existingMember != null)
			{
				// SUBSTITUIÇÃO: O método já existe, trocamos pelo novo
				updatedNode = updatedNode.ReplaceNode(existingMember, newMember);
				isReplaced = true;
			}
			else
			{
				// INSERÇÃO: O método não existe, vamos adicionar no final
				membersToAdd.Add(newMember);
			}
		}

		// Se tiver membros novos, adiciona ao final da classe
		if (membersToAdd.Any())
		{
			// Adiciona quebras de linha para não colar no último método
			updatedNode = updatedNode.AddMembers(membersToAdd.ToArray());
		}

		return updatedNode;
	}

	private MemberDeclarationSyntax? FindMatchingMember(ClassDeclarationSyntax classNode, MemberDeclarationSyntax memberToFind)
	{
		if (memberToFind is MethodDeclarationSyntax method)
		{
			// Busca método com mesmo nome e (opcionalmente) mesmos parâmetros
			// Simplificado: Busca por nome. Melhoria futura: comparar assinatura.
			return classNode.Members.OfType<MethodDeclarationSyntax>()
							.FirstOrDefault(m => m.Identifier.Text == method.Identifier.Text);
		}

		if (memberToFind is ConstructorDeclarationSyntax ctor)
		{
			return classNode.Members.OfType<ConstructorDeclarationSyntax>()
							.FirstOrDefault(c => c.Identifier.Text == ctor.Identifier.Text);
		}

		if (memberToFind is PropertyDeclarationSyntax prop)
		{
			return classNode.Members.OfType<PropertyDeclarationSyntax>()
							.FirstOrDefault(p => p.Identifier.Text == prop.Identifier.Text);
		}

		return null;
	}
}