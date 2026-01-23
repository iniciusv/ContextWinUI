using CommunityToolkit.WinUI;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Models;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Services;

public class PreviewManager : IPreviewManager
{
	private readonly IFileSelectionService _selectionService;
	private readonly DispatcherQueue _dispatcher;

	// O alvo onde executaremos a ação (ViewModel principal)
	private IGraphParserContract? _viewModel;

	// Token para cancelar a operação se o usuário mudar a seleção rápido demais
	private CancellationTokenSource? _debounceCts;

	// Constante de tempo: quanto tempo esperar o usuário "parar" no arquivo
	private const int DebounceDelayMs = 250;

	public PreviewManager(IFileSelectionService selectionService)
	{
		_selectionService = selectionService;
		_dispatcher = DispatcherQueue.GetForCurrentThread();
	}

	public void Start(IGraphParserContract viewModelContract)
	{
		_viewModel = viewModelContract;
		_selectionService.SelectionChanged += OnSelectionChanged;
	}

	private void OnSelectionChanged(object? sender, FileSystemItem? item)
	{
		// 1. Sempre cancela a tarefa de preview anterior (se houver uma pendente)
		_debounceCts?.Cancel();

		// 2. Validações: É nulo? É pasta? É C#?
		if (item == null ||
			item.IsDirectory ||
			string.IsNullOrEmpty(item.FullPath) ||
			!item.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		// 3. Prepara o novo ciclo de debounce
		_debounceCts = new CancellationTokenSource();
		var token = _debounceCts.Token;
		var pathCapture = item.FullPath; // Captura o valor atual para a thread

		// 4. Aguarda o tempo de debounce
		_ = Task.Delay(DebounceDelayMs, token).ContinueWith(async _ =>
		{
			// Se foi cancelado durante o delay, aborta
			if (token.IsCancellationRequested) return;

			// Retorna para a Thread de UI para chamar o ViewModel com segurança
			await _dispatcher.EnqueueAsync(() =>
			{
				if (token.IsCancellationRequested) return;

				try
				{
					// Delega a lógica de "como abrir" para o ViewModel
					_viewModel?.OpenAsPreview(pathCapture);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[PreviewManager] Erro ao solicitar preview: {ex.Message}");
				}
			});
		});
	}

	public void Dispose()
	{
		_selectionService.SelectionChanged -= OnSelectionChanged;
		_debounceCts?.Cancel();
		_debounceCts?.Dispose();
	}
}