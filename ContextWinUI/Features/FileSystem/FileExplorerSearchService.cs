using ContextWinUI.Core.Contracts;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;


namespace ContextWinUI.Features.FileSystem;

public class FileExplorerSearchService : IFileExplorerSearchService
{
	private CancellationTokenSource? _searchCts;
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	public async Task PerformSearchAsync(IEnumerable<FileSystemItem> items, string query, Action<string> onError)
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
			await Task.Delay(300, token); // Debounce
			if (!token.IsCancellationRequested && items != null)
			{
				await TreeSearchHelper.SearchAsync(items, query, token, _dispatcherQueue);
			}
		}
		catch (TaskCanceledException) { }
		catch (Exception ex)
		{
			onError?.Invoke($"Erro busca: {ex.Message}");
		}
	}

	public async Task<string> SubmitSearchAsync(IEnumerable<FileSystemItem> items, string query)
	{
		if (items == null) return string.Empty;

		var (success, itemsToChange, tag, isSelection) = await TreeSearchHelper.TryExecuteCommandAsync(items, query);

		if (success)
		{
			foreach (var item in itemsToChange)
			{
				item.IsChecked = isSelection;
			}
			string action = isSelection ? "selecionados" : "desselecionados";
			return itemsToChange.Count > 0
				? $"{itemsToChange.Count} itens com a tag '{tag}' foram {action}."
				: $"Nenhum item encontrado com a tag '{tag}' precisou ser alterado.";
		}
		return string.Empty;
	}

	public void Dispose()
	{
		_searchCts?.Cancel();
		_searchCts?.Dispose();
	}
}
