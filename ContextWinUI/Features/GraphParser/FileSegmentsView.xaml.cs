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

		// Adicione esta propriedade para acessar o ViewModel
		public FileSegmentsViewModel ViewModel => (FileSegmentsViewModel)DataContext;

		public FileSegmentsView()
		{
			this.InitializeComponent();
			this.DataContextChanged += (s, e) => Bindings.Update();
			this.Loaded += OnLoaded;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			// Força a atualização da UI quando carregado
			UpdateSelectionIndicator();
		}

		private void OnBlockPointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if (sender is FrameworkElement element && element.DataContext is CodeBlockItem block)
			{
				// Atualiza a seleção no ViewModel
				ViewModel.SelectedBlock = block;

				// Atualiza indicador visual
				UpdateSelectionIndicator();
			}
		}

		private void UpdateSelectionIndicator()
		{
			// Remove seleção anterior
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

			// Adiciona seleção atual
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
			// Procura recursivamente pelo container que tem o bloco
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
			if (ViewModel.SelectedBlock != null)
			{
				ViewModel.SelectedBlock.RestoreVersion(versionIndex);
			}
		}

		private void OnSaveVersionRequested(object sender, EventArgs e)
		{
			if (ViewModel.SelectedBlock != null && ViewModel.SelectedBlock.HasUnsavedChanges)
			{
				ViewModel.SelectedBlock.CreateNewVersion(
					ViewModel.SelectedBlock.Content,
					$"Versão salva {DateTime.Now:HH:mm:ss}"
				);
			}
		}

		private void OnRestoreOriginalRequested(object sender, EventArgs e)
		{
			if (ViewModel.SelectedBlock != null)
			{
				ViewModel.SelectedBlock.RestoreOriginal();
			}
		}

		private void OnSegmentSaveRequested(object sender, EventArgs e)
		{
			if (sender is SegmentCodeViewer viewer && ViewModel.SelectedBlock != null)
			{
				// Cria nova versão automaticamente quando o usuário salva
				ViewModel.SelectedBlock.CreateNewVersion(
					viewer.Text,
					"Salvo pelo usuário"
				);
			}
		}
	}
}