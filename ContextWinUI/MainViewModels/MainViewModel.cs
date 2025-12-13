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

	// Podemos manter a referência privada como Interface também, por boa prática
	private readonly IFileSystemItemFactory _itemFactory;

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	[ObservableProperty]
	private bool isLoading;

	public MainViewModel()
	{
		// 1. INSTANCIAÇÃO CONCRETA (Acontece apenas aqui)
		// Criamos os objetos reais que fazem o trabalho pesado.
		IFileSystemItemFactory itemFactory = new FileSystemItemFactory();
		IFileSystemService fileSystemService = new FileSystemService(itemFactory);
		IRoslynAnalyzerService roslynAnalyzer = new RoslynAnalyzerService();

		// Guardamos referência local se precisarmos (opcional)
		_itemFactory = itemFactory;

		// 2. INJEÇÃO DE DEPENDÊNCIA
		// Passamos os objetos para os ViewModels, que os receberão como Interfaces.

		FileExplorer = new FileExplorerViewModel(fileSystemService, itemFactory);

		// FileContentViewModel também deve ser atualizado para receber IFileSystemService
		FileContent = new FileContentViewModel(fileSystemService);

		FileSelection = new FileSelectionViewModel(fileSystemService);

		ContextAnalysis = new ContextAnalysisViewModel(roslynAnalyzer, fileSystemService, itemFactory);

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