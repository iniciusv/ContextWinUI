using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;

namespace ContextWinUI.Features.GraphParser.Handlers;

public class FileSegmentsEditHandler
{
    private readonly IBlockEditorService _editorService;
    private readonly IFileSegmentsContract _viewModel;

    public FileSegmentsEditHandler(
        IFileSegmentsContract viewModel,
        IBlockEditorService editorService)
    {
        _viewModel = viewModel;
        _editorService = editorService;
    }

    public void AddSiblingBlock(CodeBlockItem? referenceBlock)
    {
        if (referenceBlock == null) return;
        var newBlock = _editorService.AddSiblingBlock(_viewModel.Rows, referenceBlock);
        if (newBlock != null)
        {
            _viewModel.SelectedBlock = newBlock;
            _viewModel.NotifyUnsavedChanges();
            _viewModel.RefreshRowsVisibility();
        }
    }

    public void DeleteBlock(CodeBlockItem? block)
    {
        if (block == null) return;
        UpdateSelectionBeforeDelete(block);
        _editorService.DeleteBlock(_viewModel.Rows, block);
        _viewModel.NotifyUnsavedChanges();
    }

    public void LinkBlocks(Tuple<CodeBlockItem, CodeBlockItem> items)
    {
        _editorService.LinkBlocks(_viewModel.Rows, items.Item1, items.Item2);
        _viewModel.NotifyUnsavedChanges();
    }

    public void RevertBlockToOriginal(CodeBlockItem? currentBlock)
    {
        if (currentBlock == null) return;
        bool changed = _editorService.RevertBlockToOriginal(currentBlock);
        if (changed)
        {
            _viewModel.NotifyUnsavedChanges();
        }
    }

    public void PromoteEditToNewBlock(CodeBlockItem? currentBlock)
    {
        _editorService.PromoteEditToNewBlock(_viewModel.Rows, currentBlock);
        _viewModel.NotifyUnsavedChanges();
    }

    private void UpdateSelectionBeforeDelete(CodeBlockItem blockToDelete)
    {
        if (_viewModel.SelectedBlock == blockToDelete)
        {
            var row = _viewModel.Rows.FirstOrDefault(r => r.Current == blockToDelete);
            if (row == null) return;
            int idx = _viewModel.Rows.IndexOf(row);
            if (idx > 0) _viewModel.SelectedBlock = _viewModel.Rows[idx - 1].Current;
            else if (_viewModel.Rows.Count > 1) _viewModel.SelectedBlock = _viewModel.Rows[idx + 1].Current;
            else _viewModel.SelectedBlock = null;
        }
    }
}
