using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class GraphParserViewModel : ObservableObject
{
	private readonly IFileSelectionService _selectionService;
	private readonly SemanticIndexService _indexService;
	private readonly IFileSystemService _fileSystemService;

	[ObservableProperty]
	private ObservableCollection<CodeBlockItem> blocks = new();

	[ObservableProperty]
	private bool isEmpty = true;

	[ObservableProperty]
	private string currentFileName = "Nenhum arquivo";

	[ObservableProperty]
	private bool isLoading;

	public GraphParserViewModel(
		IFileSelectionService selectionService,
		SemanticIndexService indexService,
		IFileSystemService fileSystemService)
	{
		_selectionService = selectionService;
		_indexService = indexService;
		_fileSystemService = fileSystemService;

		if (_selectionService != null)
		{
			_selectionService.SelectionChanged += OnSelectionChanged;
		}
	}

	private async void OnSelectionChanged(object? sender, FileSystemItem? item)
	{
		if (item == null || item.Type != FileSystemItemType.File)
		{
			Blocks.Clear();
			IsEmpty = true;
			CurrentFileName = "Nenhum arquivo selecionado";
			return;
		}
		await LoadBlocksAsync(item);
	}

	private async Task LoadBlocksAsync(FileSystemItem item)
	{
		IsLoading = true;
		IsEmpty = false;
		CurrentFileName = item.Name;
		Blocks.Clear();

		try
		{
			var fileContent = await _fileSystemService.ReadFileContentAsync(item.FullPath);
			if (string.IsNullOrEmpty(fileContent)) return;

			// 1. Parse do SyntaxTree
			var tree = CSharpSyntaxTree.ParseText(fileContent);
			var root = await tree.GetRootAsync();

			// 2. Segmentação Recursiva
			var segments = new List<CodeBlockItem>();
			FlattenNode(root, fileContent, 0, segments);

			// 3. Popula a lista observável
			foreach (var seg in segments)
			{
				seg.FileExtension = Path.GetExtension(item.FullPath);
				Blocks.Add(seg);
			}
		}
		catch (Exception ex)
		{
			Blocks.Add(new CodeBlockItem { Name = "Erro", Content = ex.Message, SegmentType = SegmentType.Trivia });
		}
		finally
		{
			IsLoading = false;
			IsEmpty = Blocks.Count == 0;
		}
	}

	// --- CORE DO PARSER ---

	private void FlattenNode(SyntaxNode node, string fullText, int depth, List<CodeBlockItem> resultList)
	{
		// Pega todos os filhos que são "interessantes" (Métodos, Props) OU "containers" (Classes, Namespaces)
		// Isso garante que não vamos ignorar uma Classe só porque ela não é um "Método"
		var childrenToProcess = node.ChildNodes()
			.Where(n => IsInterestingNode(n) || IsContainerNode(n))
			.OrderBy(n => n.SpanStart)
			.ToList();

		int cursor = node.SpanStart;
		if (node is CompilationUnitSyntax) cursor = 0;

		foreach (var child in childrenToProcess)
		{
			// 1. Gaps antes do filho (Comentários, Declaração de Classe, Chaves Abertas)
			if (child.SpanStart > cursor)
			{
				var gapText = fullText.Substring(cursor, child.SpanStart - cursor);
				if (!string.IsNullOrEmpty(gapText))
				{
					resultList.Add(CreateSegment(node, gapText, depth, true));
				}
			}

			// 2. Processar o Filho
			if (IsContainerNode(child))
			{
				// RECURSÃO: Se é Classe ou Namespace, mergulha nele
				FlattenNode(child, fullText, depth + 1, resultList);
			}
			else
			{
				// FOLHA: Se é Método/Propriedade, adiciona o bloco inteiro
				var childText = fullText.Substring(child.SpanStart, child.Span.Length);
				resultList.Add(CreateSegment(child, childText, depth + 1, false));
			}

			cursor = child.Span.End;
		}

		// 3. Sobra final (Chaves de fechamento })
		if (cursor < node.Span.End)
		{
			var tailText = fullText.Substring(cursor, node.Span.End - cursor);
			if (!string.IsNullOrEmpty(tailText))
			{
				resultList.Add(CreateSegment(node, tailText, depth, true));
			}
		}

		// Caso especial para fim de arquivo
		if (node is CompilationUnitSyntax && cursor < fullText.Length)
		{
			var finalTrivia = fullText.Substring(cursor);
			if (!string.IsNullOrEmpty(finalTrivia))
			{
				resultList.Add(new CodeBlockItem
				{
					Name = "End of File",
					Content = finalTrivia,
					SegmentType = SegmentType.Trivia
				});
			}
		}
	}

	private CodeBlockItem CreateSegment(SyntaxNode node, string content, int depth, bool isGap)
	{
		var type = IdentifySegmentType(node, isGap, content);

		// Ajuste fino para nome
		string name = isGap
			? (content.Trim() == "}" ? "Fechamento de Escopo" : $"Estrutura ({node.GetType().Name.Replace("DeclarationSyntax", "")})")
			: (node is MemberDeclarationSyntax m ? GetMemberName(m) : node.GetType().Name);

		return new CodeBlockItem
		{
			Id = Guid.NewGuid().ToString(),
			Name = name,
			Content = content,
			DepthLevel = depth,
			SegmentType = type,
			TypeDescription = type.ToString(),
			StartLine = CountLines(content) // Simplificado
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

	private bool IsContainerNode(SyntaxNode node)
	{
		return node is ClassDeclarationSyntax ||
			   node is NamespaceDeclarationSyntax ||
			   node is FileScopedNamespaceDeclarationSyntax ||
			   node is StructDeclarationSyntax ||
			   node is InterfaceDeclarationSyntax;
	}

	private bool IsInterestingNode(SyntaxNode node)
	{
		return node is MethodDeclarationSyntax ||
			   node is ConstructorDeclarationSyntax ||
			   node is PropertyDeclarationSyntax ||
			   node is FieldDeclarationSyntax ||
			   node is EnumDeclarationSyntax ||
			   node is UsingDirectiveSyntax;
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

	private int CountLines(string text) => text.Count(c => c == '\n') + 1;
}