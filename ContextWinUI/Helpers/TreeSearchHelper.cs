using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Helpers;

public static class TreeSearchHelper
{
	public static void Search(IEnumerable<FileSystemItem> items, string searchText)
	{
		if (items == null) return;

		// Se a busca estiver vazia, limpa o filtro (mostra tudo)
		if (string.IsNullOrWhiteSpace(searchText))
		{
			ResetVisibility(items);
			return;
		}

		// Executa a busca recursiva
		foreach (var item in items)
		{
			SearchRecursive(item, searchText.Trim());
		}
	}

	private static bool SearchRecursive(FileSystemItem item, string searchText)
	{
		bool isSelfMatch = false;

		// --- LÓGICA NOVA: DETECÇÃO DE TAGS (#) ---
		if (searchText.StartsWith("#"))
		{
			// Remove o # e espaços extras (ex: "# importante " vira "importante")
			var tagQuery = searchText.Substring(1).Trim();

			// Se o usuário digitou apenas "#", podemos optar por mostrar:
			// a) Nada (espera digitar)
			// b) Tudo que tem alguma tag (útil para descoberta)
			// Vamos na opção B: Mostra itens que possuem qualquer tag
			if (string.IsNullOrEmpty(tagQuery))
			{
				isSelfMatch = item.SharedState.Tags.Any();
			}
			else
			{
				// Verifica se ALGUMA tag contém o texto digitado
				isSelfMatch = item.SharedState.Tags.Any(tag =>
					tag.Contains(tagQuery, StringComparison.OrdinalIgnoreCase));
			}
		}
		else
		{
			// --- LÓGICA PADRÃO: BUSCA POR NOME ---
			isSelfMatch = item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
		}
		// ------------------------------------------

		// 2. Verifica recursivamente os filhos (Bottom-Up)
		bool hasMatchingChildren = false;

		if (item.Children != null && item.Children.Any())
		{
			foreach (var child in item.Children)
			{
				// Chama recursivamente para cada filho passando a mesma query
				bool childMatch = SearchRecursive(child, searchText);

				if (childMatch)
				{
					hasMatchingChildren = true;
				}
			}
		}

		// 3. Determina a visibilidade deste item
		// Ele aparece se: Deu match nele mesmo OU tem filhos que deram match (para manter o caminho aberto)
		item.IsVisibleInSearch = isSelfMatch || hasMatchingChildren;

		// 4. Expande a pasta se houver filhos encontrados
		if (hasMatchingChildren)
		{
			item.IsExpanded = true;
		}
		// Opcional: Se for match direto e for diretório, expande também?
		// if (isSelfMatch && item.IsDirectory) item.IsExpanded = true;

		return item.IsVisibleInSearch;
	}

	private static void ResetVisibility(IEnumerable<FileSystemItem> items)
	{
		if (items == null) return;

		foreach (var item in items)
		{
			item.IsVisibleInSearch = true;

			// Opcional: Recolher pastas ao limpar busca deixa a árvore mais limpa
			// item.IsExpanded = false; 

			if (item.Children != null && item.Children.Any())
			{
				ResetVisibility(item.Children);
			}
		}
	}
}