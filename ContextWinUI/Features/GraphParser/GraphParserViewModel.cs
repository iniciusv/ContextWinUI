using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.Services;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class GraphParserViewModel : ObservableObject, IGraphParserContract
{
	// =================================================================================
	// 1. DEPENDÊNCIAS (Injetadas via Construtor)
	// =================================================================================
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

	// =================================================================================
	// 3. CONSTRUTOR
	// =================================================================================
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
		// Injeção
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

		// Configuração de Eventos
		sessionManager.ProjectLoaded += (s, e) => { _rootPath = e.RootPath; _ = InitializeGraphAsync(); };

		// Monitora adições/remoções para atualizar UI e conectar eventos
		Tabs.CollectionChanged += OnTabsCollectionChanged;

		InitializeGlobalHistory();

		_previewManager.Start(this);
	}

	[RelayCommand]
	public void OpenAsPermanent(object parameter)
	{
		string? path = ExtractPath(parameter);
		if (string.IsNullOrEmpty(path)) return;

		path = NormalizePath(path);

		// 1. Verifica se a aba já existe na coleção
		var existingTab = Tabs.OfType<FileSegmentsViewModel>()
							  .FirstOrDefault(t => t.FilePath == path);

		if (existingTab != null)
		{
			SelectedTab = existingTab;

			// A MÁGICA DA PROMOÇÃO:
			// Se a aba encontrada era um Preview, o usuário acabou de confirmar
			// que quer mantê-la (clique duplo). Removemos a flag de preview.
			if (existingTab.IsPreview)
			{
				existingTab.IsPreview = false;
			}
			return;
		}

		// 2. Se não existe, cria uma nova aba FIXA.
		var newTab = _vmFactory.Create(path);
		newTab.IsPreview = false; // Garante explicitamente que é permanente

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

		// 3. Criação: Gera a nova aba e marca como Preview.
		var newTab = _vmFactory.Create(path);
		newTab.IsPreview = true;

		// 4. Auto-Promoção: Se o usuário editar o código nesta aba de preview,
		// ela deve se tornar permanente automaticamente para não perder dados.


		Tabs.Add(newTab);
		SelectedTab = newTab;
	}


	// =================================================================================
	// HELPERS PRIVADOS (Necessários para os métodos acima funcionarem)
	// =================================================================================

	private string NormalizePath(string path)
	{
		// Garante que o caminho seja absoluto para comparação correta
		if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(_rootPath))
			return Path.Combine(_rootPath, path);
		return path;
	}

	private string? ExtractPath(object parameter)
	{
		// Extrai string do CommandParameter (que pode vir da Busca ou String direta)
		if (parameter is SearchSuggestion s) return s.FilePath;
		if (parameter is string p) return p;
		return null;
	}

	private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// Atualiza a propriedade HasTabs para a UI
		OnPropertyChanged(nameof(HasTabs));

		// Aqui conectaríamos os eventos globais (Save/Restore) para as novas abas
		// Omitido para brevidade, mas essencial na implementação real.
	}


	private void InitializeGlobalHistory()
	{
		GlobalHistory.Clear();

		// Estado 0: Original do Git (Passado)
		GlobalHistory.Add(new GlobalVersion
		{
			Description = "Base (Git HEAD)",
			IsOriginal = true,
			Timestamp = DateTime.MinValue // Garante que pega apenas versões marcadas como originais
		});

		// Estado 1: Trabalho em Andamento (Presente)
		GlobalHistory.Add(new GlobalVersion
		{
			Description = "Atual (Working Copy)",
			IsOriginal = false,
			// Usamos MaxValue para garantir que este estado "capture" qualquer edição feita agora ou no futuro
			Timestamp = DateTime.MaxValue
		});

		// Define o índice padrão para 1 (O estado Atual)
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