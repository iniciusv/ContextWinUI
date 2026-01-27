using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.DatabaseContext.ViewModels;

public partial class DatabaseSelectionViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<DatabaseSchemaDto> _selectedForContext = new();

    [ObservableProperty]
    private int _selectedTablesCount;

    public DatabaseSelectionViewModel()
    {
        SelectedForContext.CollectionChanged += (s, e) =>
        {
            SelectedTablesCount = SelectedForContext.Count;
        };
    }

    public void AddTable(DatabaseSchemaDto table)
    {
        if (!SelectedForContext.Contains(table))
        {
            SelectedForContext.Add(table);
        }
    }

    public void RemoveTable(DatabaseSchemaDto table)
    {
        if (SelectedForContext.Contains(table))
        {
            SelectedForContext.Remove(table);
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        var sb = new StringBuilder();
        foreach (var table in SelectedForContext)
        {
            sb.AppendLine($"Table: {table.Schema}.{table.TableName}");
            sb.AppendLine("| Column | Type | Nullable |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (var col in table.Columns)
            {
                sb.AppendLine($"| {col.Name} | {col.DataType} | {col.IsNullable} |");
            }
            sb.AppendLine();
        }

        var package = new DataPackage();
        package.SetText(sb.ToString());
        Clipboard.SetContent(package);
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedForContext.Clear();
    }
}
