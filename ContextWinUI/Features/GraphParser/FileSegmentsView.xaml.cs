using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media; // Importante para VisualTreeHelper
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
			// O sender é o Grid da coluna direita. O DataContext agora é a LINHA (Wrapper).
			if (sender is FrameworkElement element && element.DataContext is SegmentRowViewModel row)
			{
				// Extraímos o bloco atual da linha para definir a seleção
				ViewModel.SelectedBlock = row.Current;
				UpdateSelectionIndicator();
			}
		}

		private void EnsureBlockSelection(object sender)
		{
			// Usado quando o foco vem de dentro do editor de texto
			// Tenta subir a árvore visual para achar o DataContext da linha
			if (sender is FrameworkElement element)
			{
				// Sobe até achar o DataContext correto (SegmentRowViewModel) ou o CodeBlockItem direto (se estivesse bindado direto)
				// No nosso caso atual, o SegmentCodeViewer está dentro de um Grid com DataContext=SegmentRowViewModel

				// Hack seguro: Se o DataContext do sender for CodeBlockItem (as vezes acontece dependendo do binding)
				if (element.DataContext is CodeBlockItem block)
				{
					if (ViewModel.SelectedBlock != block)
					{
						ViewModel.SelectedBlock = block;
						UpdateSelectionIndicator();
					}
					return;
				}

				// Se não, tentamos achar a Row
				if (element.DataContext is SegmentRowViewModel row)
				{
					if (ViewModel.SelectedBlock != row.Current)
					{
						ViewModel.SelectedBlock = row.Current;
						UpdateSelectionIndicator();
					}
				}
			}
		}

		// --- Gerenciamento Visual da Seleção ---

		private void UpdateSelectionIndicator()
		{
			// 1. Apaga o indicador do antigo
			if (_lastSelectedBlock != null)
			{
				var container = FindContainerForBlock(_lastSelectedBlock);
				if (container != null)
				{
					var indicator = container.FindName("SelectionIndicator") as Microsoft.UI.Xaml.Shapes.Rectangle;
					if (indicator != null) indicator.Visibility = Visibility.Collapsed;
				}
			}

			// 2. Acende o indicador do novo
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
			// Procura dentro do MainScrollViewer (que contém o ItemsControl unificado)
			return FindContainerRecursive(MainScrollViewer, block);
		}

		private FrameworkElement? FindContainerRecursive(DependencyObject parent, CodeBlockItem targetBlock)
		{
			if (parent == null) return null;
			int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

			for (int i = 0; i < childrenCount; i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);

				// LÓGICA ATUALIZADA:
				// Verificamos se o elemento visual tem um DataContext do tipo Row
				// E se essa Row contém o Block que estamos procurando.
				if (child is FrameworkElement element && element.DataContext is SegmentRowViewModel row)
				{
					if (row.Current == targetBlock)
					{
						// Achamos o container da linha!
						// Verificamos se é o Grid principal do template (que contém o SelectionIndicator)
						if (element is Grid && element.FindName("SelectionIndicator") != null)
						{
							return element;
						}
					}
				}

				var result = FindContainerRecursive(child, targetBlock);
				if (result != null) return result;
			}
			return null;
		}

		// --- HANDLERS DO FOOTER ---

		private void OnSaveNewVersionRequested(object sender, EventArgs e)
		{
			// Clique principal do SplitButton ou Menu Item
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

		// --- Atalhos de Teclado (CTRL+S dentro do editor) ---
		private void OnSegmentSaveRequested(object sender, EventArgs e)
		{
			if (ViewModel.HasAnyUnsavedChanges)
			{
				if (ViewModel.SaveChangesCommand.CanExecute("Overwrite"))
				{
					ViewModel.SaveChangesCommand.Execute("Overwrite");
				}
			}
		}
	}
}