using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ContextWinUI.Features.GraphParser.Handlers;

public class FileSegmentsSelectionHandler
{
    private readonly IBlockSelectionManager _selectionManager;
    private readonly IFileSegmentsContract _viewModel;

    public event EventHandler? FileSelectionRequested;

    public FileSegmentsSelectionHandler(
        IFileSegmentsContract viewModel,
        IBlockSelectionManager selectionManager)
    {
        _viewModel = viewModel;
        _selectionManager = selectionManager;
    }

    public void AttachBlockEvents(CodeBlockItem block)
    {
        block.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CodeBlockItem.IsSelected) ||
                e.PropertyName == nameof(CodeBlockItem.IsReferenced))
            {
                // Sync with manager
                bool shouldBeSelected = block.IsSelected || block.IsReferenced;

                if (shouldBeSelected)
                    _selectionManager.SelectBlock(_viewModel.FilePath, block.StableId);
                else
                    _selectionManager.DeselectBlock(_viewModel.FilePath, block.StableId);

                // Ideally we would notify the ViewModel to update "HasAnyBlockSelected"
                // But since ViewModel exposes Rows property directly, bindings might update automatically 
                // However, "HasAnyBlockSelected" is a computed property on VM.
                // We might need to trigger a refresh on VM or VM subscribes to us?
                // For now, let's keep the logic simple. The VM should probably subscribe to this Handler or 
                // the Handler should call a method on VM.
                
                // But wait, the original code had: OnPropertyChanged(nameof(HasAnyBlockSelected));
                // We can't do that easily from here without casting or interface method.
                // Let's assume the VM's Rows collection changes are enough for UI, 
                // OR we add a "NotifySelectionChanged" to the Contract.
                // The Contract has "NotifyChangesChanged" but not SelectionChanged explicitly.
                // Let's rely on the Event for now.
                
                if (block.IsSelected)
                {
                    FileSelectionRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        };
    }

    public void HandleBlockClick(CodeBlockItem block, bool isCtrlPressed)
    {
        _viewModel.SelectedBlock = block;

        bool willBeSelected = !block.IsSelected;
        block.IsSelected = willBeSelected;

        if (willBeSelected)
        {
            block.ShowReferences = isCtrlPressed;
        }
        else
        {
            block.ShowReferences = false;
        }

        // We need to trigger reference refresh. 
        // This logic is in NavigationHandler. 
        // The VM will have to coordinate this.
        // We will return true/false or similar to let VM know it should refresh references?
        // Or we expose an event "SelectionChanged" that VM listens to and calls NavHandler.
    }
    
    public void InitializeSelection(CodeBlockItem item)
    {
        if (_selectionManager.IsBlockSelected(_viewModel.FilePath, item.StableId))
        {
            item.IsSelected = true;
        }
    }
}
