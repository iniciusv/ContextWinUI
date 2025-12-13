using ContextWinUI.Models;
using System.Collections.Generic;
using System.IO;

namespace ContextWinUI.Services;

public interface IFileSystemItemFactory
{
	FileSystemItem CreateWrapper(string fullPath, FileSystemItemType type, string? customIcon = null);
	FileSystemItem CreateWrapper(FileSystemInfo info);
	IEnumerable<FileSharedState> GetAllStates();
	void ClearCache();
}