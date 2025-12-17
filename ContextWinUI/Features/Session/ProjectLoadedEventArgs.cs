using ContextWinUI.Models;
using System;
using System.Collections.ObjectModel;

namespace ContextWinUI.Services;

public class ProjectLoadedEventArgs : EventArgs
{
	public string RootPath { get; }
	public ObservableCollection<FileSystemItem> RootItems { get; }

	public ProjectLoadedEventArgs(string rootPath, ObservableCollection<FileSystemItem> rootItems)
	{
		RootPath = rootPath;
		RootItems = rootItems;
	}
}
