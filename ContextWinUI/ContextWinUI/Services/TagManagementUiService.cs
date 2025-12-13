using ContextWinUI.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class TagManagementUiService : ITagManagementUiService
{
	// --- Lógica de Item Único ---

	public async Task PromptAndAddTagAsync(ICollection<string> targetCollection, XamlRoot xamlRoot)
	{
		var textBox = new TextBox { PlaceholderText = "Ex: Refatorar" };
		var dialog = new ContentDialog
		{
			Title = "Nova Tag",
			Content = textBox,
			PrimaryButtonText = "Adicionar",
			CloseButtonText = "Cancelar",
			DefaultButton = ContentDialogButton.Primary,
			XamlRoot = xamlRoot
		};

		var result = await dialog.ShowAsync();

		if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
		{
			var newTag = textBox.Text.Trim();
			if (!targetCollection.Contains(newTag)) targetCollection.Add(newTag);
		}
	}

	public void ToggleTag(ICollection<string> targetCollection, string tag)
	{
		if (string.IsNullOrWhiteSpace(tag)) return;

		if (targetCollection.Contains(tag))
			targetCollection.Remove(tag);
		else
			targetCollection.Add(tag);
	}

	public void ClearTags(ICollection<string> targetCollection)
	{
		targetCollection.Clear();
	}

	// --- Lógica de Lote (Batch) ---

	public void BatchToggleTag(IEnumerable<FileSystemItem> items, string tag)
	{
		if (items == null || !items.Any()) return;

		// Verifica se TODOS os itens já possuem a tag
		bool allHaveTag = items.All(i => i.SharedState.Tags.Contains(tag));

		foreach (var item in items)
		{
			if (allHaveTag)
			{
				// Se todos têm, removemos de todos (comportamento de uncheck)
				item.SharedState.Tags.Remove(tag);
			}
			else
			{
				// Se algum não tem, garantimos que todos tenham (comportamento de check/uniformizar)
				if (!item.SharedState.Tags.Contains(tag))
				{
					item.SharedState.Tags.Add(tag);
				}
			}
		}
	}

	public async Task PromptAndAddTagToBatchAsync(IEnumerable<FileSystemItem> items, XamlRoot xamlRoot)
	{
		var textBox = new TextBox { PlaceholderText = "Ex: Refatorar" };
		var dialog = new ContentDialog
		{
			Title = $"Nova Tag em Lote ({items.Count()} itens)",
			Content = textBox,
			PrimaryButtonText = "Adicionar a Todos",
			CloseButtonText = "Cancelar",
			DefaultButton = ContentDialogButton.Primary,
			XamlRoot = xamlRoot
		};

		var result = await dialog.ShowAsync();

		if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
		{
			var newTag = textBox.Text.Trim();
			foreach (var item in items)
			{
				if (!item.SharedState.Tags.Contains(newTag))
					item.SharedState.Tags.Add(newTag);
			}
		}
	}
}