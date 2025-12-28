// ARQUIVO: IFileSelectionService.cs
using ContextWinUI.Models;
using System;

namespace ContextWinUI.Core.Contracts;

public interface IFileSelectionService
{
	FileSystemItem? CurrentSelection { get; }
	void SetSelection(FileSystemItem? item);
	event EventHandler<FileSystemItem?>? SelectionChanged;
}