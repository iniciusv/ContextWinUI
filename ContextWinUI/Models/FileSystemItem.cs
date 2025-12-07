using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml; // Necessário para Visibility se usar diretamente, ou bool para converter no XAML

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
	private bool isChecked;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Icon))]
	private string? customIcon;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> children = new();

	public string Extension => IsDirectory ? string.Empty : Path.GetExtension(FullPath);

	public bool IsCodeFile => !IsDirectory && _codeExtensions.Contains(Extension);

	public string Icon
	{
		get
		{
			if (!string.IsNullOrEmpty(CustomIcon)) return CustomIcon;
			return IsDirectory ? "\uE8B7" : GetFileIcon();
		}
	}

	// NOVO: Propriedade para controlar a visibilidade do botão (+)
	// Retorna True se tiver um caminho de arquivo válido e não for diretório
	public bool CanDeepAnalyze => !IsDirectory && !string.IsNullOrEmpty(FullPath) && IsCodeFile;

	// Helper para converter bool para Visibility diretamente no x:Bind (opcional, ou use bool converter)
	// No WinUI 3 com x:Bind, o cast de bool para Visibility nem sempre é automático sem converter
	public Visibility DeepAnalyzeVisibility => CanDeepAnalyze ? Visibility.Visible : Visibility.Collapsed;

	public long? FileSize { get; set; }

	public string FileSizeFormatted => FileSize.HasValue ? FormatBytes(FileSize.Value) : string.Empty;

	// --- Helpers Privados ---

	private static readonly HashSet<string> _codeExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".cs", ".xaml", ".csproj", ".json", ".xml", ".js", ".ts", ".tsx", ".jsx",
		".html", ".css", ".scss", ".py", ".java", ".cpp", ".c", ".h", ".hpp",
		".go", ".rs", ".swift", ".kt", ".php", ".sql", ".sh", ".bat", ".ps1", ".md", ".txt"
	};

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