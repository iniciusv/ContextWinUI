using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ContextWinUI.Models;

public partial class FileSystemItem : ObservableObject, IDisposable, IFileSystemItem
{
	public FileSharedState SharedState { get; }

	// CORREÇÃO XLS0519: Expondo Tags diretamente para facilitar o acesso via x:Bind
	public ObservableCollection<string> Tags => SharedState.Tags;

	public FileSystemItem(FileSharedState sharedState)
	{
		SharedState = sharedState;
		SharedState.PropertyChanged += OnSharedStateChanged;
	}

	public string Name => SharedState.Name;

	public string FullPath => SharedState.FullPath;

	public string FileSizeFormatted => SharedState.FileSize.HasValue ? FormatBytes(SharedState.FileSize.Value) : string.Empty;

	public bool IsChecked
	{
		get => SharedState.IsChecked;
		set
		{
			if (SharedState.IsChecked != value)
			{
				SharedState.IsChecked = value;
				OnPropertyChanged(nameof(IsChecked));
			}
		}
	}

	// CORREÇÃO: Propriedade necessária para o efeito de tachado (Strikethrough)
	[ObservableProperty]
	private bool isDeleted;

	[ObservableProperty]
	private FileSystemItemType type;

	[ObservableProperty]
	private bool isExpanded;

	[ObservableProperty]
	private bool isSelected;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Icon))]
	private string? customIcon;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Visibility))]
	private bool isVisibleInSearch = true;

	public string? MethodSignature { get; set; }

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> children = new();

	public Visibility Visibility => IsVisibleInSearch ? Visibility.Visible : Visibility.Collapsed;

	public bool CanDeepAnalyze => Type == FileSystemItemType.File && IsCodeFile;

	public Visibility DeepAnalyzeVisibility =>
		(Type == FileSystemItemType.File || Type == FileSystemItemType.Dependency || Type == FileSystemItemType.Class)
		? Visibility.Visible : Visibility.Collapsed;

	public Visibility GroupAnalyzeVisibility =>
		(Type == FileSystemItemType.LogicalGroup && Name.Contains("Estrutura"))
		? Visibility.Visible : Visibility.Collapsed;

	public Thickness SelectionMargin =>
		(Type == FileSystemItemType.File) ? new Thickness(0) : new Thickness(24, 2, 0, 2);

	public Brush SelectionBackground =>
		(Type == FileSystemItemType.File)
			? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
			: (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];

	public Visibility IsSubItemVisibility =>
		(Type != FileSystemItemType.File) ? Visibility.Visible : Visibility.Collapsed;

	public bool CanAnalyzeMethodFlow => Type == FileSystemItemType.Method && !string.IsNullOrEmpty(FullPath);

	public Visibility MethodFlowVisibility => CanAnalyzeMethodFlow ? Visibility.Visible : Visibility.Collapsed;

	public bool IsDirectory => Type == FileSystemItemType.Directory;
	public bool IsCodeFile => !IsDirectory && _codeExtensions.Contains(SharedState.Extension);

	public string Icon
	{
		get
		{
			if (!string.IsNullOrEmpty(CustomIcon)) return CustomIcon;
			if (Type == FileSystemItemType.Directory) return "\uE8B7";
			if (Type == FileSystemItemType.Method) return "\uEA86";
			if (Type == FileSystemItemType.Dependency) return "\uE943";
			return GetFileIcon();
		}
	}

	IFileSharedState IFileSystemItem.SharedStateInfo => SharedState;
	IEnumerable<IFileSystemItem> IFileSystemItem.ChildrenItems => Children.Cast<IFileSystemItem>();

	private void OnSharedStateChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(FileSharedState.IsChecked))
		{
			// Notifica a View que IsChecked mudou
			OnPropertyChanged(nameof(IsChecked));

			// Propaga para os filhos (Lógica manual conforme seu código original)
			foreach (var child in Children)
			{
				child.IsChecked = this.IsChecked;
			}
		}
		else if (e.PropertyName == nameof(FileSharedState.Name))
		{
			OnPropertyChanged(nameof(Name));
		}
		// Se a coleção de tags mudar, a propriedade Tags já aponta para ela, 
		// mas se a instância mudar, precisamos notificar.
	}

	private static readonly HashSet<string> _codeExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".cs", ".xaml", ".csproj", ".json", ".xml", ".js", ".ts", ".tsx", ".jsx", ".vue",
		".html", ".css", ".scss", ".py", ".java", ".cpp", ".c", ".h", ".hpp",
		".go", ".rs", ".swift", ".kt", ".php", ".sql", ".sh", ".bat", ".ps1", ".md", ".txt", ".razor"
	};

	private string GetFileIcon()
	{
		return SharedState.Extension.ToLower() switch
		{
			".cs" => "\uE943",
			".xaml" => "\uE8A5",
			".json" => "\uE8D2",
			".xml" => "\uE8A5",
			".txt" => "\uE8A5",
			".md" => "\uE8A5",
			".js" or ".jsx" => "\uE943",
			".ts" or ".tsx" => "\uE943",
			".vue" => "\uE8A5",
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

	public void SetExpansionRecursively(bool expanded)
	{
		IsExpanded = expanded;
		foreach (var child in Children)
		{
			if (child.IsDirectory || child.Type == FileSystemItemType.LogicalGroup)
			{
				child.SetExpansionRecursively(expanded);
			}
		}
	}

	public void NotifyViewUpdate(string propertyName)
	{
		OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
	}

	public void Dispose()
	{
		SharedState.PropertyChanged -= OnSharedStateChanged;
	}
}