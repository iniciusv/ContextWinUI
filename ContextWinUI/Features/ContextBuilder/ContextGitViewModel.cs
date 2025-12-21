using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Features.ContextBuilder
{
	public partial class ContextGitViewModel : ObservableObject
	{
		private readonly IGitService _gitService;
		private readonly IFileSystemItemFactory _itemFactory;
		private readonly IProjectSessionManager _sessionManager;

		[ObservableProperty]
		private ObservableCollection<FileSystemItem> modifiedItems = new();

		public ContextGitViewModel(
			IGitService gitService,
			IFileSystemItemFactory itemFactory,
			IProjectSessionManager sessionManager)
		{
			_gitService = gitService;
			_itemFactory = itemFactory;
			_sessionManager = sessionManager;
		}

		[RelayCommand]
		public async Task RefreshChangesAsync()
		{
			var rootPath = _sessionManager.CurrentProjectPath;
			ModifiedItems.Clear();

			if (string.IsNullOrEmpty(rootPath) || !_gitService.IsGitRepository(rootPath)) return;

			var changedFiles = await _gitService.GetModifiedFilesAsync(rootPath);
			foreach (var path in changedFiles)
			{
				var item = _itemFactory.CreateWrapper(path, FileSystemItemType.File, "\uE70F");
				ModifiedItems.Add(item);
			}
		}

		public void Clear() => ModifiedItems.Clear();
	}
}