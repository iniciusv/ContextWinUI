using ContextWinUI.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ContextWinUI.ViewModels.Helpers;

public class FileExplorerTreeManager
{
	// Otimização: Evitar recursão desnecessária na thread de UI se possível
	public void ExpandAll(IEnumerable<FileSystemItem> items)
	{
		SetExpansionRecursive(items, true);
	}

	public void CollapseAll(IEnumerable<FileSystemItem> items)
	{
		SetExpansionRecursive(items, false);
	}

	private void SetExpansionRecursive(IEnumerable<FileSystemItem> items, bool isExpanded)
	{
		// Performance: Usar foreach simples é mais rápido que LINQ em coleções grandes
		foreach (var item in items)
		{
			if (isExpanded && item.SharedState.IsIgnored) continue;

			if (item.IsDirectory)
			{
				// Evita disparo de notificação se o valor não mudou
				if (item.IsExpanded != isExpanded)
					item.IsExpanded = isExpanded;

				if (item.Children != null && item.Children.Count > 0)
				{
					SetExpansionRecursive(item.Children, isExpanded);
				}
			}
		}
	}

	public void SyncFocus(IEnumerable<FileSystemItem> rootItems, FileSystemItem? selectedItem)
	{
		if (rootItems == null || !rootItems.Any()) return;

		if (selectedItem == null)
		{
			CollapseAll(rootItems);
			return;
		}

		foreach (var item in rootItems)
		{
			DetermineExpansionState(item, selectedItem);
		}
	}

	private bool DetermineExpansionState(FileSystemItem current, FileSystemItem target)
	{
		if (current == target) return true;

		bool containsTarget = false;

		// Performance: Check de nulos e Count antes de iterar
		if (current.Children != null && current.Children.Count > 0)
		{
			foreach (var child in current.Children)
			{
				if (DetermineExpansionState(child, target))
				{
					containsTarget = true;
					// Otimização: Se encontrou, não precisa verificar o resto dos irmãos
					// break; -> Cuidado: se quiser expandir múltiplos caminhos (raro), não use break. 
					// Para foco simples, break é seguro e performático.
				}
			}
		}

		if (current.IsDirectory && containsTarget)
		{
			current.IsExpanded = true;
		}

		return containsTarget;
	}
}