using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Dispatching; // Importante para garantir Thread de UI

namespace ContextWinUI.Services;

public class ClipboardService : IClipboardService
{
	public async Task<string?> GetTextContentAsync()
	{
		// Garante que rodamos na Thread de UI, pois o Clipboard exige isso
		var dispatcher = DispatcherQueue.GetForCurrentThread();
		if (dispatcher == null) return null;

		try
		{
			var package = Clipboard.GetContent();

			// Verifica se contém texto ANTES de tentar ler
			if (package.Contains(StandardDataFormats.Text))
			{
				return await package.GetTextAsync();
			}
		}
		catch (Exception ex)
		{
			// Em produção, use um Logger. Para debug, use:
			System.Diagnostics.Debug.WriteLine($"Erro no Clipboard: {ex.Message}");

			// Retorna uma string de erro para visualizarmos na tela se algo falhou
			return $"// ERRO AO LER CLIPBOARD:\n// {ex.Message}";
		}

		return null;
	}
}