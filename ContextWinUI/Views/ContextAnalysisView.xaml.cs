using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ContextWinUI.Views;

public sealed partial class ContextAnalysisView : UserControl
{
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

	// --- LÓGICA DE TAGS (REPLICADA DO EXPLORER) ---

	private void AddTag_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuItem &&
			menuItem.DataContext is FileSystemItem item &&
			menuItem.Tag is string tag)
		{
			if (!item.SharedState.Tags.Contains(tag))
				item.SharedState.Tags.Add(tag);
		}
	}

	private async void AddNewTag_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is FileSystemItem item)
		{
			var textBox = new TextBox { PlaceholderText = "Ex: Refatorar" };
			var dialog = new ContentDialog
			{
				Title = "Nova Tag",
				Content = textBox,
				PrimaryButtonText = "Adicionar",
				CloseButtonText = "Cancelar",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = this.XamlRoot
			};

			var result = await dialog.ShowAsync();

			if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
			{
				var newTag = textBox.Text.Trim();
				if (!item.SharedState.Tags.Contains(newTag))
					item.SharedState.Tags.Add(newTag);
			}
		}
	}

	private void ClearTags_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is FileSystemItem item)
			item.SharedState.Tags.Clear();
	}

	// --- AÇÃO DA LISTA DE SELEÇÃO ---

	private void RemoveFromList_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is FileSystemItem item)
		{
			// Ao setar false, o ViewModel remove da lista e atualiza o SharedState
			item.IsChecked = false;
		}
	}
}