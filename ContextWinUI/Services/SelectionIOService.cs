using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace ContextWinUI.Services;

public class SelectionIOService : ISelectionIOService
{
	public async Task SaveSelectionAsync(IEnumerable<string> filePaths)
	{
		var savePicker = new FileSavePicker();
		InitializePicker(savePicker);
		savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
		savePicker.FileTypeChoices.Add("JSON File", new List<string>() { ".json" });
		savePicker.SuggestedFileName = "context_selection";

		var file = await savePicker.PickSaveFileAsync();
		if (file != null)
		{
			var json = JsonSerializer.Serialize(filePaths, new JsonSerializerOptions { WriteIndented = true });
			await File.WriteAllTextAsync(file.Path, json);
		}
	}

	public async Task<IEnumerable<string>> LoadSelectionAsync()
	{
		var openPicker = new FileOpenPicker();
		InitializePicker(openPicker);
		openPicker.ViewMode = PickerViewMode.List;
		openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
		openPicker.FileTypeFilter.Add(".json");

		var file = await openPicker.PickSingleFileAsync();
		if (file != null)
		{
			var content = await File.ReadAllTextAsync(file.Path);
			try
			{
				return JsonSerializer.Deserialize<IEnumerable<string>>(content) ?? new List<string>();
			}
			catch
			{
				return new List<string>();
			}
		}
		return new List<string>();
	}

	// Helper para WinUI 3 (Necessário para Pickers funcionarem)
	private void InitializePicker(object picker)
	{
		if (App.MainWindow != null)
		{
			var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
			WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
		}
	}
}