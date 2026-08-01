using Luna;
using Luna.Generators;
using Penumbra.Files;
using Penumbra.Import.Structs;

namespace Penumbra;

public sealed partial class IoConfig : ConfigurationFile<FilenameService>
{
    [ConfigProperty]
    private bool _replaceNonAsciiOnImport = false;

    [ConfigProperty]
    private string _exportDirectory = string.Empty;

    [ConfigProperty]
    private string _watchDirectory = string.Empty;

    [ConfigProperty]
    private bool _enableDirectoryWatch = false;

    [ConfigProperty]
    private bool _enableAutomaticModImport = false;

    [ConfigProperty]
    private bool _enableContainerPeeking = true;

    [ConfigProperty]
    private bool _autoDismissModImportSuccessReports = true;

    [ConfigProperty]
    private bool _alwaysShowDetailedModImport = false;

    [ConfigProperty]
    private bool _preventExportLoopback = true;

    [ConfigProperty]
    private string _defaultImportFolder = string.Empty;

    [ConfigProperty]
    private bool _migrateImportedModelsToV6 = true;

    [ConfigProperty]
    private bool _migrateImportedMaterialsToLegacy = true;

    [ConfigProperty]
    private string _defaultModImportPath = string.Empty;

    [ConfigProperty]
    private bool _alwaysOpenDefaultImport = false;

    [ConfigProperty]
    private string _defaultModAuthor = DefaultTexToolsData.Author;
}
