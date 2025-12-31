using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser;
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class CodeBlockParserService : ICodeBlockParserService
{
	public async Task<List<CodeBlockItem>> ParseFileAsync(string filePath, string fileContent)
	{
		if (string.IsNullOrEmpty(fileContent)) return new List<CodeBlockItem>();

		// 1. Cria a árvore sintática do Roslyn
		var tree = CSharpSyntaxTree.ParseText(fileContent);
		var root = await tree.GetRootAsync();

		var segments = new List<CodeBlockItem>();

		// 2. Inicia o "Achatamento" da árvore em uma lista linear
		FlattenNode(root, fileContent, 0, segments, filePath);

		return segments;
	}

	// Método recursivo que transforma a árvore em uma lista sequencial de blocos
	private void FlattenNode(SyntaxNode node, string fullText, int depth, List<CodeBlockItem> resultList, string filePath)
	{
		// Define quais nós filhos queremos processar explicitamente
		var childrenToProcess = node.ChildNodes()
			.Where(n => IsInterestingNode(n) || IsContainerNode(n))
			.OrderBy(n => n.SpanStart)
			.ToList();

		int cursor = node.SpanStart;

		// Se for a raiz, começamos do zero absoluto
		if (node is CompilationUnitSyntax) cursor = 0;

		foreach (var child in childrenToProcess)
		{
			// --- GAP ANTERIOR (Cabeçalhos, chaves de abertura, comentários soltos) ---
			if (child.SpanStart > cursor)
			{
				var gapText = fullText.Substring(cursor, child.SpanStart - cursor);
				if (!string.IsNullOrWhiteSpace(gapText) || gapText.Contains("}"))
				{
					// Cria um bloco "Estrutural" para o texto entre o último nó e este
					resultList.Add(CreateSegment(node, gapText, depth, true, cursor, filePath));
				}
			}

			// --- PROCESSAMENTO DO FILHO ---
			if (IsContainerNode(child))
			{
				// Se for Class/Namespace, mergulha recursivamente (Depth + 1)
				FlattenNode(child, fullText, depth + 1, resultList, filePath);
			}
			else
			{
				// Se for Método/Propriedade, captura como um bloco "Folha" (Granular)
				var childText = fullText.Substring(child.SpanStart, child.Span.Length);
				resultList.Add(CreateSegment(child, childText, depth + 1, false, child.SpanStart, filePath));
			}

			cursor = child.Span.End;
		}

		// --- CAUDA (Fechamento de chaves, rodapé da classe/arquivo) ---
		if (cursor < node.Span.End)
		{
			var tailText = fullText.Substring(cursor, node.Span.End - cursor);
			if (!string.IsNullOrWhiteSpace(tailText) || tailText.Contains("}"))
			{
				resultList.Add(CreateSegment(node, tailText, depth, true, cursor, filePath));
			}
		}

		// --- EOF (End of File) - Captura qualquer coisa após o último nó na raiz ---
		if (node is CompilationUnitSyntax && cursor < fullText.Length)
		{
			var finalTrivia = fullText.Substring(cursor);
			if (!string.IsNullOrEmpty(finalTrivia))
			{
				var eofItem = CreateSegment(node, finalTrivia, depth, true, cursor, filePath);
				eofItem.Name = "EOF";
				eofItem.SegmentType = SegmentType.Trivia;
				resultList.Add(eofItem);
			}
		}
	}

	private CodeBlockItem CreateSegment(SyntaxNode node, string content, int depth, bool isGap, int absoluteStart, string filePath)
	{
		// Identifica o tipo baseada no nó ou se é um GAP
		var segmentType = IdentifySegmentType(node, isGap, content);

		string name = GetBlockName(node, isGap, content);

		var item = new CodeBlockItem
		{
			Name = name,
			// Importante: Inicializa versões para o Diff funcionar
			// O InitializeVersions já é chamado dentro da criação do objeto ou manualmente aqui
			DepthLevel = depth,
			SegmentType = segmentType,
			SymbolType = MapToSymbolType(segmentType, node),

			TypeDescription = segmentType.ToString(),

			FileExtension = Path.GetExtension(filePath) ?? ".cs",
			AbsoluteStartPosition = absoluteStart
		};

		// Define o conteúdo inicial e cria a versão original
		item.InitializeVersions(content);

		// Corrige o Content atual (InitializeVersions define apenas o histórico)
		item.Content = content;

		return item;
	}

	private string GetBlockName(SyntaxNode node, bool isGap, string content)
	{
		if (isGap)
		{
			var trimmed = content.Trim();
			if (trimmed == "}") return "Fechamento";
			if (trimmed == "{") return "Abertura";

			if (node is ClassDeclarationSyntax cls) return $"Corpo de {cls.Identifier.Text}";
			if (node is NamespaceDeclarationSyntax ns) return $"Namespace {ns.Name}";

			return "Estrutura";
		}

		if (node is MemberDeclarationSyntax member) return GetMemberName(member);

		return node.GetType().Name.Replace("Syntax", "");
	}

	private SegmentType IdentifySegmentType(SyntaxNode node, bool isGap, string content)
	{
		// Lógica para gaps (texto estrutural)
		if (isGap)
		{
			string trimmed = content.Trim();
			if (string.IsNullOrWhiteSpace(trimmed)) return SegmentType.Trivia;
			if (trimmed == "}") return SegmentType.Trivia; // Ou criar SegmentType.ClosingBrace

			// Gaps dentro de classes geralmente são cabeçalhos ou campos não processados
			if (node is ClassDeclarationSyntax) return SegmentType.Class;
			if (node is NamespaceDeclarationSyntax) return SegmentType.Namespace;

			return SegmentType.Trivia;
		}

		// Lógica para nós reais
		return node switch
		{
			MethodDeclarationSyntax => SegmentType.Method,
			ConstructorDeclarationSyntax => SegmentType.Method, // Tratamos construtor como método no SegmentType
			PropertyDeclarationSyntax => SegmentType.Property,
			FieldDeclarationSyntax => SegmentType.Class, // Fields muitas vezes ficam junto da classe se não forem interessantes
			EnumDeclarationSyntax => SegmentType.Class,
			ClassDeclarationSyntax => SegmentType.Class,
			InterfaceDeclarationSyntax => SegmentType.Class,
			NamespaceDeclarationSyntax => SegmentType.Namespace,
			FileScopedNamespaceDeclarationSyntax => SegmentType.Namespace,
			UsingDirectiveSyntax => SegmentType.Using,
			_ => SegmentType.Trivia
		};
	}

	private string GetMemberName(MemberDeclarationSyntax member)
	{
		if (member is MethodDeclarationSyntax m) return m.Identifier.Text;
		if (member is PropertyDeclarationSyntax p) return p.Identifier.Text;
		if (member is ClassDeclarationSyntax c) return c.Identifier.Text;
		if (member is InterfaceDeclarationSyntax i) return i.Identifier.Text;
		if (member is EnumDeclarationSyntax e) return e.Identifier.Text;
		if (member is ConstructorDeclarationSyntax ctor) return ctor.Identifier.Text;

		if (member is NamespaceDeclarationSyntax n) return n.Name.ToString();
		if (member is FileScopedNamespaceDeclarationSyntax fn) return fn.Name.ToString();

		return member.GetType().Name.Replace("DeclarationSyntax", "");
	}

	// Define onde o parser deve "mergulhar" para achar filhos
	private bool IsContainerNode(SyntaxNode node) =>
		node is ClassDeclarationSyntax ||
		node is NamespaceDeclarationSyntax ||
		node is FileScopedNamespaceDeclarationSyntax ||
		node is StructDeclarationSyntax ||
		node is RecordDeclarationSyntax ||
		node is InterfaceDeclarationSyntax;

	// Define o que queremos extrair como bloco editável isolado
	private bool IsInterestingNode(SyntaxNode node) =>
		node is MethodDeclarationSyntax ||
		node is ConstructorDeclarationSyntax ||
		node is PropertyDeclarationSyntax ||
		node is EnumDeclarationSyntax ||
		node is UsingDirectiveSyntax ||
		node is DelegateDeclarationSyntax;

	// Mapeia para o Enum visual (ícones/cores)
	private SymbolType MapToSymbolType(SegmentType type, SyntaxNode node)
	{
		// Refinamento específico baseado no Nó Roslyn
		if (node is ConstructorDeclarationSyntax) return SymbolType.Constructor;
		if (node is InterfaceDeclarationSyntax) return SymbolType.Interface;
		if (node is EnumDeclarationSyntax) return SymbolType.Enum;
		if (node is StructDeclarationSyntax) return SymbolType.Struct;

		return type switch
		{
			SegmentType.Method => SymbolType.Method,
			SegmentType.Property => SymbolType.Property,
			SegmentType.Class => SymbolType.Class,
			SegmentType.Namespace => SymbolType.Class, // Sem ícone específico de namespace no Enum, usa Class ou cria um
			SegmentType.Using => SymbolType.Keyword,
			_ => SymbolType.Statement
		};
	}
}