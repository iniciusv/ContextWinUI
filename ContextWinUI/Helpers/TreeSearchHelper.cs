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
	/// <summary>
	/// VERSÃO ASSÍNCRONA (Otimizada para grandes árvores com Dispatcher)
	/// </summary>
	public static async Task SearchAsync(
		IEnumerable<FileSystemItem> items,
		string searchText,
		CancellationToken token,
		DispatcherQueue dispatcher)
	{
		if (items == null) return;
		if (token.IsCancellationRequested) return;

		// Reset Otimizado (Async)
		if (string.IsNullOrWhiteSpace(searchText))
		{
			await ResetVisibilityAsync(items, token, dispatcher);
			return;
		}

		PerformSearch(items, searchText, token);
	}

	/// <summary>
	/// VERSÃO SÍNCRONA (Compatibilidade para ContextAnalysisViewModel e listas pequenas)
	/// </summary>
	public static void Search(
		IEnumerable<FileSystemItem> items,
		string searchText,
		CancellationToken token = default)
	{
		if (items == null) return;
		if (token.IsCancellationRequested) return;

		if (string.IsNullOrWhiteSpace(searchText))
		{
			ResetVisibilitySync(items, token);
			return;
		}

		PerformSearch(items, searchText, token);
	}

	// --- LÓGICA DE BUSCA COMPARTILHADA ---
	private static void PerformSearch(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token)
	{
		string trimmedSearch = searchText.Trim();
		bool isTagSearch = trimmedSearch.StartsWith("#");
		string query = isTagSearch ? trimmedSearch.Substring(1).Trim() : trimmedSearch;

		foreach (var item in items)
		{
			if (token.IsCancellationRequested) return;
			SearchRecursive(item, query, isTagSearch, token);
		}
	}

	private static bool SearchRecursive(FileSystemItem item, string query, bool isTagSearch, CancellationToken token)
	{
		if (token.IsCancellationRequested) return false;

		// OTIMIZAÇÃO: Se estiver marcado para ignorar, não processa este item nem seus filhos
		if (item.SharedState.IsIgnored)
		{
			item.IsVisibleInSearch = false;
			return false;
		}

		bool isSelfMatch = false;

		if (isTagSearch)
			isSelfMatch = string.IsNullOrEmpty(query) ? item.SharedState.Tags.Any() : item.SharedState.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
		else
			isSelfMatch = item.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

		bool hasMatchingChildren = false;

		if (item.Children != null && item.Children.Any())
		{
			foreach (var child in item.Children)
			{
				if (token.IsCancellationRequested) return false;
				if (SearchRecursive(child, query, isTagSearch, token)) hasMatchingChildren = true;
			}
		}

		item.IsVisibleInSearch = isSelfMatch || hasMatchingChildren;

		if (hasMatchingChildren) item.IsExpanded = true;
		else if (item.IsDirectory) item.IsExpanded = false;

		return item.IsVisibleInSearch;
	}

	// --- RESET OTIMIZADO (ASYNC/DISPATCHER) ---
	private static async Task ResetVisibilityAsync(IEnumerable<FileSystemItem> items, CancellationToken token, DispatcherQueue dispatcher)
	{
		// 1. Imediato: Fecha Raiz
		foreach (var item in items)
		{
			if (token.IsCancellationRequested) return;
			item.IsVisibleInSearch = true;
			if (item.IsDirectory) item.IsExpanded = false;
		}

		// 2. Background: Reseta filhos
		await Task.Run(() =>
		{
			foreach (var item in items)
			{
				if (token.IsCancellationRequested) return;
				if (item.Children != null && item.Children.Any())
				{
					dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
					{
						if (!token.IsCancellationRequested)
							ResetVisibilitySyncRecursive(item.Children, token);
					});
				}
			}
		});
	}

	// --- RESET SIMPLES (SYNC) ---
	private static void ResetVisibilitySync(IEnumerable<FileSystemItem> items, CancellationToken token)
	{
		foreach (var item in items)
		{
			if (token.IsCancellationRequested) return;
			item.IsVisibleInSearch = true;

			// Em listas pequenas (ContextAnalysis), expandir ou recolher é opcional.
			// Aqui mantemos recolhido por padrão para limpeza.
			// if (item.IsDirectory) item.IsExpanded = false; 

			if (item.Children != null && item.Children.Any())
			{
				ResetVisibilitySync(item.Children, token);
			}
		}
	}

	// Auxiliar recursivo para o Reset Async e Sync
	private static void ResetVisibilitySyncRecursive(IEnumerable<FileSystemItem> items, CancellationToken token)
	{
		foreach (var item in items)
		{
			if (token.IsCancellationRequested) return;
			item.IsVisibleInSearch = true;
			if (item.IsDirectory) item.IsExpanded = false;

			if (item.Children != null && item.Children.Any())
			{
				ResetVisibilitySyncRecursive(item.Children, token);
			}
		}
	}
}