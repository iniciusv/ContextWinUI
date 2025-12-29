using ContextWinUI.Models;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.Helpers;

public static class TreeSearchHelper
{
	// Estrutura leve para guardar o estado calculado antes de aplicar na UI
	private readonly record struct SearchResult(bool IsVisible, bool IsExpanded);

	public static async Task SearchAsync(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token, DispatcherQueue dispatcher)
	{
		if (items == null)
			return;

		if (token.IsCancellationRequested)
			return;

		if (string.IsNullOrWhiteSpace(searchText))
		{
			await ResetVisibilityAsync(items, token, dispatcher);
			return;
		}

		await PerformSearchAsync(items, searchText, token, dispatcher);
	}

	private static async Task PerformSearchAsync(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token, DispatcherQueue dispatcher)
	{
		// 1. PREPARAÇÃO DA QUERY
		string trimmedSearch = searchText.Trim();
		bool isTagSearch = false;
		string query = trimmedSearch;

		if (trimmedSearch.StartsWith("+#"))
		{
			isTagSearch = true;
			query = trimmedSearch.Substring(2).Trim();
		}
		else if (trimmedSearch.StartsWith("-#"))
		{
			isTagSearch = true;
			query = trimmedSearch.Substring(2).Trim();
		}
		else if (trimmedSearch.StartsWith("#"))
		{
			isTagSearch = true;
			query = trimmedSearch.Substring(1).Trim();
		}

		if (isTagSearch && string.IsNullOrEmpty(query)) return;

		// 2. CÁLCULO PESADO (BACKGROUND THREAD)
		// Isso roda fora da UI, então não trava o app enquanto processa milhares de arquivos.
		await Task.Run(() =>
		{
			var resultsToApply = new Dictionary<FileSystemItem, SearchResult>();

			foreach (var item in items)
			{
				if (token.IsCancellationRequested) return;
				CalculateVisibilityRecursive(item, query, isTagSearch, token, resultsToApply);
			}

			if (token.IsCancellationRequested) return;

			// 3. APLICAÇÃO NA UI (DISPATCHER)
			// Agora voltamos para a thread principal apenas para aplicar os valores calculados.
			dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
			{
				if (token.IsCancellationRequested) return;

				// Otimização: Pausar layout updates se possível seria ideal, 
				// mas aqui aplicamos apenas as mudanças necessárias.
				foreach (var kvp in resultsToApply)
				{
					var item = kvp.Key;
					var result = kvp.Value;

					// Só notifica a UI se o valor realmente mudou para evitar repintura desnecessária
					if (item.IsVisibleInSearch != result.IsVisible)
						item.IsVisibleInSearch = result.IsVisible;

					if (item.IsDirectory && item.IsExpanded != result.IsExpanded)
						item.IsExpanded = result.IsExpanded;
				}
			});
		});
	}

	// Método recursivo puro (apenas lógica, sem tocar em propriedades de UI)
	private static bool CalculateVisibilityRecursive(FileSystemItem item, string query, bool isTagSearch, CancellationToken token, Dictionary<FileSystemItem, SearchResult> results)
	{
		if (token.IsCancellationRequested) return false;

		// Se ignorado, já cortamos aqui
		if (item.SharedState.IsIgnored)
		{
			results[item] = new SearchResult(false, false);
			return false;
		}

		// Verifica match no próprio item
		bool isSelfMatch = false;
		if (isTagSearch)
			isSelfMatch = string.IsNullOrEmpty(query) ? item.SharedState.Tags.Any() : item.SharedState.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
		else
			isSelfMatch = item.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

		// Verifica filhos
		bool hasMatchingChildren = false;
		if (item.Children != null && item.Children.Any())
		{
			foreach (var child in item.Children)
			{
				if (CalculateVisibilityRecursive(child, query, isTagSearch, token, results))
				{
					hasMatchingChildren = true;
				}
			}
		}

		bool isVisible = isSelfMatch || hasMatchingChildren;

		// Só expandimos se houver filhos correspondentes (para não expandir pastas vazias que coincidiram apenas no nome)
		bool shouldExpand = hasMatchingChildren;

		results[item] = new SearchResult(isVisible, shouldExpand);
		return isVisible;
	}

	// Mantido similar ao original, mas garantindo execução background
	private static async Task ResetVisibilityAsync(IEnumerable<FileSystemItem> items, CancellationToken token, DispatcherQueue dispatcher)
	{
		await Task.Run(() =>
		{
			// Coletar todos os itens planos para evitar recursão na UI Thread dentro do Enqueue
			var allItems = new List<FileSystemItem>();
			CollectAllItems(items, allItems, token);

			dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
			{
				foreach (var item in allItems)
				{
					if (token.IsCancellationRequested) return;

					if (!item.IsVisibleInSearch) item.IsVisibleInSearch = true;
					if (item.IsDirectory && item.IsExpanded) item.IsExpanded = false;
				}
			});
		});
	}

	private static void CollectAllItems(IEnumerable<FileSystemItem> items, List<FileSystemItem> collector, CancellationToken token)
	{
		foreach (var item in items)
		{
			if (token.IsCancellationRequested) return;
			collector.Add(item);
			if (item.Children != null && item.Children.Any())
			{
				CollectAllItems(item.Children, collector, token);
			}
		}
	}
}