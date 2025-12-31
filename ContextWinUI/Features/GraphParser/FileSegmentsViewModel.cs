using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
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

public partial class FileSegmentsViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;
	public string FilePath { get; }
	public string FileName { get; }


	[ObservableProperty]
	private ObservableCollection<CodeBlockItem> blocks = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSelectedBlock))]
	private CodeBlockItem? selectedBlock;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool isEmpty;

	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory = new();

	[ObservableProperty]
	private int currentGlobalIndex = 0;

	[ObservableProperty]
	private string currentSymbolInfo = string.Empty;

	public bool HasSelectedBlock => SelectedBlock != null;
	public bool HasAnyUnsavedChanges => Blocks.Any(b => b.HasUnsavedChanges);

	public event EventHandler? GlobalSaveRequested;
	public event EventHandler<int>? GlobalRestoreRequested;
	private readonly SemanticIndexService _indexService;

	public FileSegmentsViewModel(
			string filePath,
			IFileSystemService fileSystemService,
			SemanticIndexService indexService)
	{
		FilePath = filePath;
		FileName = Path.GetFileName(filePath);
		_fileSystemService = fileSystemService;
		_indexService = indexService;
		_ = LoadBlocksAsync();
	}

	public void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));

	private async Task LoadBlocksAsync()
	{
		IsLoading = true;
		Blocks.Clear();
		SelectedBlock = null;

		try
		{
			var fileContent = await _fileSystemService.ReadFileContentAsync(FilePath);
			if (string.IsNullOrEmpty(fileContent)) return;

			var tree = CSharpSyntaxTree.ParseText(fileContent);
			var root = await tree.GetRootAsync();
			var segments = new List<CodeBlockItem>();

			FlattenNode(root, fileContent, 0, segments);

			foreach (var seg in segments)
			{
				seg.FileExtension = Path.GetExtension(FilePath);
				Blocks.Add(seg);
			}

			if (Blocks.Any())
			{
				SelectedBlock = Blocks.First();
			}

			// Inicializa o histórico global após carregar
			InitializeGlobalHistory();
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

	private void InitializeGlobalHistory()
	{
		GlobalHistory.Clear();
		GlobalHistory.Add(new GlobalVersion
		{
			Description = "Versão Original",
			IsOriginal = true,
			Timestamp = DateTime.MinValue
		});
		CurrentGlobalIndex = 0;
	}

	public void CommitGlobalVersion(string description = "Salvo em lote")
	{
		var newVersion = new GlobalVersion
		{
			Description = description,
			Timestamp = DateTime.Now,
			IsOriginal = false
		};
		GlobalHistory.Add(newVersion);

		CurrentGlobalIndex = GlobalHistory.Count - 1;

		foreach (var block in Blocks)
		{
			if (block.HasUnsavedChanges)
			{
				block.CreateNewVersion(block.Content, description);
			}
		}
		OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	}

	partial void OnCurrentGlobalIndexChanged(int value)
	{
		if (GlobalHistory == null || value < 0 || value >= GlobalHistory.Count) return;
		RestoreGlobalState(value);
	}

	private void RestoreGlobalState(int globalIndex)
	{
		foreach (var block in Blocks)
		{
			int targetBlockIndex = Math.Min(globalIndex, block.Versions.Count - 1);
			if (targetBlockIndex >= 0)
			{
				block.RestoreVersion(targetBlockIndex);
			}
		}
	}

	public void TriggerGlobalSave() => GlobalSaveRequested?.Invoke(this, EventArgs.Empty);
	public void TriggerGlobalRestore(int index) => GlobalRestoreRequested?.Invoke(this, index);
	public void NotifyChangesChanged() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	// ARQUIVO: ContextWinUI/Features/GraphParser/ViewModels/FileSegmentsViewModel.cs

	// =========================================================================================
	// MÉTODO 1: FLATTEN NODE (Recursivo)
	// Responsável por percorrer a árvore e calcular os offsets (posições) corretamente
	// =========================================================================================
	private void FlattenNode(SyntaxNode node, string fullText, int depth, List<CodeBlockItem> resultList)
	{
		// 1. Identificar filhos que queremos destacar (Métodos, Props) ou entrar (Classes, Namespaces)
		var childrenToProcess = node.ChildNodes()
			.Where(n => IsInterestingNode(n) || IsContainerNode(n))
			.OrderBy(n => n.SpanStart)
			.ToList();

		// 2. Inicializar o cursor
		// Se for o nó raiz (arquivo inteiro), começa do 0.
		// Se for um nó interno (ex: Classe), começa onde a classe começa.
		int cursor = node.SpanStart;
		if (node is CompilationUnitSyntax) cursor = 0;

		foreach (var child in childrenToProcess)
		{
			// ---------------------------------------------------------
			// A. GAP (Texto entre o cursor anterior e o filho atual)
			// ---------------------------------------------------------
			// Ex: Espaços, chaves de abertura '{', comentários soltos antes do método
			if (child.SpanStart > cursor)
			{
				var gapText = fullText.Substring(cursor, child.SpanStart - cursor);
				if (!string.IsNullOrEmpty(gapText))
				{
					// O Gap começa exatamente onde o cursor estava
					resultList.Add(CreateSegment(node, gapText, depth, true, cursor));
				}
			}

			// ---------------------------------------------------------
			// B. FILHO (Container ou Item Granular)
			// ---------------------------------------------------------
			if (IsContainerNode(child))
			{
				// Se for Container (Classe/Namespace), mergulhamos nele (recursão)
				FlattenNode(child, fullText, depth + 1, resultList);
			}
			else
			{
				// Se for Item Granular (Método, Propriedade), criamos o bloco fechado
				var childText = fullText.Substring(child.SpanStart, child.Span.Length);

				// A posição absoluta é o Start do próprio nó filho
				resultList.Add(CreateSegment(child, childText, depth + 1, false, child.SpanStart));
			}

			// Avançamos o cursor para o fim deste filho
			cursor = child.Span.End;
		}

		// ---------------------------------------------------------
		// C. TAIL (Texto restante após o último filho)
		// ---------------------------------------------------------
		// Ex: Chave de fechamento '}' da classe
		if (cursor < node.Span.End)
		{
			var tailText = fullText.Substring(cursor, node.Span.End - cursor);
			if (!string.IsNullOrEmpty(tailText))
			{
				resultList.Add(CreateSegment(node, tailText, depth, true, cursor));
			}
		}

		// ---------------------------------------------------------
		// D. EOF (Apenas para o nó Raiz)
		// ---------------------------------------------------------
		// Pega qualquer coisa após a última classe (espaços finais, comentários de rodapé)
		if (node is CompilationUnitSyntax && cursor < fullText.Length)
		{
			var finalTrivia = fullText.Substring(cursor);
			if (!string.IsNullOrEmpty(finalTrivia))
			{
				var eofItem = CreateSegment(node, finalTrivia, depth, true, cursor);
				eofItem.Name = "EOF";
				eofItem.TypeDescription = "END";
				resultList.Add(eofItem);
			}
		}
	}

	// =========================================================================================
	// MÉTODO 2: CREATE SEGMENT
	// Cria o objeto CodeBlockItem preenchendo o AbsoluteStartPosition
	// =========================================================================================
	private CodeBlockItem CreateSegment(SyntaxNode node, string content, int depth, bool isGap, int absoluteStart)
	{
		var type = IdentifySegmentType(node, isGap, content);

		// Determinar o Nome de Exibição
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

		// Criar o Item
		var item = new CodeBlockItem
		{
			Name = name,
			Content = content,
			DepthLevel = depth,
			SegmentType = type,
			TypeDescription = type.ToString().ToUpperInvariant(),
			StartLine = content.Count(c => c == '\n') + 1, // Estimativa visual apenas
			FileExtension = Path.GetExtension(FilePath),

			// --- CORREÇÃO IMPORTANTE ---
			AbsoluteStartPosition = absoluteStart
			// ---------------------------
		};

		// Inicializa o sistema de versionamento/undo
		item.InitializeVersions(content);

		return item;
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

	public void ResolveSymbolHeuristic(int cursorIndexInBlock)
	{
		if (SelectedBlock == null) return;

		// 1. Obter a palavra (lógica simples para pegar palavra inteira sob o cursor)
		string word = GetWordAtCursor(SelectedBlock.Content, cursorIndexInBlock);

		// 2. Calcular posição absoluta
		int absPos = SelectedBlock.AbsoluteStartPosition + cursorIndexInBlock;

		// 3. Chamar o IndexService
		var node = _indexService.InferSymbolFromGraph(word, FilePath, absPos);

		if (node != null)
		{
			// Sucesso! Mostramos o que o Grafo sabe sobre isso.
			CurrentSymbolInfo = $"[{node.Type}] {node.Name}\nDefinido em: {Path.GetFileName(node.FilePath)}";
		}
		else
		{
			CurrentSymbolInfo = $"'{word}' (Sem informações no grafo)";
		}
	}

	private string GetWordAtCursor(string text, int position)
	{
		if (string.IsNullOrEmpty(text) || position < 0 || position > text.Length) return string.Empty;

		// Lógica simples para expandir a seleção para esquerda e direita até achar espaço ou pontuação
		int start = position;
		int end = position;

		while (start > 0 && char.IsLetterOrDigit(text[start - 1])) start--;
		while (end < text.Length && char.IsLetterOrDigit(text[end])) end++;

		return text.Substring(start, end - start);
	}
}