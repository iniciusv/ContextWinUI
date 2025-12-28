using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Shared;

public class FileSelectionService : IFileSelectionService
{
	private FileSystemItem? _currentSelection;

	public FileSystemItem? CurrentSelection => _currentSelection;

	public event EventHandler<FileSystemItem?>? SelectionChanged;

	public void SetSelection(FileSystemItem? item)
	{
		if (_currentSelection != item)
		{
			_currentSelection = item;
			SelectionChanged?.Invoke(this, _currentSelection);
		}
	}
}