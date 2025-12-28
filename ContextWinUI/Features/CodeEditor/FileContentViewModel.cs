using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class FileContentViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;
	// Não precisamos guardar o _fileSelectionService em campo privado se só usarmos no construtor para assinar o evento

	// PROPRIEDADES COMPLETAS
	// SelectedItem ainda existe para o XAML fazer o bind (ex: mostrar o nome do arquivo no header),
	// mas agora ele é alimentado passivamente pelo evento.
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveContentCommand))]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private string fileContent = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<string>? StatusChanged;

	// CONSTRUTOR COMPLETO
	public FileContentViewModel(IFileSystemService fileSystemService, IFileSelectionService selectionService)
	{
		_fileSystemService = fileSystemService;

		// Assinamos o evento global de seleção.
		// Assim que o MainViewModel (ou qualquer outro lugar) mudar a seleção, nós carregamos o arquivo.
		selectionService.SelectionChanged += async (s, newItem) =>
		{
			if (newItem != null && newItem.IsCodeFile)
			{
				await LoadFileAsync(newItem);
			}
			else
			{
				// Limpa a tela se selecionar uma pasta ou nada
				SelectedItem = null;
				FileContent = string.Empty;
			}
		};
	}

	// O método LoadFileAsync permanece público, mas agora é usado principalmente internamente pelo evento acima
	public async Task LoadFileAsync(FileSystemItem item)
	{
		if (item == null || !item.IsCodeFile)
		{
			FileContent = string.Empty;
			SelectedItem = null;
			return;
		}

		SelectedItem = item; // Atualiza a propriedade para a View
		IsLoading = true;

		try
		{
			FileContent = await _fileSystemService.ReadFileContentAsync(item.FullPath);
			OnStatusChanged($"Arquivo: {item.Name} ({item.FileSizeFormatted})");
		}
		catch (Exception ex)
		{
			FileContent = $"Erro ao carregar arquivo: {ex.Message}";
			OnStatusChanged("Erro ao carregar arquivo");
		}
		finally
		{
			IsLoading = false;
		}
	}


	[RelayCommand(CanExecute = nameof(CanSave))]
	private async Task SaveContentAsync()
	{
		if (SelectedItem == null) return;

		IsLoading = true;
		try
		{
			await _fileSystemService.SaveFileContentAsync(SelectedItem.FullPath, FileContent);
			OnStatusChanged($"Salvo: {SelectedItem.Name}");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao salvar: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	private bool CanSave() => SelectedItem != null && !IsLoading;

	[RelayCommand(CanExecute = nameof(CanCopyToClipboard))]
	private void CopyToClipboard()
	{
		if (string.IsNullOrEmpty(FileContent))
			return;

		var dataPackage = new DataPackage();
		dataPackage.SetText(FileContent);
		Clipboard.SetContent(dataPackage);

		OnStatusChanged("Conteúdo copiado!");
	}

	private bool CanCopyToClipboard() => !string.IsNullOrEmpty(FileContent);

	partial void OnFileContentChanged(string value)
	{
		CopyToClipboardCommand.NotifyCanExecuteChanged();
	}

	private void OnStatusChanged(string message)
	{
		StatusChanged?.Invoke(this, message);
	}
}