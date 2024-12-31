namespace Penumbra.Mods.Settings;

public readonly record struct FullModSettings(ModSettings? Settings = null, TemporaryModSettings? TempSettings = null)
{
    public static readonly FullModSettings Empty = new();

    public ModSettings? Resolve()
    {
        if (TempSettings == null)
            return Settings;
        if (TempSettings.ForceInherit)
            return null;

        return TempSettings;
    }

    public FullModSettings DeepCopy()
        => new(Settings?.DeepCopy());
}
