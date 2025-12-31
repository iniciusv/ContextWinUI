// ARQUIVO: FileSegmentsView.xaml.cs (CORRIGIDO)
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Features.GraphParser.Views.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using Windows.UI.Xaml;

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

		private void UpdateSelectionIndicator()
		{
			if (_lastSelectedBlock != null)
			{
				var container = FindContainerForBlock(_lastSelectedBlock);
				if (container != null)
				{
					var indicator = container.FindName("SelectionIndicator") as Rectangle;
					if (indicator != null)
						indicator.Visibility = Visibility.Collapsed;
				}
			}

			if (ViewModel.SelectedBlock != null)
			{
				var container = FindContainerForBlock(ViewModel.SelectedBlock);
				if (container != null)
				{
					var indicator = container.FindName("SelectionIndicator") as Rectangle;
					if (indicator != null)
						indicator.Visibility = Visibility.Visible;
				}
			}

			_lastSelectedBlock = ViewModel.SelectedBlock;
		}

		private FrameworkElement? FindContainerForBlock(CodeBlockItem block)
		{
			return FindContainerRecursive(MainScrollViewer.Content as FrameworkElement, block);
		}

		private FrameworkElement? FindContainerRecursive(DependencyObject parent, CodeBlockItem block)
		{
			if (parent == null) return null;

			int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < childrenCount; i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);

				if (child is FrameworkElement element && element.DataContext == block)
				{
					return element;
				}

				var result = FindContainerRecursive(child, block);
				if (result != null)
					return result;
			}

			return null;
		}

		private void OnVersionRestoreRequested(object sender, int versionIndex)
		{
	
			ViewModel.TriggerGlobalRestore(versionIndex);
		}

		// ALTERADO: Ao pedir salvar, pede Globalmente
		private void OnSaveVersionRequested(object sender, EventArgs e)
		{
			// Em vez de salvar só este bloco, disparar o Save All
			ViewModel.TriggerGlobalSave();

			// Nota: O método CommitAllPendingChanges do pai vai cuidar de criar 
			// a versão nova neste bloco e em todos os outros modificados.
		}


		private void OnRestoreOriginalRequested(object sender, EventArgs e)
		{
			ViewModel.TriggerGlobalRestore(0); // Assume que 0 é sempre o original globalmente
		}

		private void OnSegmentSaveRequested(object sender, EventArgs e)
		{
			if (ViewModel.HasAnyUnsavedChanges)
			{
				ViewModel.CommitGlobalVersion("Salvo pelo Editor");
			}
		}
	}
}