using System.Text.Json;
using Luna;
using Luna.Generators;
using Penumbra.Files;
using Penumbra.Import.Structs;
using Penumbra.Services;

namespace Penumbra;

public sealed partial class IoConfig : ConfigurationFile<FilenameService>
{
    #region Import

    [ConfigProperty]
    private string _defaultModImportPath = string.Empty;

    [ConfigProperty]
    private string _defaultImportFolder = string.Empty;

    [ConfigProperty]
    private bool _alwaysOpenDefaultImport = false;

    [ConfigProperty]
    private bool _replaceNonAsciiOnImport = false;

    [ConfigProperty]
    private bool _migrateImportedModelsToV6 = true;

    [ConfigProperty]
    private bool _migrateImportedMaterialsToLegacy = true;

    [ConfigProperty]
    private bool _alwaysShowDetailedModImport = false;

    #endregion

    #region Export

    [ConfigProperty(EventName = "ExportDirectoryChanged")]
    private string _exportDirectory = string.Empty;

    [ConfigProperty]
    private string _defaultModAuthor = DefaultTexToolsData.Author;

    [ConfigProperty]
    private bool _includeShpkInSwap = false;

    #endregion

    #region Watcher

    [ConfigProperty(EventName = "WatchDirectoryChanged")]
    private string _watchDirectory = string.Empty;

    [ConfigProperty(EventName = "DirectoryWatchChanged")]
    private bool _enableDirectoryWatch = false;

    [ConfigProperty]
    private bool _enableAutomaticModImport = false;

    [ConfigProperty(EventName = "ContainerPeekingChanged")]
    private bool _enableContainerPeeking = true;

    [ConfigProperty]
    private bool _autoDismissModImportSuccessReports = true;

    [ConfigProperty]
    private bool _preventExportLoopback = true;

    #endregion

    #region PCP

    [ConfigProperty]
    private string _pcpFolderName = "PCP";

    [ConfigProperty]
    private string _pcpExtension = ".pcp";

    [ConfigProperty]
    private bool _pcpCreateCollection = true;

    [ConfigProperty]
    private bool _pcpAssignCollection = true;

    [ConfigProperty]
    private bool _pcpAllowIpc = true;

    [ConfigProperty]
    private bool _disablePcpHandling = false;

    /// <inheritdoc/>
    public IoConfig(SaveService saveService, PenumbraMessager messager)
        : base(saveService, messager)
        => Load();

    #endregion

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        using (var tempObject = j.TemporaryObject("Import"u8))
        {
            tempObject.WriteNonEmptyString("DefaultModImportPath"u8, DefaultModImportPath);
            tempObject.WriteNonEmptyString("DefaultImportFolder"u8,  DefaultImportFolder);
            tempObject.WriteIfNot("AlwaysOpenDefaultImport"u8,          AlwaysOpenDefaultImport,          false);
            tempObject.WriteIfNot("ReplaceNonAsciiOnImport"u8,          ReplaceNonAsciiOnImport,          false);
            tempObject.WriteIfNot("MigrateImportedModelsToV6"u8,        MigrateImportedModelsToV6,        true);
            tempObject.WriteIfNot("MigrateImportedMaterialsToLegacy"u8, MigrateImportedMaterialsToLegacy, true);
            tempObject.WriteIfNot("AlwaysShowDetailedModImport"u8,      AlwaysShowDetailedModImport,      false);
        }

        using (var tempObject = j.TemporaryObject("Export"u8))
        {
            tempObject.WriteNonEmptyString("ExportDirectory"u8,  ExportDirectory);
            tempObject.WriteNonEmptyString("DefaultModAuthor"u8, DefaultModAuthor);
            tempObject.WriteIfNot("IncludeShpkInSwap"u8, IncludeShpkInSwap, false);
        }

        using (var tempObject = j.TemporaryObject("Watcher"u8))
        {
            tempObject.WriteNonEmptyString("WatchDirectory"u8, WatchDirectory);
            tempObject.WriteIfNot("EnableDirectoryWatch"u8,               EnableDirectoryWatch,               false);
            tempObject.WriteIfNot("EnableAutomaticModImport"u8,           EnableAutomaticModImport,           false);
            tempObject.WriteIfNot("EnableContainerPeeking"u8,             EnableContainerPeeking,             true);
            tempObject.WriteIfNot("AutoDismissModImportSuccessReports"u8, AutoDismissModImportSuccessReports, true);
            tempObject.WriteIfNot("PreventExportLoopback"u8,              PreventExportLoopback,              true);
        }

        using (var tempObject = j.TemporaryObject("PCP"u8))
        {
            tempObject.WriteIfNot("FolderName"u8,       PcpFolderName,       "PCP");
            tempObject.WriteIfNot("Extension"u8,        PcpExtension,        ".pcp");
            tempObject.WriteIfNot("CreateCollection"u8, PcpCreateCollection, true);
            tempObject.WriteIfNot("AssignCollection"u8, PcpAssignCollection, true);
            tempObject.WriteIfNot("AllowIpc"u8,         PcpAllowIpc,         true);
            tempObject.WriteIfNot("DisableHandling"u8,  DisablePcpHandling,  false);
        }
    }

    protected override void LoadData(in JsonElement j)
    {
        if (j.TryReadObject("Import"u8, out var import))
        {
            DefaultModImportPath             = import.PropertyOrDefault("DefaultModImportPath"u8,             DefaultModImportPath);
            DefaultImportFolder              = import.PropertyOrDefault("DefaultImportFolder"u8,              DefaultImportFolder);
            AlwaysOpenDefaultImport          = import.PropertyOrDefault("AlwaysOpenDefaultImport"u8,          AlwaysOpenDefaultImport);
            ReplaceNonAsciiOnImport          = import.PropertyOrDefault("ReplaceNonAsciiOnImport"u8,          ReplaceNonAsciiOnImport);
            MigrateImportedModelsToV6        = import.PropertyOrDefault("MigrateImportedModelsToV6"u8,        MigrateImportedModelsToV6);
            MigrateImportedMaterialsToLegacy = import.PropertyOrDefault("MigrateImportedMaterialsToLegacy"u8, MigrateImportedMaterialsToLegacy);
            AlwaysShowDetailedModImport      = import.PropertyOrDefault("AlwaysShowDetailedModImport"u8,      AlwaysShowDetailedModImport);
        }

        if (j.TryReadObject("Export"u8, out var export))
        {
            ExportDirectory   = export.PropertyOrDefault("ExportDirectory"u8,   ExportDirectory);
            DefaultModAuthor  = export.PropertyOrDefault("DefaultModAuthor"u8,  DefaultModAuthor);
            IncludeShpkInSwap = export.PropertyOrDefault("IncludeShpkInSwap"u8, IncludeShpkInSwap);
        }

        if (j.TryReadObject("Watcher"u8, out var watcher))
        {
            WatchDirectory           = watcher.PropertyOrDefault("WatchDirectory"u8,           WatchDirectory);
            EnableDirectoryWatch     = watcher.PropertyOrDefault("EnableDirectoryWatch"u8,     EnableDirectoryWatch);
            EnableAutomaticModImport = watcher.PropertyOrDefault("EnableAutomaticModImport"u8, EnableAutomaticModImport);
            EnableContainerPeeking   = watcher.PropertyOrDefault("EnableContainerPeeking"u8,   EnableContainerPeeking);
            AutoDismissModImportSuccessReports =
                watcher.PropertyOrDefault("AutoDismissModImportSuccessReports"u8, AutoDismissModImportSuccessReports);
            PreventExportLoopback = watcher.PropertyOrDefault("PreventExportLoopback"u8, PreventExportLoopback);
        }

        if (j.TryReadObject("PCP"u8, out var pcp))
        {
            PcpFolderName       = pcp.PropertyOrDefault("FolderName"u8,       PcpFolderName);
            PcpExtension        = pcp.PropertyOrDefault("Extension"u8,        PcpExtension);
            PcpCreateCollection = pcp.PropertyOrDefault("CreateCollection"u8, PcpCreateCollection);
            PcpAssignCollection = pcp.PropertyOrDefault("AssignCollection"u8, PcpAssignCollection);
            PcpAllowIpc         = pcp.PropertyOrDefault("AllowIpc"u8,         PcpAllowIpc);
            DisablePcpHandling  = pcp.PropertyOrDefault("DisableHandling"u8,  DisablePcpHandling);
        }
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Io;
}
