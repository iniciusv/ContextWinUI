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

	// Serviço Factory Centralizado
	private readonly FileSystemItemFactory _itemFactory;

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		// Instancia a Factory Singleton para este contexto
		_itemFactory = new FileSystemItemFactory();

		// Passa a factory para o FileSystemService
		var fileSystemService = new FileSystemService(_itemFactory);
		var roslynAnalyzer = new RoslynAnalyzerService();

		FileExplorer = new FileExplorerViewModel(fileSystemService, _itemFactory);
		FileContent = new FileContentViewModel(fileSystemService);
		// Note que FileSelection usa a coleção do Explorer, não precisa da factory direta
		FileSelection = new FileSelectionViewModel(fileSystemService);
		ContextAnalysis = new ContextAnalysisViewModel(roslynAnalyzer, fileSystemService, _itemFactory);

		WireUpEvents();
	}

	private void WireUpEvents()
	{
		FileExplorer.FileSelected += async (s, item) => await FileContent.LoadFileAsync(item);

		FileExplorer.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(FileExplorer.RootItems))
				FileSelection.SetRootItems(FileExplorer.RootItems);
		};

		void UpdateLoading() => IsLoading = FileExplorer.IsLoading || FileContent.IsLoading || FileSelection.IsLoading || ContextAnalysis.IsLoading;

		FileExplorer.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileContent.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		FileSelection.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };
		ContextAnalysis.PropertyChanged += (s, e) => { if (e.PropertyName == "IsLoading") UpdateLoading(); };

		FileExplorer.StatusChanged += (s, msg) => StatusMessage = msg;
		FileContent.StatusChanged += (s, msg) => StatusMessage = msg;
		FileSelection.StatusChanged += (s, msg) => StatusMessage = msg;
		ContextAnalysis.StatusChanged += (s, msg) => StatusMessage = msg;
	}

	public void OnFileSelected(FileSystemItem item)
	{
		FileExplorer.SelectFile(item);
	}

	public async Task AnalyzeContextCommandAsync()
	{
		var selectedFiles = FileSelection.GetCheckedFiles().ToList();

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