using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.Features.ContextBuilder
{
	public partial class ContextTreeViewModel : ObservableObject
	{
		private readonly IFileSystemItemFactory _itemFactory;
		private readonly IDependencyAnalysisOrchestrator _analysisOrchestrator;
		private readonly IProjectSessionManager _sessionManager;

		[ObservableProperty]
		private ObservableCollection<FileSystemItem> items = new();

		public ContextTreeViewModel(
			IFileSystemItemFactory itemFactory,
			IDependencyAnalysisOrchestrator analysisOrchestrator,
			IProjectSessionManager sessionManager)
		{
			_itemFactory = itemFactory;
			_analysisOrchestrator = analysisOrchestrator;
			_sessionManager = sessionManager;
		}

		public void SetItems(IEnumerable<FileSystemItem> newItems)
		{
			Items.Clear();
			foreach (var item in newItems) Items.Add(item);
		}

		public void Clear() => Items.Clear();

		[RelayCommand]
		private async Task AnalyzeItemDepthAsync(FileSystemItem item)
		{
			if (item == null || !item.IsCodeFile) return;
			await _analysisOrchestrator.EnrichFileNodeAsync(item, _sessionManager.CurrentProjectPath);
			item.IsExpanded = true;
		}

		[RelayCommand]
		private async Task AnalyzeMethodFlowAsync(FileSystemItem item)
		{
			if (item == null || item.Type != FileSystemItemType.Method) return;
			await _analysisOrchestrator.EnrichMethodFlowAsync(item, _sessionManager.CurrentProjectPath);
			item.IsExpanded = true;
		}

		[RelayCommand] private void ExpandAll() { foreach (var item in Items) item.SetExpansionRecursively(true); }
		[RelayCommand] private void CollapseAll() { foreach (var item in Items) item.SetExpansionRecursively(false); }
		[RelayCommand] private void SyncFocus() { }
		[RelayCommand] private void Search(string query) => TreeSearchHelper.Search(Items, query, CancellationToken.None);
	}
}