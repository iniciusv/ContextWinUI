using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface ITagManagementUiService
{
	Task PromptAndAddTagAsync(ICollection<string> targetCollection, XamlRoot xamlRoot);
	void ToggleTag(ICollection<string> targetCollection, string tag);
	void ClearTags(ICollection<string> targetCollection);
}