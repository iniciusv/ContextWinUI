using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel; // For ObservableCollection

namespace ContextWinUI.Features.GraphParser.Handlers;

public class FileSegmentsComparisonHandler
{
    private readonly IVersionDiffManager _diffManager;
    private readonly IGitComparisonService _gitComparisonService;
    private readonly IFileSegmentsContract _viewModel;

    public FileSegmentsComparisonHandler(
        IFileSegmentsContract viewModel,
        IVersionDiffManager diffManager,
        IGitComparisonService gitComparisonService)
    {
        _viewModel = viewModel;
        _diffManager = diffManager;
        _gitComparisonService = gitComparisonService;
    }

    public void OnIsComparisonModeChanged(bool value)
    {
        // _viewModel.LeftColumnWidth is handled by VM or here? 
        // VM has the property, Handler updates it?
        // Let's update it here.
        _viewModel.LeftColumnWidth = value ? new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star) : new Microsoft.UI.Xaml.GridLength(0);
        
        if (value)
        {
            _diffManager.RefreshReferenceColumns(_viewModel.Rows, _viewModel.GlobalHistory, _viewModel.CompareLeftIndex);
        }
        // Visibility refresh is implicit? VM calls this handler then usually calls RefreshVisibility.
    }

    public void OnCompareLeftIndexChanged(int value)
    {
        if (_viewModel.IsComparisonMode)
        {
            _diffManager.RefreshReferenceColumns(_viewModel.Rows, _viewModel.GlobalHistory, value);
        }
    }

    public void RestoreToGlobalIndex(int globalIndex)
    {
        if (_viewModel.GlobalHistory == null || globalIndex < 0 || globalIndex >= _viewModel.GlobalHistory.Count) return;
        
        _viewModel.CurrentGlobalIndex = globalIndex;
        _diffManager.RestoreBlocksToGlobalVersion(_viewModel.Rows, _viewModel.GlobalHistory[globalIndex]);
        
        // Notify changes?
        // _viewModel.NotifyUnsavedChanges(); // VM should expose this
        
        if (_viewModel.IsComparisonMode) 
            _diffManager.RefreshReferenceColumns(_viewModel.Rows, _viewModel.GlobalHistory, _viewModel.CompareLeftIndex);
    }

    public async Task<(ObservableCollection<SegmentRowViewModel> Rows, ObservableCollection<GlobalVersion> History)> InitializeFromGitContent(string contentCurrent, string contentOld, string oldVersionLabel)
    {
         var result = await _gitComparisonService.InitializeComparisonAsync(_viewModel.FilePath, contentCurrent, contentOld, oldVersionLabel);
         
         // Setup internal state
         _viewModel.GlobalHistory = result.History;
         _viewModel.CurrentGlobalIndex = 1; 
         _viewModel.CompareLeftIndex = 0;   
         _viewModel.IsComparisonMode = true; 
         _viewModel.HideUnchangedBlocks = false;
         
         OnIsComparisonModeChanged(true);
         
         return result; 
    }
}
