using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.IAParser; // Namespace das classes novas (IA)
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.Services;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class GraphParserViewModel : ObservableObject
{
	private readonly ISemanticIndexService _indexService;
	private readonly IFileSystemService _fileSystemService;
	private string _rootPath;
	private readonly ICodeBlockParserService _parserService;
	private readonly ISymbolResolutionService _symbolService;
	private readonly IVersionDiffManager _diffManager;
	private readonly IAiCodeMerger _aiMergerService;
	private readonly IBlockEditorService _blockEditorService;

	// Alterado para 'object' para aceitar FileSegmentsViewModel E AiPreviewViewModel
	[ObservableProperty]
	private ObservableCollection<object> tabs = new();

	// Alterado para 'object' para suportar a seleção de diferentes tipos de VM
	[ObservableProperty]
	private object? selectedTab;

	[ObservableProperty]
	private ObservableCollection<SearchSuggestion> searchSuggestions = new();

	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory = new();

	[ObservableProperty]
	private int currentGlobalIndex = 0;

	public bool HasTabs => Tabs.Any();

	partial void OnTabsChanged(ObservableCollection<object> value) => OnPropertyChanged(nameof(HasTabs));
	partial void OnSelectedTabChanged(object? value) => OnPropertyChanged(nameof(HasTabs));

	public GraphParserViewModel(
		ISemanticIndexService indexService,
		IFileSystemService fileSystemService,
		IProjectSessionManager sessionManager,
		ICodeBlockParserService parserService,
		IAiCodeMerger aiMergerService, // Injeção da dependência
		ISymbolResolutionService symbolService,
		IVersionDiffManager diffManager,
		IBlockEditorService blockEditorService)
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_parserService = parserService;
		_symbolService = symbolService;
		_diffManager = diffManager;
		_aiMergerService = aiMergerService;
		_rootPath = sessionManager.CurrentProjectPath ?? string.Empty;
		_blockEditorService = blockEditorService;

		Tabs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasTabs));

		if (string.IsNullOrEmpty(_rootPath))
		{
			Debug.WriteLine("AVISO: Nenhum projeto carregado. A busca não funcionará até que um projeto seja aberto.");
		}

		sessionManager.ProjectLoaded += OnProjectLoaded;
		InitializeGlobalHistory();
	}

	private void InitializeGlobalHistory()
	{
		GlobalHistory.Clear();
		GlobalHistory.Add(new GlobalVersion
		{
			Description = "Estado Inicial",
			IsOriginal = true,
			Timestamp = DateTime.MinValue
		});
		CurrentGlobalIndex = 0;
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

		// Filtramos apenas as abas de arquivo, ignorando previews de IA
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

		// Sincroniza apenas abas de arquivo
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

		// Restaura apenas abas de arquivo
		foreach (var tab in Tabs.OfType<FileSegmentsViewModel>())
		{
			tab.RestoreToGlobalIndex(versionIndex);
		}
	}

	// Verifica alterações apenas em abas de arquivo real
	public bool HasAnyUnsavedChanges => Tabs.OfType<FileSegmentsViewModel>().Any(t => t.Blocks.Any(b => b.HasUnsavedChanges));

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		_rootPath = e.RootPath;
		Debug.WriteLine($"Projeto carregado no GraphParserViewModel: {_rootPath}");
		_ = InitializeGraphAsync();
	}

	private async Task InitializeGraphAsync()
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

	// --- COMANDO NOVO: SMART PASTE ---
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

			// 1. Executa a análise no serviço (in-memory merge)
			var result = await _aiMergerService.MergeAiSnippetAsync(snippetText);

			// 2. Cria a ViewModel de Preview usando o namespace IAParser
			// Note: AiPreviewViewModel precisa existir e aceitar (AiMergeResult, GraphParserViewModel)
			var previewVm = new AiPreviewViewModel(result, this);

			// 3. Abre a aba de preview
			Tabs.Add(previewVm);
			SelectedTab = previewVm;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Erro crítico ao colar e processar IA: {ex.Message}");
		}
	}

	[RelayCommand]
	private void UpdateSearch(string query)
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

	private void SearchInDirectoryAsSuggestions(string query)
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

		// Verifica se a aba já existe (somente entre as FileSegmentsViewModel)
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