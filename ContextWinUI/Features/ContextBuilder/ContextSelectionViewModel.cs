using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ContextWinUI.Core.Contracts;

namespace ContextWinUI.ViewModels;

public partial class ContextSelectionViewModel : ObservableObject
{
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly ISelectionIOService _ioService;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> selectedItemsList = new();

	public ContextSelectionViewModel(
		IFileSystemItemFactory itemFactory,
		ISelectionIOService ioService)
	{
		_itemFactory = itemFactory;
		_ioService = ioService;
	}


	public void AddItem(FileSystemItem item)
	{
		if (!SelectedItemsList.Any(x => x.FullPath == item.FullPath))
		{
			SelectedItemsList.Add(item);
		}
	}

	public void RemoveItem(FileSystemItem item)
	{
		var target = SelectedItemsList.FirstOrDefault(x => x.FullPath == item.FullPath);
		if (target != null) SelectedItemsList.Remove(target);
	}

	[RelayCommand]
	public void Clear()
	{
		SelectedItemsList.Clear();
	}


	[RelayCommand]
	private async Task SaveSelectionListAsync()
	{
		if (!SelectedItemsList.Any()) return;

		var paths = SelectedItemsList.Select(x => x.FullPath).ToList();
		await _ioService.SaveSelectionAsync(paths);
	}

	[RelayCommand]
	private async Task LoadSelectionListAsync()
	{
		var paths = await _ioService.LoadSelectionAsync();
		ProcessPaths(paths);
	}

	[RelayCommand]
	private async Task ImportFromTextAsync(XamlRoot xamlRoot)
	{
		var textBox = new TextBox
		{
			AcceptsReturn = true,
			Height = 200,
			PlaceholderText = "Cole aqui uma lista de caminhos de arquivos (um por linha)..."
		};

		var dialog = new ContentDialog
		{
			Title = "Importar Lista de Arquivos",
			Content = textBox,
			PrimaryButtonText = "Importar",
			CloseButtonText = "Cancelar",
			DefaultButton = ContentDialogButton.Primary,
			XamlRoot = xamlRoot
		};

		var result = await dialog.ShowAsync();

		if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
		{
			var paths = textBox.Text
				.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(p => p.Trim().Trim('"').Trim('\'')) // Remove aspas e espaços
				.Where(p => !string.IsNullOrWhiteSpace(p));

			ProcessPaths(paths);
		}
	}

	private void ProcessPaths(IEnumerable<string> paths)
	{
		if (paths == null) return;

		foreach (var path in paths)
		{
			// Valida existência física
			if (System.IO.File.Exists(path))
			{
				// Factory garante estado compartilhado (tags) se já estiver na memória
				var item = _itemFactory.CreateWrapper(path, FileSystemItemType.File, "\uE943");

				// Marca checkbox visualmente (se existir no Explorer)
				item.IsChecked = true;

				AddItem(item);
			}
		}
	}
}