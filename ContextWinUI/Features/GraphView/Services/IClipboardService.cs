using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IClipboardService
{
	Task<string?> GetTextContentAsync();
}
