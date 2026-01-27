using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Features.DatabaseContext.ViewModels;

public partial class DatabaseSchemaDataViewModel : ObservableObject
{
    private readonly IDatabaseService _dbService;
    private string _currentConnectionString;

    [ObservableProperty]
    private DatabaseSchemaDto? _selectedTable;

    [ObservableProperty]
    private ObservableCollection<DatabaseColumnDto> _columns = new();

    // Using List<Dictionary<string, object>> for simple data binding in WinUI DataGrid
    [ObservableProperty]
    private ObservableCollection<Dictionary<string, object>> _dataSamples = new();

    [ObservableProperty]
    private bool _isLoadingData;

    [ObservableProperty]
    private string _statusMessage = "Select a table to view schema and data.";

    public DatabaseSchemaDataViewModel(IDatabaseService dbService)
    {
        _dbService = dbService;
    }

    public void SetConnection(string connectionString)
    {
        _currentConnectionString = connectionString;
    }

    public async Task LoadTableAsync(DatabaseSchemaDto table)
    {
        SelectedTable = table;
        Columns = new ObservableCollection<DatabaseColumnDto>(table.Columns);
        
        if (string.IsNullOrEmpty(_currentConnectionString))
        {
             StatusMessage = "No active connection.";
             DataSamples.Clear();
             return;
        }

        try
        {
            IsLoadingData = true;
            StatusMessage = $"Loading data for {table.TableName}...";
            DataSamples.Clear();

            // Very simple SELECT TOP 50
            var query = $"SELECT TOP 50 * FROM [{table.Schema}].[{table.TableName}]";
            
            var data = await _dbService.GetRawDataAsync(_currentConnectionString, query);
            
            foreach (var row in data)
            {
                DataSamples.Add(row);
            }

            StatusMessage = $"Loaded {DataSamples.Count} rows.";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            IsLoadingData = false;
        }
    }
}
