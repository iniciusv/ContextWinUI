using ContextWinUI.Models;
using System.Collections.Generic;

namespace ContextWinUI.Core.Contracts;

public interface IFileSystemItem
{
	string Name { get; }
	string FullPath { get; }
	FileSystemItemType Type { get; }
	bool IsCodeFile { get; }

	IEnumerable<IFileSystemItem> ChildrenItems { get; }

	IFileSharedState SharedStateInfo { get; }
}