using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using LibGit2Sharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO; // <--- Essencial para Path.GetExtension funcionar

namespace ContextWinUI.Core.Models;

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

	// Propriedade não-observável para cache de conteúdo (performance)
	public string? ContentCache { get; set; }

	// Propriedade computada (Expression-bodied member)
	public string Extension => Path.GetExtension(FullPath);

	// Implementação explícita da interface para Tags
	IEnumerable<string> IFileSharedState.Tags => Tags;

	public FileSharedState(string fullPath)
	{
		FullPath = fullPath;
		// Garante que o nome seja preenchido na criação
		Name = Path.GetFileName(fullPath);
	}
}