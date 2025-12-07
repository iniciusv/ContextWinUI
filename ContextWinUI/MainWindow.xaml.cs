using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI;

public sealed partial class MainWindow : Window
{
	public MainViewModel ViewModel { get; }

	public MainWindow()
	{
		InitializeComponent();
		ViewModel = new MainViewModel();

		Title = "Context WinUI - Explorador de Código";

		// Configurar tamanho mínimo da janela
		this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700));
	}

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			_ = ViewModel.LoadFileContentCommand.ExecuteAsync(item);
		}
	}

	private void TreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			_ = ViewModel.ExpandItemCommand.ExecuteAsync(item);
		}
	}
}