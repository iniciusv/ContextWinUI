using ContextWinUI.Features.DatabaseContext.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Features.DatabaseContext.Views;

public sealed partial class DatabaseSchemaDataView : UserControl
{
    public DatabaseSchemaDataViewModel ViewModel
    {
        get => (DatabaseSchemaDataViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly Microsoft.UI.Xaml.DependencyProperty ViewModelProperty =
        Microsoft.UI.Xaml.DependencyProperty.Register("ViewModel", typeof(DatabaseSchemaDataViewModel), typeof(DatabaseSchemaDataView), new Microsoft.UI.Xaml.PropertyMetadata(null));

    public DatabaseSchemaDataView()
    {
        this.InitializeComponent();
    }
}
