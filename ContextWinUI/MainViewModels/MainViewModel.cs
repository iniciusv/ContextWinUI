using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	// --- VIEW MODELS FILHOS ---
	public FileExplorerViewModel FileExplorer { get; }
	public FileContentViewModel FileContent { get; }
	public FileSelectionViewModel FileSelection { get; } // Seleção do Explorer (Checkboxes)
	public ContextAnalysisViewModel ContextAnalysis { get; } // Painel Direito
	public PrePromptViewModel PrePrompt { get; }

	// --- SERVIÇOS E GERENCIADORES ---
	private readonly IProjectSessionManager _sessionManager;

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		// 1. BOOTSTRAPPING (Criação dos Serviços)
		IFileSystemItemFactory itemFactory = new FileSystemItemFactory();
		IFileSystemService fileSystemService = new FileSystemService(itemFactory);
		IPersistenceService persistenceService = new PersistenceService();
		IRoslynAnalyzerService roslynAnalyzer = new RoslynAnalyzerService();
		ITagManagementUiService tagService = new TagManagementUiService();
		IGitService gitService = new GitService();
		ISelectionIOService selectionIOService = new SelectionIOService(); // Novo serviço de IO

		// Manager de Sessão
		_sessionManager = new ProjectSessionManager(fileSystemService, persistenceService, itemFactory);

		// 2. INJEÇÃO E CRIAÇÃO DOS VIEWMODELS

		// Explorer e Conteúdo
		FileExplorer = new FileExplorerViewModel(_sessionManager, tagService);
		FileContent = new FileContentViewModel(fileSystemService);
		FileSelection = new FileSelectionViewModel(fileSystemService, _sessionManager);
		PrePrompt = new PrePromptViewModel(_sessionManager);

		// Sub-ViewModel para a lista de Seleção da Análise (Composição)
		var contextSelectionVM = new ContextSelectionViewModel(itemFactory, selectionIOService);

		// ViewModel de Análise (Recebe o Sub-ViewModel injetado)
		ContextAnalysis = new ContextAnalysisViewModel(
			roslynAnalyzer,
			fileSystemService,
			itemFactory,
			tagService,
			gitService,
			contextSelectionVM
		);

		// 3. WIRING (Conexão de Eventos)
		WireUpEvents();
	}

	private void WireUpEvents()
	{
		// Quando seleciona um arquivo na árvore do Explorer -> Carrega no Visualizador
		FileExplorer.FileSelected += async (s, item) => await FileContent.LoadFileAsync(item);

		// Mantém o FileSelectionViewModel sincronizado com a árvore do Explorer
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.RootItems))
				FileSelection.SetRootItems(FileExplorer.RootItems);
		};

		// --- SINCRONIZAÇÃO EM TEMPO REAL ---
		// Quando usuário marca/desmarca checkbox no Explorer -> Atualiza o Preview na Análise
		FileSelection.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileSelection.SelectedFilesCount))
			{
				var currentSelection = FileSelection.GetCheckedFiles();
				ContextAnalysis.UpdateSelectionPreview(currentSelection);
			}
		};

		// Consolidação de Loading
		void UpdateLoading() => IsLoading = FileExplorer.IsLoading || FileContent.IsLoading || FileSelection.IsLoading || ContextAnalysis.IsLoading;

		FileExplorer.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileContent.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileSelection.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		ContextAnalysis.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };

		// Consolidação de Status
		_sessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	// Chamado pelo Code-Behind da View
	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	// Comando Principal: Analisar Contexto
	[RelayCommand]
	public async Task AnalyzeContextAsync()
	{
		var selectedFiles = FileSelection.GetCheckedFiles().ToList();

		// Fallback: Se nada selecionado, usa o arquivo aberto
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
			// 1. Atualiza Git
			await ContextAnalysis.RefreshGitChangesAsync(_sessionManager.CurrentProjectPath);

			// 2. Roda Análise Roslyn (Isso vai popular a árvore e atualizar a lista de seleção)
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