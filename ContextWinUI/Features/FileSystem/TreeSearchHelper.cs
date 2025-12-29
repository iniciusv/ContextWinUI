using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using ContextWinUI.Models; // Certifique-se que o namespace do FileSystemItem está correto

namespace ContextWinUI.Helpers
{
	public static class TreeSearchHelper
	{
		// Estrutura leve para guardar o cálculo antes de aplicar na UI
		private readonly record struct SearchResult(bool IsVisible, bool IsExpanded);

		/// <summary>
		/// Ponto de entrada principal para a pesquisa assíncrona.
		/// </summary>
		public static async Task SearchAsync(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token, DispatcherQueue dispatcher)
		{
			if (items == null) return;
			if (token.IsCancellationRequested) return;

			// Se a busca estiver vazia, restaura a visibilidade original
			if (string.IsNullOrWhiteSpace(searchText))
			{
				await ResetVisibilityAsync(items, token, dispatcher);
				return;
			}

			// Executa a lógica de busca
			await PerformSearchAsync(items, searchText, token, dispatcher);
		}

		private static async Task PerformSearchAsync(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token, DispatcherQueue dispatcher)
		{
			// 1. Parsing da Query
			string trimmedSearch = searchText.Trim();
			bool isTagSearch = false;
			bool isFolderTagSearch = false; // Modo []#
			string query = trimmedSearch;

			// Ordem importa: verificar os prefixos mais longos primeiro
			if (trimmedSearch.StartsWith("[]#"))
			{
				isFolderTagSearch = true;
				isTagSearch = true;
				query = trimmedSearch.Substring(3).Trim();
			}
			else if (trimmedSearch.StartsWith("+#"))
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

			// Se for busca por tag mas não tiver texto, aborta
			if (isTagSearch && string.IsNullOrEmpty(query)) return;

			// 2. Cálculo Pesado (Background Thread)
			await Task.Run(() =>
			{
				var resultsToApply = new Dictionary<FileSystemItem, SearchResult>();

				foreach (var item in items)
				{
					if (token.IsCancellationRequested) return;

					// Inicia a recursão. parentIsMatch começa como false na raiz.
					CalculateVisibilityRecursive(item, query, isTagSearch, isFolderTagSearch, false, token, resultsToApply);
				}

				if (token.IsCancellationRequested) return;

				// 3. Aplicação na UI (Dispatcher)
				dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
				{
					if (token.IsCancellationRequested) return;

					foreach (var kvp in resultsToApply)
					{
						var item = kvp.Key;
						var result = kvp.Value;

						// Só notifica a UI se o valor realmente mudou (otimização de renderização)
						if (item.IsVisibleInSearch != result.IsVisible)
							item.IsVisibleInSearch = result.IsVisible;

						if (item.IsDirectory && item.IsExpanded != result.IsExpanded)
							item.IsExpanded = result.IsExpanded;
					}
				});
			});
		}

		private static bool CalculateVisibilityRecursive(FileSystemItem item,string query,bool isTagSearch,bool isFolderTagSearch,bool parentIsMatch, CancellationToken token,Dictionary<FileSystemItem, SearchResult> results)
		{
			if (token.IsCancellationRequested) return false;

			// Se o item está marcado como ignorado (.git, bin, obj...), ele e seus filhos somem
			if (item.SharedState.IsIgnored)
			{
				results[item] = new SearchResult(false, false);
				return false;
			}

			bool isSelfMatch = false;

			// --- LÓGICA DE MATCH ---

			if (parentIsMatch && isFolderTagSearch)
			{
				// MODO CASCATA: Se o pai deu match no modo []#, este filho (arquivo ou pasta)
				// torna-se visível automaticamente, independente de ter a tag ou não.
				isSelfMatch = true;
			}
			else
			{
				if (isFolderTagSearch)
				{
					// Modo []#: Apenas PASTAS com a tag específica dão match explícito.
					if (item.IsDirectory)
					{
						isSelfMatch = string.IsNullOrEmpty(query)
							? item.SharedState.Tags.Any()
							: item.SharedState.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
					}
					else
					{
						// Arquivos nunca dão match "sozinhos" neste modo (precisam do pai)
						isSelfMatch = false;
					}
				}
				else if (isTagSearch)
				{
					// Modo Tag Normal (#tag): Qualquer item com a tag serve
					isSelfMatch = string.IsNullOrEmpty(query)
						? item.SharedState.Tags.Any()
						: item.SharedState.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
				}
				else
				{
					// Modo Texto Normal: Match pelo nome do arquivo/pasta
					isSelfMatch = item.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
				}
			}

			// --- RECURSÃO (Filhos) ---

			bool hasMatchingChildren = false;

			// Se estamos no modo []# e este item deu match, avisamos os filhos para aparecerem (Cascata)
			bool shouldPassMatchDown = isFolderTagSearch && isSelfMatch;

			if (item.Children != null && item.Children.Any())
			{
				foreach (var child in item.Children)
				{
					if (CalculateVisibilityRecursive(child, query, isTagSearch, isFolderTagSearch, shouldPassMatchDown, token, results))
					{
						hasMatchingChildren = true;
					}
				}
			}

			// --- DECISÃO FINAL ---

			// O item aparece se ele mesmo deu match OU se tem algum filho que deu match
			bool isVisible = isSelfMatch || hasMatchingChildren;

			// Devemos expandir a pasta?
			// 1. Se tem filhos encontrados (padrão)
			// 2. OU se estamos no modo []# e encontramos esta pasta (queremos ver o conteúdo dela)
			bool shouldExpand = hasMatchingChildren || (isFolderTagSearch && isSelfMatch);

			results[item] = new SearchResult(isVisible, shouldExpand);
			return isVisible;
		}

		private static async Task ResetVisibilityAsync(IEnumerable<FileSystemItem> items, CancellationToken token, DispatcherQueue dispatcher)
		{
			await Task.Run(() =>
			{
				// Achata a lista para evitar recursão na UI Thread
				var allItems = new List<FileSystemItem>();
				CollectAllItems(items, allItems, token);

				dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
				{
					foreach (var item in allItems)
					{
						if (token.IsCancellationRequested) return;

						if (!item.IsVisibleInSearch) item.IsVisibleInSearch = true;

						// Recolhe pastas ao limpar a busca para limpar a visualização
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

		public static async Task<(bool Success, int Count, string Tag, bool IsSelection)> TryExecuteCommandAsync(IEnumerable<FileSystemItem> items, string query)
		{
			if (string.IsNullOrWhiteSpace(query) || items == null)
				return (false, 0, string.Empty, false);

			string trimmedQuery = query.Trim();
			bool? selectMode = null;
			string tagToProcess = string.Empty;

			// 1. Lógica de Interpretação (Parsing) movida para cá
			if (trimmedQuery.StartsWith("+#"))
			{
				selectMode = true;
				tagToProcess = trimmedQuery.Substring(2);
			}
			else if (trimmedQuery.StartsWith("-#"))
			{
				selectMode = false;
				tagToProcess = trimmedQuery.Substring(2);
			}

			// Se não for um comando conhecido, retorna falso
			if (!selectMode.HasValue || string.IsNullOrWhiteSpace(tagToProcess))
			{
				return (false, 0, string.Empty, false);
			}

			// 2. Execução (envolvida em Task.Run para não travar a UI)
			int count = await Task.Run(() =>
			{
				return ModifySelectionByTagRecursive(items, tagToProcess, selectMode.Value);
			});

			return (true, count, tagToProcess, selectMode.Value);
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
}
