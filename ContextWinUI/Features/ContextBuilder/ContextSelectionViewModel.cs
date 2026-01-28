using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.ContextBuilder;


public partial class ContextSelectionViewModel : ObservableObject
{
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly ISelectionIOService _ioService;
	private readonly IDependencyAnalysisOrchestrator _orchestrator;
	private readonly IProjectSessionManager _sessionManager;
    private readonly IBlockSelectionManager _blockManager; // NEW

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> selectedItemsList = new();

	public ObservableCollection<FileSystemItem> DisplayedItems { get; } = new();

	[ObservableProperty]
	private bool hideSubItems;

	[ObservableProperty]
	private bool isCopying;

	[ObservableProperty]
	private int selectedFilesCount;

	public ContextSelectionViewModel(
				IFileSystemItemFactory itemFactory,
				ISelectionIOService ioService,
				IDependencyAnalysisOrchestrator orchestrator,
				IProjectSessionManager sessionManager,
                IBlockSelectionManager blockManager) // NEW
	{
		_itemFactory = itemFactory;
		_ioService = ioService;
		_orchestrator = orchestrator;
		_sessionManager = sessionManager;
        _blockManager = blockManager;

		// Quando a lista original mudar, atualizamos a lista de exibição
		SelectedItemsList.CollectionChanged += (s, e) =>
		{
			SelectedFilesCount = SelectedItemsList.Count;
			CopySelectedFilesCommand.NotifyCanExecuteChanged();
			UpdateDisplayedItems(); // <--- ATUALIZA A VISUALIZAÇÃO
		};

		// Inicializa a lista de exibição
		UpdateDisplayedItems();
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

	public Visibility IsListEmptyVisibility =>	SelectedItemsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;


	[RelayCommand]
	public void Clear()
	{
		var itemsToClear = SelectedItemsList.ToList();

		foreach (var item in itemsToClear)
		{
			item.IsChecked = false;
		}

		SelectedItemsList.Clear();
        _blockManager.ClearAllSelections(); // NEW
	}

	public IEnumerable<FileSystemItem> GetCheckedFiles()
	{
		return SelectedItemsList.ToList();
	}

    public List<string> GetCurrentSelectionSnapshot()
    {
        var paths = SelectedItemsList.Select(x => x.FullPath).ToList();
        
        // Append selected blocks if manager is available
        if (_blockManager != null)
        {
            var allSelectedBlocks = _blockManager.GetAllSelectedBlocks();
            foreach (var kvp in allSelectedBlocks)
            {
                if (paths.Contains(kvp.Key)) // Only if file is still selected
                {
                    foreach (var blockId in kvp.Value)
                    {
                        paths.Add($"{kvp.Key}::{blockId}");
                    }
                }
            }
        }
        return paths;
    }

	[RelayCommand]
	private async Task SaveSelectionListAsync()
	{
		if (!SelectedItemsList.Any()) return;

		var paths = SelectedItemsList.Select(x => x.FullPath).ToList();
        
        // Append selected blocks
        var allSelectedBlocks = _blockManager.GetAllSelectedBlocks();
        foreach (var kvp in allSelectedBlocks)
        {
            if (paths.Contains(kvp.Key)) // Only if file is still selected
            {
                foreach (var blockId in kvp.Value)
                {
                    paths.Add($"{kvp.Key}::{blockId}");
                }
            }
        }

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

	public void ProcessPaths(IEnumerable<string> paths)
	{
		if (paths == null) return;

        // Separate blocks from files
        var filePaths = new HashSet<string>();
        var blockSelections = new List<(string FilePath, string BlockId)>();

        foreach (var p in paths)
        {
            if (p.Contains("::"))
            {
                var parts = p.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string filePath = parts[0];
                    string blockId = parts[1];
                    blockSelections.Add((filePath, blockId));
                    // Also ensure file is added
                    if (!filePaths.Contains(filePath)) filePaths.Add(filePath);
                }
            }
            else
            {
                if (!filePaths.Contains(p)) filePaths.Add(p);
            }
        }

		// Obter o caminho raiz do projeto atual
		string? projectRoot = _sessionManager.CurrentProjectPath;

        // Process files first
		foreach (var path in filePaths)
		{
			string? resolvedPath = null;

			// Caso 1: Caminho absoluto que existe
			if (System.IO.File.Exists(path))
			{
				resolvedPath = path;
			}
			// Caso 2: Caminho relativo ao projeto
			else if (!string.IsNullOrEmpty(projectRoot))
			{
				// Tentar como caminho relativo ao projeto
				string relativePath = System.IO.Path.Combine(projectRoot, path);
				if (System.IO.File.Exists(relativePath))
				{
					resolvedPath = relativePath;
				}
				// Caso 3: Apenas nome do arquivo - buscar recursivamente no projeto
				else
				{
					string fileName = path;

					// Se não tem extensão, adicionar extensões comuns para buscar
					string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
					bool hasExtension = !string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName));

					// Buscar arquivos no projeto
					try
					{
						var allFiles = Directory.EnumerateFiles(projectRoot, "*.*", SearchOption.AllDirectories);

						foreach (var file in allFiles)
						{
							string currentFileName = System.IO.Path.GetFileName(file);
							string currentFileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(file);

							// Comparar com ou sem extensão
							if (hasExtension)
							{
								// Com extensão: comparar nomes completos
								if (string.Equals(currentFileName, fileName, StringComparison.OrdinalIgnoreCase))
								{
									resolvedPath = file;
									break;
								}
							}
							else
							{
								// Sem extensão: comparar apenas o nome base
								if (string.Equals(currentFileNameWithoutExt, fileName, StringComparison.OrdinalIgnoreCase))
								{
									resolvedPath = file;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Erro ao buscar arquivo {fileName}: {ex.Message}");
					}
				}
			}

			if (!string.IsNullOrEmpty(resolvedPath))
			{
				var item = _itemFactory.CreateWrapper(resolvedPath, FileSystemItemType.File, "\uE943");
				item.IsChecked = true;
				AddItem(item);
			}
			else
			{
				// Log para debug - arquivo não encontrado
				System.Diagnostics.Debug.WriteLine($"Arquivo não encontrado: {path}");
			}
		}

        // Now populate blocks
        foreach (var (filePath, blockId) in blockSelections)
        {
             // We need to resolveFilePath again if it was relative? 
             // Ideally we reused the resolved map, but simply trusting exact match or filename match if simplified
             // For robustness let's rely on Manager having the path matching what the file system has.
             // If we resolved 'foo.cs' to 'c:\...\foo.cs', we need to register 'c:\...\foo.cs'
             
             // Simplification: We only adding to manager if file exists on disk as checked above.
             // We can assume user uses absolute paths usually or we resolved them.
             // To be safe, re-resolve or use the logic above.
             
             // Let's just try to find the item in SelectedItemsList to get the resolved full path
             var existingItem = SelectedItemsList.FirstOrDefault(i => i.FullPath.EndsWith(Path.GetFileName(filePath)));
             if (existingItem != null)
             {
                 _blockManager.SelectBlock(existingItem.FullPath, blockId);
             }
        }
	}

	[RelayCommand]
	private async Task CopySelectedFilesAsync()
	{
		if (!SelectedItemsList.Any()) return;

		IsCopying = true;
		try
		{
			string text = await _orchestrator.BuildContextStringAsync(SelectedItemsList, _sessionManager);

			var dp = new DataPackage();
			dp.SetText(text);
			Clipboard.SetContent(dp);
		}
		catch (Exception)
		{
			// Tratar erro ou notificar via serviço de mensageria se necessário
		}
		finally
		{
			IsCopying = false;
		}
	}
	private void CollectCheckedRecursive(FileSystemItem item, List<FileSystemItem> result)
	{
		if (item.IsChecked)
		{
			result.Add(item);
		}

		foreach (var child in item.Children)
		{
			CollectCheckedRecursive(child, result);
		}
	}
	partial void OnHideSubItemsChanged(bool value)
	{
		UpdateDisplayedItems();
	}

	private void UpdateDisplayedItems()
	{
		DisplayedItems.Clear();

		IEnumerable<FileSystemItem> items = SelectedItemsList;

		if (HideSubItems)
		{
			// Filtra mantendo apenas Arquivos e Diretórios (remove métodos, classes, etc.)
			items = items.Where(x => x.Type == FileSystemItemType.File || x.Type == FileSystemItemType.Directory);
		}

		foreach (var item in items)
		{
			DisplayedItems.Add(item);
		}
	}
}