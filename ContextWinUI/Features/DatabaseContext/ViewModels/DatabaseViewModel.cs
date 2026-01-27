using CommunityToolkit.Mvvm.ComponentModel;

namespace ContextWinUI.Features.DatabaseContext.ViewModels;

public class DatabaseViewModel : ObservableObject
{
    public DatabaseExplorerViewModel Explorer { get; }
    public DatabaseSchemaDataViewModel SchemaViewer { get; }
    public DatabaseSelectionViewModel Selection { get; }

    public DatabaseViewModel(
        DatabaseExplorerViewModel explorer, 
        DatabaseSchemaDataViewModel schemaViewer, 
        DatabaseSelectionViewModel selection)
    {
        Explorer = explorer;
        SchemaViewer = schemaViewer;
        Selection = selection;
    }
}
