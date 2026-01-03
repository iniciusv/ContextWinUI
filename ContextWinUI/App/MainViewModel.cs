// ARQUIVO: MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Features.GraphAnalysis;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Models;
using ContextWinUI.Services; // Necessário para SemanticIndexService concreto, se não tiver interface
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace ContextWinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	// =========================================================
	// DEPENDÊNCIAS (Injetadas via Construtor)
	// =========================================================
	private readonly ISemanticIndexService _semanticIndexService;
	private readonly IFileSelectionService _fileSelectionService;
	private readonly IAiCodeMerger _aiMergerService;


	// Dispatcher para atualizações de UI em threads de fundo
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	// =========================================================
	// PROPRIEDADES DOS VIEWMODELS FILHOS
	// =========================================================
	public FileExplorerViewModel FileExplorer { get; }
	public ContextAnalysisViewModel ContextAnalysis { get; }
	public PrePromptViewModel PrePrompt { get; }
	public FileContentViewModel FileContent { get; }
	public GraphParserViewModel GraphParser { get; }
	public IProjectSessionManager SessionManager { get; }
	public SemanticGraphViewModel SemanticGraph { get; }

	// Atalho para binding na View (aponta para a instância dentro do Explorer)
	public ContextSelectionViewModel FileSelection => FileExplorer.SelectionViewModel;

	// =========================================================
	// ESTADO DA UI
	// =========================================================
	[ObservableProperty]
	private string statusMessage = "Pronto";

	[ObservableProperty]
	private bool isLoading;

	// =========================================================
	// CONSTRUTOR (Agora limpo e rápido)
	// =========================================================
	public MainViewModel(
				FileExplorerViewModel fileExplorer,
				ContextAnalysisViewModel contextAnalysis,
				PrePromptViewModel prePrompt,
				FileContentViewModel fileContent,
				GraphParserViewModel graphParser,
				IAiCodeMerger aiMergerService,
			IProjectSessionManager sessionManager,      // <--- MUDANÇA 2: Pedir a Interface (I...)
				ISemanticIndexService semanticIndexService,
			IFileSelectionService fileSelectionService, // <--- MUDANÇA 3: Pedir a Interface (I...)
				SemanticGraphViewModel semanticGraph)
	{
		FileExplorer = fileExplorer;
		ContextAnalysis = contextAnalysis;
		PrePrompt = prePrompt;
		FileContent = fileContent;
		GraphParser = graphParser;
		SessionManager = sessionManager;
		_semanticIndexService = semanticIndexService;
		_fileSelectionService = fileSelectionService;
		_aiMergerService = aiMergerService;

		SemanticGraph = semanticGraph;

		RegisterEvents();
	}

	// =========================================================
	// COMANDOS E LÓGICA (Inalterados, mas agora usam DI)
	// =========================================================

	[RelayCommand]
	private async Task ImportContextFileAsync()
	{
		if (!SessionManager.IsProjectLoaded)
		{
			StatusMessage = "Abra um projeto primeiro.";
			return;
		}

		var openPicker = new FileOpenPicker();

		// Configuração de Janela para WinUI 3
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

	[RelayCommand]
	private async Task OpenInParserAsync() // Removido async void implícito se não for event handler direto
	{
		// Como não há await aqui, poderia ser síncrono, mas mantive a assinatura Task por compatibilidade
		await Task.CompletedTask;

		var item = FileContent.SelectedItem;
		if (item == null)
		{
			var checkedItems = FileExplorer.SelectionViewModel.GetCheckedFiles();
			item = checkedItems.FirstOrDefault();
		}

		if (item != null)
		{
			// Lógica de abrir no parser (se houver navegação ou ação específica, insira aqui)
			// No código original apenas validava o item.
			StatusMessage = $"Arquivo selecionado para parser: {item.Name}";
		}
		else
		{
			StatusMessage = "Selecione um arquivo de código válido para editar.";
		}
	}



	public void OnFileSelected(FileSystemItem item)
	{
		_fileSelectionService.SetSelection(item);
	}

	private void RegisterEvents()
	{
		// Eventos do SessionManager
		SessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		SessionManager.ProjectLoaded += OnProjectLoaded_IndexGraph;

		// Eventos dos ViewModels filhos
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;

		// Sincronizar estado de Loading do Explorer com o Main
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorerViewModel.IsLoading))
			{
				IsLoading = FileExplorer.IsLoading;
			}
		};
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
		// Executa indexação pesada em background para não travar a UI
		await Task.Run(async () =>
		{
			try
			{
				_dispatcherQueue.TryEnqueue(() => StatusMessage = "Indexando grafo de dependências...");

				await _semanticIndexService.GetOrIndexProjectAsync(e.RootPath);

				_dispatcherQueue.TryEnqueue(() =>
				{
					StatusMessage = "Grafo de dependências pronto.";
				});
			}
			catch (Exception ex)
			{
				_dispatcherQueue.TryEnqueue(() => StatusMessage = $"Erro na indexação: {ex.Message}");
			}
		});
	}
}