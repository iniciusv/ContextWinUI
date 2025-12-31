using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public class CodeBlockParserService : ICodeBlockParserService
{
	public async Task<List<CodeBlockItem>> ParseFileAsync(string filePath, string fileContent)
	{
		if (string.IsNullOrEmpty(fileContent)) return new List<CodeBlockItem>();

		var tree = CSharpSyntaxTree.ParseText(fileContent);
		var root = await tree.GetRootAsync();
		var segments = new List<CodeBlockItem>();

		FlattenNode(root, fileContent, 0, segments, filePath);

		return segments;
	}

	private void FlattenNode(SyntaxNode node, string fullText, int depth, List<CodeBlockItem> resultList, string filePath)
	{
		var childrenToProcess = node.ChildNodes()
			.Where(n => IsInterestingNode(n) || IsContainerNode(n))
			.OrderBy(n => n.SpanStart)
			.ToList();

		int cursor = node.SpanStart;
		if (node is CompilationUnitSyntax) cursor = 0;

		foreach (var child in childrenToProcess)
		{
			// Gap antes do filho (ex: abertura de chaves, espaços, definições de classe)
			if (child.SpanStart > cursor)
			{
				var gapText = fullText.Substring(cursor, child.SpanStart - cursor);
				if (!string.IsNullOrEmpty(gapText))
				{
					resultList.Add(CreateSegment(node, gapText, depth, true, cursor, filePath));
				}
			}

			// Processar recursivamente containers ou adicionar itens granulares
			if (IsContainerNode(child))
			{
				FlattenNode(child, fullText, depth + 1, resultList, filePath);
			}
			else
			{
				var childText = fullText.Substring(child.SpanStart, child.Span.Length);
				resultList.Add(CreateSegment(child, childText, depth + 1, false, child.SpanStart, filePath));
			}

			cursor = child.Span.End;
		}

		// Cauda (ex: fechamento de chaves)
		if (cursor < node.Span.End)
		{
			var tailText = fullText.Substring(cursor, node.Span.End - cursor);
			if (!string.IsNullOrEmpty(tailText))
			{
				resultList.Add(CreateSegment(node, tailText, depth, true, cursor, filePath));
			}
		}

		// EOF handling
		if (node is CompilationUnitSyntax && cursor < fullText.Length)
		{
			var finalTrivia = fullText.Substring(cursor);
			if (!string.IsNullOrEmpty(finalTrivia))
			{
				var eofItem = CreateSegment(node, finalTrivia, depth, true, cursor, filePath);
				eofItem.Name = "EOF";
				eofItem.TypeDescription = "END";
				resultList.Add(eofItem);
			}
		}
	}

	private CodeBlockItem CreateSegment(SyntaxNode node, string content, int depth, bool isGap, int absoluteStart, string filePath)
	{
		var type = IdentifySegmentType(node, isGap, content);
		string name;

		if (isGap)
		{
			if (content.Trim() == "}")
				name = "Fechamento";
			else if (node is ClassDeclarationSyntax cls)
				name = $"Estrutura ({cls.Identifier.Text})";
			else if (node is NamespaceDeclarationSyntax ns)
				name = $"Estrutura (Namespace)";
			else
				name = $"Estrutura ({node.GetType().Name.Replace("DeclarationSyntax", "")})";
		}
		else
		{
			name = node is MemberDeclarationSyntax m ? GetMemberName(m) : node.GetType().Name;
		}

		var item = new CodeBlockItem
		{
			Name = name,
			Content = content,
			DepthLevel = depth,
			SegmentType = type,
			TypeDescription = type.ToString().ToUpperInvariant(),
			StartLine = content.Count(c => c == '\n') + 1,
			FileExtension = Path.GetExtension(filePath),
			AbsoluteStartPosition = absoluteStart,
			SymbolType = MapToSymbolType(type) // Método auxiliar simples
		};

		item.InitializeVersions(content);
		return item;
	}

	private SegmentType IdentifySegmentType(SyntaxNode node, bool isGap, string content)
	{
		if (string.IsNullOrWhiteSpace(content)) return SegmentType.Trivia;
		if (content.Trim() == "}") return SegmentType.CloseBrace;

		if (isGap)
		{
			if (node is ClassDeclarationSyntax) return SegmentType.ClassHeader;
			if (node is NamespaceDeclarationSyntax) return SegmentType.NamespaceDecl;
			return SegmentType.Gap;
		}

		return node switch
		{
			MethodDeclarationSyntax => SegmentType.Method,
			ConstructorDeclarationSyntax => SegmentType.Constructor,
			PropertyDeclarationSyntax => SegmentType.Property,
			FieldDeclarationSyntax => SegmentType.Field,
			EnumDeclarationSyntax => SegmentType.Enum,
			UsingDirectiveSyntax => SegmentType.FileHeader,
			_ => SegmentType.Gap
		};
	}

	private string GetMemberName(MemberDeclarationSyntax member)
	{
		if (member is MethodDeclarationSyntax m) return m.Identifier.Text;
		if (member is PropertyDeclarationSyntax p) return p.Identifier.Text;
		if (member is ClassDeclarationSyntax c) return c.Identifier.Text;
		if (member is InterfaceDeclarationSyntax i) return i.Identifier.Text;
		if (member is NamespaceDeclarationSyntax n) return n.Name.ToString();
		if (member is FileScopedNamespaceDeclarationSyntax fn) return fn.Name.ToString();
		return member.GetType().Name.Replace("DeclarationSyntax", "");
	}

	private bool IsContainerNode(SyntaxNode node) =>
		node is ClassDeclarationSyntax || node is NamespaceDeclarationSyntax ||
		node is FileScopedNamespaceDeclarationSyntax || node is StructDeclarationSyntax ||
		node is InterfaceDeclarationSyntax;

	private bool IsInterestingNode(SyntaxNode node) =>
		node is MethodDeclarationSyntax || node is ConstructorDeclarationSyntax ||
		node is PropertyDeclarationSyntax || node is FieldDeclarationSyntax ||
		node is EnumDeclarationSyntax || node is UsingDirectiveSyntax;

	private SymbolType MapToSymbolType(SegmentType type)
	{
		return type switch
		{
			SegmentType.Method => SymbolType.Method,
			SegmentType.ClassHeader => SymbolType.Class,
			SegmentType.Property => SymbolType.Property,
			SegmentType.Field => SymbolType.Field,
			SegmentType.Enum => SymbolType.Enum,
			SegmentType.Constructor => SymbolType.Constructor,
			_ => SymbolType.Statement // Default ou ajustável
		};
	}
}
