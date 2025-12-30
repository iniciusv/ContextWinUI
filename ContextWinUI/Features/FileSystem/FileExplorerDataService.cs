using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Models;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.FileSystem;

public class FileExplorerDataService : IFileExplorerDataService
{
	private readonly IProjectSessionManager _sessionManager;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly ContextSelectionViewModel _selectionViewModel;
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	public ObservableCollection<FileSystemItem> RootItems { get; private set; } = new();
	public ObservableCollection<string> AllProjectTags { get; } = new();
	public string CurrentPath { get; private set; } = "Nenhum projeto carregado";

	public event EventHandler<string>? StatusChanged;

	public FileExplorerDataService(
		IProjectSessionManager sessionManager,
		IFileSystemItemFactory itemFactory,
		ContextSelectionViewModel selectionViewModel)
	{
		_sessionManager = sessionManager;
		_itemFactory = itemFactory;
		_selectionViewModel = selectionViewModel;
	}

	public async Task LoadProjectAsync()
	{
		await _sessionManager.LoadProjectAsync();
	}

	public void Initialize(ObservableCollection<FileSystemItem> items, string path)
	{
		UnregisterEventsRecursively(RootItems);
		RootItems = items;
		CurrentPath = path;

		_selectionViewModel.Clear();

		foreach (var item in RootItems)
		{
			RegisterItemEvents(item);
			if (item.IsChecked) _selectionViewModel.AddItem(item);
		}

		RefreshTags();
	}

	public void RefreshTags()
	{
		var uniqueTags = _itemFactory.GetAllStates()
			.SelectMany(s => s.Tags)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(t => t);

		AllProjectTags.Clear();
		foreach (var tag in uniqueTags)
		{
			AllProjectTags.Add(tag);
		}
	}

	public void SetSelectionState(IEnumerable<FileSystemItem> items, bool isChecked)
	{
		if (items == null) return;
		foreach (var item in items)
		{
			if (item.IsCodeFile) item.IsChecked = isChecked;
			if (item.Children != null && item.Children.Any())
			{
				SetSelectionState(item.Children, isChecked);
			}
		}
	}

	private void RegisterItemEvents(FileSystemItem item)
	{
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		if (item.SharedState != null)
		{
			item.SharedState.Tags.CollectionChanged -= OnItemTagsChanged;
			item.SharedState.Tags.CollectionChanged += OnItemTagsChanged;
		}

		if (item.Children != null)
		{
			foreach (var child in item.Children) RegisterItemEvents(child);
		}
	}

	private void UnregisterEventsRecursively(IEnumerable<FileSystemItem> items)
	{
		if (items == null) return;
		foreach (var item in items)
		{
			item.PropertyChanged -= OnItemPropertyChanged;
			if (item.SharedState != null)
				item.SharedState.Tags.CollectionChanged -= OnItemTagsChanged;

			if (item.Children != null)
				UnregisterEventsRecursively(item.Children);
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(FileSystemItem.IsChecked) || sender is not FileSystemItem item) return;

		if (item.IsChecked) _selectionViewModel.AddItem(item);
		else _selectionViewModel.RemoveItem(item);
	}

	private void OnItemTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems == null) return;
		_dispatcherQueue.TryEnqueue(() =>
		{
			foreach (string newTag in e.NewItems)
			{
				if (!AllProjectTags.Contains(newTag)) AllProjectTags.Add(newTag);
			}
		});
	}

	public void Dispose()
	{
		UnregisterEventsRecursively(RootItems);
	}
}
