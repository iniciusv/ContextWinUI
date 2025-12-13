using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
	// Dependência apenas da Interface do Manager
	private readonly IProjectSessionManager _sessionManager;

	// Item com foco visual (azul)
	private FileSystemItem? _selectedItem;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private string currentPath = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<FileSystemItem>? FileSelected;
	public event EventHandler<string>? StatusChanged;

	public FileExplorerViewModel(IProjectSessionManager sessionManager)
	{
		_sessionManager = sessionManager;

		// INSCRIÇÃO: Quando o Manager terminar de carregar tudo (arquivos + cache + tags)
		// ele vai disparar este evento.
		_sessionManager.ProjectLoaded += OnProjectLoaded;
	}

	/// <summary>
	/// Reage ao evento de projeto carregado pelo Manager.
	/// </summary>
	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		// Atualiza a UI com a árvore já montada e enriquecida (tags, etc)
		RootItems = e.RootItems;
		CurrentPath = e.RootPath;

		// (Opcional) Poderia expandir automaticamente o primeiro nível aqui se quisesse
	}

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		try
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker();

			// Configuração para WinUI 3 (Necessário obter o Handle da Janela)
			if (App.MainWindow != null)
			{
				var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
				WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
			}

			folderPicker.FileTypeFilter.Add("*");

			var folder = await folderPicker.PickSingleFolderAsync();

			if (folder != null)
			{
				// DELEGAÇÃO: Não carregamos aqui. Pedimos ao Manager.
				// O Manager vai limpar a memória, ler o disco, ler o JSON e chamar OnProjectLoaded.
				await _sessionManager.OpenProjectAsync(folder.Path);
			}
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao selecionar pasta: {ex.Message}");
		}
	}

	// --- COMANDOS VISUAIS (Busca, Expansão, Foco) ---
	// Estes comandos operam sobre a coleção RootItems já carregada na memória.

	[RelayCommand]
	private void Search(string query)
	{
		TreeSearchHelper.Search(RootItems, query);
	}

	[RelayCommand]
	private void ExpandAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems) item.SetExpansionRecursively(true);
	}

	[RelayCommand]
	private void CollapseAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems) item.SetExpansionRecursively(false);
	}

	// Comando "SyncFocus" (Botão de Alvo): Foca no item selecionado e fecha os outros ramos
	[RelayCommand]
	private void SyncFocus()
	{
		if (_selectedItem == null || RootItems == null) return;

		foreach (var item in RootItems)
		{
			SyncFocusRecursive(item, _selectedItem);
		}
	}

	private bool SyncFocusRecursive(FileSystemItem currentItem, FileSystemItem targetItem)
	{
		// Graças ao Flyweight, podemos comparar FullPath com segurança
		if (currentItem.FullPath == targetItem.FullPath)
		{
			if (currentItem.IsDirectory) currentItem.IsExpanded = true;
			return true;
		}

		bool keepExpanded = false;
		foreach (var child in currentItem.Children)
		{
			if (SyncFocusRecursive(child, targetItem))
			{
				keepExpanded = true;
			}
		}

		currentItem.IsExpanded = keepExpanded;
		return keepExpanded;
	}

	// Comando interno usado pelo evento "Expanding" do TreeView para lazy loading (se houvesse)
	// ou apenas para atualizar estado visual
	[RelayCommand]
	private void ExpandItem(FileSystemItem item)
	{
		item.IsExpanded = true;
	}

	// Chamado pela View quando o usuário clica num item
	public void SelectFile(FileSystemItem item)
	{
		_selectedItem = item;

		if (item.IsCodeFile)
		{
			FileSelected?.Invoke(this, item);
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}