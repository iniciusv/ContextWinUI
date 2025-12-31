using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class FileSegmentsViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;

	public string FilePath { get; }
	public string FileName { get; }

	[ObservableProperty]
	private ObservableCollection<CodeBlockItem> blocks = new();

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool isEmpty;

	public FileSegmentsViewModel(string filePath, IFileSystemService fileSystemService)
	{
		FilePath = filePath;
		FileName = Path.GetFileName(filePath);
		_fileSystemService = fileSystemService;

		// Inicia o carregamento automaticamente
		_ = LoadBlocksAsync();
	}

	private async Task LoadBlocksAsync()
	{
		IsLoading = true;
		Blocks.Clear();

		try
		{
			// 1. Lê o arquivo do disco
			var fileContent = await _fileSystemService.ReadFileContentAsync(FilePath);

			if (string.IsNullOrEmpty(fileContent))
				return;

			// 2. Parseia com Roslyn
			var tree = CSharpSyntaxTree.ParseText(fileContent);
			var root = await tree.GetRootAsync();

			var segments = new List<CodeBlockItem>();

			// 3. Achata a árvore em segmentos lineares
			FlattenNode(root, fileContent, 0, segments);

			foreach (var seg in segments)
			{
				seg.FileExtension = Path.GetExtension(FilePath);
				Blocks.Add(seg);
			}
		}
		catch (Exception ex)
		{
			Blocks.Add(new CodeBlockItem
			{
				Name = "Erro de Leitura",
				Content = ex.Message,
				SegmentType = SegmentType.Trivia,
				TypeDescription = "ERROR"
			});
		}
		finally
		{
			IsLoading = false;
			IsEmpty = Blocks.Count == 0;
		}
	}

	// Lógica recursiva para quebrar o código em blocos
	private void FlattenNode(SyntaxNode node, string fullText, int depth, List<CodeBlockItem> resultList)
	{
		var childrenToProcess = node.ChildNodes()
			.Where(n => IsInterestingNode(n) || IsContainerNode(n))
			.OrderBy(n => n.SpanStart)
			.ToList();

		int cursor = node.SpanStart;
		if (node is CompilationUnitSyntax) cursor = 0;

		foreach (var child in childrenToProcess)
		{
			// Captura GAPs (espaços entre métodos, comentários soltos, etc)
			if (child.SpanStart > cursor)
			{
				var gapText = fullText.Substring(cursor, child.SpanStart - cursor);
				if (!string.IsNullOrEmpty(gapText))
					resultList.Add(CreateSegment(node, gapText, depth, true));
			}

			if (IsContainerNode(child))
			{
				// Recursão para classes/namespaces
				FlattenNode(child, fullText, depth + 1, resultList);
			}
			else
			{
				// Nó folha (método, propriedade, field)
				var childText = fullText.Substring(child.SpanStart, child.Span.Length);
				resultList.Add(CreateSegment(child, childText, depth + 1, false));
			}

			cursor = child.Span.End;
		}

		// Captura o final do bloco (fechamento de chaves, etc)
		if (cursor < node.Span.End)
		{
			var tailText = fullText.Substring(cursor, node.Span.End - cursor);
			if (!string.IsNullOrEmpty(tailText))
				resultList.Add(CreateSegment(node, tailText, depth, true));
		}

		// Caso especial para final de arquivo
		if (node is CompilationUnitSyntax && cursor < fullText.Length)
		{
			var finalTrivia = fullText.Substring(cursor);
			if (!string.IsNullOrEmpty(finalTrivia))
				resultList.Add(new CodeBlockItem { Name = "EOF", Content = finalTrivia, SegmentType = SegmentType.Trivia, TypeDescription = "EOF" });
		}
	}

	private CodeBlockItem CreateSegment(SyntaxNode node, string content, int depth, bool isGap)
	{
		var type = IdentifySegmentType(node, isGap, content);

		string name = isGap
			? (content.Trim() == "}" ? "Fechamento" : $"Estrutura ({node.GetType().Name.Replace("DeclarationSyntax", "")})")
			: (node is MemberDeclarationSyntax m ? GetMemberName(m) : node.GetType().Name);

		var item = new CodeBlockItem
		{
			Name = name,
			Content = content,
			DepthLevel = depth,
			SegmentType = type,
			TypeDescription = type.ToString().ToUpperInvariant(),
			StartLine = content.Count(c => c == '\n') + 1
		};

		// -----------------------------------------------------------------------
		// REQUISITO 1: SALVAR VERSÃO ORIGINAL NO CARREGAMENTO
		// Aqui garantimos que assim que o bloco nasce, ele tem a versão "Original"
		// salva na memória.
		// -----------------------------------------------------------------------
		item.InitializeVersions(content);

		return item;
	}

	// Métodos auxiliares de Roslyn (GetMemberName, IsContainerNode, etc...) mantidos iguais ao original
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

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSelectedBlock))]
	private CodeBlockItem? selectedBlock;

	public bool HasSelectedBlock => SelectedBlock != null;
}