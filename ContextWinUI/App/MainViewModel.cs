// ARQUIVO: MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Features.GraphView;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace ContextWinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	// ViewModels Filhos
	public FileExplorerViewModel FileExplorer { get; }
	public ContextAnalysisViewModel ContextAnalysis { get; }
	public PrePromptViewModel PrePrompt { get; }
	public FileContentViewModel FileContent { get; }
	public AiChangesViewModel AiChanges { get; }

	// NOVO: ViewModel da Visualização do Grafo
	public GraphVisualizationViewModel GraphVisualization { get; }

	// Serviços
	private readonly SemanticIndexService _semanticIndexService;
	public IProjectSessionManager SessionManager { get; }

	// Atalho para Seleção
	public ContextSelectionViewModel FileSelection => FileExplorer.SelectionViewModel;

	[ObservableProperty]
	private string statusMessage = "Pronto";

	[ObservableProperty]
	private bool isLoading;

	private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

	public MainViewModel()
	{
		// 1. Instanciação dos Serviços Básicos
		IFileSystemItemFactory itemFactory = new FileSystemItemFactory();
		IFileSystemService fileSystemService = new FileSystemService(itemFactory);
		IPersistenceService persistenceService = new PersistenceService();
		IGitService gitService = new GitService();
		ISelectionIOService selectionIOService = new SelectionIOService();
		ITagManagementUiService tagService = new TagManagementUiService();

		SessionManager = new ProjectSessionManager(fileSystemService, persistenceService, itemFactory);
		_semanticIndexService = new SemanticIndexService();

		ISyntaxAnalysisService syntaxService = new RoslynSyntaxAnalysisService();
		IClipboardService clipboardService = new ClipboardService();

		// 2. Instanciação de ViewModels Independentes ou de Dependência Circular
		AiChanges = new AiChangesViewModel(fileSystemService, SessionManager, _semanticIndexService);

		var dependencyTrackerService = new DependencyTrackerService();
		IDependencyAnalysisOrchestrator orchestrator = new DependencyAnalysisOrchestrator(
			_semanticIndexService,
			dependencyTrackerService,
			itemFactory,
			fileSystemService
		);

		var sharedSelectionVM = new ContextSelectionViewModel(
			itemFactory,
			selectionIOService,
			orchestrator,
			SessionManager
		);

		// 3. Instanciação dos ViewModels Principais
		FileExplorer = new FileExplorerViewModel(
				SessionManager,
				tagService,
				fileSystemService,
				sharedSelectionVM,
				itemFactory
			);

		ContextAnalysis = new ContextAnalysisViewModel(
			itemFactory,
			orchestrator,
			SessionManager,
			gitService,
			tagService,
			sharedSelectionVM
		);

		// --- PONTO CRÍTICO DE CORREÇÃO ---
		// O FileContentViewModel DEVE ser instanciado ANTES do GraphVisualizationViewModel
		FileContent = new FileContentViewModel(fileSystemService);

		// Agora podemos instanciar o GraphVisualization, pois ele acessa this.FileContent no construtor
		GraphVisualization = new GraphVisualizationViewModel(this, syntaxService, clipboardService);
		// ---------------------------------

		PrePrompt = new PrePromptViewModel(SessionManager);

		// 4. Registro de Eventos
		RegisterEvents();
	}

	// Restante dos métodos (Commandos, Eventos, etc) permanece igual...

	[RelayCommand]
	private async Task ImportContextFileAsync()
	{
		if (!SessionManager.IsProjectLoaded)
		{
			StatusMessage = "Abra um projeto primeiro.";
			return;
		}

		var openPicker = new FileOpenPicker();

		// Necessário para WinUI 3 em Desktop
		if (App.MainWindow != null)
		{
			var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
			WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
		}

		openPicker.ViewMode = PickerViewMode.List;
		openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
		openPicker.FileTypeFilter.Add(".json");

		var file = await openPicker.PickSingleFileAsync();
		if (file != null)
		{
			IsLoading = true;
			try
			{
				await SessionManager.LoadContextFromFileAsync(file.Path);
				StatusMessage = $"Contexto importado: {file.Name}";
			}
			catch (Exception ex)
			{
				StatusMessage = $"Erro ao importar: {ex.Message}";
			}
			finally
			{
				IsLoading = false;
			}
		}
	}

	[RelayCommand]
	private async Task ExportContextFileAsync()
	{
		if (!SessionManager.IsProjectLoaded)
		{
			StatusMessage = "Abra um projeto primeiro.";
			return;
		}

		var savePicker = new FileSavePicker();

		if (App.MainWindow != null)
		{
			var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
			WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
		}

		savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
		savePicker.FileTypeChoices.Add("JSON Context", new List<string>() { ".json" });
		savePicker.SuggestedFileName = "contexto_exportado";

		var file = await savePicker.PickSaveFileAsync();
		if (file != null)
		{
			IsLoading = true;
			try
			{
				await SessionManager.ExportContextAsAsync(file.Path);
				StatusMessage = $"Contexto exportado para: {file.Name}";
			}
			catch (Exception ex)
			{
				StatusMessage = $"Erro ao exportar: {ex.Message}";
			}
			finally
			{
				IsLoading = false;
			}
		}
	}

	private void RegisterEvents()
	{
		SessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		SessionManager.ProjectLoaded += OnProjectLoaded_IndexGraph;

		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;

		// Repassar Loading do Explorer para a Main
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorerViewModel.IsLoading))
			{
				IsLoading = FileExplorer.IsLoading;
			}
		};
	}

	public void OnFileSelected(FileSystemItem item)
	{
		if (item != null && item.IsCodeFile)
		{
			_ = FileContent.LoadFileAsync(item);
		}
	}

	[RelayCommand]
	private async Task AnalyzeContextAsync()
	{
		var selectedFiles = FileExplorer.SelectionViewModel.GetCheckedFiles().ToList();
		var rootPath = SessionManager.CurrentProjectPath;

		if (selectedFiles.Any() && !string.IsNullOrEmpty(rootPath))
		{
			StatusMessage = "Iniciando análise...";
			IsLoading = true;
			try
			{
				await ContextAnalysis.AnalyzeContextAsync(selectedFiles, rootPath);
			}
			finally
			{
				IsLoading = false;
			}
		}
	}

	[RelayCommand]
	private async Task SaveWorkAsync()
	{
		if (!SessionManager.IsProjectLoaded) return;

		IsLoading = true;
		try
		{
			await SessionManager.SaveSessionAsync();
			StatusMessage = "Trabalho salvo com sucesso.";
		}
		finally
		{
			IsLoading = false;
		}
	}

	private async void OnProjectLoaded_IndexGraph(object? sender, ProjectLoadedEventArgs e)
	{
		await Task.Run(async () =>
		{
			try
			{
				_dispatcherQueue.TryEnqueue(() => StatusMessage = "Indexando grafo de dependências...");

				await _semanticIndexService.GetOrIndexProjectAsync(e.RootPath);

				_dispatcherQueue.TryEnqueue(() =>
				{
					StatusMessage = "Grafo de dependências pronto.";
					// Notifica a aba de visualização que o grafo pode ter mudado (opcional, pois ela reage a seleção de arquivo)
					GraphVisualization.UpdateVisualization();
				});
			}
			catch (Exception ex)
			{
				_dispatcherQueue.TryEnqueue(() => StatusMessage = $"Erro na indexação: {ex.Message}");
			}
		});
	}
}