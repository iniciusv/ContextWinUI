using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Helpers;
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Views;

public sealed partial class ContextAnalysisView : UserControl
{
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

	// Clique na Árvore (já existia)
	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ContextViewModel?.SelectFileForPreview(item);
		}
	}

	// --- NOVO: Clique nas Listas (Seleção e Git) ---
	private void OnListViewItemClick(object sender, ItemClickEventArgs e)
	{
		if (e.ClickedItem is FileSystemItem item)
		{
			// Dispara o evento que o MainViewModel está ouvindo para carregar o arquivo
			ContextViewModel?.SelectFileForPreview(item);
		}
	}


private void OnTagMenuOpening(object sender, object e)
{
	// O sender aqui originalmente era MenuFlyout.
	// Se você não mudou o XAML, ele ainda espera popular um MenuFlyout.
	// O MenuFlyout NÃO aceita StackPanel complexo facilmente.

	// SOLUÇÃO: Vamos fechar esse MenuFlyout imediatamente e abrir o nosso Flyout customizado,
	// OU (melhor) alterar o XAML para usar <Flyout> em vez de <MenuFlyout>.

	// Assumindo que você alterou o XAML para <Flyout Opening="OnTagMenuOpening">...

	if (sender is Flyout flyout && flyout.Target.DataContext is FileSystemItem rightClickedItem)
	{
		// Lógica de seleção múltipla (mantida do seu código original)
		List<FileSystemItem> targetItems = new();
		var treeSelection = AnalysisTreeView.SelectedItems.Cast<FileSystemItem>().ToList();
		var listSelection = AnalysisListView.SelectedItems.Cast<FileSystemItem>().ToList();
		var gitSelection = GitListView.SelectedItems.Cast<FileSystemItem>().ToList();

		if (treeSelection.Contains(rightClickedItem)) targetItems = treeSelection;
		else if (listSelection.Contains(rightClickedItem)) targetItems = listSelection;
		else if (gitSelection.Contains(rightClickedItem)) targetItems = gitSelection;
		else targetItems.Add(rightClickedItem);

		// Usa o Builder
		flyout.Content = TagMenuBuilder.BuildContent(
			targetItems,
			ContextViewModel.TagService,
			this.XamlRoot,
			() => flyout.Hide()
		);
	}
}
private void OnDeepAnalyzeClick(object sender, RoutedEventArgs e)
	{
		// 1. O sender é o botão que foi clicado
		if (sender is Button button && button.DataContext is FileSystemItem item)
		{
			// 2. Verificamos se o ViewModel e o Comando existem
			if (ContextViewModel != null && ContextViewModel.AnalyzeItemDepthCommand.CanExecute(item))
			{
				// 3. Executamos o comando manualmente passando o item
				ContextViewModel.AnalyzeItemDepthCommand.Execute(item);
			}
		}
	}
	private void OnMethodFlowClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button button && button.DataContext is FileSystemItem item)
		{
			if (ContextViewModel != null && ContextViewModel.AnalyzeMethodFlowCommand.CanExecute(item))
			{
				ContextViewModel.AnalyzeMethodFlowCommand.Execute(item);
			}
		}
	}
}