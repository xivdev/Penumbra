using System.Text.Json;
using ImSharp;
using Luna;
using Luna.Generators;
using Newtonsoft.Json.Linq;
using Penumbra.Files;
using Penumbra.Import.Textures;

namespace Penumbra;

public sealed partial class AdvancedConfig(SaveService saveService, MessageService messager)
    : ConfigurationFile<FilenameService>(saveService, messager, TimeSpan.FromSeconds(0))
{
    public const int MinimumSizeX = 900;
    public const int MinimumSizeY = 675;

    public override int CurrentVersion
        => 1;

    [ConfigProperty]
    private Vector2 _minimumSize = new(MinimumSizeX, MinimumSizeY);

    [ConfigProperty]
#if DEBUG
    private bool _debugMode = true;
#else
    private bool _debugMode = false;
#endif

    [ConfigProperty(EventName = "UseCrashHandlerChanged")]
    private bool? _useCrashHandler = null;

    [ConfigProperty]
    private bool _enableCustomShapes = true;

    [ConfigProperty]
    private bool _includeShpkInSwap = false;

    [ConfigProperty(EventName = "AuxiliaryDeviceModeChanged")]
    private AuxiliaryDeviceMode _auxiliaryDeviceMode = AuxiliaryDeviceMode.Singleton;

    [ConfigProperty]
    private bool _autoDeduplicateOnImport = true;

    [ConfigProperty]
    private bool _autoReduplicateUiOnImport = true;

    [ConfigProperty]
    private bool _useFileSystemCompression = true;

    [ConfigProperty(EventName = "HttpApiChanged")]
    private bool _enableHttpApi = true;

    [ConfigProperty]
    private bool _keepDefaultMetaChanges = false;

    [ConfigProperty]
    private bool _hdrRenderTargets = true;


    protected override void AddData(Utf8JsonWriter j)
    {
        j.WriteBoolean("DebugMode"u8, DebugMode);
        j.WriteIfNot("MinimumSizeX"u8,              MinimumSize.X,             MinimumSizeX);
        j.WriteIfNot("MinimumSizeY"u8,              MinimumSize.Y,             MinimumSizeY);
        if(UseCrashHandler.HasValue)
            j.WriteBoolean("UseCrashHandler"u8, UseCrashHandler.Value);
        if(AuxiliaryDeviceMode is not AuxiliaryDeviceMode.Singleton)
            j.WriteString("AuxiliaryDeviceMode"u8, AuxiliaryDeviceMode.StringU8);
        j.WriteIfNot("EnableCustomShapes"u8,        EnableCustomShapes,        true);
        j.WriteIfNot("IncludeShpkInSwap"u8,         IncludeShpkInSwap,         false);
        j.WriteIfNot("AutoDeduplicateOnImport"u8,   AutoDeduplicateOnImport,   true);
        j.WriteIfNot("AutoReduplicateUiOnImport"u8, AutoReduplicateUiOnImport, true);
        j.WriteIfNot("UseFileSystemCompression"u8,  UseFileSystemCompression,  true);
        j.WriteIfNot("EnableHttpApi"u8,             EnableHttpApi,             true);
        j.WriteIfNot("KeepDefaultMetaChanges"u8,    KeepDefaultMetaChanges,    false);
        j.WriteIfNot("HdrRenderTargets"u8,          HdrRenderTargets,          true);
    }

    protected override void LoadData(JObject j)
    {
        throw new NotImplementedException();
    }

    public override string ToFilePath(FilenameService fileNames)
        => throw new NotImplementedException();
}
