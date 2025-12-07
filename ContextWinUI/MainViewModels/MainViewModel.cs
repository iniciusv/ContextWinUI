using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	public FileExplorerViewModel FileExplorer { get; }
	public FileContentViewModel FileContent { get; }
	public FileSelectionViewModel FileSelection { get; }
	public ContextAnalysisViewModel ContextAnalysis { get; }

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		var fileSystemService = new FileSystemService();
		var roslynAnalyzer = new RoslynAnalyzerService();

		FileExplorer = new FileExplorerViewModel(fileSystemService);
		FileContent = new FileContentViewModel(fileSystemService);
		FileSelection = new FileSelectionViewModel(fileSystemService);
		ContextAnalysis = new ContextAnalysisViewModel(roslynAnalyzer, fileSystemService);

		WireUpEvents();
	}

	private void WireUpEvents()
	{
		// Conexão entre Explorer e Visualizador
		FileExplorer.FileSelected += async (s, item) => await FileContent.LoadFileAsync(item);

		// Conexão entre Explorer e Seleção em Massa
		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.RootItems))
				FileSelection.SetRootItems(FileExplorer.RootItems);
		};

		// Consolidação do Loading
		void UpdateLoading() => IsLoading = FileExplorer.IsLoading || FileContent.IsLoading || FileSelection.IsLoading || ContextAnalysis.IsLoading;

		FileExplorer.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileContent.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileSelection.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		ContextAnalysis.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };

		// Consolidação das Mensagens de Status
		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	// Chamado pela View quando um arquivo é clicado
	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	// Comando do botão "Analisar Contexto"
	public async Task AnalyzeContextCommandAsync()
	{
		// Pega arquivos marcados com checkbox
		var selectedFiles = FileSelection.GetCheckedFiles().ToList();

		// Se nenhum marcado, tenta usar o arquivo aberto no editor
		if (!selectedFiles.Any() && FileContent.SelectedItem?.IsCodeFile == true)
		{
			selectedFiles.Add(FileContent.SelectedItem);
		}

		if (!selectedFiles.Any())
		{
			StatusMessage = "Selecione arquivos para analisar.";
			return;
		}

		await ContextAnalysis.AnalyzeContextAsync(selectedFiles, FileExplorer.CurrentPath);
	}
}