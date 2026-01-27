using ContextWinUI.Features.DatabaseContext.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Features.DatabaseContext.Views;

public sealed partial class DatabaseExplorerView : UserControl
{
    public DatabaseExplorerViewModel ViewModel
    {
        get => (DatabaseExplorerViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly Microsoft.UI.Xaml.DependencyProperty ViewModelProperty =
        Microsoft.UI.Xaml.DependencyProperty.Register("ViewModel", typeof(DatabaseExplorerViewModel), typeof(DatabaseExplorerView), new Microsoft.UI.Xaml.PropertyMetadata(null));

    public DatabaseExplorerView()
    {
        this.InitializeComponent();
    }
}
