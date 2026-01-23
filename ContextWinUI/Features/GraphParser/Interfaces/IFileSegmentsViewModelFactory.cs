using ContextWinUI.Features.GraphParser.ViewModels;
namespace ContextWinUI.Features.GraphParser.Interfaces;

public interface IFileSegmentsViewModelFactory
{
	FileSegmentsViewModel Create(string filePath);
}
