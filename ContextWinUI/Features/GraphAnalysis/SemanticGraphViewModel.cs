using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models; // Necessário para FileSystemItemType
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphAnalysis;

public partial class SemanticGraphViewModel : ObservableObject
{
	private readonly ISemanticIndexService _indexService;

	// --- NOVOS SERVIÇOS INJETADOS ---
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IFileSelectionService _selectionService;
	// --------------------------------

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string statusMessage = "Aguardando carregamento...";

	public ObservableCollection<GraphNodeViewModel> RootNodes { get; } = new();

	// Construtor atualizado com as novas dependências
	public SemanticGraphViewModel(
		ISemanticIndexService indexService,
		IFileSystemItemFactory itemFactory,
		IFileSelectionService selectionService)
	{
		_indexService = indexService;
		_itemFactory = itemFactory;
		_selectionService = selectionService;
	}

	[RelayCommand]
	public async Task LoadGraphAsync()
	{
		IsLoading = true;
		StatusMessage = "Carregando grafo semântico...";
		RootNodes.Clear();

		await Task.Run(() =>
		{
			var graph = _indexService.GetCurrentGraph();

			// Filtra apenas Classes e Interfaces para a raiz
			var rootSymbols = graph.Nodes.Values
				.Where(n => n.Type == SymbolType.Class || n.Type == SymbolType.Interface)
				.OrderBy(n => n.Name);

			foreach (var node in rootSymbols)
			{
				App.MainWindow.DispatcherQueue.TryEnqueue(() =>
				{
					RootNodes.Add(new GraphNodeViewModel(node, graph));
				});
			}
		});

		IsLoading = false;
		StatusMessage = $"{RootNodes.Count} tipos carregados.";
	}

	// --- NOVO COMANDO DE NAVEGAÇÃO ---
	[RelayCommand]
	public void NavigateToNode(GraphNodeViewModel? nodeVm)
	{
		if (nodeVm == null) return;

		var graph = _indexService.GetCurrentGraph();

		// Obtém o caminho do arquivo físico usando o ID armazenado no nó
		string filePath = graph.GetFilePath(nodeVm.InternalNode.FileId);

		// Verifica se o caminho é válido e existe
		if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
		{
			// Cria um FileSystemItem temporário para representar esse arquivo
			var fileItem = _itemFactory.CreateWrapper(filePath, FileSystemItemType.File);

			// Avisa o sistema global que este arquivo foi selecionado
			// Isso fará o FileContentView atualizar automaticamente
			_selectionService.SetSelection(fileItem);
		}
	}
}