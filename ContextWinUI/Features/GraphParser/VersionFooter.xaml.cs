using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ContextWinUI.Features.GraphParser.Models;

namespace ContextWinUI.Features.GraphParser.Views.Components
{
	public sealed partial class VersionFooter : UserControl, INotifyPropertyChanged
	{
		public event EventHandler? SaveNewVersionRequested;
		public event EventHandler? SaveOverwriteRequested;
		public event EventHandler? RestoreOriginalRequested;
		public event PropertyChangedEventHandler? PropertyChanged;
		public event EventHandler? SaveToFileRequested;

		public VersionFooter()
		{
			this.InitializeComponent();
		}

		#region Dependency Properties

		// New: Symbol Info Text to allow full-width footer management
		public static readonly DependencyProperty SymbolInfoProperty =
			DependencyProperty.Register(nameof(SymbolInfo), typeof(string), typeof(VersionFooter), new PropertyMetadata(string.Empty));

		public string SymbolInfo
		{
			get => (string)GetValue(SymbolInfoProperty);
			set => SetValue(SymbolInfoProperty, value);
		}

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

		public static readonly DependencyProperty IsComparisonModeProperty =
			DependencyProperty.Register(nameof(IsComparisonMode), typeof(bool), typeof(VersionFooter), new PropertyMetadata(false, OnPropertyChangedStatic));

		public bool IsComparisonMode
		{
			get => (bool)GetValue(IsComparisonModeProperty);
			set => SetValue(IsComparisonModeProperty, value);
		}

		// New: Hide Unchanged Property moved to footer
		public static readonly DependencyProperty HideUnchangedProperty =
			DependencyProperty.Register(nameof(HideUnchanged), typeof(bool), typeof(VersionFooter), new PropertyMetadata(false));

		public bool HideUnchanged
		{
			get => (bool)GetValue(HideUnchangedProperty);
			set => SetValue(HideUnchangedProperty, value);
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

		public static readonly DependencyProperty ImplementationCandidatesProperty =
			DependencyProperty.Register(nameof(ImplementationCandidates), typeof(ObservableCollection<SymbolResolutionResult>), typeof(VersionFooter), new PropertyMetadata(null));

		public ObservableCollection<SymbolResolutionResult> ImplementationCandidates
		{
			get => (ObservableCollection<SymbolResolutionResult>)GetValue(ImplementationCandidatesProperty);
			set => SetValue(ImplementationCandidatesProperty, value);
		}

		public static readonly DependencyProperty SelectedImplementationProperty =
			DependencyProperty.Register(nameof(SelectedImplementation), typeof(SymbolResolutionResult), typeof(VersionFooter), new PropertyMetadata(null));

		public SymbolResolutionResult SelectedImplementation
		{
			get => (SymbolResolutionResult)GetValue(SelectedImplementationProperty);
			set => SetValue(SelectedImplementationProperty, value);
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

		private void OnRestoreOriginalClick(object sender, RoutedEventArgs e) => RestoreOriginalRequested?.Invoke(this, EventArgs.Empty);

		private void OnSplitButtonCommit(SplitButton sender, SplitButtonClickEventArgs args) => SaveNewVersionRequested?.Invoke(this, EventArgs.Empty);

		private void OnMenuItemCommit(object sender, RoutedEventArgs e) => SaveNewVersionRequested?.Invoke(this, EventArgs.Empty);

		private void OnOverwriteClick(object sender, RoutedEventArgs e) => SaveOverwriteRequested?.Invoke(this, EventArgs.Empty);

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void OnSaveToFileClick(object sender, RoutedEventArgs e) => SaveToFileRequested?.Invoke(this, EventArgs.Empty);
		#endregion
	}
}