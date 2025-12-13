using ContextWinUI.Models;
using System.IO;

namespace ContextWinUI.Services;

public interface IFileSystemItemFactory
{
	FileSystemItem CreateWrapper(string fullPath, FileSystemItemType type, string? customIcon = null);
	FileSystemItem CreateWrapper(FileSystemInfo info);
	void ClearCache();
}