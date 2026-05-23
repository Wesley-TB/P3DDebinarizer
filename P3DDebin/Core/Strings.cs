using System;
using System.Collections.Generic;
using System.IO;

namespace P3DDebin.Core;

public enum Language
{
    English,
    Portuguese,
    German,
    Spanish
}

// Tabela de traducoes da interface. Ingles e o idioma padrao.
public static class Strings
{
    private static Language _current = Language.English;

    public static event Action? LanguageChanged;

    public static Language Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            SavePreference(value);
            LanguageChanged?.Invoke();
        }
    }

    public static (Language Lang, string Name)[] Available { get; } = new[]
    {
        (Language.English,    "English"),
        (Language.Portuguese, "Português"),
        (Language.German,     "Deutsch"),
        (Language.Spanish,    "Español"),
    };

    public static string T(string key, params object[] args)
    {
        if (!_data.TryGetValue(_current, out var dict) ||
            !dict.TryGetValue(key, out string? value))
        {
            if (!_data[Language.English].TryGetValue(key, out value))
                value = key;
        }
        return args.Length > 0 ? string.Format(value, args) : value;
    }

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "P3DDebin", "config.txt");

    public static void LoadPreference()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            foreach (string line in File.ReadAllLines(ConfigPath))
            {
                if (line.StartsWith("Language=", StringComparison.Ordinal))
                {
                    string v = line["Language=".Length..].Trim();
                    if (Enum.TryParse<Language>(v, ignoreCase: true, out var lang))
                        _current = lang;
                }
            }
        }
        catch { /* sem persistencia se houver problema */ }
    }

    private static void SavePreference(Language lang)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, $"Language={lang}{Environment.NewLine}");
        }
        catch { }
    }

    // -------------------------------------------------------------------------
    // Tabela de traducoes
    // -------------------------------------------------------------------------
    private static readonly Dictionary<Language, Dictionary<string, string>> _data =
        new()
    {
        [Language.English] = new()
        {
            ["Main.Title"]              = "P3D Debinarizer — Arma 3",
            ["Main.Subtitle"]           = "Debinarize, extract RVMATs / model.cfg and change paths of .p3d models",
            ["Main.LblInput"]           = "P3D file or folder (batch) — or drop here:",
            ["Main.LblOutput"]          = "Output folder:",
            ["Main.LblLog"]             = "Log:",
            ["Main.LblLanguage"]        = "Language:",
            ["Main.StatusReady"]        = "Ready.",
            ["Main.StatusBusy"]         = "Processing...",
            ["Main.StatusCancelled"]    = "Operation cancelled.",

            ["Main.FmtNone"]            = "Format: — (select a file or folder)",
            ["Main.FmtOdol"]            = "Format: ODOL (binarized) — all functions available.",
            ["Main.FmtMlod"]            = "Format: MLOD (non-binarized) — paths change only.",
            ["Main.FmtUnknown"]         = "Format: unknown — not a valid P3D.",
            ["Main.FmtBatch"]           = "Batch: {0} .p3d file(s) in folder and subfolders.",

            ["Main.BtnFile"]            = "File...",
            ["Main.BtnFolder"]          = "Folder...",
            ["Main.BtnSelect"]          = "Select...",
            ["Main.BtnChangePaths"]     = "Change Paths",
            ["Main.BtnDebin"]           = "Debinarize",
            ["Main.BtnExtractRvmats"]   = "Extract RVMATs",
            ["Main.BtnExtractCfg"]      = "Extract Model.cfg",

            ["Main.DlgFileTitle"]       = "Select P3D file",
            ["Main.DlgFileFilter"]      = "P3D models (*.p3d)|*.p3d|All files (*.*)|*.*",
            ["Main.DlgFolderInTitle"]   = "Select folder with .p3d models (batch)",
            ["Main.DlgFolderOutTitle"]  = "Select output folder",

            ["Main.LogInputNotFound"]   = "Input not found: {0}",
            ["Main.LogOutputNotFound"]  = "Output folder not found: {0}",
            ["Main.LogNoP3dFound"]      = "No .p3d files found.",

            ["Main.LogDebinarizing"]    = "Debinarizing: {0}",
            ["Main.LogSavedTo"]         = "   Saved to: {0}",
            ["Main.LogConcluded"]       = "Concluded: {0}",
            ["Main.LogFailureDebin"]    = "Debinarization failed. Check the log.",

            ["Main.LogExtractingRvmats"]   = "Extracting RVMATs: {0}",
            ["Main.LogNoRvmats"]           = "No embedded RVMATs in this model.",
            ["Main.LogStatusNoRvmats"]     = "No RVMATs found.",
            ["Main.LogRvmatsReconstructed"]= "✓  {0} RVMAT(s) reconstructed.",
            ["Main.LogStatusRvmatsPreview"]= "{0} RVMAT(s) — preview opened.",
            ["Main.LogFailureRvmats"]      = "Failed to extract RVMATs. Check the log.",

            ["Main.LogExtractingCfg"]    = "Extracting model.cfg: {0}",
            ["Main.LogCfgReconstructed"] = "✓  {0} reconstructed.",
            ["Main.LogStatusCfgPreview"] = "model.cfg reconstructed — preview opened.",
            ["Main.LogFailureCfg"]       = "Failed to extract model.cfg. Check the log.",

            ["Main.LogChangePathsReading"]    = "Change paths: reading {0} file(s)...",
            ["Main.LogChangePathsItem"]       = "  {0}: {1} path(s)",
            ["Main.LogChangePathsNoneRead"]   = "No file could be read.",
            ["Main.LogChangePathsCancelled"]  = "Change paths cancelled.",
            ["Main.LogChangePathsApplying"]   = "  Applying {0} change(s) to {1} file(s)...",
            ["Main.LogChangePathsBatchOk"]    = "  ✓ {0}: {1} occurrence(s) (.bak backup created)",
            ["Main.LogChangePathsSingleOk"]   = "  ✓ {0}: {1} occurrence(s)",
            ["Main.LogChangePathsDone"]       = "✓  Change paths completed in {0} file(s).",
            ["Main.StatusChangePathsDone"]    = "Change paths: {0} file(s) processed.",
            ["Main.LogFailureChangePaths"]    = "Failed to change paths. Check the log.",

            ["Main.OpDebinarize"]       = "Debinarize",
            ["Main.OpExtractRvmats"]    = "Extract RVMATs",
            ["Main.OpExtractCfg"]       = "Extract Model.cfg",

            ["Main.LogBatchHeader"]     = "{0} (batch): {1} file(s) in {2}",
            ["Main.LogBatchSkipNonOdol"]= "  — skipped (non-ODOL): {0}",
            ["Main.LogBatchSummary"]    = "✓  {0}: {1} processed, {2} skipped, {3} failed.",
            ["Main.StatusBatchSummary"] = "{0} (batch): {1} ok / {2} skipped / {3} failed",

            // Core logs (Converter / OdolExtract / P3DRepath)
            ["Core.LogOdolVersion"]        = "  ODOL v{0}  |  BisDll v{1}",
            ["Core.LogProtectedOdol"]      = "  Protected/obfuscated P3D detected; trying to open the real file instead of a reference.",
            ["Core.LogConvertingOdol"]     = "  Converting ODOL -> MLOD...",
            ["Core.LogLoadDirectFailed"]   = "  Direct load failed: {0}",
            ["Core.LogLoadStreamFailed"]   = "  Stream load failed: {0}",
            ["Core.LogTryFixHeader"]       = "  Trying to fix v74/v75 header order...",
            ["Core.LogApplyHeaderPatch"]   = "  Applying v75 header patch...",
            ["Core.LogHeaderReordered"]    = "  Header reordered: muzzleFlash before extras ({0},{1}).",
            ["Core.LogPatchSkipped"]       = "  Patch skipped: unexpected extra fields ({0},{1})",
            ["Core.LogPatchOk"]            = "  Patch v75 -> v{0} OK",
            ["Core.LogNoPatchWorked"]      = "  No version patch worked.",
            ["Core.LogSafeOutputName"]     = "  Output name normalized for Object Builder: {0}_debin.p3d",
            ["Core.ResultProtectedOdol"]   = "P3D ODOL v{0} protected/obfuscated. Could not decode the real file yet.",
            ["Core.ResultCannotLoadOdol"]  = "Could not load ODOL v{0}.",
            ["Core.ResultOdol2MlodNull"]   = "ODOL2MLOD returned null.",
            ["Core.ResultOdolOk"]          = "OK - ODOL v{0} converted successfully.",
            ["Core.ResultError"]           = "Error: {0}",
            ["Core.LogRvmatObfuscated"]    = "  rvmat (obfuscated name, renamed): {0} -> {1}",
            ["Core.LogRvmat"]              = "  rvmat: {0}",
            ["Core.LogRvmatsWarning"]      = "  warning: {0} RVMAT(s) had obfuscated names and were renamed; file content is intact.",
            ["Core.LogModelCfgLines"]      = "  model.cfg: {0} line(s)",
            ["Core.LogSkeleton"]           = "  skeleton: \"{0}\", {1} bone(s), {2} section(s)",
            ["Core.LogAnimationsRebuilt"]  = "  animations: {0} class(es) reconstructed",
            ["Core.LogPathNotFound"]       = "  warning: path not found in file: {0}",
            ["Core.LogPathReplaced"]       = "  {0}  ->  {1}   ({2}x)",
            ["Core.LogOdolStructNotRead"]  = "  warning: ODOL structure not read; scanning the entire file.",
            ["Core.ExCannotLoadOdol"]      = "Could not load ODOL (protected file or unsupported version).",
            ["Core.ExOdolStructure"]       = "Could not read ODOL structure (protected or unusual format).",
            ["Core.ExOdolAddressTable"]    = "ODOL offset table not located; path change aborted.",
            ["Core.ExOverlappingChanges"]  = "Overlapping path changes; review the edits.",

            // ChangePathsForm
            ["Cp.Title"]                = "Change paths",
            ["Cp.Info"]                 = "Check the files that will receive the changes. The path list is cumulative.",
            ["Cp.LblFiles"]             = "Loaded files:",
            ["Cp.LblFilesHint"]         = "Use the checkboxes to include/exclude; or click the buttons on the right.",
            ["Cp.LblPathsHeader"]       = "Paths (double-click to copy; Ctrl+C also works):",
            ["Cp.LblPathsHeaderCount"]  = "Paths (double-click to copy; Ctrl+C also works) — {0} in cumulative list:",
            ["Cp.ColCurrent"]           = "Current path",
            ["Cp.ColNew"]               = "New path",
            ["Cp.BtnAll"]               = "Check all",
            ["Cp.BtnNone"]              = "Uncheck all",
            ["Cp.LblFrom"]              = "Replace segment:",
            ["Cp.BtnApplyPart"]         = "Apply segment",
            ["Cp.Hint"]                 = "The segment must begin at the folder root. E.g.: replace  my_mod\\mods\\  with  new_mod\\data\\",
            ["Cp.BtnImport"]            = "Import list...",
            ["Cp.BtnExport"]            = "Export list...",
            ["Cp.BtnOk"]                = "Apply changes",
            ["Cp.BtnCancel"]            = "Cancel",
            ["Cp.MsgNeedFrom"]          = "Enter the segment to replace.",
            ["Cp.MsgSegmentApplied"]    = "Segment applied: {0} path(s) updated.",
            ["Cp.MsgImported"]          = "{0} rule(s) imported; {1} path(s) updated.",
            ["Cp.MsgImportFailed"]      = "Failed to import: {0}",
            ["Cp.MsgNoRulesToExport"]   = "No segment rule recorded. Use \"Replace segment\" or import a list to create reusable rules before exporting.",
            ["Cp.MsgExported"]          = "{0} rule(s) exported.",
            ["Cp.MsgExportFailed"]      = "Failed to export: {0}",
            ["Cp.MsgNoFileChecked"]     = "Check at least one file.",
            ["Cp.MsgNoChange"]          = "No path change was made.",
            ["Cp.MsgCopied"]            = "Copied: {0}",
            ["Cp.DlgImportTitle"]       = "Import path-change list",
            ["Cp.DlgExportTitle"]       = "Export path-change list",
            ["Cp.DlgListFilter"]        = "Path list (*.txt)|*.txt|All files (*.*)|*.*",
            ["Cp.DlgExportFilter"]      = "Path list (*.txt)|*.txt",
            ["Cp.DlgExportDefault"]     = "change_paths.txt",

            // TextPreviewForm
            ["Tp.TitleRvmats"]          = "RVMATs — {0}",
            ["Tp.TitleCfg"]             = "model.cfg — {0}",
            ["Tp.Info"]                 = "{0} file(s) reconstructed. Review below and save if desired.",
            ["Tp.BtnSave"]              = "Save to output folder",
            ["Tp.BtnClose"]             = "Close",
            ["Tp.MsgInvalidOutput"]     = "Invalid output folder.",
            ["Tp.MsgSaved"]             = "{0} file(s) saved to:\n{1}",
            ["Tp.MsgSaveFailed"]        = "Failed to save: {0}",
        },

        [Language.Portuguese] = new()
        {
            ["Main.Title"]              = "P3D Debinarizer — Arma 3",
            ["Main.Subtitle"]           = "Debinariza, extrai RVMATs / model.cfg e troca caminhos de modelos .p3d",
            ["Main.LblInput"]           = "Arquivo .p3d ou pasta (lote) — ou arraste aqui:",
            ["Main.LblOutput"]          = "Pasta de saída:",
            ["Main.LblLog"]             = "Log:",
            ["Main.LblLanguage"]        = "Idioma:",
            ["Main.StatusReady"]        = "Pronto.",
            ["Main.StatusBusy"]         = "Processando...",
            ["Main.StatusCancelled"]    = "Operação cancelada.",

            ["Main.FmtNone"]            = "Formato: — (selecione um arquivo ou pasta)",
            ["Main.FmtOdol"]            = "Formato: ODOL (binarizado) — todas as funções disponíveis.",
            ["Main.FmtMlod"]            = "Formato: MLOD (não binarizado) — apenas troca de caminhos.",
            ["Main.FmtUnknown"]         = "Formato: desconhecido — não é um P3D válido.",
            ["Main.FmtBatch"]           = "Lote: {0} arquivo(s) .p3d na pasta e subpastas.",

            ["Main.BtnFile"]            = "Arquivo...",
            ["Main.BtnFolder"]          = "Pasta...",
            ["Main.BtnSelect"]          = "Selecionar...",
            ["Main.BtnChangePaths"]     = "Trocar Caminhos",
            ["Main.BtnDebin"]           = "Debinarizar",
            ["Main.BtnExtractRvmats"]   = "Extrair RVMATs",
            ["Main.BtnExtractCfg"]      = "Extrair Model.cfg",

            ["Main.DlgFileTitle"]       = "Selecionar arquivo P3D",
            ["Main.DlgFileFilter"]      = "Modelos P3D (*.p3d)|*.p3d|Todos os arquivos (*.*)|*.*",
            ["Main.DlgFolderInTitle"]   = "Selecionar pasta com modelos .p3d (lote)",
            ["Main.DlgFolderOutTitle"]  = "Selecionar pasta de saída",

            ["Main.LogInputNotFound"]   = "Entrada não encontrada: {0}",
            ["Main.LogOutputNotFound"]  = "Pasta de saída não encontrada: {0}",
            ["Main.LogNoP3dFound"]      = "Nenhum arquivo .p3d encontrado.",

            ["Main.LogDebinarizing"]    = "Debinarizando: {0}",
            ["Main.LogSavedTo"]         = "   Salvo em: {0}",
            ["Main.LogConcluded"]       = "Concluído: {0}",
            ["Main.LogFailureDebin"]    = "Falha na debinarização. Veja o log.",

            ["Main.LogExtractingRvmats"]   = "Extraindo RVMATs: {0}",
            ["Main.LogNoRvmats"]           = "Nenhum RVMAT embutido neste modelo.",
            ["Main.LogStatusNoRvmats"]     = "Nenhum RVMAT encontrado.",
            ["Main.LogRvmatsReconstructed"]= "✓  {0} RVMAT(s) reconstruído(s).",
            ["Main.LogStatusRvmatsPreview"]= "{0} RVMAT(s) — preview aberto.",
            ["Main.LogFailureRvmats"]      = "Falha ao extrair RVMATs. Veja o log.",

            ["Main.LogExtractingCfg"]    = "Extraindo model.cfg: {0}",
            ["Main.LogCfgReconstructed"] = "✓  {0} reconstruído.",
            ["Main.LogStatusCfgPreview"] = "model.cfg reconstruído — preview aberto.",
            ["Main.LogFailureCfg"]       = "Falha ao extrair model.cfg. Veja o log.",

            ["Main.LogChangePathsReading"]    = "Trocar caminhos: lendo {0} arquivo(s)...",
            ["Main.LogChangePathsItem"]       = "  {0}: {1} caminho(s)",
            ["Main.LogChangePathsNoneRead"]   = "Nenhum arquivo pôde ser lido.",
            ["Main.LogChangePathsCancelled"]  = "Troca de caminhos cancelada.",
            ["Main.LogChangePathsApplying"]   = "  Aplicando {0} alteração(ões) em {1} arquivo(s)...",
            ["Main.LogChangePathsBatchOk"]    = "  ✓ {0}: {1} ocorrência(s) (backup .bak criado)",
            ["Main.LogChangePathsSingleOk"]   = "  ✓ {0}: {1} ocorrência(s)",
            ["Main.LogChangePathsDone"]       = "✓  Troca de caminhos concluída em {0} arquivo(s).",
            ["Main.StatusChangePathsDone"]    = "Troca de caminhos: {0} arquivo(s) processado(s).",
            ["Main.LogFailureChangePaths"]    = "Falha ao trocar caminhos. Veja o log.",

            ["Main.OpDebinarize"]       = "Debinarizar",
            ["Main.OpExtractRvmats"]    = "Extrair RVMATs",
            ["Main.OpExtractCfg"]       = "Extrair Model.cfg",

            ["Main.LogBatchHeader"]     = "{0} (lote): {1} arquivo(s) em {2}",
            ["Main.LogBatchSkipNonOdol"]= "  — ignorado (não-ODOL): {0}",
            ["Main.LogBatchSummary"]    = "✓  {0}: {1} processado(s), {2} ignorado(s), {3} falha(s).",
            ["Main.StatusBatchSummary"] = "{0} (lote): {1} ok / {2} ignorados / {3} falhas",

            // Core logs
            ["Core.LogOdolVersion"]        = "  ODOL v{0}  |  BisDll v{1}",
            ["Core.LogProtectedOdol"]      = "  P3D protegido/obfuscado detectado; tentando abrir o arquivo real em vez de uma referência.",
            ["Core.LogConvertingOdol"]     = "  Convertendo ODOL -> MLOD...",
            ["Core.LogLoadDirectFailed"]   = "  Load direto falhou: {0}",
            ["Core.LogLoadStreamFailed"]   = "  Load via stream falhou: {0}",
            ["Core.LogTryFixHeader"]       = "  Tentando corrigir ordem do header v74/v75...",
            ["Core.LogApplyHeaderPatch"]   = "  Aplicando patch de header v75...",
            ["Core.LogHeaderReordered"]    = "  Header reordenado: muzzleFlash antes dos extras ({0},{1}).",
            ["Core.LogPatchSkipped"]       = "  Patch ignorado: campos extras inesperados ({0},{1})",
            ["Core.LogPatchOk"]            = "  Patch v75 -> v{0} OK",
            ["Core.LogNoPatchWorked"]      = "  Nenhum patch de versão funcionou.",
            ["Core.LogSafeOutputName"]     = "  Nome de saída normalizado para Object Builder: {0}_debin.p3d",
            ["Core.ResultProtectedOdol"]   = "P3D ODOL v{0} protegido/obfuscado. Ainda não foi possível decodificar o arquivo real.",
            ["Core.ResultCannotLoadOdol"]  = "Não foi possível carregar ODOL v{0}.",
            ["Core.ResultOdol2MlodNull"]   = "ODOL2MLOD retornou null.",
            ["Core.ResultOdolOk"]          = "OK - ODOL v{0} convertido com sucesso.",
            ["Core.ResultError"]           = "Erro: {0}",
            ["Core.LogRvmatObfuscated"]    = "  rvmat (nome obfuscado, renomeado): {0} -> {1}",
            ["Core.LogRvmat"]              = "  rvmat: {0}",
            ["Core.LogRvmatsWarning"]      = "  aviso: {0} RVMAT(s) tinham nome obfuscado e foram renomeados; o conteúdo dos arquivos está intacto.",
            ["Core.LogModelCfgLines"]      = "  model.cfg: {0} linha(s)",
            ["Core.LogSkeleton"]           = "  esqueleto: \"{0}\", {1} osso(s), {2} seção(ões)",
            ["Core.LogAnimationsRebuilt"]  = "  animações: {0} classe(s) reconstruída(s)",
            ["Core.LogPathNotFound"]       = "  aviso: caminho não encontrado no arquivo: {0}",
            ["Core.LogPathReplaced"]       = "  {0}  ->  {1}   ({2}x)",
            ["Core.LogOdolStructNotRead"]  = "  aviso: estrutura do ODOL não lida; varrendo o arquivo inteiro.",
            ["Core.ExCannotLoadOdol"]      = "Não foi possível carregar o ODOL (arquivo protegido ou versão não suportada).",
            ["Core.ExOdolStructure"]       = "Não foi possível ler a estrutura do ODOL (formato protegido ou incomum).",
            ["Core.ExOdolAddressTable"]    = "Tabela de offsets do ODOL não localizada; troca de caminhos abortada.",
            ["Core.ExOverlappingChanges"]  = "Trocas de caminho sobrepostas; revise as alterações.",

            ["Cp.Title"]                = "Trocar caminhos",
            ["Cp.Info"]                 = "Marque os arquivos que vão receber as alterações. A lista de caminhos é acumulada.",
            ["Cp.LblFiles"]             = "Arquivos carregados:",
            ["Cp.LblFilesHint"]         = "Use as caixas para incluir/excluir; ou clique nos botões à direita.",
            ["Cp.LblPathsHeader"]       = "Caminhos (duplo clique para copiar; Ctrl+C também funciona):",
            ["Cp.LblPathsHeaderCount"]  = "Caminhos (duplo clique para copiar; Ctrl+C também funciona) — {0} no acumulado:",
            ["Cp.ColCurrent"]           = "Caminho atual",
            ["Cp.ColNew"]               = "Novo caminho",
            ["Cp.BtnAll"]               = "Marcar todos",
            ["Cp.BtnNone"]              = "Desmarcar todos",
            ["Cp.LblFrom"]              = "Trocar trecho:",
            ["Cp.BtnApplyPart"]         = "Aplicar trecho",
            ["Cp.Hint"]                 = "O trecho deve começar na raiz da pasta. Ex.: trocar  meu_mod\\mods\\  por  novo_mod\\data\\",
            ["Cp.BtnImport"]            = "Importar lista...",
            ["Cp.BtnExport"]            = "Exportar lista...",
            ["Cp.BtnOk"]                = "Aplicar alterações",
            ["Cp.BtnCancel"]            = "Cancelar",
            ["Cp.MsgNeedFrom"]          = "Informe o trecho a ser trocado.",
            ["Cp.MsgSegmentApplied"]    = "Trecho aplicado: {0} caminho(s) atualizado(s).",
            ["Cp.MsgImported"]          = "{0} regra(s) importada(s); {1} caminho(s) atualizado(s).",
            ["Cp.MsgImportFailed"]      = "Falha ao importar: {0}",
            ["Cp.MsgNoRulesToExport"]   = "Nenhuma regra de trecho registrada. Use \"Trocar trecho\" ou importe uma lista para criar regras reutilizáveis antes de exportar.",
            ["Cp.MsgExported"]          = "{0} regra(s) exportada(s).",
            ["Cp.MsgExportFailed"]      = "Falha ao exportar: {0}",
            ["Cp.MsgNoFileChecked"]     = "Marque pelo menos um arquivo.",
            ["Cp.MsgNoChange"]          = "Nenhuma alteração de caminho foi feita.",
            ["Cp.MsgCopied"]            = "Copiado: {0}",
            ["Cp.DlgImportTitle"]       = "Importar lista de troca de caminhos",
            ["Cp.DlgExportTitle"]       = "Exportar lista de troca de caminhos",
            ["Cp.DlgListFilter"]        = "Lista de caminhos (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",
            ["Cp.DlgExportFilter"]      = "Lista de caminhos (*.txt)|*.txt",
            ["Cp.DlgExportDefault"]     = "trocar_caminhos.txt",

            ["Tp.TitleRvmats"]          = "RVMATs — {0}",
            ["Tp.TitleCfg"]             = "model.cfg — {0}",
            ["Tp.Info"]                 = "{0} arquivo(s) reconstruído(s). Revise abaixo e salve se desejar.",
            ["Tp.BtnSave"]              = "Salvar na pasta de saída",
            ["Tp.BtnClose"]             = "Fechar",
            ["Tp.MsgInvalidOutput"]     = "Pasta de saída inválida.",
            ["Tp.MsgSaved"]             = "{0} arquivo(s) salvo(s) em:\n{1}",
            ["Tp.MsgSaveFailed"]        = "Falha ao salvar: {0}",
        },

        [Language.German] = new()
        {
            ["Main.Title"]              = "P3D Debinarizer — Arma 3",
            ["Main.Subtitle"]           = "Debinarisiert, extrahiert RVMATs / model.cfg und ändert Pfade von .p3d-Modellen",
            ["Main.LblInput"]           = "P3D-Datei oder Ordner (Stapel) — oder hierher ziehen:",
            ["Main.LblOutput"]          = "Ausgabeordner:",
            ["Main.LblLog"]             = "Protokoll:",
            ["Main.LblLanguage"]        = "Sprache:",
            ["Main.StatusReady"]        = "Bereit.",
            ["Main.StatusBusy"]         = "Verarbeitung...",
            ["Main.StatusCancelled"]    = "Vorgang abgebrochen.",

            ["Main.FmtNone"]            = "Format: — (Datei oder Ordner wählen)",
            ["Main.FmtOdol"]            = "Format: ODOL (binarisiert) — alle Funktionen verfügbar.",
            ["Main.FmtMlod"]            = "Format: MLOD (nicht binarisiert) — nur Pfadänderung.",
            ["Main.FmtUnknown"]         = "Format: unbekannt — keine gültige P3D-Datei.",
            ["Main.FmtBatch"]           = "Stapel: {0} .p3d-Datei(en) im Ordner und Unterordnern.",

            ["Main.BtnFile"]            = "Datei...",
            ["Main.BtnFolder"]          = "Ordner...",
            ["Main.BtnSelect"]          = "Auswählen...",
            ["Main.BtnChangePaths"]     = "Pfade ändern",
            ["Main.BtnDebin"]           = "Debinarisieren",
            ["Main.BtnExtractRvmats"]   = "RVMATs extrahieren",
            ["Main.BtnExtractCfg"]      = "Model.cfg extrahieren",

            ["Main.DlgFileTitle"]       = "P3D-Datei auswählen",
            ["Main.DlgFileFilter"]      = "P3D-Modelle (*.p3d)|*.p3d|Alle Dateien (*.*)|*.*",
            ["Main.DlgFolderInTitle"]   = "Ordner mit .p3d-Modellen wählen (Stapel)",
            ["Main.DlgFolderOutTitle"]  = "Ausgabeordner wählen",

            ["Main.LogInputNotFound"]   = "Eingabe nicht gefunden: {0}",
            ["Main.LogOutputNotFound"]  = "Ausgabeordner nicht gefunden: {0}",
            ["Main.LogNoP3dFound"]      = "Keine .p3d-Datei gefunden.",

            ["Main.LogDebinarizing"]    = "Debinarisierung: {0}",
            ["Main.LogSavedTo"]         = "   Gespeichert in: {0}",
            ["Main.LogConcluded"]       = "Abgeschlossen: {0}",
            ["Main.LogFailureDebin"]    = "Debinarisierung fehlgeschlagen. Siehe Protokoll.",

            ["Main.LogExtractingRvmats"]   = "Extrahiere RVMATs: {0}",
            ["Main.LogNoRvmats"]           = "Keine eingebetteten RVMATs in diesem Modell.",
            ["Main.LogStatusNoRvmats"]     = "Keine RVMATs gefunden.",
            ["Main.LogRvmatsReconstructed"]= "✓  {0} RVMAT(s) rekonstruiert.",
            ["Main.LogStatusRvmatsPreview"]= "{0} RVMAT(s) — Vorschau geöffnet.",
            ["Main.LogFailureRvmats"]      = "RVMAT-Extraktion fehlgeschlagen. Siehe Protokoll.",

            ["Main.LogExtractingCfg"]    = "Extrahiere model.cfg: {0}",
            ["Main.LogCfgReconstructed"] = "✓  {0} rekonstruiert.",
            ["Main.LogStatusCfgPreview"] = "model.cfg rekonstruiert — Vorschau geöffnet.",
            ["Main.LogFailureCfg"]       = "model.cfg-Extraktion fehlgeschlagen. Siehe Protokoll.",

            ["Main.LogChangePathsReading"]    = "Pfade ändern: lese {0} Datei(en)...",
            ["Main.LogChangePathsItem"]       = "  {0}: {1} Pfad(e)",
            ["Main.LogChangePathsNoneRead"]   = "Keine Datei konnte gelesen werden.",
            ["Main.LogChangePathsCancelled"]  = "Pfadänderung abgebrochen.",
            ["Main.LogChangePathsApplying"]   = "  Wende {0} Änderung(en) auf {1} Datei(en) an...",
            ["Main.LogChangePathsBatchOk"]    = "  ✓ {0}: {1} Vorkommen ( .bak-Backup erstellt)",
            ["Main.LogChangePathsSingleOk"]   = "  ✓ {0}: {1} Vorkommen",
            ["Main.LogChangePathsDone"]       = "✓  Pfadänderung in {0} Datei(en) abgeschlossen.",
            ["Main.StatusChangePathsDone"]    = "Pfade ändern: {0} Datei(en) verarbeitet.",
            ["Main.LogFailureChangePaths"]    = "Pfadänderung fehlgeschlagen. Siehe Protokoll.",

            ["Main.OpDebinarize"]       = "Debinarisieren",
            ["Main.OpExtractRvmats"]    = "RVMATs extrahieren",
            ["Main.OpExtractCfg"]       = "Model.cfg extrahieren",

            ["Main.LogBatchHeader"]     = "{0} (Stapel): {1} Datei(en) in {2}",
            ["Main.LogBatchSkipNonOdol"]= "  — übersprungen (nicht-ODOL): {0}",
            ["Main.LogBatchSummary"]    = "✓  {0}: {1} verarbeitet, {2} übersprungen, {3} fehlgeschlagen.",
            ["Main.StatusBatchSummary"] = "{0} (Stapel): {1} ok / {2} übersprungen / {3} Fehler",

            // Core logs
            ["Core.LogOdolVersion"]        = "  ODOL v{0}  |  BisDll v{1}",
            ["Core.LogProtectedOdol"]      = "  Geschütztes/verschleiertes P3D erkannt; versuche die echte Datei statt eines Verweises zu öffnen.",
            ["Core.LogConvertingOdol"]     = "  Konvertiere ODOL -> MLOD...",
            ["Core.LogLoadDirectFailed"]   = "  Direktes Laden fehlgeschlagen: {0}",
            ["Core.LogLoadStreamFailed"]   = "  Laden via Stream fehlgeschlagen: {0}",
            ["Core.LogTryFixHeader"]       = "  Versuche v74/v75-Header-Reihenfolge zu korrigieren...",
            ["Core.LogApplyHeaderPatch"]   = "  Wende v75-Header-Patch an...",
            ["Core.LogHeaderReordered"]    = "  Header neu geordnet: muzzleFlash vor Extras ({0},{1}).",
            ["Core.LogPatchSkipped"]       = "  Patch übersprungen: unerwartete Extra-Felder ({0},{1})",
            ["Core.LogPatchOk"]            = "  Patch v75 -> v{0} OK",
            ["Core.LogNoPatchWorked"]      = "  Kein Versions-Patch hat funktioniert.",
            ["Core.LogSafeOutputName"]     = "  Ausgabename für Object Builder normalisiert: {0}_debin.p3d",
            ["Core.ResultProtectedOdol"]   = "P3D ODOL v{0} geschützt/verschleiert. Die echte Datei konnte noch nicht dekodiert werden.",
            ["Core.ResultCannotLoadOdol"]  = "ODOL v{0} konnte nicht geladen werden.",
            ["Core.ResultOdol2MlodNull"]   = "ODOL2MLOD lieferte null.",
            ["Core.ResultOdolOk"]          = "OK - ODOL v{0} erfolgreich konvertiert.",
            ["Core.ResultError"]           = "Fehler: {0}",
            ["Core.LogRvmatObfuscated"]    = "  rvmat (verschleierter Name, umbenannt): {0} -> {1}",
            ["Core.LogRvmat"]              = "  rvmat: {0}",
            ["Core.LogRvmatsWarning"]      = "  Warnung: {0} RVMAT(s) hatten verschleierte Namen und wurden umbenannt; der Dateiinhalt ist intakt.",
            ["Core.LogModelCfgLines"]      = "  model.cfg: {0} Zeile(n)",
            ["Core.LogSkeleton"]           = "  Skelett: \"{0}\", {1} Knochen, {2} Sektion(en)",
            ["Core.LogAnimationsRebuilt"]  = "  Animationen: {0} Klasse(n) rekonstruiert",
            ["Core.LogPathNotFound"]       = "  Warnung: Pfad nicht in der Datei gefunden: {0}",
            ["Core.LogPathReplaced"]       = "  {0}  ->  {1}   ({2}x)",
            ["Core.LogOdolStructNotRead"]  = "  Warnung: ODOL-Struktur nicht gelesen; durchsuche die gesamte Datei.",
            ["Core.ExCannotLoadOdol"]      = "ODOL konnte nicht geladen werden (geschützte Datei oder nicht unterstützte Version).",
            ["Core.ExOdolStructure"]       = "ODOL-Struktur konnte nicht gelesen werden (geschützt oder ungewöhnliches Format).",
            ["Core.ExOdolAddressTable"]    = "ODOL-Offset-Tabelle nicht gefunden; Pfadänderung abgebrochen.",
            ["Core.ExOverlappingChanges"]  = "Überlappende Pfadänderungen; bitte Änderungen überprüfen.",

            ["Cp.Title"]                = "Pfade ändern",
            ["Cp.Info"]                 = "Wählen Sie die Dateien aus, die geändert werden. Die Pfadliste ist kumulativ.",
            ["Cp.LblFiles"]             = "Geladene Dateien:",
            ["Cp.LblFilesHint"]         = "Mit den Kontrollkästchen ein-/ausschließen; oder die Schaltflächen rechts nutzen.",
            ["Cp.LblPathsHeader"]       = "Pfade (Doppelklick kopiert; Strg+C funktioniert auch):",
            ["Cp.LblPathsHeaderCount"]  = "Pfade (Doppelklick kopiert; Strg+C funktioniert auch) — {0} in der kumulativen Liste:",
            ["Cp.ColCurrent"]           = "Aktueller Pfad",
            ["Cp.ColNew"]               = "Neuer Pfad",
            ["Cp.BtnAll"]               = "Alle markieren",
            ["Cp.BtnNone"]              = "Alle abwählen",
            ["Cp.LblFrom"]              = "Abschnitt ersetzen:",
            ["Cp.BtnApplyPart"]         = "Abschnitt anwenden",
            ["Cp.Hint"]                 = "Der Abschnitt muss an der Stammordner-Wurzel beginnen. Z.B.: ersetze  mein_mod\\mods\\  durch  neu_mod\\data\\",
            ["Cp.BtnImport"]            = "Liste importieren...",
            ["Cp.BtnExport"]            = "Liste exportieren...",
            ["Cp.BtnOk"]                = "Änderungen anwenden",
            ["Cp.BtnCancel"]            = "Abbrechen",
            ["Cp.MsgNeedFrom"]          = "Bitte den zu ersetzenden Abschnitt angeben.",
            ["Cp.MsgSegmentApplied"]    = "Abschnitt angewendet: {0} Pfad(e) aktualisiert.",
            ["Cp.MsgImported"]          = "{0} Regel(n) importiert; {1} Pfad(e) aktualisiert.",
            ["Cp.MsgImportFailed"]      = "Import fehlgeschlagen: {0}",
            ["Cp.MsgNoRulesToExport"]   = "Keine Abschnittsregel erfasst. Nutzen Sie \"Abschnitt ersetzen\" oder importieren Sie eine Liste, um wiederverwendbare Regeln zu erstellen, bevor Sie exportieren.",
            ["Cp.MsgExported"]          = "{0} Regel(n) exportiert.",
            ["Cp.MsgExportFailed"]      = "Export fehlgeschlagen: {0}",
            ["Cp.MsgNoFileChecked"]     = "Mindestens eine Datei markieren.",
            ["Cp.MsgNoChange"]          = "Keine Pfadänderung vorgenommen.",
            ["Cp.MsgCopied"]            = "Kopiert: {0}",
            ["Cp.DlgImportTitle"]       = "Liste der Pfadänderungen importieren",
            ["Cp.DlgExportTitle"]       = "Liste der Pfadänderungen exportieren",
            ["Cp.DlgListFilter"]        = "Pfadliste (*.txt)|*.txt|Alle Dateien (*.*)|*.*",
            ["Cp.DlgExportFilter"]      = "Pfadliste (*.txt)|*.txt",
            ["Cp.DlgExportDefault"]     = "pfade_aendern.txt",

            ["Tp.TitleRvmats"]          = "RVMATs — {0}",
            ["Tp.TitleCfg"]             = "model.cfg — {0}",
            ["Tp.Info"]                 = "{0} Datei(en) rekonstruiert. Unten prüfen und ggf. speichern.",
            ["Tp.BtnSave"]              = "In Ausgabeordner speichern",
            ["Tp.BtnClose"]             = "Schließen",
            ["Tp.MsgInvalidOutput"]     = "Ungültiger Ausgabeordner.",
            ["Tp.MsgSaved"]             = "{0} Datei(en) gespeichert in:\n{1}",
            ["Tp.MsgSaveFailed"]        = "Speichern fehlgeschlagen: {0}",
        },

        [Language.Spanish] = new()
        {
            ["Main.Title"]              = "P3D Debinarizer — Arma 3",
            ["Main.Subtitle"]           = "Debinariza, extrae RVMATs / model.cfg y cambia rutas de modelos .p3d",
            ["Main.LblInput"]           = "Archivo .p3d o carpeta (lote) — o arrástrelo aquí:",
            ["Main.LblOutput"]          = "Carpeta de salida:",
            ["Main.LblLog"]             = "Registro:",
            ["Main.LblLanguage"]        = "Idioma:",
            ["Main.StatusReady"]        = "Listo.",
            ["Main.StatusBusy"]         = "Procesando...",
            ["Main.StatusCancelled"]    = "Operación cancelada.",

            ["Main.FmtNone"]            = "Formato: — (seleccione un archivo o carpeta)",
            ["Main.FmtOdol"]            = "Formato: ODOL (binarizado) — todas las funciones disponibles.",
            ["Main.FmtMlod"]            = "Formato: MLOD (no binarizado) — solo cambio de rutas.",
            ["Main.FmtUnknown"]         = "Formato: desconocido — no es un P3D válido.",
            ["Main.FmtBatch"]           = "Lote: {0} archivo(s) .p3d en la carpeta y subcarpetas.",

            ["Main.BtnFile"]            = "Archivo...",
            ["Main.BtnFolder"]          = "Carpeta...",
            ["Main.BtnSelect"]          = "Seleccionar...",
            ["Main.BtnChangePaths"]     = "Cambiar Rutas",
            ["Main.BtnDebin"]           = "Debinarizar",
            ["Main.BtnExtractRvmats"]   = "Extraer RVMATs",
            ["Main.BtnExtractCfg"]      = "Extraer Model.cfg",

            ["Main.DlgFileTitle"]       = "Seleccionar archivo P3D",
            ["Main.DlgFileFilter"]      = "Modelos P3D (*.p3d)|*.p3d|Todos los archivos (*.*)|*.*",
            ["Main.DlgFolderInTitle"]   = "Seleccionar carpeta con modelos .p3d (lote)",
            ["Main.DlgFolderOutTitle"]  = "Seleccionar carpeta de salida",

            ["Main.LogInputNotFound"]   = "Entrada no encontrada: {0}",
            ["Main.LogOutputNotFound"]  = "Carpeta de salida no encontrada: {0}",
            ["Main.LogNoP3dFound"]      = "No se encontraron archivos .p3d.",

            ["Main.LogDebinarizing"]    = "Debinarizando: {0}",
            ["Main.LogSavedTo"]         = "   Guardado en: {0}",
            ["Main.LogConcluded"]       = "Concluido: {0}",
            ["Main.LogFailureDebin"]    = "Falló la debinarización. Vea el registro.",

            ["Main.LogExtractingRvmats"]   = "Extrayendo RVMATs: {0}",
            ["Main.LogNoRvmats"]           = "Sin RVMATs incrustados en este modelo.",
            ["Main.LogStatusNoRvmats"]     = "No se encontraron RVMATs.",
            ["Main.LogRvmatsReconstructed"]= "✓  {0} RVMAT(s) reconstruido(s).",
            ["Main.LogStatusRvmatsPreview"]= "{0} RVMAT(s) — vista previa abierta.",
            ["Main.LogFailureRvmats"]      = "Falló la extracción de RVMATs. Vea el registro.",

            ["Main.LogExtractingCfg"]    = "Extrayendo model.cfg: {0}",
            ["Main.LogCfgReconstructed"] = "✓  {0} reconstruido.",
            ["Main.LogStatusCfgPreview"] = "model.cfg reconstruido — vista previa abierta.",
            ["Main.LogFailureCfg"]       = "Falló la extracción de model.cfg. Vea el registro.",

            ["Main.LogChangePathsReading"]    = "Cambiar rutas: leyendo {0} archivo(s)...",
            ["Main.LogChangePathsItem"]       = "  {0}: {1} ruta(s)",
            ["Main.LogChangePathsNoneRead"]   = "Ningún archivo pudo ser leído.",
            ["Main.LogChangePathsCancelled"]  = "Cambio de rutas cancelado.",
            ["Main.LogChangePathsApplying"]   = "  Aplicando {0} cambio(s) en {1} archivo(s)...",
            ["Main.LogChangePathsBatchOk"]    = "  ✓ {0}: {1} ocurrencia(s) (copia .bak creada)",
            ["Main.LogChangePathsSingleOk"]   = "  ✓ {0}: {1} ocurrencia(s)",
            ["Main.LogChangePathsDone"]       = "✓  Cambio de rutas completado en {0} archivo(s).",
            ["Main.StatusChangePathsDone"]    = "Cambiar rutas: {0} archivo(s) procesado(s).",
            ["Main.LogFailureChangePaths"]    = "Falló el cambio de rutas. Vea el registro.",

            ["Main.OpDebinarize"]       = "Debinarizar",
            ["Main.OpExtractRvmats"]    = "Extraer RVMATs",
            ["Main.OpExtractCfg"]       = "Extraer Model.cfg",

            ["Main.LogBatchHeader"]     = "{0} (lote): {1} archivo(s) en {2}",
            ["Main.LogBatchSkipNonOdol"]= "  — omitido (no-ODOL): {0}",
            ["Main.LogBatchSummary"]    = "✓  {0}: {1} procesado(s), {2} omitido(s), {3} fallo(s).",
            ["Main.StatusBatchSummary"] = "{0} (lote): {1} ok / {2} omitidos / {3} fallos",

            // Core logs
            ["Core.LogOdolVersion"]        = "  ODOL v{0}  |  BisDll v{1}",
            ["Core.LogProtectedOdol"]      = "  P3D protegido/ofuscado detectado; intentando abrir el archivo real en vez de una referencia.",
            ["Core.LogConvertingOdol"]     = "  Convirtiendo ODOL -> MLOD...",
            ["Core.LogLoadDirectFailed"]   = "  Carga directa falló: {0}",
            ["Core.LogLoadStreamFailed"]   = "  Carga vía stream falló: {0}",
            ["Core.LogTryFixHeader"]       = "  Intentando corregir orden del header v74/v75...",
            ["Core.LogApplyHeaderPatch"]   = "  Aplicando parche de header v75...",
            ["Core.LogHeaderReordered"]    = "  Header reordenado: muzzleFlash antes de los extras ({0},{1}).",
            ["Core.LogPatchSkipped"]       = "  Parche omitido: campos extra inesperados ({0},{1})",
            ["Core.LogPatchOk"]            = "  Parche v75 -> v{0} OK",
            ["Core.LogNoPatchWorked"]      = "  Ningún parche de versión funcionó.",
            ["Core.LogSafeOutputName"]     = "  Nombre de salida normalizado para Object Builder: {0}_debin.p3d",
            ["Core.ResultProtectedOdol"]   = "P3D ODOL v{0} protegido/ofuscado. Aún no se pudo decodificar el archivo real.",
            ["Core.ResultCannotLoadOdol"]  = "No se pudo cargar ODOL v{0}.",
            ["Core.ResultOdol2MlodNull"]   = "ODOL2MLOD devolvió null.",
            ["Core.ResultOdolOk"]          = "OK - ODOL v{0} convertido con éxito.",
            ["Core.ResultError"]           = "Error: {0}",
            ["Core.LogRvmatObfuscated"]    = "  rvmat (nombre ofuscado, renombrado): {0} -> {1}",
            ["Core.LogRvmat"]              = "  rvmat: {0}",
            ["Core.LogRvmatsWarning"]      = "  aviso: {0} RVMAT(s) tenían nombres ofuscados y fueron renombrados; el contenido de los archivos está intacto.",
            ["Core.LogModelCfgLines"]      = "  model.cfg: {0} línea(s)",
            ["Core.LogSkeleton"]           = "  esqueleto: \"{0}\", {1} hueso(s), {2} sección(es)",
            ["Core.LogAnimationsRebuilt"]  = "  animaciones: {0} clase(s) reconstruida(s)",
            ["Core.LogPathNotFound"]       = "  aviso: ruta no encontrada en el archivo: {0}",
            ["Core.LogPathReplaced"]       = "  {0}  ->  {1}   ({2}x)",
            ["Core.LogOdolStructNotRead"]  = "  aviso: estructura ODOL no leída; escaneando el archivo completo.",
            ["Core.ExCannotLoadOdol"]      = "No se pudo cargar el ODOL (archivo protegido o versión no soportada).",
            ["Core.ExOdolStructure"]       = "No se pudo leer la estructura del ODOL (formato protegido o inusual).",
            ["Core.ExOdolAddressTable"]    = "Tabla de offsets del ODOL no localizada; cambio de rutas abortado.",
            ["Core.ExOverlappingChanges"]  = "Cambios de ruta superpuestos; revise las modificaciones.",

            ["Cp.Title"]                = "Cambiar rutas",
            ["Cp.Info"]                 = "Marque los archivos que recibirán los cambios. La lista de rutas es acumulativa.",
            ["Cp.LblFiles"]             = "Archivos cargados:",
            ["Cp.LblFilesHint"]         = "Use las casillas para incluir/excluir; o haga clic en los botones de la derecha.",
            ["Cp.LblPathsHeader"]       = "Rutas (doble clic para copiar; Ctrl+C también funciona):",
            ["Cp.LblPathsHeaderCount"]  = "Rutas (doble clic para copiar; Ctrl+C también funciona) — {0} en el acumulado:",
            ["Cp.ColCurrent"]           = "Ruta actual",
            ["Cp.ColNew"]               = "Nueva ruta",
            ["Cp.BtnAll"]               = "Marcar todos",
            ["Cp.BtnNone"]              = "Desmarcar todos",
            ["Cp.LblFrom"]              = "Reemplazar segmento:",
            ["Cp.BtnApplyPart"]         = "Aplicar segmento",
            ["Cp.Hint"]                 = "El segmento debe comenzar en la raíz de la carpeta. Ej.: reemplazar  mi_mod\\mods\\  por  nuevo_mod\\data\\",
            ["Cp.BtnImport"]            = "Importar lista...",
            ["Cp.BtnExport"]            = "Exportar lista...",
            ["Cp.BtnOk"]                = "Aplicar cambios",
            ["Cp.BtnCancel"]            = "Cancelar",
            ["Cp.MsgNeedFrom"]          = "Indique el segmento a reemplazar.",
            ["Cp.MsgSegmentApplied"]    = "Segmento aplicado: {0} ruta(s) actualizada(s).",
            ["Cp.MsgImported"]          = "{0} regla(s) importada(s); {1} ruta(s) actualizada(s).",
            ["Cp.MsgImportFailed"]      = "Falló la importación: {0}",
            ["Cp.MsgNoRulesToExport"]   = "Ninguna regla de segmento registrada. Use \"Reemplazar segmento\" o importe una lista para crear reglas reutilizables antes de exportar.",
            ["Cp.MsgExported"]          = "{0} regla(s) exportada(s).",
            ["Cp.MsgExportFailed"]      = "Falló la exportación: {0}",
            ["Cp.MsgNoFileChecked"]     = "Marque al menos un archivo.",
            ["Cp.MsgNoChange"]          = "No se hizo ningún cambio de ruta.",
            ["Cp.MsgCopied"]            = "Copiado: {0}",
            ["Cp.DlgImportTitle"]       = "Importar lista de cambio de rutas",
            ["Cp.DlgExportTitle"]       = "Exportar lista de cambio de rutas",
            ["Cp.DlgListFilter"]        = "Lista de rutas (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
            ["Cp.DlgExportFilter"]      = "Lista de rutas (*.txt)|*.txt",
            ["Cp.DlgExportDefault"]     = "cambiar_rutas.txt",

            ["Tp.TitleRvmats"]          = "RVMATs — {0}",
            ["Tp.TitleCfg"]             = "model.cfg — {0}",
            ["Tp.Info"]                 = "{0} archivo(s) reconstruido(s). Revise abajo y guarde si lo desea.",
            ["Tp.BtnSave"]              = "Guardar en carpeta de salida",
            ["Tp.BtnClose"]             = "Cerrar",
            ["Tp.MsgInvalidOutput"]     = "Carpeta de salida inválida.",
            ["Tp.MsgSaved"]             = "{0} archivo(s) guardado(s) en:\n{1}",
            ["Tp.MsgSaveFailed"]        = "Falló al guardar: {0}",
        },
    };
}
