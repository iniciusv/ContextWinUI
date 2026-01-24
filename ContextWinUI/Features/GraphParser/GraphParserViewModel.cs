using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class GraphParserViewModel : ObservableObject, IGraphParserContract
{
	private readonly ISemanticIndexService _indexService;
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeBlockParserService _parserService;
	private readonly ISymbolResolutionService _symbolService;
	private readonly IVersionDiffManager _diffManager;
	private readonly IAiCodeMerger _aiMergerService;
	private readonly IBlockLoaderService _blockLoaderService;
	private readonly IBlockEditorService _blockEditorService;
	private readonly IFileSegmentsViewModelFactory _vmFactory;
	private readonly IPreviewManager _previewManager;

	private string _rootPath;

	[ObservableProperty]
	private ObservableCollection<object> tabs = new();

	[ObservableProperty]
	private object? selectedTab;

	[ObservableProperty]
	private ObservableCollection<SearchSuggestion> searchSuggestions = new();

	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory = new();

	[ObservableProperty]
	private int currentGlobalIndex = 0;

	public event EventHandler<int>? CurrentGlobalIndexChanged;

	public bool HasAnyUnsavedChanges => Tabs.OfType<FileSegmentsViewModel>().Any(t => t.HasAnyUnsavedChanges);
	public bool HasTabs => Tabs.Any();

	partial void OnTabsChanged(ObservableCollection<object> value) => OnPropertyChanged(nameof(HasTabs));
	partial void OnSelectedTabChanged(object? value) => OnPropertyChanged(nameof(HasTabs));

	public GraphParserViewModel(
		ISemanticIndexService indexService,
		IFileSystemService fileSystemService,
		IProjectSessionManager sessionManager,
		ICodeBlockParserService parserService,
		IAiCodeMerger aiMergerService,
		ISymbolResolutionService symbolService,
		IVersionDiffManager diffManager,
		IBlockLoaderService blockLoaderService,
		IBlockEditorService blockEditorService,
		IPreviewManager previewManager,
		IFileSegmentsViewModelFactory vmFactory)
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_parserService = parserService;
		_symbolService = symbolService;
		_diffManager = diffManager;
		_aiMergerService = aiMergerService;
		_blockLoaderService = blockLoaderService;
		_blockEditorService = blockEditorService;
		_previewManager = previewManager;
		_vmFactory = vmFactory;

		_rootPath = sessionManager.CurrentProjectPath ?? string.Empty;
		sessionManager.ProjectLoaded += (s, e) => { _rootPath = e.RootPath; _ = InitializeGraphAsync(); };

		Tabs.CollectionChanged += OnTabsCollectionChanged;
		InitializeGlobalHistory();
		_previewManager.Start(this);
	}

	// --- NOVO MÉTODO: Copiar Contexto Inteligente ---
	[RelayCommand]
	public void CopyContextToClipboard()
	{
		var sb = new StringBuilder();
		sb.AppendLine("Here is the relevant code context:\n");
		int fileCount = 0;

		foreach (var tab in Tabs.OfType<FileSegmentsViewModel>())
		{
			// O GetExportContent já cuida da lógica: 
			// Se tiver blocos selecionados -> retorna parcial
			// Se não tiver -> retorna tudo
			string content = tab.GetExportContent();

			if (!string.IsNullOrWhiteSpace(content))
			{
				sb.AppendLine("--------------------------------------------------");
				sb.AppendLine(content);
				fileCount++;
			}
		}

		if (fileCount > 0)
		{
			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);
			Debug.WriteLine($"Contexto copiado! {fileCount} arquivos processados.");
		}
	}
	// ------------------------------------------------

	[RelayCommand]
	public void OpenAsPermanent(object parameter)
	{
		string? path = ExtractPath(parameter);
		if (string.IsNullOrEmpty(path)) return;

		path = NormalizePath(path);

		var existingTab = Tabs.OfType<FileSegmentsViewModel>()
							  .FirstOrDefault(t => t.FilePath == path);

		if (existingTab != null)
		{
			SelectedTab = existingTab;
			if (existingTab.IsPreview)
			{
				existingTab.IsPreview = false;
			}
			return;
		}

		var newTab = _vmFactory.Create(path);
		newTab.IsPreview = false;
		Tabs.Add(newTab);
		SelectedTab = newTab;
	}

	public void OpenAsPreview(string path)
	{
		if (string.IsNullOrEmpty(path)) return;
		path = NormalizePath(path);

		var existingTab = Tabs.OfType<FileSegmentsViewModel>()
							  .FirstOrDefault(t => t.FilePath == path);

		if (existingTab != null)
		{
			SelectedTab = existingTab;
			return;
		}

		var oldPreview = Tabs.OfType<FileSegmentsViewModel>()
							 .FirstOrDefault(t => t.IsPreview);
		if (oldPreview != null)
		{
			Tabs.Remove(oldPreview);
		}

		var newTab = _vmFactory.Create(path);
		newTab.IsPreview = true;
		Tabs.Add(newTab);
		SelectedTab = newTab;
	}

	private string NormalizePath(string path)
	{
		if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(_rootPath))
			return Path.Combine(_rootPath, path);
		return path;
	}

	private string? ExtractPath(object parameter)
	{
		if (parameter is SearchSuggestion s) return s.FilePath;
		if (parameter is string p) return p;
		return null;
	}

	private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		OnPropertyChanged(nameof(HasTabs));
	}

	private void InitializeGlobalHistory()
	{
		GlobalHistory.Clear();
		GlobalHistory.Add(new GlobalVersion
		{
			Description = "Base (Git HEAD)",
			IsOriginal = true,
			Timestamp = DateTime.MinValue
		});
		GlobalHistory.Add(new GlobalVersion
		{
			Description = "Atual (Working Copy)",
			IsOriginal = false,
			Timestamp = DateTime.MaxValue
		});
		CurrentGlobalIndex = 1;
	}

	partial void OnCurrentGlobalIndexChanged(int value)
	{
		RestoreAllToVersion(value);
	}

	[RelayCommand]
	public void CommitAllPendingChanges()
	{
		var batchTime = DateTime.Now;
		string batchTimestamp = batchTime.ToString("HH:mm:ss");
		string description = $"Lote {batchTimestamp}";
		bool anyChange = false;

		var newGlobalVersion = new GlobalVersion
		{
			Description = description,
			Timestamp = batchTime,
			IsOriginal = false
		};

		GlobalHistory.Add(newGlobalVersion);

		foreach (var tab in Tabs.OfType<FileSegmentsViewModel>())
		{
			if (tab.HasAnyUnsavedChanges)
			{
				tab.SnapshotBlocksForGlobalVersion(description, batchTime);
				anyChange = true;
			}
		}

		if (anyChange)
		{
			Debug.WriteLine($"Commit Global realizado: {description}");
		}

		CurrentGlobalIndex = GlobalHistory.Count - 1;
		foreach (var tab in Tabs.OfType<FileSegmentsViewModel>())
		{
			tab.SyncGlobalIndex(CurrentGlobalIndex);
		}
	}

	public void RestoreAllToVersion(int versionIndex)
	{
		if (versionIndex < 0 || versionIndex >= GlobalHistory.Count) return;

		Debug.WriteLine($"Restaurando todas as abas para versão global: {versionIndex}");
		if (CurrentGlobalIndex != versionIndex)
		{
			CurrentGlobalIndex = versionIndex;
		}

		foreach (var tab in Tabs.OfType<FileSegmentsViewModel>())
		{
			tab.RestoreToGlobalIndex(versionIndex);
		}
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		_rootPath = e.RootPath;
		Debug.WriteLine($"Projeto carregado no GraphParserViewModel: {_rootPath}");
		_ = InitializeGraphAsync();
	}

	public async Task InitializeGraphAsync()
	{
		try
		{
			if (!string.IsNullOrEmpty(_rootPath))
			{
				await _indexService.GetOrIndexProjectAsync(_rootPath);
				Debug.WriteLine("Grafo indexado e pronto para busca.");
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Erro ao indexar grafo: {ex.Message}");
		}
	}

	[RelayCommand]
	public async Task PasteAndMergeFromClipboard()
	{
		try
		{
			var dataPackageView = Clipboard.GetContent();
			if (!dataPackageView.Contains(StandardDataFormats.Text))
			{
				Debug.WriteLine("Clipboard vazio ou sem texto.");
				return;
			}

			string snippetText = await dataPackageView.GetTextAsync();
			var result = await _aiMergerService.MergeAiSnippetAsync(snippetText);

			var previewVm = new AiPreviewViewModel(result, this);
			Tabs.Add(previewVm);
			SelectedTab = previewVm;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Erro crítico ao colar e processar IA: {ex.Message}");
		}
	}

	[RelayCommand]
	public void UpdateSearch(string query)
	{
		if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(_rootPath))
		{
			SearchSuggestions.Clear();
			return;
		}

		try
		{
			var graph = _indexService.GetCurrentGraph();
			if (graph == null || !graph.Nodes.Any())
			{
				SearchInDirectoryAsSuggestions(query);
				return;
			}

			var queryLower = query.ToLowerInvariant();
			var suggestions = new List<SearchSuggestion>();

			var nodesByFile = graph.Nodes.Values
				.GroupBy(node => graph.GetFilePath(node.FileId))
				.Where(group => !string.IsNullOrEmpty(group.Key) && File.Exists(group.Key));

			foreach (var fileGroup in nodesByFile)
			{
				try
				{
					if (string.IsNullOrEmpty(fileGroup.Key)) continue;
					var fileName = Path.GetFileName(fileGroup.Key);
					string filePath = fileGroup.Key;

					var matchingNodes = fileGroup
						.Where(node =>
							node.Name.ToLowerInvariant().Contains(queryLower) ||
							node.Type.ToString().ToLowerInvariant().Contains(queryLower) ||
							fileName.ToLowerInvariant().Contains(queryLower))
						.ToList();

					if (matchingNodes.Any())
					{
						string relativePath = Path.GetRelativePath(_rootPath, filePath);
						var symbolsFound = string.Join(", ", matchingNodes.Select(n => $"{n.Type}: {n.Name}").Take(3));

						suggestions.Add(new SearchSuggestion
						{
							Title = fileName,
							Subtitle = $"Encontrado: {symbolsFound}",
							FilePath = relativePath,
							Icon = "\uE943",
							MatchCount = matchingNodes.Count
						});
					}
				}
				catch { }
			}

			suggestions = suggestions.OrderByDescending(s => s.MatchCount).ThenBy(s => s.Title).Take(15).ToList();

			SearchSuggestions.Clear();
			foreach (var s in suggestions) SearchSuggestions.Add(s);

			if (!SearchSuggestions.Any()) SearchInDirectoryAsSuggestions(query);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Erro na busca: {ex.Message}");
			SearchInDirectoryAsSuggestions(query);
		}
	}

	public void SearchInDirectoryAsSuggestions(string query)
	{
		try
		{
			if (string.IsNullOrEmpty(_rootPath) || !Directory.Exists(_rootPath)) return;

			var files = Directory.EnumerateFiles(_rootPath, "*.cs", SearchOption.AllDirectories)
				.Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
				.Where(f => Path.GetFileName(f).Contains(query, StringComparison.OrdinalIgnoreCase))
				.Take(15)
				.Select(f => new SearchSuggestion
				{
					Title = Path.GetFileName(f),
					Subtitle = "Arquivo no diretório",
					FilePath = Path.GetRelativePath(_rootPath, f),
					Icon = "\uE943",
					MatchCount = 1
				})
				.ToList();

			SearchSuggestions.Clear();
			foreach (var s in files) SearchSuggestions.Add(s);
		}
		catch { SearchSuggestions.Clear(); }
	}

	[RelayCommand]
	public void OpenFile(object parameter)
	{
		string? path = null;
		if (parameter is SearchSuggestion suggestion) path = suggestion.FilePath;
		else if (parameter is string filePathString) path = filePathString;
		else if (parameter is null) return;

		if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(_rootPath)) return;

		string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_rootPath, path);

		var existingTab = Tabs.OfType<FileSegmentsViewModel>().FirstOrDefault(t => t.FilePath == fullPath);
		if (existingTab != null)
		{
			SelectedTab = existingTab;
			return;
		}

		var newTab = new FileSegmentsViewModel(
					fullPath,
					_fileSystemService,
					_parserService,
					_symbolService,
					_diffManager,
					GlobalHistory,
					CurrentGlobalIndex,
					_aiMergerService,
					_blockLoaderService,
					_blockEditorService
				);

		newTab.GlobalSaveRequested += (s, e) => CommitAllPendingChanges();
		newTab.GlobalRestoreRequested += (s, index) => CurrentGlobalIndex = index;
		Tabs.Add(newTab);
		SelectedTab = newTab;
	}

	[RelayCommand]
	public void CloseTab(object tab)
	{
		if (Tabs.Contains(tab))
		{
			Tabs.Remove(tab);
		}
	}
}