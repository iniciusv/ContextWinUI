using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace ContextWinUI.Features.GraphParser.Views
{
	public sealed partial class FileSegmentsView : UserControl
	{
		private CodeBlockItem? _lastSelectedBlock;

		public FileSegmentsViewModel ViewModel => (FileSegmentsViewModel)DataContext;

		public FileSegmentsView()
		{
			this.InitializeComponent();
			this.DataContextChanged += (s, e) => Bindings.Update();
			this.Loaded += OnLoaded;
		}

		private void OnLoaded(object sender, RoutedEventArgs e) => UpdateSelectionIndicator();

		// --- Eventos de Edição de Segmento ---

		private void OnSegmentContentModified(object sender, EventArgs e)
		{
			EnsureBlockSelection(sender);
			ViewModel.NotifyUnsavedChanges();
			ViewModel.NotifyChangesChanged();
		}

		private void OnSegmentGotFocus(object sender, RoutedEventArgs e) => EnsureBlockSelection(sender);

		private void OnBlockPointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if (sender is FrameworkElement element && element.DataContext is CodeBlockItem block)
			{
				ViewModel.SelectedBlock = block;
				UpdateSelectionIndicator();
			}
		}

		private void EnsureBlockSelection(object sender)
		{
			if (sender is FrameworkElement element && element.DataContext is CodeBlockItem block)
			{
				if (ViewModel.SelectedBlock != block)
				{
					ViewModel.SelectedBlock = block;
					UpdateSelectionIndicator();
				}
			}
		}

		// --- Gerenciamento Visual da Seleção ---

		private void UpdateSelectionIndicator()
		{
			if (_lastSelectedBlock != null)
			{
				var container = FindContainerForBlock(_lastSelectedBlock);
				if (container != null)
				{
					// Nota: O nome deve ser "SelectionIndicator", igual está no XAML
					var indicator = container.FindName("SelectionIndicator") as Microsoft.UI.Xaml.Shapes.Rectangle;
					if (indicator != null) indicator.Visibility = Visibility.Collapsed;
				}
			}

			if (ViewModel.SelectedBlock != null)
			{
				var container = FindContainerForBlock(ViewModel.SelectedBlock);
				if (container != null)
				{
					var indicator = container.FindName("SelectionIndicator") as Microsoft.UI.Xaml.Shapes.Rectangle;
					if (indicator != null) indicator.Visibility = Visibility.Visible;
				}
			}
			_lastSelectedBlock = ViewModel.SelectedBlock;
		}

		private FrameworkElement? FindContainerForBlock(CodeBlockItem block)
		{
			return FindContainerRecursive(MainScrollViewer, block);
		}

		private FrameworkElement? FindContainerRecursive(DependencyObject parent, CodeBlockItem block)
		{
			if (parent == null) return null;
			int childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);

			for (int i = 0; i < childrenCount; i++)
			{
				var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);

				if (child is FrameworkElement element && element.DataContext == block)
				{
					// Procuramos o Grid que contém os elementos visuais
					if (element is Grid) return element;
				}

				var result = FindContainerRecursive(child, block);
				if (result != null) return result;
			}
			return null;
		}

		// --- HANDLERS DO FOOTER (Estes faltavam e causavam o erro CS1061) ---

		private void OnSaveNewVersionRequested(object sender, EventArgs e)
		{
			if (ViewModel.SaveChangesCommand.CanExecute("NewVersion"))
			{
				ViewModel.SaveChangesCommand.Execute("NewVersion");
			}
		}

		private void OnSaveOverwriteRequested(object sender, EventArgs e)
		{
			if (ViewModel.SaveChangesCommand.CanExecute("Overwrite"))
			{
				ViewModel.SaveChangesCommand.Execute("Overwrite");
			}
		}

		private void OnRestoreOriginalRequested(object sender, EventArgs e)
		{
			ViewModel.TriggerGlobalRestore(0);
		}

		// Evento antigo do CodeViewer (CTRL+S no editor)
		private void OnSegmentSaveRequested(object sender, EventArgs e)
		{
			if (ViewModel.HasAnyUnsavedChanges)
			{
				// Atalho rápido -> Sobrescrever
				if (ViewModel.SaveChangesCommand.CanExecute("Overwrite"))
				{
					ViewModel.SaveChangesCommand.Execute("Overwrite");
				}
			}
		}
	}
}