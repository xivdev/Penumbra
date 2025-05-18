namespace Penumbra.Mods.Settings;

public sealed class TemporaryModSettings : ModSettings
{
    public new static readonly TemporaryModSettings Empty = new(true);

    public const string OwnSource = "yourself";
    public       string Source    = string.Empty;
    public       int    Lock;
    public       bool   ForceInherit;

    // Create default settings for a given mod.
    public static TemporaryModSettings DefaultSettings(Mod mod, string source, bool enabled = false, int key = 0)
        => new()
        {
            Enabled  = enabled,
            Source   = source,
            Lock     = key,
            Priority = ModPriority.Default,
            Settings = SettingList.Default(mod),
        };

    public TemporaryModSettings()
    { }

    private TemporaryModSettings(bool empty)
        : base(empty)
    { }

    public TemporaryModSettings(Mod mod, ModSettings? clone, string source = OwnSource, int key = 0)
    {
        Source       = source;
        Lock         = key;
        ForceInherit = clone == null;
        if (clone is { IsEmpty: false })
        {
            Enabled  = clone.Enabled;
            Priority = clone.Priority;
            Settings = clone.Settings.Clone();
        }
        else
        {
            IsEmpty  = true;
            Enabled  = false;
            Priority = ModPriority.Default;
            Settings = SettingList.Default(mod);
        }
    }
}

public static class ModSettingsExtensions
{
    public static bool IsTemporary(this ModSettings? settings)
        => settings is TemporaryModSettings;
}
