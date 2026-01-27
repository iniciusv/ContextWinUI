// ARQUIVO: IFileExplorerTreeService.cs
using ContextWinUI.Models;
using System.Collections.Generic;

namespace ContextWinUI.Core.Contracts;

public interface IFileExplorerTreeService
{
	void ExpandAll(IEnumerable<FileSystemItem> items);
	void CollapseAll(IEnumerable<FileSystemItem> items);
	void SyncFocus(IEnumerable<FileSystemItem> rootItems, FileSystemItem? selectedItem);
    FileSystemItem? FindItemByPath(IEnumerable<FileSystemItem> rootItems, string path);
}