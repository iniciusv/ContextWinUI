using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
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

	// O Orquestrador de Sessão
	private readonly IProjectSessionManager _sessionManager;

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		// 1. BOOTSTRAPPING
		IFileSystemItemFactory itemFactory = new FileSystemItemFactory();
		IFileSystemService fileSystemService = new FileSystemService(itemFactory);
		IPersistenceService persistenceService = new PersistenceService();
		IRoslynAnalyzerService roslynAnalyzer = new RoslynAnalyzerService();
		ITagManagementUiService tagService = new TagManagementUiService();

		_sessionManager = new ProjectSessionManager(fileSystemService, persistenceService, itemFactory);

		// 2. INJEÇÃO NOS VIEWMODELS
		FileExplorer = new FileExplorerViewModel(_sessionManager, tagService);
		FileContent = new FileContentViewModel(fileSystemService);
		FileSelection = new FileSelectionViewModel(fileSystemService);
		ContextAnalysis = new ContextAnalysisViewModel(roslynAnalyzer, fileSystemService, itemFactory, tagService);

		// 3. CONEXÃO DE EVENTOS (Wiring)
		WireUpEvents();
	}

	private void WireUpEvents()
	{
		// Explorer -> Content (Preview)
		FileExplorer.FileSelected += async (s, item) => await FileContent.LoadFileAsync(item);

		// Explorer -> Selection (Sincroniza base de itens para contagem)
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.RootItems))
				FileSelection.SetRootItems(FileExplorer.RootItems);
		};

		// --- NOVA CONEXÃO: Sincronização em Tempo Real (Selection -> ContextAnalysis) ---
		// Toda vez que a contagem de arquivos selecionados mudar (check/uncheck), 
		// atualizamos o visual da Análise de Contexto.
		FileSelection.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileSelection.SelectedFilesCount))
			{
				// Pega a lista atualizada de checked
				var currentSelection = FileSelection.GetCheckedFiles();
				// Atualiza o visual da outra tela sem rodar análise pesada
				ContextAnalysis.UpdateSelectionPreview(currentSelection);
			}
		};

		// Loading Global
		void UpdateLoading() => IsLoading = FileExplorer.IsLoading || FileContent.IsLoading || FileSelection.IsLoading || ContextAnalysis.IsLoading;

		FileExplorer.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileContent.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileSelection.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		ContextAnalysis.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };

		// Status Global
		_sessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	// Comando: Analisar Contexto (Agora aprofunda a análise dos itens já mostrados)
	public async Task AnalyzeContextCommandAsync()
	{
		var selectedFiles = FileSelection.GetCheckedFiles().ToList();

		// Fallback: Usa arquivo aberto se não houver seleção
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
			// Isso agora vai rodar o Roslyn e substituir os nós simples por nós ricos
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
			StatusMessage = "Projeto e tags salvos com sucesso.";
		}
		finally { IsLoading = false; }
	}
}