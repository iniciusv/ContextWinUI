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

		bool isEmpty = string.IsNullOrWhiteSpace(searchText);

		foreach (var item in items)
		{
			if (isEmpty)
			{
				// Se busca vazia, mostra tudo e reseta filhos
				item.IsVisibleInSearch = true;
				Search(item.Children, searchText);
				continue;
			}

			// Verifica match no próprio item
			bool isSelfMatch = item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);

			// Verifica match nos filhos
			bool hasMatchingChildren = CheckChildren(item.Children, searchText);

			// Item visível se ele der match OU se tiver filhos que dão match
			item.IsVisibleInSearch = isSelfMatch || hasMatchingChildren;

			// Expande para mostrar os filhos encontrados
			if (hasMatchingChildren)
			{
				item.IsExpanded = true;
			}
		}
	}

	// Método auxiliar que retorna bool para ajudar na decisão do pai
	private static bool CheckChildren(IEnumerable<FileSystemItem> children, string searchText)
	{
		if (children == null || !children.Any()) return false;

		bool anyChildMatch = false;

		foreach (var child in children)
		{
			bool isSelfMatch = child.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
			bool hasMatchingGrandChildren = CheckChildren(child.Children, searchText);

			child.IsVisibleInSearch = isSelfMatch || hasMatchingGrandChildren;

			if (child.IsVisibleInSearch)
				anyChildMatch = true;

			if (hasMatchingGrandChildren)
				child.IsExpanded = true;
		}

		return anyChildMatch;
	}
}