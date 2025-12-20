using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;

using ContextWinUI.Core.Shared;
using ContextWinUI.Features.CodeAnalyses;

using ContextWinUI.Features.Session;
using ContextWinUI.Features.Tagging;
using ContextWinUI.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI;

public partial class MainViewModel : ObservableObject
{
	// Serviços (Camada de Infraestrutura)
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IFileSystemService _fileSystemService;
	private readonly IPersistenceService _persistenceService; // Adicionado
	private readonly IProjectSessionManager _sessionManager;
	private readonly IRoslynAnalyzerService _roslynAnalyzer;
	private readonly ITagManagementUiService _tagService;
	private readonly IGitService _gitService;
	private readonly ISelectionIOService _selectionIOService;
	private readonly IContentGenerationService _contentGenService;

	// ViewModels (Camada de Apresentação)
	public FileExplorerViewModel FileExplorer { get; }
	public FileSelectionViewModel FileSelection { get; } // Seleção do Explorer (Esquerda)
	public ContextAnalysisViewModel ContextAnalysis { get; }
	public FileContentViewModel FileContent { get; }
	public PrePromptViewModel PrePrompt { get; }

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string statusMessage = "Pronto";

	public MainViewModel()
	{
		// 1. Inicialização de Serviços (Ordem importa devido às dependências)
		_itemFactory = new FileSystemItemFactory();
		_fileSystemService = new FileSystemService(_itemFactory);
		_persistenceService = new PersistenceService(); // Instância concreta criada
		_roslynAnalyzer = new RoslynAnalyzerService();
		_tagService = new TagManagementUiService();
		_gitService = new GitService();
		_selectionIOService = new SelectionIOService();

		// Novo serviço de geração de conteúdo
		_contentGenService = new ContentGenerationService(_fileSystemService, _roslynAnalyzer);

		// SessionManager agora recebe suas 3 dependências corretamente
		_sessionManager = new ProjectSessionManager(_fileSystemService, _persistenceService, _itemFactory);

		// 2. Inicialização de ViewModels

		// ViewModel do Explorer (Esquerda)
		FileExplorer = new FileExplorerViewModel(_sessionManager, _tagService);
		FileSelection = new FileSelectionViewModel(_contentGenService, _sessionManager);

		// ViewModel de Análise (Direita) - Precisa de seu próprio VM de seleção
		// CORREÇÃO: Criamos ContextSelectionViewModel explicitamente aqui
		var contextSelectionVM = new ContextSelectionViewModel(_itemFactory, _selectionIOService);

		ContextAnalysis = new ContextAnalysisViewModel(
			_roslynAnalyzer,
			_fileSystemService,
			_itemFactory,
			_tagService,
			_gitService,
			_sessionManager,
			contextSelectionVM, // Passamos o VM correto (Argumento 7 corrigido)
			_contentGenService
		);

		// Outros ViewModels
		FileContent = new FileContentViewModel(_fileSystemService);
		PrePrompt = new PrePromptViewModel(_sessionManager);

		// 3. Wiring de Eventos Globais para Status
		_sessionManager.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	[RelayCommand]
	private async Task SaveWorkAsync()
	{
		IsLoading = true;
		StatusMessage = "Salvando sessão...";
		try
		{
			await _sessionManager.SaveSessionAsync();
			StatusMessage = "Sessão salva com sucesso.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Erro ao salvar: {ex.Message}";
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private async Task AnalyzeContextAsync()
	{
		// Pega os arquivos marcados
		var selected = FileSelection.GetCheckedFiles().ToList();

		// CORREÇÃO: Feedback se nada estiver selecionado
		if (!selected.Any())
		{
			StatusMessage = "Selecione pelo menos um arquivo para analisar.";
			return;
		}

		StatusMessage = $"Analisando {selected.Count} arquivos...";

		// Envia para o painel de análise
		await ContextAnalysis.AnalyzeContextAsync(selected, _sessionManager.CurrentProjectPath);
	}

	public void OnFileSelected(FileSystemItem item)
	{
		_ = FileContent.LoadFileAsync(item);
	}
}