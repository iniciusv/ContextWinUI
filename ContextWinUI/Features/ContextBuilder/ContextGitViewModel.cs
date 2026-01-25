using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.ContextBuilder
{
	public partial class ContextGitViewModel : ObservableObject
	{
		private readonly IGitService _gitService;
		private readonly IFileSystemItemFactory _itemFactory;
		private readonly IProjectSessionManager _sessionManager;
        private readonly IGraphParserContract _graphParser; // New dependency

		[ObservableProperty]
		private ObservableCollection<FileSystemItem> modifiedItems = new();

        [ObservableProperty]
        private ObservableCollection<GitCommitItem> history = new();

        [ObservableProperty]
        private ObservableCollection<GitCommitItem> selectedCommits = new();

        // Used to toggle UI tabs
        [ObservableProperty]
        private int selectedPivotIndex = 0;

		public ContextGitViewModel(
			IGitService gitService,
			IFileSystemItemFactory itemFactory,
			IProjectSessionManager sessionManager,
            IGraphParserContract graphParser)
		{
			_gitService = gitService;
			_itemFactory = itemFactory;
			_sessionManager = sessionManager;
            _graphParser = graphParser;
		}

		[RelayCommand]
		public async Task RefreshChangesAsync()
		{
			var rootPath = _sessionManager.CurrentProjectPath;
			ModifiedItems.Clear();

			if (string.IsNullOrEmpty(rootPath) || !_gitService.IsGitRepository(rootPath)) return;

			// Agora recebe tuplas (path, isDeleted)
			var changedFiles = await _gitService.GetModifiedFilesAsync(rootPath);

			foreach (var (path, isDeleted) in changedFiles)
			{
				// Ícone: Se deletado usa Lixeira, senão usa ícone padrão de arquivo
				string icon = isDeleted ? "\uE74D" : "\uE70F";

				var item = _itemFactory.CreateWrapper(path, FileSystemItemType.File, icon);

				// Define a propriedade visual
				item.IsDeleted = isDeleted;

				ModifiedItems.Add(item);
			}
		}

        [RelayCommand]
        public async Task LoadHistoryAsync()
        {
            var rootPath = _sessionManager.CurrentProjectPath;
            History.Clear();
            if (string.IsNullOrEmpty(rootPath)) return;

            var commits = await _gitService.GetCommitHistoryAsync(rootPath, 50);
            foreach (var c in commits) History.Add(c);
        }

        public void OnSelectionChanged(IList<object> added, IList<object> removed)
        {
            foreach(var r in removed) if (r is GitCommitItem c) SelectedCommits.Remove(c);
            foreach (var a in added) if (a is GitCommitItem c && !SelectedCommits.Contains(c)) SelectedCommits.Add(c);
        }

        public event EventHandler<List<string>>? AddFilesToContextRequested;

        [RelayCommand]
        public async Task SelectAllModifiedFilesAsync()
        {
            if (!SelectedCommits.Any()) return;

            var rootPath = _sessionManager.CurrentProjectPath;
            if (string.IsNullOrEmpty(rootPath)) return;

            var (oldSha, newSha) = GetCommitRange();
             var changes = await _gitService.GetChangesBetweenCommitsAsync(rootPath, oldSha, newSha);

             // Filter only existing files (not deleted)
             var filesToAdd = changes.Where(c => !c.IsDeleted).Select(c => c.Path).ToList();

             if (filesToAdd.Any())
             {
                 AddFilesToContextRequested?.Invoke(this, filesToAdd);
             }
        }

        private (string OldSha, string NewSha) GetCommitRange()
        {
             var sorted = SelectedCommits.OrderBy(c => c.Date).ToList();
            var oldest = sorted.First();
            var newest = sorted.Last();

            string oldShaTarget = oldest.Sha;
            string newShaTarget = newest.Sha;

            // If only one is selected, compare with its PARENT
            if (sorted.Count == 1)
            {
                 if (oldest.ParentShas.Any())
                 {
                     oldShaTarget = oldest.ParentShas.First();
                 }
            }
            else
            {
                // Multi selection: Compare Parent of Oldest vs Newest
                 if (oldest.ParentShas.Any())
                 {
                     oldShaTarget = oldest.ParentShas.First();
                 }
            }
            return (oldShaTarget, newShaTarget);
        }

        [RelayCommand]
        public async Task CompareSelectedCommitsAsync()
        {
            if (!SelectedCommits.Any()) return;

            var rootPath = _sessionManager.CurrentProjectPath;
            if (string.IsNullOrEmpty(rootPath)) return;

            // Sort selected by Date
            var sorted = SelectedCommits.OrderBy(c => c.Date).ToList();
            var oldest = sorted.First();
            
            var (oldShaTarget, newShaTarget) = GetCommitRange();
            string oldLabel = sorted.Count == 1 && !oldest.ParentShas.Any() ? "Initial" : 
                              (sorted.Count == 1 ? $"Parent of {oldest.ShortSha}" : $"Before {oldest.ShortSha}");

            var changes = await _gitService.GetChangesBetweenCommitsAsync(rootPath, oldShaTarget, newShaTarget);
            
            foreach(var (fullPath, isDeleted) in changes)
            {
                if (isDeleted) continue; // Skip deleted for now

                // Get contents
                string? contentOld = await _gitService.GetFileContentAtCommitAsync(rootPath, fullPath, oldShaTarget);
                string? contentNew = await _gitService.GetFileContentAtCommitAsync(rootPath, fullPath, newShaTarget);

                if (contentNew == null) continue;
                contentOld ??= string.Empty;
                
                 if (_graphParser is GraphParserViewModel vm)
                 {
                     await vm.OpenGitComparison(fullPath, contentNew, contentOld, oldLabel);
                 }
            }
        }

		public void Clear() => ModifiedItems.Clear();
	}
}