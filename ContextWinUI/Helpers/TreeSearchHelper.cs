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

		// Se a busca estiver vazia, limpa o filtro (mostra tudo) e não altera a expansão
		if (string.IsNullOrWhiteSpace(searchText))
		{
			ResetVisibility(items);
			return;
		}

		// Executa a busca recursiva
		foreach (var item in items)
		{
			SearchRecursive(item, searchText);
		}
	}

	private static bool SearchRecursive(FileSystemItem item, string searchText)
	{
		// 1. Verifica se o próprio item bate com a busca
		bool isSelfMatch = item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);

		// 2. Verifica recursivamente os filhos (Bottom-Up)
		// Precisamos verificar TODOS os filhos para garantir que todos que derem match apareçam
		bool hasMatchingChildren = false;

		if (item.Children != null && item.Children.Any())
		{
			foreach (var child in item.Children)
			{
				// Chama recursivamente para cada filho
				bool childMatch = SearchRecursive(child, searchText);

				// Se algum filho der match, registramos isso
				if (childMatch)
				{
					hasMatchingChildren = true;
				}
			}
		}

		// 3. Determina a visibilidade deste item
		// Ele aparece se: Nome dele bate OU tem filhos que batem
		item.IsVisibleInSearch = isSelfMatch || hasMatchingChildren;

		// 4. CRUCIAL: Expande a pasta se houver filhos encontrados
		// Isso garante que o arquivo não fique escondido dentro de uma pasta fechada
		if (hasMatchingChildren)
		{
			item.IsExpanded = true;
		}
		// Opcional: Se for um match direto no arquivo, não precisa expandir ele mesmo (pois é folha), 
		// mas se for pasta e der match no nome, você pode decidir se quer expandir ou não. 
		// A lógica acima expande apenas se tiver CONTEÚDO relevante dentro.

		return item.IsVisibleInSearch;
	}

	private static void ResetVisibility(IEnumerable<FileSystemItem> items)
	{
		if (items == null) return;

		foreach (var item in items)
		{
			item.IsVisibleInSearch = true;
			// Opcional: Se quiser recolher tudo ao limpar a busca, descomente a linha abaixo:
			// item.IsExpanded = false; 

			if (item.Children != null && item.Children.Any())
			{
				ResetVisibility(item.Children);
			}
		}
	}
}