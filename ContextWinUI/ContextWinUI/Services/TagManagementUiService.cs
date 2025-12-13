using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class TagManagementUiService : ITagManagementUiService
{
	public async Task PromptAndAddTagAsync(ICollection<string> targetCollection, XamlRoot xamlRoot)
	{
		var textBox = new TextBox { PlaceholderText = "Ex: Refatorar, Urgente..." };

		var dialog = new ContentDialog
		{
			Title = "Nova Tag",
			Content = textBox,
			PrimaryButtonText = "Adicionar",
			CloseButtonText = "Cancelar",
			DefaultButton = ContentDialogButton.Primary,
			XamlRoot = xamlRoot // Essencial no WinUI 3
		};

		var result = await dialog.ShowAsync();

		if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
		{
			var newTag = textBox.Text.Trim();
			AddTag(targetCollection, newTag);
		}
	}

	public void ToggleTag(ICollection<string> targetCollection, string tag)
	{
		if (string.IsNullOrWhiteSpace(tag)) return;

		if (targetCollection.Contains(tag))
		{
			targetCollection.Remove(tag); // Remove se já existe
		}
		else
		{
			targetCollection.Add(tag); // Adiciona se não existe
		}
	}

	public void AddTag(ICollection<string> targetCollection, string tag)
	{
		if (!targetCollection.Contains(tag))
		{
			targetCollection.Add(tag);
		}
	}

	public void ClearTags(ICollection<string> targetCollection)
	{
		targetCollection.Clear();
	}
}