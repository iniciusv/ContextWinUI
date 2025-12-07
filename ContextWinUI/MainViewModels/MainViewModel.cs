using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	// Sub-ViewModels
	public FileExplorerViewModel FileExplorer { get; }
	public FileContentViewModel FileContent { get; }
	public FileSelectionViewModel FileSelection { get; }
	public MethodAnalysisViewModel MethodAnalysis { get; }

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	// Propriedade computada para inverter IsVisible
	[ObservableProperty]
	private bool isMethodAnalysisHidden = true;

	public MainViewModel()
	{
		// Criar serviços compartilhados
		var fileSystemService = new FileSystemService();
		var roslynAnalyzer = new RoslynAnalyzerService();

		// Inicializar sub-ViewModels
		FileExplorer = new FileExplorerViewModel(fileSystemService);
		FileContent = new FileContentViewModel(fileSystemService);
		FileSelection = new FileSelectionViewModel(fileSystemService);
		MethodAnalysis = new MethodAnalysisViewModel(roslynAnalyzer);

		// Conectar eventos
		WireUpEvents();
	}

	private void WireUpEvents()
	{
		// Quando um arquivo é selecionado no explorer
		FileExplorer.FileSelected += async (s, item) =>
		{
			await FileContent.LoadFileAsync(item);
		};

		// Quando rootItems mudam, atualizar selection
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.RootItems))
			{
				FileSelection.SetRootItems(FileExplorer.RootItems);
			}
		};

		// Sincronizar estados de loading
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.IsLoading))
				UpdateLoadingState();
		};

		FileContent.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileContent.IsLoading))
				UpdateLoadingState();
		};

		FileSelection.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileSelection.IsLoading))
				UpdateLoadingState();
		};

		MethodAnalysis.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(MethodAnalysis.IsLoading))
				UpdateLoadingState();

			// NOVO: Sincronizar IsMethodAnalysisHidden quando IsVisible mudar
			if (e.PropertyName == nameof(MethodAnalysis.IsVisible))
			{
				IsMethodAnalysisHidden = !MethodAnalysis.IsVisible;
			}
		};

		// Sincronizar mensagens de status
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		MethodAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	private void UpdateLoadingState()
	{
		IsLoading = FileExplorer.IsLoading ||
					FileContent.IsLoading ||
					FileSelection.IsLoading ||
					MethodAnalysis.IsLoading;
	}

	// Métodos públicos para a View
	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	public async Task AnalyzeFileMethodsAsync(FileSystemItem? item)
	{
		if (item != null)
		{
			await MethodAnalysis.AnalyzeFileAsync(item);
		}
	}
}