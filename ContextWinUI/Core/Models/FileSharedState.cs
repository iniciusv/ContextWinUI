using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace ContextWinUI.Models;

public partial class FileSharedState : ObservableObject, IFileSharedState
{
	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private bool isChecked;

	[ObservableProperty]
	private bool isIgnored;

	[ObservableProperty]
	private long? fileSize;

	[ObservableProperty]
	private ObservableCollection<string> tags = new();

	public string? ContentCache { get; set; }

	public string Extension => Path.GetExtension(FullPath);

    IEnumerable<string> IFileSharedState.Tags => Tags;

    public FileSharedState(string fullPath)
	{
		FullPath = fullPath;
		Name = Path.GetFileName(fullPath);
	}
}