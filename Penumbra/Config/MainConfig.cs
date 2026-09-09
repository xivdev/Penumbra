using System.Text.Json;
using Luna;
using Luna.Generators;
using Penumbra.Files;
using Penumbra.Services;

namespace Penumbra;

public sealed partial class MainConfig : ConfigurationFile<FilenameService>
{
    #region Main

    [ConfigProperty(EventName = "ModsEnabled")]
    private bool _enableMods = true;

    [ConfigProperty]
    private string _modDirectory = string.Empty;

    [ConfigProperty]
    private ChangeLogDisplayType _changeLogDisplayType = ChangeLogDisplayType.New;

    [ConfigProperty]
    private bool _defaultTemporaryMode = false;

    [ConfigProperty]
    private bool _printSuccessfulCommandsToChat = true;

    /// <inheritdoc/>
    public MainConfig(SaveService saveService, PenumbraMessager messager)
        : base(saveService, messager)
        => Load();

    public DoubleModifier DestructiveModifier
    {
        get => LunaStyle.Modifier.Destructive.Modifier;
        set => LunaStyle.Modifier.Destructive.Set(value);
    }

    public DoubleModifier MisclickModifier
    {
        get => LunaStyle.Modifier.Misclick.Modifier;
        set => LunaStyle.Modifier.Misclick.Set(value);
    }

    #endregion

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        j.WriteNonEmptyString("ModDirectory"u8, ModDirectory);
        j.WriteIfNot("EnableMods"u8, EnableMods, true);
        j.WriteEnumIfNot("ChangeLogDisplayType"u8, ChangeLogDisplayType, ChangeLogDisplayType.New);
        j.WriteIfNot("DefaultTemporaryMode"u8,          DefaultTemporaryMode,          false);
        j.WriteIfNot("PrintSuccessfulCommandsToChat"u8, PrintSuccessfulCommandsToChat, true);
        j.WritePropertyName("DestructiveModifier"u8);
        DestructiveModifier.Serialize(j);
        j.WritePropertyName("MisclickModifier"u8);
        MisclickModifier.Serialize(j);
    }

    protected override void LoadData(in JsonElement j)
    {
        ModDirectory                  = j.PropertyOrDefault("ModDirectory"u8, ModDirectory);
        EnableMods                    = j.PropertyOrDefault("EnableMods"u8,   EnableMods);
        ChangeLogDisplayType          = j.EnumOrDefault("ChangeLogDisplayType"u8, ChangeLogDisplayType);
        DefaultTemporaryMode          = j.PropertyOrDefault("DefaultTemporaryMode"u8,          DefaultTemporaryMode);
        PrintSuccessfulCommandsToChat = j.PropertyOrDefault("PrintSuccessfulCommandsToChat"u8, PrintSuccessfulCommandsToChat);
        DestructiveModifier = j.TryGetProperty("DestructiveModifier"u8, out var d) && DoubleModifier.TryDeserialize(d, out var dm, true)
            ? dm
            : DestructiveModifier;
        MisclickModifier = j.TryGetProperty("MisclickModifier"u8, out var m) && DoubleModifier.TryDeserialize(m, out var mm, true)
            ? mm
            : MisclickModifier;
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Main;
}
