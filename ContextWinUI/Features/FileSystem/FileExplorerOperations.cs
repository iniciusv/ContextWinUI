using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels.Helpers;

public class FileExplorerOperations
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly Action<string> _statusCallback;
	private readonly Action<FileSystemItem> _onItemCreatedCallback;

	public FileExplorerOperations(
		IFileSystemService fileSystemService,
		IFileSystemItemFactory itemFactory,
		Action<string> statusCallback,
		Action<FileSystemItem> onItemCreatedCallback)
	{
		_fileSystemService = fileSystemService;
		_itemFactory = itemFactory;
		_statusCallback = statusCallback;
		_onItemCreatedCallback = onItemCreatedCallback;
	}

	public async Task CreateNewItemAsync(FileSystemItem targetItem, bool isFolder, XamlRoot xamlRoot)
	{
		FileSystemItem? parentFolder = targetItem.IsDirectory ? targetItem : GetParentItem(targetItem); // Precisará refatorar GetParent para ser acessível ou passado
																										// Nota: A lógica de GetParentItem depende da Raiz. Vamos simplificar assumindo que targetItem é o contexto.

		if (parentFolder == null && targetItem.IsDirectory) parentFolder = targetItem;
		if (parentFolder == null) return;

		var inputTextBox = new TextBox { PlaceholderText = isFolder ? "Nome da Pasta" : "Nome do Arquivo.txt" };
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
		if (result != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(inputTextBox.Text)) return;

		string newName = inputTextBox.Text.Trim();
		string newFullPath = Path.Combine(parentFolder.SharedState.FullPath, newName);

		try
		{
			if (isFolder) await _fileSystemService.CreateDirectoryAsync(newFullPath);
			else await _fileSystemService.CreateFileAsync(newFullPath);

			var newItem = _itemFactory.CreateWrapper(newFullPath, isFolder ? FileSystemItemType.Directory : FileSystemItemType.File);

			// Callback para registrar eventos no ViewModel principal
			_onItemCreatedCallback(newItem);

			parentFolder.Children.Add(newItem);
			parentFolder.IsExpanded = true;
			_statusCallback($"Criado: {newName}");
		}
		catch (Exception ex)
		{
			_statusCallback($"Erro ao criar: {ex.Message}");
		}
	}

	public async Task DeleteItemAsync(FileSystemItem itemToDelete, ObservableCollection<FileSystemItem> rootItems, XamlRoot xamlRoot)
	{
		var dialog = new ContentDialog
		{
			Title = "Confirmar Exclusão",
			Content = $"Tem certeza que deseja excluir '{itemToDelete.Name}' permanentemente?",
			PrimaryButtonText = "Excluir",
			CloseButtonText = "Cancelar",
			DefaultButton = ContentDialogButton.Close,
			XamlRoot = xamlRoot
		};

		if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

		try
		{
			await _fileSystemService.DeleteItemAsync(itemToDelete.SharedState.FullPath);

			// Lógica de remoção visual
			var parent = FindParentRecursive(rootItems, itemToDelete);
			if (parent != null)
			{
				parent.Children.Remove(itemToDelete);
			}
			else if (rootItems.Contains(itemToDelete))
			{
				rootItems.Remove(itemToDelete);
			}

			itemToDelete.Dispose(); // Importante para evitar memory leaks de eventos
			_statusCallback($"Excluído: {itemToDelete.Name}");
		}
		catch (Exception ex)
		{
			_statusCallback($"Erro ao excluir: {ex.Message}");
		}
	}

	// Helper utilitário movido para cá
	public static FileSystemItem? FindParentRecursive(IEnumerable<FileSystemItem> scope, FileSystemItem target)
	{
		foreach (var item in scope)
		{
			if (item.Children.Contains(target)) return item;
			if (item.Children.Count > 0)
			{
				var found = FindParentRecursive(item.Children, target);
				if (found != null) return found;
			}
		}
		return null;
	}

	private FileSystemItem? GetParentItem(FileSystemItem child) => null; // Implementado via FindParentRecursive externo ou passando a Raiz
}