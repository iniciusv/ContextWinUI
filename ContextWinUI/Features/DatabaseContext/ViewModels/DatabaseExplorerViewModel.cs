using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.DatabaseContext.ViewModels;

public partial class DatabaseExplorerViewModel : ObservableObject
{
    private readonly IDatabaseService _dbService;
    public DatabaseSelectionViewModel SelectionViewModel { get; }
    private readonly DatabaseSchemaDataViewModel _schemaDataViewModel;
    private readonly IProjectSessionManager _sessionManager;

    [ObservableProperty]
    private ObservableCollection<DatabaseConnectionDto> _connections = new();

    [ObservableProperty]
    private DatabaseConnectionDto? _selectedConnection;

    [ObservableProperty]
    private ObservableCollection<string> _schemas = new();

    [ObservableProperty]
    private string? _selectedSchema;

    [ObservableProperty]
    private ObservableCollection<DatabaseTableItem> _tables = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    public DatabaseExplorerViewModel(
        IDatabaseService dbService,
        IProjectSessionManager sessionManager,
        DatabaseSelectionViewModel selectionViewModel,
        DatabaseSchemaDataViewModel schemaDataViewModel)
    {
        _dbService = dbService;
        _sessionManager = sessionManager;
        SelectionViewModel = selectionViewModel;
        _schemaDataViewModel = schemaDataViewModel;
        
        SelectionViewModel.SelectedForContext.CollectionChanged += OnSelectionChanged;

        // Load connections from current session
        LoadConnections();
    }

    private void OnSelectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Sync visible tables with selection state
        foreach (var item in Tables)
        {
            var isSelected = SelectionViewModel.SelectedForContext.Contains(item.Dto);
            if (item.IsSelected != isSelected)
            {
                item.IsSelected = isSelected;
            }
        }
    }

    private void LoadConnections()
    {
        if (_sessionManager?.CurrentState?.DatabaseConnections != null)
        {
            Connections = new ObservableCollection<DatabaseConnectionDto>(_sessionManager.CurrentState.DatabaseConnections);
        }
    }

    partial void OnSelectedConnectionChanged(DatabaseConnectionDto? value)
    {
        if (value != null)
        {
            _schemaDataViewModel.SetConnection(value.ConnectionString);
            
            // Populate Schemas/Tables from cache if available, or just clear.
            if (value.CachedSchemas != null && value.CachedSchemas.Any())
            {
                PopulateTables(value.CachedSchemas);
            }
            else
            {
                Tables.Clear();
                Schemas.Clear();
            }
        }
    }

    partial void OnSelectedSchemaChanged(string? value)
    {
        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private async Task RefreshConnectionAsync()
    {
        if (SelectedConnection == null) return;

        try
        {
            var schemas = await _dbService.GetSchemaAsync(SelectedConnection.ConnectionString);
            SelectedConnection.CachedSchemas = schemas;
            
            PopulateTables(schemas);
        }
        catch (Exception)
        {
            // Handle error
        }
    }

    [RelayCommand]
    private void AddConnection()
    {
        var newConn = new DatabaseConnectionDto { Name = "New Connection", ConnectionString = "Server=...;Database=...;" };
        Connections.Add(newConn);
        _sessionManager.CurrentState.DatabaseConnections.Add(newConn);
        SelectedConnection = newConn;
    }
    
    [RelayCommand]
    private void SelectTable(DatabaseTableItem item)
    {
        _schemaDataViewModel.LoadTableAsync(item.Dto);
    }

    [RelayCommand]
    private void Search(string query)
    {
        FilterText = query;
    }

    [RelayCommand]
    private void SubmitSearch(string query)
    {
        FilterText = query;
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var table in Tables)
        {
            table.IsSelected = true;
        }
    }

    [RelayCommand]
    private void UnselectAll()
    {
        foreach (var table in Tables)
        {
            table.IsSelected = false;
        }
    }

    [RelayCommand]
    private void ToggleTableSelection(DatabaseTableItem item)
    {
        item.IsSelected = !item.IsSelected;
    }

    private void PopulateTables(System.Collections.Generic.List<DatabaseSchemaDto> allTables)
    {
        var schemaNames = allTables.Select(t => t.Schema).Distinct().OrderBy(s => s).ToList();
        Schemas = new ObservableCollection<string>(schemaNames);

        // Default to first schema or 'dbo'
        SelectedSchema = schemaNames.Contains("dbo") ? "dbo" : schemaNames.FirstOrDefault();
        
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (SelectedConnection?.CachedSchemas == null) return;

        var query = SelectedConnection.CachedSchemas.AsEnumerable();

        if (!string.IsNullOrEmpty(SelectedSchema))
        {
            query = query.Where(t => t.Schema == SelectedSchema);
        }

        if (!string.IsNullOrEmpty(FilterText))
        {
            query = query.Where(t => t.TableName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        var wrappers = query.Select(dto => 
        {
            var item = new DatabaseTableItem(dto, SelectionViewModel.SelectedForContext.Contains(dto));
            // When user checks box on UI, update SelectionViewModel
            item.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(DatabaseTableItem.IsSelected))
                {
                    if (item.IsSelected) SelectionViewModel.AddTable(item.Dto);
                    else SelectionViewModel.RemoveTable(item.Dto);
                }
            };
            return item;
        }).ToList();

        Tables = new ObservableCollection<DatabaseTableItem>(wrappers);
    }
}
