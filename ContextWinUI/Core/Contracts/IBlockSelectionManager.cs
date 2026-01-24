using System.Collections.Generic;

namespace ContextWinUI.Core.Contracts;

public interface IBlockSelectionManager
{
    void SelectBlock(string filePath, string blockId);
    void DeselectBlock(string filePath, string blockId);
    bool IsBlockSelected(string filePath, string blockId);
    void ClearSelection(string filePath);
    void ClearAllSelections();
    IEnumerable<string> GetSelectedBlocks(string filePath);
    Dictionary<string, HashSet<string>> GetAllSelectedBlocks();
    void SetSelectionFromState(Dictionary<string, HashSet<string>> state);
}
