using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Features.GraphParser.Views.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.GraphParser;

public sealed partial class FileSegmentsView : UserControl
{
	private CodeBlockItem? _lastSelectedBlock;

	private SegmentRowViewModel? _draggedItem;

	// Propriedade tipada para facilitar o acesso ao ViewModel no code-behind
	public FileSegmentsViewModel ViewModel => (FileSegmentsViewModel)DataContext;

	public FileSegmentsView()
	{
		this.InitializeComponent();

		this.DataContextChanged += (s, e) =>
		{
			Bindings.Update(); // <--- O PULO DO GATO
			UpdateSelectionIndicator();
		};

		this.Loaded += (s, e) => UpdateSelectionIndicator();
	}

	// --- Tratamento de Eventos de UI ---

	/// <summary>
	/// Chamado quando o usuário clica em qualquer parte de uma linha (Bloco)
	/// </summary>
	private void OnBlockPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (sender is FrameworkElement element && element.DataContext is SegmentRowViewModel row)
		{
			SetSelectedBlock(row.Current);
		}
	}

	/// <summary>
	/// Chamado quando o conteúdo do editor de código é modificado (digitação)
	/// </summary>
	private void OnSegmentContentModified(object sender, EventArgs e)
	{
		// Se o usuário está digitando, este bloco deve ser o selecionado
		EnsureBlockSelectionFromSender(sender);

		// Notifica o VM para atualizar estados (ex: mostrar asterisco de não salvo)
		ViewModel.NotifyUnsavedChanges();
	}

	/// <summary>
	/// Chamado quando o editor solicita salvamento (ex: Ctrl+S dentro do editor)
	/// </summary>
	private void OnSegmentSaveRequested(object sender, EventArgs e)
	{
		EnsureBlockSelectionFromSender(sender);

		// Executa o comando de salvar (Modo Overwrite padrão para Ctrl+S)
		if (ViewModel.SaveChangesCommand.CanExecute("Overwrite"))
		{
			ViewModel.SaveChangesCommand.Execute("Overwrite");
		}
	}

	// --- Lógica de Seleção Visual ---

	private void EnsureBlockSelectionFromSender(object sender)
	{
		if (sender is FrameworkElement element)
		{
			// Tenta encontrar o bloco associado ao elemento que disparou o evento
			// O DataContext pode ser o próprio CodeBlockItem (dentro do editor) 
			// ou o SegmentRowViewModel (container da linha)

			if (element.DataContext is CodeBlockItem block)
			{
				SetSelectedBlock(block);
			}
			else if (element.DataContext is SegmentRowViewModel row)
			{
				SetSelectedBlock(row.Current);
			}
		}
	}

	private void SetSelectedBlock(CodeBlockItem block)
	{
		if (ViewModel.SelectedBlock != block)
		{
			ViewModel.SelectedBlock = block;
			UpdateSelectionIndicator();
		}
	}

	/// <summary>
	/// Atualiza a barra azul lateral (SelectionIndicator) percorrendo a árvore visual.
	/// Isso é necessário porque o indicador visual é um elemento de UI, não um estado de dados.
	/// </summary>
	private void UpdateSelectionIndicator()
	{
		if (ViewModel == null) return;

		// 1. Ocultar indicador do bloco anteriormente selecionado
		if (_lastSelectedBlock != null)
		{
			ToggleIndicatorVisibility(_lastSelectedBlock, Visibility.Collapsed);
		}

		// 2. Mostrar indicador no novo bloco selecionado
		if (ViewModel.SelectedBlock != null)
		{
			ToggleIndicatorVisibility(ViewModel.SelectedBlock, Visibility.Visible);
			_lastSelectedBlock = ViewModel.SelectedBlock;
		}
	}

	private void ToggleIndicatorVisibility(CodeBlockItem block, Visibility visibility)
	{
		var container = FindContainerForBlock(block);
		if (container != null)
		{
			// "SelectionIndicator" é o nome do Rectangle definido no XAML
			if (container.FindName("SelectionIndicator") is Microsoft.UI.Xaml.Shapes.Rectangle indicator)
			{
				indicator.Visibility = visibility;
			}
		}
	}
	// Em FileSegmentsView.xaml.cs

	private FrameworkElement? FindContainerForBlock(CodeBlockItem block)
	{
		// Verifica se a View e o ViewModel estão prontos
		if (MainListView == null || ViewModel == null) return null;

		// 1. Encontrar o ViewModel da linha (Wrapper) que contém o bloco desejado
		var rowViewModel = ViewModel.Rows.FirstOrDefault(r => r.Current == block);

		if (rowViewModel == null) return null;

		// 2. Usar o método nativo do ListView para pegar o elemento visual (ListViewItem)
		var container = MainListView.ContainerFromItem(rowViewModel) as FrameworkElement;

		// 3. Se o container ainda não foi gerado (virtualização), retorna null
		if (container == null) return null;

		// 4. Precisamos descer na árvore visual do ListViewItem para achar o Grid com o indicador
		// O ListViewItem contém o ContentPresenter, que contém o nosso DataTemplate (Grid)
		return FindElementInVisualTree<Grid>(container, "SelectionIndicator")?.Parent as FrameworkElement;
	}

	// Método auxiliar genérico mais seguro para achar elemento por nome
	private T? FindElementInVisualTree<T>(DependencyObject parent, string name) where T : FrameworkElement
	{
		int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);

			if (child is T element && element.Name == name)
				return element;

			var result = FindElementInVisualTree<T>(child, name);
			if (result != null)
				return result;
		}
		return null;
	}

	private void OnEditorCaretPositionChanged(object sender, int cursorPosition)
	{
		// 1. Garante que sabemos qual bloco estamos editando
		EnsureBlockSelectionFromSender(sender);

		// 2. Chama o método no ViewModel que usa o Serviço
		// O ViewModel vai usar o SelectedBlock atual + a posição do cursor
		ViewModel.ResolveSymbolHeuristic(cursorPosition);
	}

	// 1. INÍCIO: Disparado pela ListView quando você começa a arrastar
	// IMPORTANTE: Adicione DragItemsStarting="OnDragItemsStarting" no XAML da ListView
	private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
	{
		// Limpa estado anterior por segurança
		_draggedItem = null;

		if (e.Items.Count > 0)
		{
			// Captura o objeto real que está sendo arrastado
			_draggedItem = e.Items[0] as SegmentRowViewModel;

			// Define a operação permitida
			e.Data.RequestedOperation = DataPackageOperation.Move;
		}
	}

	// 2. DURANTE: Disparado pelo Grid do ItemTemplate enquanto o mouse passa por cima
	private void OnItemDragOver(object sender, DragEventArgs e)
	{
		// Se não sabemos o que está sendo arrastado, cancela.
		if (_draggedItem == null)
		{
			e.AcceptedOperation = DataPackageOperation.None;
			e.Handled = true;
			return;
		}

		// Tenta identificar o alvo (onde o mouse está agora)
		if (sender is FrameworkElement targetElement &&
			targetElement.DataContext is SegmentRowViewModel targetRow)
		{
			// BLOQUEIO: Não permitir soltar o item nele mesmo
			if (targetRow == _draggedItem)
			{
				e.AcceptedOperation = DataPackageOperation.None;
				e.Handled = true;
				return;
			}

			// SUCESSO: É um alvo válido
			e.AcceptedOperation = DataPackageOperation.Move;

			// Feedback Visual Customizado (opcional, mas recomendado)
			if (e.DragUIOverride != null)
			{
				e.DragUIOverride.Caption = $"Vincular a '{targetRow.Current.Name}'";
				e.DragUIOverride.IsCaptionVisible = true;
				e.DragUIOverride.IsContentVisible = false; // Esconde o "fantasma" do item arrastado para limpar a visão
				e.DragUIOverride.IsGlyphVisible = true;
			}
		}
		else
		{
			e.AcceptedOperation = DataPackageOperation.None;
		}

		e.Handled = true;
	}

	// 3. FINAL: Disparado quando o usuário solta o botão do mouse
	private void OnItemDrop(object sender, DragEventArgs e)
	{
		try
		{
			if (_draggedItem != null &&
				sender is FrameworkElement targetElement &&
				targetElement.DataContext is SegmentRowViewModel targetRow)
			{
				// Validação final para garantir que não é o mesmo item
				if (targetRow != _draggedItem)
				{
					// Prepara os argumentos (Origem -> Destino)
					var args = new Tuple<CodeBlockItem, CodeBlockItem>(_draggedItem.Current, targetRow.Current);

					// Executa o comando no ViewModel
					if (ViewModel.LinkBlocksCommand.CanExecute(args))
					{
						ViewModel.LinkBlocksCommand.Execute(args);
					}
				}
			}
		}
		finally
		{
			// LIMPEZA CRÍTICA: Sempre limpe a variável ao terminar
			_draggedItem = null;
			e.Handled = true;
		}
	}
}