using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ContextWinUI.Features.DatabaseContext.Views;

public sealed partial class AddConnectionDialog : ContentDialog, INotifyPropertyChanged
{
    private readonly IDatabaseService _dbService;

    public string ConnectionName { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));
            }
        }
    }

    private SolidColorBrush _statusBrush = new SolidColorBrush(Colors.Gray);
    public SolidColorBrush StatusBrush
    {
        get => _statusBrush;
        set
        {
            if (_statusBrush != value)
            {
                _statusBrush = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusBrush)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AddConnectionDialog(IDatabaseService dbService)
    {
        this.InitializeComponent();
        _dbService = dbService;
        this.PrimaryButtonClick += AddConnectionDialog_PrimaryButtonClick;
    }

    private void AddConnectionDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(ConnectionName) || string.IsNullOrWhiteSpace(ConnectionString))
        {
            StatusMessage = "Please fill in all fields.";
            StatusBrush = new SolidColorBrush(Colors.Red);
            args.Cancel = true;
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            StatusMessage = "Enter a connection string first.";
            StatusBrush = new SolidColorBrush(Colors.Orange);
            return;
        }

        StatusMessage = "Testing...";
        StatusBrush = new SolidColorBrush(Colors.Blue);

        try
        {
            bool success = await _dbService.TestConnectionAsync(ConnectionString);
            if (success)
            {
                StatusMessage = "Connection Successful!";
                StatusBrush = new SolidColorBrush(Colors.Green);
            }
            else
            {
                StatusMessage = "Connection Failed.";
                StatusBrush = new SolidColorBrush(Colors.Red);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            StatusBrush = new SolidColorBrush(Colors.Red);
        }
    }

    public DatabaseConnectionDto GetResult()
    {
        return new DatabaseConnectionDto
        {
            Name = ConnectionName,
            ConnectionString = ConnectionString
        };
    }
}
