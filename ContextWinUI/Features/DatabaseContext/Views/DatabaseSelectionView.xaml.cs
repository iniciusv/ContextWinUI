using ContextWinUI.Features.DatabaseContext.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Features.DatabaseContext.Views;

public sealed partial class DatabaseSelectionView : UserControl
{
    public DatabaseSelectionViewModel ViewModel
    {
        get => (DatabaseSelectionViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly Microsoft.UI.Xaml.DependencyProperty ViewModelProperty =
        Microsoft.UI.Xaml.DependencyProperty.Register("ViewModel", typeof(DatabaseSelectionViewModel), typeof(DatabaseSelectionView), new Microsoft.UI.Xaml.PropertyMetadata(null));

    public DatabaseSelectionView()
    {
        this.InitializeComponent();
    }
}
