using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;
public partial class FileExplorerViewModel : ObservableObject
{
    public readonly IProjectSessionManager _sessionManager;
    private readonly IFileSystemService _fileSystemService;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private CancellationTokenSource? _searchCts;
    private FileSystemItem? _selectedItem;
    private readonly IFileSystemItemFactory _itemFactory;
    public ITagManagementUiService TagService { get; }
    public ContextSelectionViewModel SelectionViewModel { get; }

    [ObservableProperty]
    private ObservableCollection<FileSystemItem> rootItems = new();
    [ObservableProperty]
    private bool isLoading;
    [ObservableProperty]
    private string currentPath = "Nenhum projeto carregado";
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<FileSystemItem>? FileSelected;
	[ObservableProperty]
	private ObservableCollection<string> allProjectTags = new();

	public FileExplorerViewModel(IProjectSessionManager sessionManager, ITagManagementUiService tagService, IFileSystemService fileSystemService, ContextSelectionViewModel sharedSelectionViewModel, IFileSystemItemFactory itemFactory)
    {
        _sessionManager = sessionManager;
        TagService = tagService;
        _fileSystemService = fileSystemService;
        SelectionViewModel = sharedSelectionViewModel;
        _sessionManager.ProjectLoaded += OnProjectLoaded;
        _sessionManager.StatusChanged += (s, msg) => OnStatusChanged(msg);
    }

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		RootItems = e.RootItems;
		CurrentPath = e.RootPath;
		IsLoading = false;

		SelectionViewModel.Clear();
		foreach (var item in RootItems)
		{
			RegisterItemEvents(item);
			if (item.IsChecked)
				SelectionViewModel.AddItem(item);
		}

		// ATUALIZAÇÃO CRÍTICA: Carrega as tags assim que o projeto abre
		RefreshAllTags();

		OnStatusChanged("Projeto carregado com sucesso.");
	}

	// Chame este método também sempre que adicionar uma nova tag via TagService
	public void RefreshAllTags()
	{
		if (_itemFactory != null)
		{
			var uniqueTags = _itemFactory.GetAllStates()
				.SelectMany(s => s.Tags)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(t => t)
				.ToList();

			AllProjectTags.Clear();
			foreach (var tag in uniqueTags)
			{
				AllProjectTags.Add(tag);
			}

			// Debug para verificar se carregou
			System.Diagnostics.Debug.WriteLine($"Tags carregadas: {AllProjectTags.Count}");
		}
	}


	// EM: FileExplorerViewModel.cs

	private void RegisterItemEvents(FileSystemItem item)
	{
		// Eventos existentes de PropertyChanged
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		if (item.SharedState != null)
		{
			item.SharedState.Tags.CollectionChanged -= OnItemTagsChanged;
			item.SharedState.Tags.CollectionChanged += OnItemTagsChanged;
		}

		if (item.Children != null)
		{
			foreach (var child in item.Children)
			{
				RegisterItemEvents(child);
			}
		}
	}

	private void OnItemTagsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		// Se novos itens foram adicionados à lista de tags de um arquivo
		if (e.NewItems != null)
		{
			foreach (string newTag in e.NewItems)
			{
				// Garante que a operação ocorra na Thread de UI
				_dispatcherQueue.TryEnqueue(() =>
				{
					// Se a tag ainda não existe na lista mestra do autocompletar, adiciona
					if (!AllProjectTags.Contains(newTag))
					{
						// Opcional: Manter ordenado se desejar
						AllProjectTags.Add(newTag);
					}
				});
			}
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileSystemItem.IsChecked) && sender is FileSystemItem item)
        {
            if (item.IsChecked)
                SelectionViewModel.AddItem(item);
            else
                SelectionViewModel.RemoveItem(item);
        }
    }

    public void SelectFile(FileSystemItem item)
    {
        _selectedItem = item;
        FileSelected?.Invoke(this, item);
    }

    [RelayCommand]
    private void SyncFocus()
    {
        if (RootItems == null || !RootItems.Any())
            return;
        if (_selectedItem == null)
        {
            CollapseAll();
            return;
        }

        foreach (var item in RootItems)
        {
            DetermineExpansionState(item, _selectedItem);
        }
    }

    private bool DetermineExpansionState(FileSystemItem current, FileSystemItem target)
    {
        if (current == target)
        {
            return true;
        }

        bool containsTarget = false;
        if (current.Children != null && current.Children.Any())
        {
            foreach (var child in current.Children)
            {
                if (DetermineExpansionState(child, target))
                {
                    containsTarget = true;
                }
            }
        }

        if (current.IsDirectory)
        {
            current.IsExpanded = containsTarget;
        }

        return containsTarget;
    }

    [RelayCommand]
    private void ExpandAll()
    {
        if (RootItems == null)
            return;
        SetExpansionRecursive(RootItems, true);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        if (RootItems == null)
            return;
        SetExpansionRecursive(RootItems, false);
    }

    private void SetExpansionRecursive(IEnumerable<FileSystemItem> items, bool isExpanded)
    {
        foreach (var item in items)
        {
            if (isExpanded && item.SharedState.IsIgnored)
            {
                continue;
            }

            // ------------------------
            if (item.IsDirectory)
            {
                item.IsExpanded = isExpanded;
                if (item.Children != null && item.Children.Any())
                {
                    SetExpansionRecursive(item.Children, isExpanded);
                }
            }
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        if (IsLoading)
            return;
        try
        {
            IsLoading = true;
            await _sessionManager.LoadProjectAsync();
        }
        catch (Exception ex)
        {
            OnStatusChanged($"Erro: {ex.Message}");
        }

        if (!IsLoading && string.IsNullOrEmpty(CurrentPath))
            IsLoading = false;
    }

	[RelayCommand]
	private async Task SearchAsync(string query)
	{
		if (_searchCts != null)
		{
			_searchCts.Cancel();
			_searchCts.Dispose();
		}

		_searchCts = new CancellationTokenSource();
		var token = _searchCts.Token;

		try
		{
			// AUMENTADO DE 300 PARA 500ms para dar mais tempo de respiro em projetos grandes
			await Task.Delay(500, token);

			if (!token.IsCancellationRequested && RootItems != null)
			{
				// Agora o TreeSearchHelper gerencia o Task.Run internamente
				await TreeSearchHelper.SearchAsync(RootItems, query, token, _dispatcherQueue);
			}
		}
		catch (TaskCanceledException)
		{
			// Busca cancelada, normal ao digitar rápido
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro busca: {ex.Message}");
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
    [RelayCommand]
    private void ExpandItem(FileSystemItem item)
    {
        if (item != null)
        {
            item.IsExpanded = true;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (RootItems == null)
            return;
        SetCheckedRecursive(RootItems, true);
    }

    [RelayCommand]
    private void UnselectAll()
    {
        if (RootItems == null)
            return;
        SetCheckedRecursive(RootItems, false);
    }

    // Helper recursivo para marcar/desmarcar
    private void SetCheckedRecursive(IEnumerable<FileSystemItem> items, bool isChecked)
    {
        foreach (var item in items)
        {
            if (item.IsCodeFile)
            {
                item.IsChecked = isChecked;
            }

            if (item.Children != null && item.Children.Any())
            {
                SetCheckedRecursive(item.Children, isChecked);
            }
        }
    }

	// ARQUIVO: FileExplorerViewModel.cs

	[RelayCommand]
	private async Task SubmitSearch(string query)
	{
		if (RootItems == null) return;

		IsLoading = true;
		try
		{
			// Chama o Helper e "desconstrói" a tupla retornada nas variáveis
			var (success, count, tag, isSelection) = await TreeSearchHelper.TryExecuteCommandAsync(RootItems, query);

			if (success)
			{
				// O comando foi reconhecido e executado
				string action = isSelection ? "selecionados" : "desselecionados";

				if (count > 0)
					OnStatusChanged($"{count} itens com a tag '{tag}' foram {action}.");
				else
					OnStatusChanged($"Nenhum item encontrado com a tag '{tag}' para ser alterado.");
			}
			else
			{
				// Não era um comando de tag (ex: busca normal ou texto vazio)
				// Aqui você pode colocar lógica futura ou apenas ignorar
			}
		}
		finally
		{
			IsLoading = false;
		}
	}


	[RelayCommand]
	private async Task CreateNewItemAsync(object[] args)
	{
		// 1. Validação dos argumentos vindos do CommandParameter
		if (args.Length < 3 || args[0] is not FileSystemItem targetItem || args[2] is not XamlRoot xamlRoot)
			return;

		bool isFolder = (bool)args[1];

		// 2. Determinar a pasta pai correta
		FileSystemItem? parentFolder = targetItem.IsDirectory ? targetItem : GetParentItem(targetItem);

		// Fallback: se não achou pai mas o target é diretório, usa ele
		if (parentFolder == null && targetItem.IsDirectory)
			parentFolder = targetItem;

		if (parentFolder == null)
			return;

		// 3. Preparar e exibir o diálogo de entrada
		var inputTextBox = new TextBox
		{
			PlaceholderText = isFolder ? "Nome da Pasta" : "Nome do Arquivo.txt"
		};

		var dialog = new ContentDialog
		{
			Title = isFolder ? "Nova Pasta" : "Novo Arquivo",
			Content = inputTextBox,
			PrimaryButtonText = "Criar",
			CloseButtonText = "Cancelar",
			DefaultButton = ContentDialogButton.Primary,
			XamlRoot = xamlRoot
		};

		var result = await dialog.ShowAsync();

		if (result != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(inputTextBox.Text))
			return;

		string newName = inputTextBox.Text.Trim();
		string newFullPath = Path.Combine(parentFolder.SharedState.FullPath, newName);

		try
		{
			// 4. Criar fisicamente no disco
			if (isFolder)
				await _fileSystemService.CreateDirectoryAsync(newFullPath);
			else
				await _fileSystemService.CreateFileAsync(newFullPath);

			// 5. Criar o wrapper visual (ViewModel)
			var newItem = _itemFactory.CreateWrapper(newFullPath, isFolder ? FileSystemItemType.Directory : FileSystemItemType.File);

			// ---------------------------------------------------------
			// CORREÇÃO: Registrar eventos para o novo item imediatamente
			// Isso conecta os listeners de Tags e Checked
			// ---------------------------------------------------------
			RegisterItemEvents(newItem);

			// 6. Adicionar à árvore visual e expandir o pai
			parentFolder.Children.Add(newItem);
			parentFolder.IsExpanded = true;

			OnStatusChanged($"Criado: {newName}");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao criar: {ex.Message}");
		}
	}

	// Comando para Deletar
	[RelayCommand]
    private async Task DeleteItemAsync(object[] args)
    {
        // Args: [0] = FileSystemItem, [1] = XamlRoot
        if (args.Length < 2 || args[0] is not FileSystemItem itemToDelete || args[1] is not XamlRoot xamlRoot)
            return;
        var dialog = new ContentDialog
        {
            Title = "Confirmar Exclusão",
            Content = $"Tem certeza que deseja excluir '{itemToDelete.Name}' permanentemente?",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;
        try
        {
            // 1. Deletar do disco
            await _fileSystemService.DeleteItemAsync(itemToDelete.SharedState.FullPath);
            // 2. Remover da UI
            var parent = GetParentItem(itemToDelete);
            if (parent != null)
            {
                parent.Children.Remove(itemToDelete);
            }
            else if (RootItems.Contains(itemToDelete))
            {
                RootItems.Remove(itemToDelete);
            }

            OnStatusChanged($"Excluído: {itemToDelete.Name}");
        }
        catch (Exception ex)
        {
            OnStatusChanged($"Erro ao excluir: {ex.Message}");
        }
    }

    // Método auxiliar para encontrar o pai na árvore visual
    // Como FileSystemItem não tem propriedade "Parent", precisamos buscar recursivamente
    private FileSystemItem? GetParentItem(FileSystemItem child)
    {
        return FindParentRecursive(RootItems, child);
    }

    private FileSystemItem? FindParentRecursive(IEnumerable<FileSystemItem> scope, FileSystemItem target)
    {
        foreach (var item in scope)
        {
            if (item.Children.Contains(target))
                return item;
            if (item.Children.Any())
            {
                var found = FindParentRecursive(item.Children, target);
                if (found != null)
                    return found;
            }
        }

        return null;
    }



	private static int ModifySelectionByTagRecursive(IEnumerable<FileSystemItem> items, string tag, bool shouldSelect)
	{
		int count = 0;
		foreach (var item in items)
		{
			bool hasTag = item.SharedState.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));

			if (hasTag && item.IsCodeFile)
			{
				if (item.IsChecked != shouldSelect)
				{
					item.IsChecked = shouldSelect;
					count++;
				}
			}

			if (item.Children != null && item.Children.Any())
			{
				count += ModifySelectionByTagRecursive(item.Children, tag, shouldSelect);
			}
		}
		return count;
	}
}