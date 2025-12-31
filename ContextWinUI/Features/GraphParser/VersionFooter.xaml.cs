using ContextWinUI.Features.GraphParser.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls; // Necessário para SplitButton e SplitButtonClickEventArgs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContextWinUI.Features.GraphParser.Views.Components
{
	public sealed partial class VersionFooter : UserControl, INotifyPropertyChanged
	{
		// Eventos que o FileSegmentsView vai escutar
		public event EventHandler? SaveNewVersionRequested;
		public event EventHandler? SaveOverwriteRequested;
		public event EventHandler? RestoreOriginalRequested;
		public event PropertyChangedEventHandler? PropertyChanged;

		public VersionFooter()
		{
			this.InitializeComponent();
		}

		#region Dependency Properties

		public static readonly DependencyProperty GlobalVersionsProperty =
			DependencyProperty.Register(nameof(GlobalVersions), typeof(ObservableCollection<GlobalVersion>), typeof(VersionFooter), new PropertyMetadata(null, OnPropertyChangedStatic));

		public ObservableCollection<GlobalVersion> GlobalVersions
		{
			get => (ObservableCollection<GlobalVersion>)GetValue(GlobalVersionsProperty);
			set => SetValue(GlobalVersionsProperty, value);
		}

		public static readonly DependencyProperty SelectedGlobalIndexProperty =
			DependencyProperty.Register(nameof(SelectedGlobalIndex), typeof(int), typeof(VersionFooter), new PropertyMetadata(0, OnPropertyChangedStatic));

		public int SelectedGlobalIndex
		{
			get => (int)GetValue(SelectedGlobalIndexProperty);
			set => SetValue(SelectedGlobalIndexProperty, value);
		}

		public static readonly DependencyProperty CanSaveProperty =
			DependencyProperty.Register(nameof(CanSave), typeof(bool), typeof(VersionFooter), new PropertyMetadata(false, OnPropertyChangedStatic));

		public bool CanSave
		{
			get => (bool)GetValue(CanSaveProperty);
			set => SetValue(CanSaveProperty, value);
		}

		public static readonly DependencyProperty TargetBlockProperty =
			DependencyProperty.Register(nameof(TargetBlock), typeof(CodeBlockItem), typeof(VersionFooter), new PropertyMetadata(null));

		public CodeBlockItem TargetBlock
		{
			get => (CodeBlockItem)GetValue(TargetBlockProperty);
			set => SetValue(TargetBlockProperty, value);
		}

		public static readonly DependencyProperty IsComparisonModeProperty =
			DependencyProperty.Register(nameof(IsComparisonMode), typeof(bool), typeof(VersionFooter), new PropertyMetadata(false));

		public bool IsComparisonMode
		{
			get => (bool)GetValue(IsComparisonModeProperty);
			set => SetValue(IsComparisonModeProperty, value);
		}

		public static readonly DependencyProperty LeftIndexProperty =
			DependencyProperty.Register(nameof(LeftIndex), typeof(int), typeof(VersionFooter), new PropertyMetadata(0));

		public int LeftIndex
		{
			get => (int)GetValue(LeftIndexProperty);
			set => SetValue(LeftIndexProperty, value);
		}

		public static readonly DependencyProperty RightIndexProperty =
			DependencyProperty.Register(nameof(RightIndex), typeof(int), typeof(VersionFooter), new PropertyMetadata(0));

		public int RightIndex
		{
			get => (int)GetValue(RightIndexProperty);
			set => SetValue(RightIndexProperty, value);
		}

		private static void OnPropertyChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is VersionFooter control)
			{
				control.UpdateVisualState();
			}
		}

		#endregion

		#region UI Logic & Helpers

		private void UpdateVisualState()
		{
			OnPropertyChanged(nameof(VersionCounterText));
			OnPropertyChanged(nameof(CanNavigatePrevious));
			OnPropertyChanged(nameof(CanNavigateNext));
		}

		public string VersionCounterText
		{
			get
			{
				if (GlobalVersions == null || GlobalVersions.Count == 0) return "0/0";
				return $"{SelectedGlobalIndex + 1}/{GlobalVersions.Count}";
			}
		}

		public bool CanNavigatePrevious => SelectedGlobalIndex > 0;
		public bool CanNavigateNext => GlobalVersions != null && SelectedGlobalIndex < GlobalVersions.Count - 1;

		#endregion

		#region Event Handlers

		private void OnPreviousVersionClick(object sender, RoutedEventArgs e)
		{
			if (CanNavigatePrevious) SelectedGlobalIndex--;
		}

		private void OnNextVersionClick(object sender, RoutedEventArgs e)
		{
			if (CanNavigateNext) SelectedGlobalIndex++;
		}

		private void OnRestoreOriginalClick(object sender, RoutedEventArgs e)
		{
			RestoreOriginalRequested?.Invoke(this, EventArgs.Empty);
		}

		// --- HANDLERS DO SPLIT BUTTON (Aqui estava o erro CS0123) ---

		// SplitButton requer 'SplitButtonClickEventArgs', não 'RoutedEventArgs'
		private void OnSplitButtonCommit(SplitButton sender, SplitButtonClickEventArgs args)
		{
			SaveNewVersionRequested?.Invoke(this, EventArgs.Empty);
		}

		// 2. Handler exclusivo para o ITEM DE MENU dentro do Flyout
		// Assinatura: (object, RoutedEventArgs)
		private void OnMenuItemCommit(object sender, RoutedEventArgs e)
		{
			SaveNewVersionRequested?.Invoke(this, EventArgs.Empty);
		}

		// Handler do botão "Sobrescrever" (já estava correto, usa RoutedEventArgs)
		private void OnOverwriteClick(object sender, RoutedEventArgs e)
		{
			SaveOverwriteRequested?.Invoke(this, EventArgs.Empty);
		}

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
	}
}