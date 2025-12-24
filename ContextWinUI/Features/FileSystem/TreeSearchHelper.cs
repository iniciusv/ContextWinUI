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
    public static async Task SearchAsync(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token, DispatcherQueue dispatcher)
    {
        if (items == null)
            return;
        if (token.IsCancellationRequested)
            return;
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
    public static void Search(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token = default)
    {
        if (items == null)
            return;
        if (token.IsCancellationRequested)
            return;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            ResetVisibilitySync(items, token);
            return;
        }

        PerformSearch(items, searchText, token);
    }

    private static void PerformSearch(IEnumerable<FileSystemItem> items, string searchText, CancellationToken token)
    {
        string trimmedSearch = searchText.Trim();
        bool isTagSearch = false;
        string query = trimmedSearch;
        // Lógica para detectar e limpar os prefixos de comando (+# e -#)
        if (trimmedSearch.StartsWith("+#"))
        {
            isTagSearch = true;
            // Remove os 2 primeiros caracteres (+#) e limpa espaços
            query = trimmedSearch.Substring(2).Trim();
        }
        else if (trimmedSearch.StartsWith("-#"))
        {
            isTagSearch = true;
            // Remove os 2 primeiros caracteres (-#) e limpa espaços
            query = trimmedSearch.Substring(2).Trim();
        }
        else if (trimmedSearch.StartsWith("#"))
        {
            isTagSearch = true;
            // Remove o primeiro caractere (#) e limpa espaços
            query = trimmedSearch.Substring(1).Trim();
        }

        // Se o usuário digitou apenas "+#" ou "-#" sem texto depois, 
        // podemos decidir mostrar tudo ou nada. 
        // Aqui optei por interromper se a query ficou vazia para evitar travar a UI ou mostrar tudo errado.
        if (isTagSearch && string.IsNullOrEmpty(query))
        {
            // Opcional: Se quiser mostrar tudo enquanto não tiver texto da tag:
            // ResetVisibilitySync(items, token); 
            return;
        }

        foreach (var item in items)
        {
            if (token.IsCancellationRequested)
                return;
            SearchRecursive(item, query, isTagSearch, token);
        }
    }

	private static bool SearchRecursive(FileSystemItem item, string query, bool isTagSearch, CancellationToken token)
	{
		if (token.IsCancellationRequested) return false;

		// --- ALTERAÇÃO AQUI ---
		// Se o item estiver marcado como ignorado no estado compartilhado:
		if (item.SharedState.IsIgnored)
		{
			item.IsVisibleInSearch = false;
			item.IsExpanded = false; // Garante que não expanda automaticamente
			return false; // Retorna false para que o pai não considere este item como um "filho correspondente"
		}
		// ----------------------

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

				// Como o filho ignorado retornará 'false' logo no início (bloco acima),
				// hasMatchingChildren permanecerá 'false' se apenas itens ignorados existirem dentro.
				if (SearchRecursive(child, query, isTagSearch, token))
				{
					hasMatchingChildren = true;
				}
			}
		}

		// Define a visibilidade: ou o próprio item bate com a busca, ou tem filhos visíveis
		item.IsVisibleInSearch = isSelfMatch || hasMatchingChildren;

		// Lógica de Expansão Automática
		if (hasMatchingChildren)
		{
			item.IsExpanded = true; // Expande apenas se tiver filhos VÁLIDOS (não ignorados) que deram match
		}
		else if (item.IsDirectory)
		{
			item.IsExpanded = false; // Se for pasta e não tiver filhos correspondentes, mantém fechada
		}

		return item.IsVisibleInSearch;
	}
	private static async Task ResetVisibilityAsync(IEnumerable<FileSystemItem> items, CancellationToken token, DispatcherQueue dispatcher)
    {
        // 1. Imediato: Fecha Raiz
        foreach (var item in items)
        {
            if (token.IsCancellationRequested)
                return;
            item.IsVisibleInSearch = true;
            if (item.IsDirectory)
                item.IsExpanded = false;
        }

        // 2. Background: Reseta filhos
        await Task.Run(() =>
        {
            foreach (var item in items)
            {
                if (token.IsCancellationRequested)
                    return;
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

    private static void ResetVisibilitySync(IEnumerable<FileSystemItem> items, CancellationToken token)
    {
        foreach (var item in items)
        {
            if (token.IsCancellationRequested)
                return;
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
            if (token.IsCancellationRequested)
                return;
            item.IsVisibleInSearch = true;
            if (item.IsDirectory)
                item.IsExpanded = false;
            if (item.Children != null && item.Children.Any())
            {
                ResetVisibilitySyncRecursive(item.Children, token);
            }
        }
    }
}