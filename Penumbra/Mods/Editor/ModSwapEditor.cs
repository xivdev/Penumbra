using OtterGui.Services;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.Util;

public class ModSwapEditor(ModManager modManager) : IService
{
    private readonly Dictionary<Utf8GamePath, FullPath> _swaps = [];

    public IReadOnlyDictionary<Utf8GamePath, FullPath> Swaps
        => _swaps;

    public void Revert(IModDataContainer option)
    {
        _swaps.SetTo(option.FileSwaps);
        Changes = false;
    }

    public void Apply(IModDataContainer container)
    {
        if (!Changes)
            return;

        modManager.OptionEditor.SetFileSwaps(container, _swaps);
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
