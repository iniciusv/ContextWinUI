using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;   // Para RoslynAnalyzer e Orchestrator
using ContextWinUI.Features.ContextBuilder; // Para ContextAnalysisViewModel e sub-VMs
using ContextWinUI.Features.Prompting;      // Para PrePromptViewModel

using ContextWinUI.Models;
using ContextWinUI.Services;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels; // Ou namespace ContextWinUI.App, dependendo da sua preferência

public partial class MainViewModel : ObservableObject
{
	// --- ViewModels Filhos ---
	public FileExplorerViewModel FileExplorer { get; }
	public FileContentViewModel FileContent { get; }
	public FileSelectionViewModel FileSelection { get; }
	public ContextAnalysisViewModel ContextAnalysis { get; }
	public PrePromptViewModel PrePrompt { get; }

	// --- Gerenciador de Sessão ---
	private readonly IProjectSessionManager _sessionManager;

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		// 1. Instanciação dos Serviços Básicos (Core Services)
		// Certifique-se que essas classes concretas existem nos namespaces importados acima
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

		// 4a. ViewModel de Seleção (Compartilhado internamente no ContextAnalysis)
		var contextSelectionVM = new ContextSelectionViewModel(itemFactory, selectionIOService);

		// 4b. Orchestrator (Lógica pesada de análise)
		IDependencyAnalysisOrchestrator analysisOrchestrator = new DependencyAnalysisOrchestrator(
			roslynAnalyzer, // Corrigido: Orchestrator precisa do RoslynService
			itemFactory,    // Corrigido: precisa da Factory
			fileSystemService // Corrigido: precisa de acesso a arquivos
		);

		// 5. Instanciação do ContextAnalysisViewModel (Orquestrador Mestre)
		// A ordem aqui deve bater EXATAMENTE com o construtor refatorado em ContextAnalysisViewModel.cs
		ContextAnalysis = new ContextAnalysisViewModel(
			roslynAnalyzer,
			itemFactory,
			analysisOrchestrator,
			_sessionManager,
			gitService,
			tagService,
			contextSelectionVM
		);

		// 6. Conectar Eventos
		WireUpEvents();
	}

	private void WireUpEvents()
	{
		// Quando um arquivo é selecionado na árvore principal, carrega o conteúdo no editor
		FileExplorer.FileSelected += async (s, item) => await FileContent.LoadFileAsync(item);

		// Sincroniza a árvore principal com a lista de seleção global
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
				// Importante: Certifique-se que você adicionou o método UpdateSelectionPreview
				// de volta no ContextAnalysisViewModel como instruído anteriormente.
				ContextAnalysis.UpdateSelectionPreview(currentSelection);
			}
		};

		// Centraliza o estado de Loading (Qualquer VM carregando ativa o spinner global)
		void UpdateLoading() => IsLoading = FileExplorer.IsLoading || FileContent.IsLoading || FileSelection.IsLoading || ContextAnalysis.IsLoading;

		FileExplorer.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileContent.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileSelection.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		ContextAnalysis.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };

		// Centraliza Mensagens de Status na barra inferior
		_sessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;

		// Propaga status dos sub-VMs do ContextAnalysis (se necessário, ou deixe o ContextAnalysis propagar)
		// ContextAnalysis.GitVM não tem evento StatusChanged exposto diretamente, 
		// mas o ContextAnalysis orquestra isso geralmente.
	}

	// Chamado pela View (Code-behind da MainWindow) quando clica na árvore
	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	// Comando Principal: "Analisar Contexto" (Botão Grande da Toolbar)
	[RelayCommand]
	public async Task AnalyzeContextAsync()
	{
		var selectedFiles = FileSelection.GetCheckedFiles().ToList();

		// Se nada selecionado mas tem arquivo aberto no editor, usa ele como contexto único
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
			// --- CORREÇÃO DE REFATORAÇÃO ---
			// O comando RefreshChangesCommand agora vive dentro do GitVM
			if (ContextAnalysis.GitVM.RefreshChangesCommand.CanExecute(null))
			{
				await ContextAnalysis.GitVM.RefreshChangesCommand.ExecuteAsync(null);
			}

			// Inicia análise principal (Geração de Árvore e Indexação)
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
		catch (System.Exception ex)
		{
			StatusMessage = $"Erro ao salvar: {ex.Message}";
		}
		finally
		{
			IsLoading = false;
		}
	}
}