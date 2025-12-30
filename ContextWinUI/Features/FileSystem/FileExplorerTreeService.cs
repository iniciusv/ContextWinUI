using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using ContextWinUI.ViewModels.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.FileSystem;

public class FileExplorerTreeService : IFileExplorerTreeService
{
	private readonly FileExplorerTreeManager _manager = new();

	public void ExpandAll(IEnumerable<FileSystemItem> items) => _manager.ExpandAll(items);
	public void CollapseAll(IEnumerable<FileSystemItem> items) => _manager.CollapseAll(items);
	public void SyncFocus(IEnumerable<FileSystemItem> rootItems, FileSystemItem? selectedItem) => _manager.SyncFocus(rootItems, selectedItem);
}
