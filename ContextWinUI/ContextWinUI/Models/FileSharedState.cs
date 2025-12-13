using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;

namespace ContextWinUI.Models;

/// <summary>
/// FLYWEIGHT: Representa o estado intrínseco e compartilhado de um arquivo.
/// Existe apenas UMA instância desta classe por caminho de arquivo em toda a aplicação.
/// </summary>
public partial class FileSharedState : ObservableObject
{
	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private bool isChecked;

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