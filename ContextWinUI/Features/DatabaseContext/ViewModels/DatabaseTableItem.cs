using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Models;

namespace ContextWinUI.Features.DatabaseContext.ViewModels;

public partial class DatabaseTableItem : ObservableObject
{
    public DatabaseSchemaDto Dto { get; }

    [ObservableProperty]
    private bool _isSelected;

    public string Name => Dto.TableName;
    public string Schema => Dto.Schema;

    public DatabaseTableItem(DatabaseSchemaDto dto, bool initialSelectionState)
    {
        Dto = dto;
        _isSelected = initialSelectionState;
    }
}
