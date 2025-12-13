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
		// 1. Instanciar dependências
		IFileSystemItemFactory itemFactory = new FileSystemItemFactory();
		IFileSystemService fileSystemService = new FileSystemService(itemFactory);
		IPersistenceService persistenceService = new PersistenceService();
		IRoslynAnalyzerService roslynAnalyzer = new RoslynAnalyzerService();

		// NOVO SERVIÇO
		ITagManagementUiService tagService = new TagManagementUiService();

		_sessionManager = new ProjectSessionManager(fileSystemService, persistenceService, itemFactory);

		// 2. Injeção nos ViewModels (AQUI ESTAVA O ERRO)

		// Passando tagService para o Explorer
		FileExplorer = new FileExplorerViewModel(_sessionManager, tagService);

		FileContent = new FileContentViewModel(fileSystemService);
		FileSelection = new FileSelectionViewModel(fileSystemService);

		// ContextAnalysis também precisa gerenciar tags, injete nele também!
		ContextAnalysis = new ContextAnalysisViewModel(roslynAnalyzer, fileSystemService, itemFactory, tagService);

		WireUpEvents();
	}

	private void WireUpEvents()
	{
		// Conexão: Selecionou no Explorer -> Carrega no Visualizador
		FileExplorer.FileSelected += async (s, item) => await FileContent.LoadFileAsync(item);

		// Conexão: Explorer carregou novos itens -> Atualiza a base do ViewModel de Seleção em Massa
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.RootItems))
				FileSelection.SetRootItems(FileExplorer.RootItems);
		};

		// Consolidação do Loading (Qualquer um carregando = App carregando)
		void UpdateLoading() => IsLoading = FileExplorer.IsLoading || FileContent.IsLoading || FileSelection.IsLoading || ContextAnalysis.IsLoading;

		FileExplorer.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileContent.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileSelection.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		ContextAnalysis.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };

		// Consolidação de Mensagens de Status (O Manager tem prioridade)
		_sessionManager.StatusChanged += (s, msg) => StatusMessage = msg;

		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	// Chamado pelo Code-Behind da View (MainWindow.xaml.cs)
	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	// Comando: Analisar Contexto
	public async Task AnalyzeContextCommandAsync()
	{
		// Pega arquivos marcados no Checkbox
		var selectedFiles = FileSelection.GetCheckedFiles().ToList();

		// Fallback: Se nada marcado, usa o arquivo aberto no editor
		if (!selectedFiles.Any() && FileContent.SelectedItem?.IsCodeFile == true)
		{
			selectedFiles.Add(FileContent.SelectedItem);
		}

		if (!selectedFiles.Any())
		{
			StatusMessage = "Selecione arquivos para analisar.";
			return;
		}

		// Inicia a análise usando o caminho atual do projeto
		if (_sessionManager.CurrentProjectPath != null)
		{
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
		finally
		{
			IsLoading = false;
		}
	}
}