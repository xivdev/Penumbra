namespace Penumbra.Mods.Settings;

public sealed class TemporaryModSettings : ModSettings
{
    public string Source = string.Empty;
    public int    Lock   = 0;
    public bool   ForceInherit;
}
