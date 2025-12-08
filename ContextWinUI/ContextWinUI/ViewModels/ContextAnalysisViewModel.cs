using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class ContextAnalysisViewModel : ObservableObject
{
	private readonly RoslynAnalyzerService _roslynAnalyzer;
	private readonly FileSystemService _fileSystemService;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	// Item atualmente selecionado na árvore (clicado para visualizar)
	[ObservableProperty]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private int selectedCount;

	[ObservableProperty]
	private bool canGoBack;

	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	// Construtor (ajuste conforme seu código existente)
	public ContextAnalysisViewModel(RoslynAnalyzerService roslynAnalyzer, FileSystemService fileSystemService)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
	}

	// --- LÓGICA DO NOVO BOTÃO DE FOCO ---
	[RelayCommand]
	private void SyncFocus()
	{
		// Se não tiver nada selecionado para focar, não faz nada
		if (SelectedItem == null || ContextTreeItems == null) return;

		foreach (var item in ContextTreeItems)
		{
			// Executa a lógica recursiva:
			// 1. Se encontrar o SelectedItem, expande os pais.
			// 2. Se não estiver no caminho, fecha (Collapse).
			SyncFocusRecursive(item, SelectedItem);
		}
	}

	private bool SyncFocusRecursive(FileSystemItem currentItem, FileSystemItem targetItem)
	{
		// Caso Base: Encontramos o item alvo
		if (currentItem == targetItem)
		{
			// Garante que se for uma pasta, ela também se abra (opcional, pode remover se preferir)
			if (currentItem.IsDirectory || currentItem.Children.Count > 0)
			{
				currentItem.IsExpanded = true;
			}
			return true; // Retorna true indicando que este nó faz parte do caminho
		}

		bool keepExpanded = false;

		// Verifica os filhos
		foreach (var child in currentItem.Children)
		{
			// Se algum filho retornar true, significa que o alvo está lá embaixo
			if (SyncFocusRecursive(child, targetItem))
			{
				keepExpanded = true;
			}
		}

		// Aplica o estado: 
		// Se keepExpanded é true, expandimos esta pasta para mostrar o caminho.
		// Se é false, fechamos esta pasta para limpar a visão.
		currentItem.IsExpanded = keepExpanded;

		return keepExpanded;
	}
	// ------------------------------------

	[RelayCommand]
	private void ExpandAll()
	{
		if (ContextTreeItems == null) return;
		foreach (var item in ContextTreeItems) item.SetExpansionRecursively(true);
	}

	[RelayCommand]
	private void CollapseAll()
	{
		if (ContextTreeItems == null) return;
		foreach (var item in ContextTreeItems) item.SetExpansionRecursively(false);
	}

	[RelayCommand]
	private void Search(string query)
	{
		TreeSearchHelper.Search(ContextTreeItems, query);
	}

	// Método chamado pelo TreeView no CodeBehind
	public void SelectFileForPreview(FileSystemItem item)
	{
		SelectedItem = item; // Atualiza a propriedade usada pelo SyncFocus
		if (item.IsCodeFile)
		{
			FileSelectedForPreview?.Invoke(this, item);
		}
	}

	// --- Seus outros comandos (AnalyzeItemDepth, AnalyzeMethodFlow, etc) ---
	// Mantenha o restante da sua implementação aqui...

	[RelayCommand]
	private void Close() { /* Lógica existente */ }

	[RelayCommand]
	private void GoBack() { /* Lógica existente */ }

	[RelayCommand]
	private void ItemChecked() { /* Lógica existente */ }

	[RelayCommand]
	private void CopyContextToClipboard() { /* Lógica existente */ }

	public async Task AnalyzeContextAsync(List<FileSystemItem> files, string rootPath)
	{
		// Implementação existente...
	}
}