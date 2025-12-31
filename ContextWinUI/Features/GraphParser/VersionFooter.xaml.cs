// ARQUIVO: VersionFooter.xaml.cs (CORRIGIDO)
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml;


namespace ContextWinUI.Features.GraphParser.Views.Components
{
	public sealed partial class VersionFooter : UserControl, INotifyPropertyChanged
	{
		private CodeBlockItem? _currentBlock;

		public event EventHandler<int>? VersionRestoreRequested;
		public event EventHandler? SaveVersionRequested;
		public event EventHandler? RestoreOriginalRequested;

		public VersionFooter()
		{
			this.InitializeComponent();
		}

		public CodeBlockItem? CurrentBlock
		{
			get => _currentBlock;
			set
			{
				if (_currentBlock != value)
				{
					if (_currentBlock != null)
					{
						_currentBlock.PropertyChanged -= OnBlockPropertyChanged;
					}

					_currentBlock = value;

					if (_currentBlock != null)
					{
						_currentBlock.PropertyChanged += OnBlockPropertyChanged;
					}

					UpdateUI();
					OnPropertyChanged();
				}
			}
		}

		// Propriedade para binding do ComboBox - agora x:Bind pode acessar
		public ObservableCollection<CodeBlockVersion> Versions { get; } = new ObservableCollection<CodeBlockVersion>();

		// Propriedade para binding do SelectedIndex
		public int SelectedVersionIndex
		{
			get => _currentBlock?.CurrentVersionIndex ?? 0;
			set
			{
				if (_currentBlock != null && _currentBlock.CurrentVersionIndex != value)
				{
					VersionRestoreRequested?.Invoke(this, value);
				}
			}
		}

		private string VersionStatus
		{
			get
			{
				if (_currentBlock == null)
					return "Nenhum bloco selecionado";

				if (_currentBlock.IsCurrentVersionOriginal)
					return "Versão original";

				return _currentBlock.HasUnsavedChanges
					? "Modificações não salvas"
					: "Versão salva";
			}
		}

		private SolidColorBrush VersionIconColor
		{
			get
			{
				if (_currentBlock == null)
					return new SolidColorBrush(Colors.Gray);

				if (_currentBlock.IsCurrentVersionOriginal)
					return new SolidColorBrush(Colors.Green);

				return _currentBlock.HasUnsavedChanges
					? new SolidColorBrush(Colors.Orange)
					: new SolidColorBrush(Colors.Blue);
			}
		}

		private string VersionCounterText
		{
			get
			{
				if (_currentBlock == null || !_currentBlock.Versions.Any())
					return "0/0";

				return $"{_currentBlock.CurrentVersionIndex + 1}/{_currentBlock.Versions.Count}";
			}
		}

		public bool CanRestoreOriginal =>
			_currentBlock != null &&
			_currentBlock.Versions.Any() &&
			!_currentBlock.IsCurrentVersionOriginal;

		public bool CanNavigatePrevious =>
			_currentBlock != null &&
			_currentBlock.CurrentVersionIndex > 0;

		public bool CanNavigateNext =>
			_currentBlock != null &&
			_currentBlock.CurrentVersionIndex < _currentBlock.Versions.Count - 1;

		public bool CanSaveVersion =>
			_currentBlock != null &&
			_currentBlock.HasUnsavedChanges;

		private void OnBlockPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CodeBlockItem.Content) ||
				e.PropertyName == nameof(CodeBlockItem.CurrentVersionIndex) ||
				e.PropertyName == nameof(CodeBlockItem.Versions) ||
				e.PropertyName == nameof(CodeBlockItem.HasUnsavedChanges))
			{
				UpdateUI();
			}
		}

		private void UpdateUI()
		{
			Versions.Clear();

			if (_currentBlock != null)
			{
				foreach (var version in _currentBlock.Versions)
				{
					Versions.Add(version);
				}
			}

			OnPropertyChanged(nameof(VersionStatus));
			OnPropertyChanged(nameof(VersionIconColor));
			OnPropertyChanged(nameof(VersionCounterText));
			OnPropertyChanged(nameof(CanRestoreOriginal));
			OnPropertyChanged(nameof(CanNavigatePrevious));
			OnPropertyChanged(nameof(CanNavigateNext));
			OnPropertyChanged(nameof(CanSaveVersion));
			OnPropertyChanged(nameof(SelectedVersionIndex));
		}

		private void OnRestoreOriginalClick(object sender, RoutedEventArgs e)
		{
			RestoreOriginalRequested?.Invoke(this, EventArgs.Empty);
		}

		private void OnPreviousVersionClick(object sender, RoutedEventArgs e)
		{
			if (_currentBlock != null && _currentBlock.CurrentVersionIndex > 0)
			{
				VersionRestoreRequested?.Invoke(this, _currentBlock.CurrentVersionIndex - 1);
			}
		}

		private void OnNextVersionClick(object sender, RoutedEventArgs e)
		{
			if (_currentBlock != null && _currentBlock.CurrentVersionIndex < _currentBlock.Versions.Count - 1)
			{
				VersionRestoreRequested?.Invoke(this, _currentBlock.CurrentVersionIndex + 1);
			}
		}

		private void OnVersionSelected(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count > 0 && e.AddedItems[0] is CodeBlockVersion version)
			{
				// Encontra o índice da versão selecionada
				if (_currentBlock != null && _currentBlock.Versions.Contains(version))
				{
					int index = _currentBlock.Versions.IndexOf(version);
					if (_currentBlock.CurrentVersionIndex != index)
					{
						VersionRestoreRequested?.Invoke(this, index);
					}
				}
			}
		}

		private void OnSaveVersionClick(object sender, RoutedEventArgs e)
		{
			SaveVersionRequested?.Invoke(this, EventArgs.Empty);
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}