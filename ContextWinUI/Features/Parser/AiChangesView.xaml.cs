using ColorCode.Styling;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Views;

public sealed partial class AiChangesView : UserControl
{
	// Serviços (idealmente injetados, aqui instanciados por brevidade)
	private readonly RoslynHighlightService _highlightService = new();
	//private readonly TextDiffService _diffService = new();
	private readonly RoslynSemanticDiffService _diffService = new(); // <--- Mudou de TextDiffService para RoslynSemanticDiffService

	// Controle de Debounce para não travar UI
	private CancellationTokenSource? _renderCts;

	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(nameof(ViewModel), typeof(AiChangesViewModel), typeof(AiChangesView), new PropertyMetadata(null, OnViewModelChanged));

	public AiChangesViewModel ViewModel
	{
		get => (AiChangesViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}


	public AiChangesView()
	{
		this.InitializeComponent();
	}



	private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var view = (AiChangesView)d;
		if (e.NewValue is AiChangesViewModel vm)
		{
			// Assina evento de mudança de seleção para atualizar o editor
			vm.PropertyChanged += view.OnViewModelPropertyChanged;
		}
		if (e.OldValue is AiChangesViewModel oldVm)
		{
			oldVm.PropertyChanged -= view.OnViewModelPropertyChanged;
		}
	}

	private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AiChangesViewModel.SelectedChange))
		{
			// Dispara a geração do Diff no ViewModel
			if (ViewModel != null)
			{
				await ViewModel.GenerateDiffForSelectedAsync();
			}
		}
	}

	public static Color GetBackgroundColor(DiffType type)
	{
		// Como é estático, usamos Application.Current para saber o tema
		bool isDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;

		return type switch
		{
			// Cores translúcidas: (Alpha, R, G, B)
			DiffType.Added => isDark ? Color.FromArgb(40, 46, 160, 67) : Color.FromArgb(40, 0, 255, 0),
			DiffType.Deleted => isDark ? Color.FromArgb(40, 200, 50, 50) : Color.FromArgb(40, 255, 0, 0),
			_ => Colors.Transparent
		};
	}

	// Adicione 'static' na assinatura
	public static Brush GetForegroundColor(DiffType type)
	{
		// Retorna branco para legibilidade geral (ou ajuste conforme necessário)
		return new SolidColorBrush(Colors.White);
	}
}