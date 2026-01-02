using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.IAParser;
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

	[ObservableProperty]
	private ObservableCollection<FileSegmentsViewModel> tabs = new();

	[ObservableProperty]
	private FileSegmentsViewModel? selectedTab;

	[ObservableProperty]
	private ObservableCollection<SearchSuggestion> searchSuggestions = new();

	// --- VERSIONAMENTO CENTRALIZADO ---
	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory = new();

	[ObservableProperty]
	private int currentGlobalIndex = 0;
	// ----------------------------------

	public bool HasTabs => Tabs.Any();

	// Triggers para atualizar a UI quando abas mudam
	partial void OnTabsChanged(ObservableCollection<FileSegmentsViewModel> value) => OnPropertyChanged(nameof(HasTabs));
	partial void OnSelectedTabChanged(FileSegmentsViewModel? value) => OnPropertyChanged(nameof(HasTabs));

	public GraphParserViewModel(
		ISemanticIndexService indexService,
		IFileSystemService fileSystemService,
		IProjectSessionManager sessionManager,
		ICodeBlockParserService parserService,
		IAiCodeMerger aiMergerService,
		ISymbolResolutionService symbolService,
		IVersionDiffManager diffManager)
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_parserService = parserService;

		// ATRIBUIR AOS CAMPOS
		_symbolService = symbolService;
		_diffManager = diffManager;

		_rootPath = sessionManager.CurrentProjectPath ?? string.Empty;
		_parserService = parserService;

		Tabs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasTabs));

		if (string.IsNullOrEmpty(_rootPath))
		{
			Debug.WriteLine("AVISO: Nenhum projeto carregado. A busca não funcionará até que um projeto seja aberto.");
		}

		sessionManager.ProjectLoaded += OnProjectLoaded;

		// Inicializa o histórico com o estado zero
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

	// --- PONTO CRÍTICO: Implementação do método parcial gerado pelo ObservableProperty ---
	partial void OnCurrentGlobalIndexChanged(int value)
	{
		// Se o índice mudou (seja via UI, seja via código), propagamos para todas as abas
		RestoreAllToVersion(value);
	}

	[RelayCommand]
	public void CommitAllPendingChanges()
	{
		// Define um Timestamp ÚNICO para todo o lote
		var batchTime = DateTime.Now;
		string batchTimestamp = batchTime.ToString("HH:mm:ss");
		string description = $"Lote {batchTimestamp}";

		bool anyChange = false;

		// 1. Cria a versão Global
		var newGlobalVersion = new GlobalVersion
		{
			Description = description,
			Timestamp = batchTime,
			IsOriginal = false
		};
		GlobalHistory.Add(newGlobalVersion);

		// 2. Comanda TODAS as abas a criar snapshot com o MESMO horário
		foreach (var tab in Tabs)
		{
			// Opcional: só snapshotar se tiver mudanças, ou snapshotar tudo para garantir sincronia.
			// Aqui vamos snapshotar quem tem mudanças.
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

		// 3. Atualiza o índice para a nova versão (isso vai disparar OnCurrentGlobalIndexChanged)
		CurrentGlobalIndex = GlobalHistory.Count - 1;

		// 4. Força a sincronia visual do índice nas abas (caso OnCurrentGlobalIndexChanged não pegue algo)
		foreach (var tab in Tabs)
		{
			tab.SyncGlobalIndex(CurrentGlobalIndex);
		}
	}

	public void RestoreAllToVersion(int versionIndex)
	{
		if (versionIndex < 0 || versionIndex >= GlobalHistory.Count) return;

		Debug.WriteLine($"Restaurando todas as abas para versão global: {versionIndex}");

		// Garante que a propriedade local está correta (evita reentrância se já for igual)
		if (CurrentGlobalIndex != versionIndex)
		{
			CurrentGlobalIndex = versionIndex;
		}

		foreach (var tab in Tabs)
		{
			tab.RestoreToGlobalIndex(versionIndex);
		}
	}

	public bool HasAnyUnsavedChanges => Tabs.Any(t => t.Blocks.Any(b => b.HasUnsavedChanges));

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
				catch { /* Ignore */ }
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
	private void OpenFile(object parameter)
	{
		string? path = null;
		if (parameter is SearchSuggestion suggestion) path = suggestion.FilePath;
		else if (parameter is string filePathString) path = filePathString;
		else if (parameter is null) return;

		if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(_rootPath)) return;

		string fullPath = Path.Combine(_rootPath, path);
		var existingTab = Tabs.FirstOrDefault(t => t.FilePath == fullPath);
		if (existingTab != null)
		{
			SelectedTab = existingTab;
			return;
		}

		// --- AQUI ESTÁ A LIGAÇÃO CHAVE: Passamos o GlobalHistory e CurrentGlobalIndex ---
		var newTab = new FileSegmentsViewModel(
			fullPath,
			_fileSystemService,
			_parserService,
			_symbolService,  // <--- Passando o serviço injetado
			_diffManager,    // <--- Passando o serviço injetado
			GlobalHistory,
			CurrentGlobalIndex,
			_aiMergerService
		);

		// Assinamos os eventos para comunicação bi-direcional
		newTab.GlobalSaveRequested += (s, e) => CommitAllPendingChanges();

		// Quando a aba pede restore (ex: clicou no footer da aba), atualizamos o índice do pai
		// Isso vai disparar OnCurrentGlobalIndexChanged do Pai, que vai propagar para todos.
		newTab.GlobalRestoreRequested += (s, index) => CurrentGlobalIndex = index;

		Tabs.Add(newTab);
		SelectedTab = newTab;
	}

	[RelayCommand]
	private void CloseTab(FileSegmentsViewModel tab)
	{
		if (Tabs.Contains(tab))
		{
			Tabs.Remove(tab);
		}
	}
}