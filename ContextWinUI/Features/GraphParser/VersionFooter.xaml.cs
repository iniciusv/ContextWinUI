using ContextWinUI.Features.GraphParser.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContextWinUI.Features.GraphParser.Views.Components
{
	public sealed partial class VersionFooter : UserControl, INotifyPropertyChanged
	{
		public event EventHandler? SaveRequested;
		public event EventHandler? RestoreOriginalRequested;
		public event PropertyChangedEventHandler? PropertyChanged;

		public VersionFooter()
		{
			this.InitializeComponent();
		}

		#region Dependency Properties

		// 1. LISTA DE VERSÕES (Vem do GlobalHistory do ViewModel)
		public static readonly DependencyProperty GlobalVersionsProperty =
			DependencyProperty.Register(nameof(GlobalVersions), typeof(ObservableCollection<GlobalVersion>), typeof(VersionFooter), new PropertyMetadata(null, OnPropertyChangedStatic));

		public ObservableCollection<GlobalVersion> GlobalVersions
		{
			get => (ObservableCollection<GlobalVersion>)GetValue(GlobalVersionsProperty);
			set => SetValue(GlobalVersionsProperty, value);
		}

		// 2. ÍNDICE SELECIONADO (Vem do CurrentGlobalIndex do ViewModel)
		public static readonly DependencyProperty SelectedGlobalIndexProperty =
			DependencyProperty.Register(nameof(SelectedGlobalIndex), typeof(int), typeof(VersionFooter), new PropertyMetadata(0, OnPropertyChangedStatic));

		public int SelectedGlobalIndex
		{
			get => (int)GetValue(SelectedGlobalIndexProperty);
			set => SetValue(SelectedGlobalIndexProperty, value);
		}

		// 3. PODE SALVAR? (Vem do HasAnyUnsavedChanges do ViewModel)
		public static readonly DependencyProperty CanSaveProperty =
			DependencyProperty.Register(nameof(CanSave), typeof(bool), typeof(VersionFooter), new PropertyMetadata(false, OnPropertyChangedStatic));

		public bool CanSave
		{
			get => (bool)GetValue(CanSaveProperty);
			set => SetValue(CanSaveProperty, value);
		}

		// Callback genérico para avisar a UI interna quando as propriedades externas mudarem
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
			// Atualiza propriedades calculadas visuais
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
			if (CanNavigatePrevious)
			{
				SelectedGlobalIndex--; // O TwoWay binding vai atualizar o ViewModel
			}
		}

		private void OnNextVersionClick(object sender, RoutedEventArgs e)
		{
			if (CanNavigateNext)
			{
				SelectedGlobalIndex++; // O TwoWay binding vai atualizar o ViewModel
			}
		}

		private void OnRestoreOriginalClick(object sender, RoutedEventArgs e)
		{
			// Opção A: Resetar índice direto (mais simples)
			// SelectedGlobalIndex = 0; 

			// Opção B: Disparar evento (conforme seu pedido original)
			RestoreOriginalRequested?.Invoke(this, EventArgs.Empty);
		}

		private void OnSaveVersionClick(object sender, RoutedEventArgs e)
		{
			SaveRequested?.Invoke(this, EventArgs.Empty);
		}

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
	}
}