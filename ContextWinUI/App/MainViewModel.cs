using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace ContextWinUI.ViewModels; // Ou namespace ContextWinUI.App, dependendo da sua preferência

public partial class MainViewModel : ObservableObject
{
	// --- ViewModels Filhos ---
	public FileExplorerViewModel FileExplorer { get; }
	public ContextAnalysisViewModel ContextAnalysis { get; }
	public PrePromptViewModel PrePrompt { get; }
	public FileContentViewModel FileContent { get; }
	private readonly SemanticIndexService _semanticIndexService;
	public ContextSelectionViewModel FileSelection => FileExplorer.SelectionViewModel;
	public AiChangesViewModel AiChanges { get; }

	public IProjectSessionManager SessionManager { get; }

	// [CORREÇÃO] Propriedades de Estado Global exigidas pelo XAML
	[ObservableProperty]
	private string statusMessage = "Pronto";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		// 1. Serviços (Mantenha igual)
		IFileSystemItemFactory itemFactory = new FileSystemItemFactory();
		IFileSystemService fileSystemService = new FileSystemService(itemFactory);
		IPersistenceService persistenceService = new PersistenceService();
		IGitService gitService = new GitService();
		ISelectionIOService selectionIOService = new SelectionIOService();
		ITagManagementUiService tagService = new TagManagementUiService();

		SessionManager = new ProjectSessionManager(fileSystemService, persistenceService, itemFactory);
		var semanticIndexService = new SemanticIndexService();

		AiChanges = new AiChangesViewModel(fileSystemService, SessionManager, semanticIndexService);
		var dependencyTrackerService = new DependencyTrackerService();

		IDependencyAnalysisOrchestrator orchestrator = new DependencyAnalysisOrchestrator(
			semanticIndexService,
			dependencyTrackerService,
			itemFactory,
			fileSystemService
		);



		// 2. [CORREÇÃO AQUI] Instanciar ContextSelectionViewModel com as novas dependências
		var sharedSelectionVM = new ContextSelectionViewModel(
			itemFactory,
			selectionIOService,
			orchestrator, 
			SessionManager
		);

		// 3. Passar a MESMA instância para os ViewModels filhos (Mantenha igual)
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

		FileContent = new FileContentViewModel(fileSystemService);
		PrePrompt = new PrePromptViewModel(SessionManager);

		RegisterEvents();
	}

	[RelayCommand]
	private async Task ImportContextFileAsync()
	{
		if (!SessionManager.IsProjectLoaded)
		{
			StatusMessage = "Abra um projeto primeiro.";
			return;
		}

		var openPicker = new FileOpenPicker();

		// Inicialização de janela para WinUI 3
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
				// Chama o método que criamos na interface IProjectSessionManager
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
		// Atualiza barra de status global
		SessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;

		// Sincroniza Loading global com o Explorer (carregamento inicial)
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorerViewModel.IsLoading))
			{
				IsLoading = FileExplorer.IsLoading;
			}
		};
	}

	// Método chamado pelo Code-Behind (MainWindow.xaml.cs)
	public void OnFileSelected(FileSystemItem item)
	{
		if (item != null && item.IsCodeFile)
		{
			_ = FileContent.LoadFileAsync(item);
		}
	}

	// Comando para o botão "Analisar Contexto" (se houver)
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

	// [CORREÇÃO] Comando para o botão "Salvar" no header
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
		// Evita travar a UI rodando em Task.Run
		await Task.Run(async () =>
		{
			try
			{
				// Atualiza status na UI (precisa de dispatcher se não for ObservableProperty thread-safe)
				_dispatcherQueue.TryEnqueue(() => StatusMessage = "Indexando grafo de dependências...");

				// O serviço já tem lógica de cache, então se for a mesma pasta, é rápido
				await _semanticIndexService.GetOrIndexProjectAsync(e.RootPath);

				_dispatcherQueue.TryEnqueue(() => StatusMessage = "Grafo de dependências pronto.");
			}
			catch (Exception ex)
			{
				_dispatcherQueue.TryEnqueue(() => StatusMessage = $"Erro na indexação: {ex.Message}");
			}
		});
	}

	// Necessário para acessar thread de UI dentro do Task.Run
	private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
}