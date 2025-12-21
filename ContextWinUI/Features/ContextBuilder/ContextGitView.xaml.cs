using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Helpers;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Views.Components
{
	public sealed partial class ContextGitView : UserControl
	{
		public static readonly DependencyProperty ViewModelProperty =
			DependencyProperty.Register(nameof(ViewModel), typeof(ContextGitViewModel), typeof(ContextGitView), new PropertyMetadata(null));

		public ContextGitViewModel ViewModel
		{
			get => (ContextGitViewModel)GetValue(ViewModelProperty);
			set => SetValue(ViewModelProperty, value);
		}

		private ITagManagementUiService TagService => ((App.MainWindow.ViewModel).ContextAnalysis).TagService;

		public ContextGitView()
		{
			this.InitializeComponent();
		}

		private void ListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			if (e.ClickedItem is FileSystemItem item)
			{
				App.MainWindow.ViewModel.ContextAnalysis.SelectFileForPreview(item);
			}
		}

		private void OnTagMenuOpening(object sender, object e)
		{
			if (sender is Flyout flyout && flyout.Target.DataContext is FileSystemItem item)
			{
				var listView = FindParent<ListView>((DependencyObject)flyout.Target);

				List<FileSystemItem> targetItems;
				if (listView != null && listView.SelectedItems.Contains(item))
					targetItems = listView.SelectedItems.Cast<FileSystemItem>().ToList();
				else
					targetItems = new List<FileSystemItem> { item };

				flyout.Content = TagMenuBuilder.BuildContent(
					targetItems, TagService, this.XamlRoot, () => flyout.Hide());
			}
		}

		private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
		{
			if (child == null) return null;
			var parent = VisualTreeHelper.GetParent(child);
			if (parent == null) return null;
			if (parent is T typedParent) return typedParent;
			return FindParent<T>(parent);
		}
	}
}