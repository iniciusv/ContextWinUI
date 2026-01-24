using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Features.GraphParser.Views.Components;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.GraphParser
{
	public sealed partial class FileSegmentsView : UserControl
	{
		public FileSegmentsViewModel ViewModel
		{
			get => (FileSegmentsViewModel)GetValue(ViewModelProperty);
			set => SetValue(ViewModelProperty, value);
		}

		public static readonly DependencyProperty ViewModelProperty =
			DependencyProperty.Register(nameof(ViewModel), typeof(FileSegmentsViewModel), typeof(FileSegmentsView), new PropertyMetadata(null));

		public FileSegmentsView()
		{
			this.InitializeComponent();
			this.DataContextChanged += (s, e) =>
			{
				if (DataContext is FileSegmentsViewModel vm)
				{
					ViewModel = vm;
				}
			};
		}

		#region Drag and Drop Logic

		private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
		{
			// Permite arrastar apenas um item por vez para simplificar a reordenação
			if (e.Items.Count > 0)
			{
				e.Data.SetText("SegmentRowMove");
				e.Data.RequestedOperation = DataPackageOperation.Move;
				// Armazena o item sendo arrastado internamente se necessário, 
				// mas geralmente o ListView lida com a coleção se for ObservableCollection
			}
		}

		private void OnItemDragOver(object sender, DragEventArgs e)
		{
			// Aceita o drop se for operação de Move interna
			if (e.DataView.Contains(StandardDataFormats.Text))
			{
				e.AcceptedOperation = DataPackageOperation.Move;
				e.DragUIOverride.Caption = "Reordenar";
			}
		}

		private void OnItemDrop(object sender, DragEventArgs e)
		{
			// Lógica simples de reordenação visual via ViewModel
			// Nota: Uma implementação completa de reordenação requereria
			// calcular o índice de destino baseado na posição Y do mouse sobre o item alvo.
			// Devido à complexidade, recomenda-se que a ViewModel exponha um método "Move(source, targetIndex)".

			// Aqui estamos apenas marcando como handled para não quebrar a UI
			e.Handled = true;
		}

		#endregion

		#region Editor Interaction


		private void OnSegmentSaveRequested(object sender, EventArgs e)
		{
			// Atalho Ctrl+S dentro do editor dispara salvamento
			if (ViewModel != null)
			{
				ViewModel.CommitAllPendingChanges();
			}
		}

		private void OnSegmentContentModified(object sender, EventArgs e)
		{
			// Notifica a ViewModel que houve alteração de texto
			ViewModel?.NotifyChangesChanged();
		}

		private void OnEditorCaretPositionChanged(object sender, int cursorPosition)
		{
			// 1. Recupera o controle que disparou o evento
			if (sender is SegmentCodeViewer editor &&
				editor.DataContext is SegmentRowViewModel row &&
				ViewModel != null)
			{
				// 2. FORÇA a atualização do SelectedBlock para o bloco onde o cursor está
				// Isso garante que o ViewModel saiba qual é o "Current Block" correto
				if (ViewModel.SelectedBlock != row.Current)
				{
					ViewModel.SelectedBlock = row.Current;
				}

				// 3. Agora chama a heurística, que usará o SelectedBlock.Content correto
				ViewModel.ResolveSymbolHeuristic(cursorPosition);
			}
		}

		#endregion

		#region Version Footer Events

		private void OnFooterSaveNewVersion(object sender, EventArgs e)
		{
			ViewModel?.SaveChanges("NewVersion");
		}

		private void OnFooterSaveOverwrite(object sender, EventArgs e)
		{
			ViewModel?.SaveChanges("Overwrite");
		}

		private void OnFooterRestoreOriginal(object sender, EventArgs e)
		{
			ViewModel?.RestoreToGlobalIndex(0);
		}

		private async void OnFooterSaveToFile(object sender, EventArgs e)
		{
			if (ViewModel != null)
			{
				await ViewModel.SaveToDiskAsync();
			}
		}

		#endregion
		private void OnMetadataPointerEntered(object sender, PointerRoutedEventArgs e)
		{
			// TRUQUE: Ao invés de tentar mudar o cursor do 'sender' (que é protegido),
			// mudamos o cursor da própria classe 'FileSegmentsView' (this).
			// Como FileSegmentsView herda de UIElement, ela tem acesso à SUA PRÓPRIA propriedade protegida.
			this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
		}

		private void OnMetadataPointerExited(object sender, PointerRoutedEventArgs e)
		{
			// Quando o mouse sai da área azul, voltamos o cursor da View para o padrão (null = seta).
			this.ProtectedCursor = null;
		}

		// Seu método de clique existente continua igual
		private void OnBlockPointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if ((sender as FrameworkElement)?.DataContext is SegmentRowViewModel row && ViewModel != null)
			{
				ViewModel.SelectedBlock = row.Current;

				// Alterna a seleção (Toggle)
				row.Current.IsSelected = !row.Current.IsSelected;
			}
		}
	}
}