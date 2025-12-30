// ARQUIVO: IFileExplorerSearchService.cs
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;
public interface IFileExplorerDataService : IDisposable
{
	ObservableCollection<FileSystemItem> RootItems { get; }
	ObservableCollection<string> AllProjectTags { get; }
	string CurrentPath { get; }

	event EventHandler<string>? StatusChanged;

	Task LoadProjectAsync();
	void Initialize(ObservableCollection<FileSystemItem> items, string path);
	void RefreshTags();

	void SetSelectionState(IEnumerable<FileSystemItem> items, bool isChecked);
}