using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Services;
using ContextWinUI.Features.CodeAnalyses; // Necessário para o Orchestrator
using ContextWinUI.Models;
using ContextWinUI.Services; // Assumindo que suas implementações de serviço estão aqui
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	// ViewModels Filhos
	public FileExplorerViewModel FileExplorer { get; }
	public FileContentViewModel FileContent { get; }
	public FileSelectionViewModel FileSelection { get; }
	public ContextAnalysisViewModel ContextAnalysis { get; }
	public PrePromptViewModel PrePrompt { get; }

	// Gerenciador de Sessão
	private readonly IProjectSessionManager _sessionManager;

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		// 1. Instanciação dos Serviços Básicos (Core Services)
		IFileSystemItemFactory itemFactory = new FileSystemItemFactory();
		IFileSystemService fileSystemService = new FileSystemService(itemFactory);
		IPersistenceService persistenceService = new PersistenceService();
		IRoslynAnalyzerService roslynAnalyzer = new RoslynAnalyzerService();
		ITagManagementUiService tagService = new TagManagementUiService();
		IGitService gitService = new GitService();
		ISelectionIOService selectionIOService = new SelectionIOService();

		// 2. Gerenciador de Sessão
		_sessionManager = new ProjectSessionManager(fileSystemService, persistenceService, itemFactory);

		// 3. ViewModels Independentes
		FileExplorer = new FileExplorerViewModel(_sessionManager, tagService);
		FileContent = new FileContentViewModel(fileSystemService);
		FileSelection = new FileSelectionViewModel(fileSystemService, _sessionManager);
		PrePrompt = new PrePromptViewModel(_sessionManager);

		// 4. Configuração da Análise de Contexto
		var contextSelectionVM = new ContextSelectionViewModel(itemFactory, selectionIOService);

		// --- CORREÇÃO AQUI: Criar o Orchestrator ---
		IDependencyAnalysisOrchestrator analysisOrchestrator = new DependencyAnalysisOrchestrator(
			roslynAnalyzer,
			itemFactory,
			fileSystemService
		);

		// 5. Instanciação do ContextAnalysisViewModel com a nova dependência
		ContextAnalysis = new ContextAnalysisViewModel(
			roslynAnalyzer,
			fileSystemService,
			itemFactory,
			tagService,
			gitService,
			_sessionManager,
			contextSelectionVM,
			analysisOrchestrator // <--- Injeção da nova dependência
		);

		// 6. Conectar Eventos
		WireUpEvents();
	}

	private void WireUpEvents()
	{
		// Quando um arquivo é selecionado na árvore principal, carrega o conteúdo
		FileExplorer.FileSelected += async (s, item) => await FileContent.LoadFileAsync(item);

		// Sincroniza a árvore principal com a lista de seleção
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.RootItems))
				FileSelection.SetRootItems(FileExplorer.RootItems);
		};

		// Atualiza o preview da análise quando a seleção muda na aba principal
		FileSelection.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileSelection.SelectedFilesCount))
			{
				var currentSelection = FileSelection.GetCheckedFiles();
				ContextAnalysis.UpdateSelectionPreview(currentSelection);
			}
		};

		// Centraliza o estado de Loading
		void UpdateLoading() => IsLoading = FileExplorer.IsLoading || FileContent.IsLoading || FileSelection.IsLoading || ContextAnalysis.IsLoading;

		FileExplorer.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileContent.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileSelection.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		ContextAnalysis.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };

		// Centraliza Mensagens de Status
		_sessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	// Chamado pela View (Code-behind)
	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	// Comando Principal: "Analisar Contexto" (Botão Grande da Toolbar)
	[RelayCommand]
	public async Task AnalyzeContextAsync()
	{
		var selectedFiles = FileSelection.GetCheckedFiles().ToList();

		// Se nada selecionado mas tem arquivo aberto no editor, usa ele
		if (!selectedFiles.Any() && FileContent.SelectedItem?.IsCodeFile == true)
		{
			selectedFiles.Add(FileContent.SelectedItem);
		}

		if (!selectedFiles.Any())
		{
			StatusMessage = "Selecione arquivos para analisar.";
			return;
		}

		if (_sessionManager.CurrentProjectPath != null)
		{
			// Atualiza Git antes de analisar
			await ContextAnalysis.RefreshGitChangesCommand.ExecuteAsync(null);

			// Inicia análise principal
			await ContextAnalysis.AnalyzeContextAsync(selectedFiles, _sessionManager.CurrentProjectPath);
		}
	}

	[RelayCommand]
	public async Task SaveWorkAsync()
	{
		if (IsLoading) return;

		IsLoading = true;
		try
		{
			await _sessionManager.SaveSessionAsync();
			StatusMessage = "Projeto salvo com sucesso.";
		}
		finally
		{
			IsLoading = false;
		}
	}
}