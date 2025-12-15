using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;

namespace ContextWinUI.Models;

public partial class FileSharedState : ObservableObject
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

	public FileSharedState(string fullPath)
	{
		FullPath = fullPath;
		Name = Path.GetFileName(fullPath);
	}
}