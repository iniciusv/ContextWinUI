using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace ContextWinUI;

public sealed partial class MainWindow : Window
{
	public MainViewModel ViewModel { get; }

	public MainWindow()
	{
		InitializeComponent();
		ViewModel = new MainViewModel();

		Title = "Context WinUI - Explorador de Código";
		this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700));
	}

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ViewModel.OnFileSelected(item);
		}
	}

	private void TreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			_ = ViewModel.FileExplorer.ExpandItemCommand.ExecuteAsync(item);
		}
	}

	private void TreeView_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			item.IsExpanded = false;
		}
	}

	[RelayCommand]
	private async Task AnalyzeMethodsAsync()
	{
		await ViewModel.AnalyzeFileMethodsAsync(ViewModel.FileContent.SelectedItem);
	}
}