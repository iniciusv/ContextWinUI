using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace ContextWinUI.Models;

public partial class FileSystemItem : ObservableObject
{
	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private bool isDirectory;

	[ObservableProperty]
	private bool isExpanded;

	[ObservableProperty]
	private bool isSelected;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> children = new();

	public string Extension => IsDirectory ? string.Empty : Path.GetExtension(FullPath);

	// Extensões de código comuns
	private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".cs", ".xaml", ".csproj", ".json", ".xml",
		".js", ".ts", ".tsx", ".jsx", ".html", ".css", ".scss",
		".py", ".java", ".cpp", ".c", ".h", ".hpp",
		".go", ".rs", ".swift", ".kt", ".php",
		".sql", ".sh", ".bat", ".ps1", ".md", ".txt",
		".yaml", ".yml", ".toml", ".ini", ".config"
	};

	public bool IsCodeFile => !IsDirectory && CodeExtensions.Contains(Extension);

	public string Icon => IsDirectory ? "\uE8B7" : GetFileIcon();

	private string GetFileIcon()
	{
		return Extension.ToLower() switch
		{
			".cs" => "\uE943",
			".xaml" => "\uE8A5",
			".json" => "\uE8D2",
			".xml" => "\uE8A5",
			".txt" => "\uE8A5",
			".md" => "\uE8A5",
			_ => "\uE8A5"
		};
	}

	public long? FileSize { get; set; }

	public string FileSizeFormatted => FileSize.HasValue
		? FormatBytes(FileSize.Value)
		: string.Empty;

	private static string FormatBytes(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB" };
		double len = bytes;
		int order = 0;
		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len /= 1024;
		}
		return $"{len:0.##} {sizes[order]}";
	}
}