using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ContextWinUI.Views;

public sealed partial class ContextAnalysisView : UserControl
{
	// Tags padrão sugeridas
	private readonly List<string> _standardTags = new() { "Importante", "Revisar", "Documentação", "Bug", "Refatorar" };

	public static readonly DependencyProperty ContextViewModelProperty =
		DependencyProperty.Register(nameof(ContextViewModel), typeof(ContextAnalysisViewModel), typeof(ContextAnalysisView), new PropertyMetadata(null));

	public ContextAnalysisViewModel ContextViewModel
	{
		get => (ContextAnalysisViewModel)GetValue(ContextViewModelProperty);
		set => SetValue(ContextViewModelProperty, value);
	}

	public ContextAnalysisView()
	{
		this.InitializeComponent();
		this.Name = "RootAnalysisView";
	}

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ContextViewModel?.SelectFileForPreview(item);
		}
	}

	// --- LÓGICA DINÂMICA DO MENU DE TAGS ---

	private void OnTagMenuOpening(object sender, object e)
	{
		// Funciona tanto para o TreeView quanto para o ListView, desde que o DataContext seja FileSystemItem
		if (sender is MenuFlyout flyout && flyout.Target.DataContext is FileSystemItem item)
		{
			// 1. Limpa menu
			flyout.Items.Clear();

			// 2. Nova Tag
			var newTagItem = new MenuFlyoutItem { Text = "Nova Tag...", Icon = new FontIcon { Glyph = "\uE710" } };
			// Usa ContextViewModel.TagService
			newTagItem.Click += (s, args) => _ = ContextViewModel.TagService.PromptAndAddTagAsync(item.SharedState.Tags, this.XamlRoot);
			flyout.Items.Add(newTagItem);

			flyout.Items.Add(new MenuFlyoutSeparator());

			// 3. Lista Dinâmica (Padrão + Atuais)
			var allTagsDisplay = _standardTags.Union(item.SharedState.Tags).OrderBy(x => x).ToList();

			foreach (var tag in allTagsDisplay)
			{
				var isChecked = item.SharedState.Tags.Contains(tag);

				var toggleItem = new ToggleMenuFlyoutItem
				{
					Text = tag,
					IsChecked = isChecked
				};

				toggleItem.Click += (s, args) =>
				{
					ContextViewModel.TagService.ToggleTag(item.SharedState.Tags, tag);
				};

				flyout.Items.Add(toggleItem);
			}

			// 4. Opção Limpar
			if (item.SharedState.Tags.Any())
			{
				flyout.Items.Add(new MenuFlyoutSeparator());
				var clearItem = new MenuFlyoutItem { Text = "Limpar Tags", Icon = new FontIcon { Glyph = "\uE74D" } };
				clearItem.Click += (s, args) => ContextViewModel.TagService.ClearTags(item.SharedState.Tags);
				flyout.Items.Add(clearItem);
			}

			// 5. (Opcional) Manter a opção "Remover da Seleção" se estivermos no contexto da Lista de Seleção
			// Para isso, precisaria verificar se o sender vem do ListView ou se faz sentido adicionar aqui.
			// Se você quiser manter o item "Remover da Seleção" que existia no XAML original da Lista,
			// você deve adicioná-lo manualmente aqui no início ou fim.

			/* Exemplo:
			if (flyout.Target is FrameworkElement fe && fe.FindParent<ListView>() != null) {
				flyout.Items.Add(new MenuFlyoutSeparator());
				var removeItem = new MenuFlyoutItem { Text = "Remover da Seleção", Icon = new FontIcon { Glyph = "Remove" } };
				removeItem.Click += (s, args) => item.IsChecked = false;
				flyout.Items.Add(removeItem);
			}
			*/
		}
	}
}