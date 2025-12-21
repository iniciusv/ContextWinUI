using ContextWinUI.Features.ContextBuilder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Views
{
	public sealed partial class ContextAnalysisView : UserControl
	{
		public static readonly DependencyProperty ContextViewModelProperty =
			DependencyProperty.Register(
				nameof(ContextViewModel),
				typeof(ContextAnalysisViewModel),
				typeof(ContextAnalysisView),
				new PropertyMetadata(null));

		public ContextAnalysisViewModel ContextViewModel
		{
			get => (ContextAnalysisViewModel)GetValue(ContextViewModelProperty);
			set => SetValue(ContextViewModelProperty, value);
		}

		public ContextAnalysisView()
		{
			this.InitializeComponent();
		}
	}
}