// ARQUIVO: IFileSegmentsContract.cs
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

/// <summary>
/// CONTRATO COMPLETO: Todos os comportamentos públicos da FileSegmentsViewModel
/// Documentação completa para entendimento do que a ViewModel FAZ
/// </summary>
public interface IFileSegmentsContract
{
	// ==================== PROPRIEDADES ====================
	string FilePath { get; }
	string FileName { get; }
	CodeBlockItem? SelectedBlock { get; set; }
	ObservableCollection<SegmentRowViewModel> Rows { get; }
	string CurrentSymbolInfo { get; set; }
    ObservableCollection<SymbolResolutionResult> ImplementationCandidates { get; }
    SymbolResolutionResult? SelectedImplementation { get; set; }
	bool HasAnyUnsavedChanges { get; }
	bool HasSelectedBlock { get; }
	bool IsComparisonMode { get; set; }
	bool HideUnchangedBlocks { get; set; }
	ObservableCollection<GlobalVersion> GlobalHistory { get; }
	int CurrentGlobalIndex { get; set; }
	int CompareLeftIndex { get; set; }

	/// <summary>Largura da coluna esquerda (modo comparação)</summary>
	GridLength LeftColumnWidth { get; set; }
	bool IsLoading { get; set; }
	bool IsEmpty { get; set; }
	IEnumerable<CodeBlockItem> Blocks { get; }

	// ==================== EVENTOS ====================

	event EventHandler? GlobalSaveRequested;

	event EventHandler<int>? GlobalRestoreRequested;

	// ==================== MÉTODOS PÚBLICOS (COMANDOS) ====================
	// Nota: Na implementação atual com [RelayCommand], estes métodos são chamados pelos comandos

	/// <summary>Adiciona novo bloco irmão após o bloco de referência</summary>
	/// <param name="referenceBlock">Bloco de referência (opcional)</param>
	/// <remarks>Comando: AddSiblingBlockCommand</remarks>
	void AddSiblingBlock(CodeBlockItem? referenceBlock);

	/// <summary>Exclui bloco especificado</summary>
	/// <param name="block">Bloco a excluir (opcional)</param>
	/// <remarks>Comando: DeleteBlockCommand</remarks>
	void DeleteBlock(CodeBlockItem? block);

	/// <summary>Vincula dois blocos (drag & drop)</summary>
	/// <param name="items">Par (source, target) de blocos para vincular</param>
	/// <remarks>Comando: LinkBlocksCommand</remarks>
	void LinkBlocks(Tuple<CodeBlockItem, CodeBlockItem> items);

	/// <summary>Reverte bloco para sua versão original</summary>
	/// <param name="currentBlock">Bloco a reverter (opcional)</param>
	/// <remarks>Comando: RevertBlockToOriginalCommand</remarks>
	void RevertBlockToOriginal(CodeBlockItem? currentBlock);

	/// <summary>Promove edição para novo bloco (desvincula)</summary>
	/// <param name="currentBlock">Bloco atual (opcional)</param>
	/// <remarks>Comando: PromoteEditToNewBlockCommand</remarks>
	void PromoteEditToNewBlock(CodeBlockItem? currentBlock);

	/// <summary>Salva alterações (modo: "NewVersion" ou "Overwrite")</summary>
	/// <param name="mode">Modo de salvamento ("NewVersion" para nova versão, "Overwrite" para sobrescrever)</param>
	/// <remarks>Comando: SaveChangesCommand</remarks>
	void SaveChanges(string mode);

	/// <summary>Comita todas alterações pendentes (novo versionamento global)</summary>
	/// <remarks>Comando: CommitAllPendingChangesCommand</remarks>
	void CommitAllPendingChanges();

	// ==================== MÉTODOS PÚBLICOS (NÃO-COMANDOS) ====================

	/// <summary>Restaura todos os blocos para uma versão global</summary>
	/// <param name="globalIndex">Índice da versão no histórico global</param>
	/// <exception cref="ArgumentOutOfRangeException">Índice fora do intervalo</exception>
	void RestoreToGlobalIndex(int globalIndex);

	/// <summary>Cria snapshot dos blocos alterados para versão global</summary>
	/// <param name="description">Descrição da versão</param>
	/// <param name="batchTimestamp">Timestamp do batch</param>
	void SnapshotBlocksForGlobalVersion(string description, DateTime batchTimestamp);

	/// <summary>Força notificação de alterações não salvas</summary>
	void NotifyUnsavedChanges();

	/// <summary>Notifica mudança no estado de alterações</summary>
	void NotifyChangesChanged();

	/// <summary>Sincroniza índice global com estado externo</summary>
	/// <param name="index">Novo índice global</param>
	void SyncGlobalIndex(int index);

	/// <summary>Dispara evento de salvamento global</summary>
	void TriggerGlobalSave();

	/// <summary>Resolve símbolo na posição do cursor</summary>
	/// <param name="cursorIndexInBlock">Posição relativa no bloco atual</param>
	Task ResolveSymbolHeuristic(int cursorIndexInBlock);

	/// <summary>Aplica uma sugestão de IA ao código atual</summary>
	/// <param name="aiCode">Código gerado pela IA para mesclar</param>
	/// <returns>Task da operação assíncrona</returns>
	/// <exception cref="InvalidOperationException">Se o arquivo não corresponder</exception>
	Task ApplyAiSuggestion(string aiCode);

	/// <summary>Aplica um merge externo de blocos</summary>
	/// <param name="mergedBlocks">Lista de blocos resultante do merge</param>
	void ApplyExternalMerge(List<CodeBlockItem> mergedBlocks);
	Task SaveToDiskAsync();
}