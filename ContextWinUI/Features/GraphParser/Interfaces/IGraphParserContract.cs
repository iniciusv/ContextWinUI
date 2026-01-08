// ARQUIVO: IGraphParserContract.cs
using ContextWinUI.Features.GraphParser.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

/// <summary>
/// CONTRATO ÚNICO: Definição completa do que o GraphParserViewModel FAZ
/// 
/// CONTEXTO: Controlador principal da aba de Parser de Blocos
/// Gerencia abas de arquivos, busca, histórico global e integração com IA.
/// 
/// RESPONSABILIDADES:
/// 1. Gerenciamento de abas (arquivos e previews de IA)
/// 2. Busca semântica no projeto (grafo de símbolos)
/// 3. Versionamento global compartilhado entre abas
/// 4. Integração com IA (smart paste, merge de código)
/// 5. Navegação e abertura de arquivos
/// </summary>
public interface IGraphParserContract
{
	// ==================== ESTADO DA APLICAÇÃO ====================

	/// <summary>Lista de abas abertas (FileSegmentsViewModel ou AiPreviewViewModel)</summary>
	ObservableCollection<object> Tabs { get; }

	object? SelectedTab { get; set; }

	/// <summary>Sugestões de busca dinâmica (arquivos, classes, métodos)</summary>
	ObservableCollection<SearchSuggestion> SearchSuggestions { get; }

	/// <summary>Histórico global compartilhado entre todas as abas de arquivo</summary>
	ObservableCollection<GlobalVersion> GlobalHistory { get; }

	/// <summary>Índice da versão global atualmente ativa</summary>
	int CurrentGlobalIndex { get; set; }

	bool HasTabs { get; }

	/// <summary>Indica se há alterações não salvas em qualquer aba de arquivo</summary>
	bool HasAnyUnsavedChanges { get; }

	// ==================== OPERAÇÕES PRINCIPAIS ====================

	/// <summary>Executa commit de todas alterações pendentes, criando nova versão global</summary>
	/// <remarks>
	/// Comportamento:
	/// 1. Cria nova entrada no GlobalHistory
	/// 2. Snapshot de todas as abas com alterações
	/// 3. Atualiza CurrentGlobalIndex para nova versão
	/// 4. Sincroniza todas as abas
	/// </remarks>
	void CommitAllPendingChanges();

	/// <summary>Cola snippet do clipboard e aplica merge inteligente com IA</summary>
	/// <returns>Task da operação assíncrona</returns>
	/// <remarks>
	/// Fluxo:
	/// 1. Obtém texto do clipboard
	/// 2. Envia para serviço de merge (IA)
	/// 3. Cria aba de preview com resultado
	/// 4. Abre aba de preview automaticamente
	/// </remarks>
	Task PasteAndMergeFromClipboard();

	/// <summary>Busca arquivos e símbolos no projeto com base na query</summary>
	/// <param name="query">Texto para busca</param>
	/// <remarks>
	/// Busca em duas camadas:
	/// 1. Grafo semântico (símbolos: classes, métodos, propriedades)
	/// 2. Sistema de arquivos (fallback quando grafo não disponível)
	/// </remarks>
	void UpdateSearch(string query);

	/// <summary>Abre arquivo em nova aba de edição</summary>
	/// <param name="parameter">Caminho do arquivo (string) ou SearchSuggestion</param>
	/// <remarks>
	/// Comportamento:
	/// 1. Converte parâmetro para caminho absoluto
	/// 2. Verifica se aba já existe (evita duplicatas)
	/// 3. Cria nova aba com FileSegmentsViewModel
	/// 4. Configura eventos de sincronização com histórico global
	/// </remarks>
	void OpenFile(object parameter);

	/// <summary>Fecha aba especificada</summary>
	/// <param name="tab">Aba a ser fechada (objeto da coleção Tabs)</param>
	void CloseTab(object tab);

	/// <summary>Restaura todas as abas de arquivo para versão global específica</summary>
	/// <param name="versionIndex">Índice da versão no GlobalHistory</param>
	/// <remarks>
	/// Aplica apenas para abas do tipo FileSegmentsViewModel.
	/// Abas de preview (AiPreviewViewModel) não são afetadas.
	/// </remarks>
	void RestoreAllToVersion(int versionIndex);

	// ==================== MÉTODOS AUXILIARES ====================

	/// <summary>Busca arquivos no diretório (fallback quando grafo não indexado)</summary>
	/// <param name="query">Texto para busca nos nomes de arquivo</param>
	/// <remarks>
	/// Usado quando:
	/// 1. Projeto não tem grafo indexado
	/// 2. Query não encontra resultados no grafo
	/// </remarks>
	void SearchInDirectoryAsSuggestions(string query);

	/// <summary>Inicializa grafo semântico do projeto (indexação em background)</summary>
	/// <returns>Task da operação assíncrona</returns>
	/// <remarks>
	/// Chamado automaticamente quando projeto é carregado.
	/// Indexa todos os arquivos .cs do projeto para busca semântica.
	/// </remarks>
	Task InitializeGraphAsync();
}