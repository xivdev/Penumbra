namespace Penumbra.Mods.Settings;

public sealed class TemporaryModSettings : ModSettings
{
    public string Source = string.Empty;
    public int    Lock   = 0;
    public bool   ForceInherit;

    // Create default settings for a given mod.
    public static TemporaryModSettings DefaultSettings(Mod mod, string source, int key = 0)
        => new()
        {
            Enabled  = false,
            Source   = source,
            Lock     = key,
            Priority = ModPriority.Default,
            Settings = SettingList.Default(mod),
        };
}

public static class ModSettingsExtensions
{
    public static bool IsTemporary(this ModSettings? settings)
        => settings is TemporaryModSettings;
}
