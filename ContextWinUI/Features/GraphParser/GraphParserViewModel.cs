// ARQUIVO: ContextWinUI/Features/GraphParser/ViewModels/GraphParserViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Models;
using Microsoft.UI.Xaml; // <--- ADICIONE ISSO
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

	// Notifica as propriedades de visualização quando IsEmpty mudar
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ListVisibility))]
	[NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
	private bool isEmpty = true;

	[ObservableProperty]
	private string currentFileName = "Nenhum arquivo";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(LoadingVisibility))]
	private bool isLoading;

	// Propriedades de Visibilidade (Remove a necessidade de Converters no XAML)
	public Visibility ListVisibility => (!IsEmpty && !IsLoading) ? Visibility.Visible : Visibility.Collapsed;
	public Visibility EmptyStateVisibility => (IsEmpty && !IsLoading) ? Visibility.Visible : Visibility.Collapsed;
	public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

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

		// REMOVIDO: LoadDummyData();
	}

	private async void OnSelectionChanged(object? sender, FileSystemItem? item)
	{
		if (item == null || item.Type != FileSystemItemType.File)
		{
			Blocks.Clear();
			IsEmpty = true;
			CurrentFileName = "Nenhum arquivo de código selecionado";
			return;
		}

		await LoadBlocksAsync(item);
	}

	private async Task LoadBlocksAsync(FileSystemItem item)
	{
		IsLoading = true;
		// Força atualização imediata da UI para esconder a lista antiga
		OnPropertyChanged(nameof(ListVisibility));
		OnPropertyChanged(nameof(EmptyStateVisibility));

		IsEmpty = false;
		CurrentFileName = item.Name;
		Blocks.Clear();

		try
		{
			var graph = _indexService.GetCurrentGraph();
			var normalizedPath = Path.GetFullPath(item.FullPath).ToLowerInvariant();
			var fileContent = await _fileSystemService.ReadFileContentAsync(item.FullPath);

			if (graph != null && graph.FileIndex.TryGetValue(normalizedPath, out var nodes) && !string.IsNullOrEmpty(fileContent))
			{
				var relevantNodes = nodes
					.Where(n => IsVisualBlock(n.Type))
					.OrderBy(n => n.StartPosition)
					.ToList();

				if (relevantNodes.Any())
				{
					foreach (var node in relevantNodes)
					{
						if (node.StartPosition < 0 || node.StartPosition + node.Length > fileContent.Length)
							continue;

						string snippet = fileContent.Substring(node.StartPosition, node.Length);

						int startLine = CountLines(fileContent, 0, node.StartPosition) + 1;
						int lineCount = CountLines(snippet, 0, snippet.Length);

						Blocks.Add(new CodeBlockItem
						{
							Id = node.Id,
							Name = node.Name,
							SymbolType = node.Type,
							TypeDescription = node.Type.ToString(),
							Content = snippet,
							FileExtension = Path.GetExtension(item.FullPath),
							Icon = GetIconForType(node.Type),
							StartLine = startLine,
							EndLine = startLine + lineCount
						});
					}
				}
				else
				{
					AddFullFileBlock(fileContent, item.Name, item.FullPath);
				}
			}
			else
			{
				AddFullFileBlock(fileContent ?? "", item.Name, item.FullPath);
			}
		}
		catch
		{
			IsEmpty = true;
		}
		finally
		{
			IsLoading = false;
			if (Blocks.Count == 0) IsEmpty = true;
		}
	}

	// ... (Mantenha AddFullFileBlock, IsVisualBlock, GetIconForType, CountLines iguais ao anterior) ...
	private void AddFullFileBlock(string content, string name, string path)
	{
		Blocks.Add(new CodeBlockItem
		{
			Name = name,
			SymbolType = SymbolType.Class,
			TypeDescription = "Arquivo Completo",
			Content = content,
			FileExtension = Path.GetExtension(path),
			Icon = "\uE9F3",
			StartLine = 1,
			EndLine = CountLines(content, 0, content.Length) + 1
		});
	}

	private bool IsVisualBlock(SymbolType type) => type switch
	{
		SymbolType.Class => true,
		SymbolType.Interface => true,
		SymbolType.Method => true,
		SymbolType.Constructor => true,
		SymbolType.Property => true,
		_ => false
	};

	private string GetIconForType(SymbolType type) => type switch
	{
		SymbolType.Method => "\uEA86",
		SymbolType.Constructor => "\uEA86",
		SymbolType.Class => "\uE943",
		SymbolType.Interface => "\uE943",
		SymbolType.Property => "\uE8A5",
		_ => "\uE74C"
	};

	private int CountLines(string text, int start, int length)
	{
		int count = 0;
		int end = start + length;
		for (int i = start; i < end; i++)
		{
			if (text[i] == '\n') count++;
		}
		return count;
	}
}