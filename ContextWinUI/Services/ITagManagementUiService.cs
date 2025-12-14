using ContextWinUI.Models;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface ITagManagementUiService
{
	Task PromptAndAddTagAsync(ICollection<string> targetCollection, XamlRoot xamlRoot);
	void ToggleTag(ICollection<string> targetCollection, string tag);
	void ClearTags(ICollection<string> targetCollection);
	void BatchToggleTag(IEnumerable<FileSystemItem> items, string tag);
	Task PromptAndAddTagToBatchAsync(IEnumerable<FileSystemItem> items, XamlRoot xamlRoot);
}