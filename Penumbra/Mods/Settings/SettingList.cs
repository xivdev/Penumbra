namespace Penumbra.Mods.Settings;

public class SettingList : List<Setting>
{
    public SettingList()
    { }

    public SettingList(int capacity)
        : base(capacity)
    { }

    public SettingList(IEnumerable<Setting> settings)
        => AddRange(settings);

    public SettingList Clone()
        => new(this);

    public static SettingList Default(Mod mod)
        => new(mod.Groups.Select(g => g.DefaultSettings));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool FixSize(Mod mod)
    {
        var diff = Count - mod.Groups.Count;

        switch (diff)
        {
            case 0: return false;
            case > 0:
                RemoveRange(mod.Groups.Count, diff);
                return true;
            default:
                EnsureCapacity(mod.Groups.Count);
                for (var i = Count; i < mod.Groups.Count; ++i)
                    Add(mod.Groups[i].DefaultSettings);
                return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool FixAll(Mod mod)
    {
        var ret = false;
        for (var i = 0; i < Count; ++i)
        {
            var oldValue = this[i];
            var newValue = mod.Groups[i].FixSetting(oldValue);
            if (newValue == oldValue)
                continue;

            ret     = true;
            this[i] = newValue;
        }

        return FixSize(mod) | ret;
    }
}
