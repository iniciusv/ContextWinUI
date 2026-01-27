using ContextWinUI.Features.DatabaseContext.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System; // Required for await IAsyncOperation

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
    private async void AddConnection_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var services = ((App)App.Current).Services;
        var dbService = services.GetService(typeof(ContextWinUI.Core.Contracts.IDatabaseService)) as ContextWinUI.Core.Contracts.IDatabaseService;
        if (dbService == null) return;

        var dialog = new AddConnectionDialog(dbService);
        dialog.XamlRoot = this.XamlRoot;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newConnection = dialog.GetResult();
            ViewModel.AddConnection(newConnection);
        }
    }
}
