using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using Microsoft.UI.Dispatching; // Necessário para o DispatcherQueue
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.Features.ContextBuilder;

public partial class ContextTreeViewModel : ObservableObject
{
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IDependencyAnalysisOrchestrator _analysisOrchestrator;
	private readonly IProjectSessionManager _sessionManager;

	// Adicionado para suportar a atualização de UI da pesquisa assíncrona
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
	// Adicionado para cancelar buscas anteriores ao digitar rápido
	private CancellationTokenSource? _searchCts;

	public event EventHandler<FileSystemItem>? StructureUpdated;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> items = new();

	public ContextTreeViewModel(
		IFileSystemItemFactory itemFactory,
		IDependencyAnalysisOrchestrator analysisOrchestrator,
		IProjectSessionManager sessionManager)
	{
		_itemFactory = itemFactory;
		_analysisOrchestrator = analysisOrchestrator;
		_sessionManager = sessionManager;
	}

	public void SetItems(IEnumerable<FileSystemItem> newItems)
	{
		Items.Clear();
		foreach (var item in newItems) Items.Add(item);
	}

	public void Clear() => Items.Clear();

	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		if (item == null || !item.IsCodeFile) return;

		await _analysisOrchestrator.EnrichFileNodeAsync(item, _sessionManager.CurrentProjectPath);

		item.IsExpanded = true;

		StructureUpdated?.Invoke(this, item);
	}

	[RelayCommand]
	private async Task AnalyzeMethodFlowAsync(FileSystemItem item)
	{
		if (item == null || item.Type != FileSystemItemType.Method) return;

		await _analysisOrchestrator.EnrichMethodFlowAsync(item, _sessionManager.CurrentProjectPath);

		item.IsExpanded = true;

		StructureUpdated?.Invoke(this, item);
	}

	[RelayCommand]
	private async Task AnalyzeAllCheckedSubtypesAsync(FileSystemItem groupItem)
	{
		if (groupItem == null || groupItem.Children.Count == 0) return;

		var itemsToAnalyze = groupItem.Children
			.Where(c => c.IsChecked && !string.IsNullOrEmpty(c.MethodSignature))
			.ToList();

		foreach (var item in itemsToAnalyze)
		{
			await _analysisOrchestrator.EnrichMethodFlowAsync(item, _sessionManager.CurrentProjectPath);
		}

		groupItem.IsExpanded = true;
		StructureUpdated?.Invoke(this, groupItem);
	}

	[RelayCommand] private void ExpandAll() { foreach (var item in Items) item.SetExpansionRecursively(true); }
	[RelayCommand] private void CollapseAll() { foreach (var item in Items) item.SetExpansionRecursively(false); }
	[RelayCommand] private void SyncFocus() { }

	// --- CORREÇÃO AQUI ---
	[RelayCommand]
	private async Task SearchAsync(string query)
	{
		// Cancela a busca anterior se ainda estiver rodando
		if (_searchCts != null)
		{
			_searchCts.Cancel();
			_searchCts.Dispose();
		}

		_searchCts = new CancellationTokenSource();
		var token = _searchCts.Token;

		try
		{
			// Chama o novo método assíncrono otimizado
			await TreeSearchHelper.SearchAsync(Items, query, token, _dispatcherQueue);
		}
		catch (TaskCanceledException)
		{
			// Ignora cancelamento
		}
	}
}