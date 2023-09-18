using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.String.Classes;
using Penumbra.Util;

public class ModSwapEditor
{
    private readonly ModManager                         _modManager;
    private readonly Dictionary<Utf8GamePath, FullPath> _swaps = new();

    public IReadOnlyDictionary<Utf8GamePath, FullPath> Swaps
        => _swaps;

    public ModSwapEditor(ModManager modManager)
        => _modManager = modManager;

    public void Revert(ISubMod option)
    {
        _swaps.SetTo(option.FileSwaps);
        Changes = false;
    }

    public void Apply(Mod mod, int groupIdx, int optionIdx)
    {
        if (!Changes)
            return;

        _modManager.OptionEditor.OptionSetFileSwaps(mod, groupIdx, optionIdx, _swaps);
        Changes = false;
    }

    public bool Changes { get; private set; }

    public void Remove(Utf8GamePath path)
        => Changes |= _swaps.Remove(path);

    public void Add(Utf8GamePath path, FullPath file)
        => Changes |= _swaps.TryAdd(path, file);

    public void Change(Utf8GamePath path, Utf8GamePath newPath)
    {
        if (_swaps.Remove(path, out var file))
            Add(newPath, file);
    }

    public void Change(Utf8GamePath path, FullPath file)
    {
        _swaps[path] = file;
        Changes      = true;
    }
}
