using System.Text.Json;
using Luna;
using Luna.Generators;
using Newtonsoft.Json.Linq;
using Penumbra.Files;

namespace Penumbra;

public sealed partial class MainConfig(SaveService saveService, MessageService messager)
    : ConfigurationFile<FilenameService>(saveService, messager)
{
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

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        j.WriteNonEmptyString("ModDirectory"u8, ModDirectory);
        j.WriteIfNot("EnableMods"u8, EnableMods, true);
        j.WriteEnumIfNot("ChangeLogDisplayType"u8, ChangeLogDisplayType, ChangeLogDisplayType.New);
        j.WriteIfNot("DefaultTemporaryMode"u8, DefaultTemporaryMode, false);
        j.WriteIfNot("PrintSuccessfulCommandsToChat"u8, PrintSuccessfulCommandsToChat, true);
        DestructiveModifier.Serialize(j);
        MisclickModifier.Serialize(j);
    }

    protected override void LoadData(JObject j)
    {
        throw new NotImplementedException();
    }

    public override string ToFilePath(FilenameService fileNames)
        => throw new NotImplementedException();
}
