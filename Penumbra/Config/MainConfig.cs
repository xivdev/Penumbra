using Luna;
using Luna.Generators;
using Penumbra.Files;

namespace Penumbra;

public sealed partial class MainConfig : ConfigurationFile<FilenameService>
{
    [ConfigProperty(EventName = "ModsEnabled")]
    private bool _enableMods;

    [ConfigProperty]
    private string _modDirectory = string.Empty;

    [ConfigProperty]
    private ChangeLogDisplayType _changeLogDisplayType = ChangeLogDisplayType.New;

    [ConfigProperty]
    private bool _defaultTemporaryMode = false;

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
}
