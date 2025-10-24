namespace Penumbra.Mods.Manager;

public class ModStorage : IReadOnlyList<Mod>
{
    /// <summary> The actual list of mods. </summary>
    protected readonly List<Mod> Mods = [];

    public int Count
        => Mods.Count;

    public Mod this[int idx]
        => Mods[idx];

    public Mod this[Index idx]
        => Mods[idx];

    public IEnumerator<Mod> GetEnumerator()
        => Mods.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    /// Try to obtain a mod by its directory name (unique identifier).
    /// </summary>
    public bool TryGetMod(string identifier, [NotNullWhen(true)] out Mod? mod)
    {
        mod = this.FirstOrDefault(m => string.Equals(m.Identifier, identifier, StringComparison.OrdinalIgnoreCase));
        return mod is not null;
    }

    /// <summary>
    /// Try to obtain a mod by its directory name (unique identifier, preferred),
    /// or the first mod of the given name if no directory fits.
    /// </summary>
    public bool TryGetMod(string identifier, string modName, [NotNullWhen(true)] out Mod? mod)
    {
        if (modName.Length is 0)
            return TryGetMod(identifier, out mod);

        mod = null;
        foreach (var m in Mods)
        {
            if (string.Equals(m.Identifier, identifier, StringComparison.OrdinalIgnoreCase))
            {
                mod = m;
                return true;
            }

            if (m.Name == modName)
                mod ??= m;
        }

        return mod != null;
    }

    /// <summary>
    /// An easily accessible set of new mods.
    /// Mods are added when they are created or imported.
    /// Mods are removed when they are deleted or when they are toggled in any collection.
    /// Also gets cleared on mod rediscovery.
    /// </summary>
    private readonly HashSet<Mod> _newMods = [];

    public bool IsNew(Mod mod)
        => _newMods.Contains(mod);

    public void SetNew(Mod mod)
        => _newMods.Add(mod);

    public void SetKnown(Mod mod)
        => _newMods.Remove(mod);

    public void ClearNewMods()
        => _newMods.Clear();
}
