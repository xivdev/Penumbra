using System.Text.Json;
using Luna;
using Luna.Generators;
using Penumbra.Files;
using Penumbra.Import.Textures;
using Penumbra.Services;

namespace Penumbra;

public sealed partial class AdvancedConfig : ConfigurationFile<FilenameService>
{
    public const int MinimumSizeX = 900;
    public const int MinimumSizeY = 675;

    public override int CurrentVersion
        => 100;

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

    [ConfigProperty(EventName = "CustomShapesChanged")]
    private bool _enableCustomShapes = true;

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

    /// <inheritdoc/>
    public AdvancedConfig(SaveService saveService, PenumbraMessager messager)
        : base(saveService, messager)
        => Load();

    protected override void AddData(Utf8JsonWriter j)
    {
        j.WriteBoolean("DebugMode"u8, DebugMode);
        j.WriteIfNot("MinimumSizeX"u8, MinimumSize.X, MinimumSizeX);
        j.WriteIfNot("MinimumSizeY"u8, MinimumSize.Y, MinimumSizeY);
        if (UseCrashHandler.HasValue)
            j.WriteBoolean("UseCrashHandler"u8, UseCrashHandler.Value);
        j.WriteEnumIfNot("AuxiliaryDeviceMode"u8, AuxiliaryDeviceMode, AuxiliaryDeviceMode.Singleton);
        j.WriteIfNot("EnableCustomShapes"u8,        EnableCustomShapes,        true);
        j.WriteIfNot("AutoDeduplicateOnImport"u8,   AutoDeduplicateOnImport,   true);
        j.WriteIfNot("AutoReduplicateUiOnImport"u8, AutoReduplicateUiOnImport, true);
        j.WriteIfNot("UseFileSystemCompression"u8,  UseFileSystemCompression,  true);
        j.WriteIfNot("EnableHttpApi"u8,             EnableHttpApi,             true);
        j.WriteIfNot("KeepDefaultMetaChanges"u8,    KeepDefaultMetaChanges,    false);
        j.WriteIfNot("HdrRenderTargets"u8,          HdrRenderTargets,          true);
    }

    protected override void LoadData(in JsonElement j)
    {
        DebugMode = j.PropertyOrDefault("DebugMode"u8, DebugMode);
        MinimumSize = new Vector2(j.PropertyOrDefault("MinimumSizeX"u8, MinimumSize.X), j.PropertyOrDefault("MinimumSizeY"u8, MinimumSize.Y));
        UseCrashHandler = j.TryReadProperty("UseCrashHandler"u8, out bool? v) ? v : UseCrashHandler;
        AuxiliaryDeviceMode = j.EnumOrDefault("AuxiliaryDeviceMode"u8, AuxiliaryDeviceMode);
        EnableCustomShapes = j.PropertyOrDefault("EnableCustomShapes"u8, EnableCustomShapes);
        AutoDeduplicateOnImport = j.PropertyOrDefault("AutoDeduplicateOnImport"u8, AutoDeduplicateOnImport);
        AutoReduplicateUiOnImport = j.PropertyOrDefault("AutoReduplicateUiOnImport"u8, AutoReduplicateUiOnImport);
        UseFileSystemCompression = j.PropertyOrDefault("UseFileSystemCompression"u8, UseFileSystemCompression);
        EnableHttpApi = j.PropertyOrDefault("EnableHttpApi"u8, EnableHttpApi);
        KeepDefaultMetaChanges = j.PropertyOrDefault("KeepDefaultMetaChanges"u8, KeepDefaultMetaChanges);
        HdrRenderTargets = j.PropertyOrDefault("HdrRenderTargets"u8, HdrRenderTargets);
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Advanced;
}
