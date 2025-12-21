using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Helpers;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace ContextWinUI.Views.Components
{
	public sealed partial class ContextTreeView : UserControl
	{
		public static readonly DependencyProperty ViewModelProperty =
			DependencyProperty.Register(nameof(ViewModel), typeof(ContextTreeViewModel), typeof(ContextTreeView), new PropertyMetadata(null));

		public ContextTreeViewModel ViewModel
		{
			get => (ContextTreeViewModel)GetValue(ViewModelProperty);
			set => SetValue(ViewModelProperty, value);
		}

		private ITagManagementUiService TagService => ((App.MainWindow.ViewModel).ContextAnalysis).TagService;

		public ContextTreeView()
		{
			this.InitializeComponent();
			this.Name = "RootTree";
		}

		private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
		{
			if (args.InvokedItem is FileSystemItem item)
			{
				App.MainWindow.ViewModel.ContextAnalysis.SelectFileForPreview(item);
			}
		}

		private void OnTagMenuOpening(object sender, object e)
		{
			if (sender is Flyout flyout && flyout.Target.DataContext is FileSystemItem item)
			{
				flyout.Content = TagMenuBuilder.BuildContent(
					new List<FileSystemItem> { item },
					TagService,
					this.XamlRoot,
					() => flyout.Hide()
				);
			}
		}
	}
}