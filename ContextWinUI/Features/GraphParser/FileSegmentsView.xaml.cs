using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using System;
using System.Linq;

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

		private void OnBlockPointerPressed(object sender, PointerRoutedEventArgs e)
		{
			// Define o bloco selecionado ao clicar na linha
			if ((sender as FrameworkElement)?.DataContext is SegmentRowViewModel row && ViewModel != null)
			{
				ViewModel.SelectedBlock = row.Current;
			}
		}

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
			// Tenta resolver o símbolo onde o cursor está
			ViewModel?.ResolveSymbolHeuristic(cursorPosition);
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

		#endregion
	}
}