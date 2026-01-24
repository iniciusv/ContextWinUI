using ContextWinUI.Core.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Features.Session;

public class BlockSelectionService : IBlockSelectionManager
{
    // Maps FilePath -> Set of BlockIds
    private readonly Dictionary<string, HashSet<string>> _selectedBlocks = new();

    public void SelectBlock(string filePath, string blockId)
    {
        if (!_selectedBlocks.ContainsKey(filePath))
        {
            _selectedBlocks[filePath] = new HashSet<string>();
        }
        _selectedBlocks[filePath].Add(blockId);
    }

    public void DeselectBlock(string filePath, string blockId)
    {
        if (_selectedBlocks.ContainsKey(filePath))
        {
            _selectedBlocks[filePath].Remove(blockId);
            if (_selectedBlocks[filePath].Count == 0)
            {
                _selectedBlocks.Remove(filePath);
            }
        }
    }

    public bool IsBlockSelected(string filePath, string blockId)
    {
        return _selectedBlocks.ContainsKey(filePath) && _selectedBlocks[filePath].Contains(blockId);
    }

    public void ClearSelection(string filePath)
    {
        if (_selectedBlocks.ContainsKey(filePath))
        {
            _selectedBlocks.Remove(filePath);
        }
    }

    public void ClearAllSelections()
    {
        _selectedBlocks.Clear();
    }

    public IEnumerable<string> GetSelectedBlocks(string filePath)
    {
        if (_selectedBlocks.TryGetValue(filePath, out var blocks))
        {
            return blocks;
        }
        return Enumerable.Empty<string>();
    }

    public Dictionary<string, HashSet<string>> GetAllSelectedBlocks()
    {
        // Return a deep copy or just the reference?
        // Reference is fine for now as long as consumers don't mutate it directly without knowing.
        // Better to return a copy to avoid side effects.
        var copy = new Dictionary<string, HashSet<string>>();
        foreach (var kvp in _selectedBlocks)
        {
            copy[kvp.Key] = new HashSet<string>(kvp.Value);
        }
        return copy;
    }

    public void SetSelectionFromState(Dictionary<string, HashSet<string>> state)
    {
        _selectedBlocks.Clear();
        foreach (var kvp in state)
        {
            _selectedBlocks[kvp.Key] = new HashSet<string>(kvp.Value);
        }
    }
}
