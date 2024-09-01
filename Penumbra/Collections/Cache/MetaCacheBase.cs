using OtterGui.Classes;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.Collections.Cache;

public abstract class MetaCacheBase<TIdentifier, TEntry>(MetaFileManager manager, ModCollection collection)
    : ReadWriteDictionary<TIdentifier, (IMod Source, TEntry Entry)>
    where TIdentifier : unmanaged, IMetaIdentifier
    where TEntry : unmanaged
{
    protected readonly MetaFileManager      Manager    = manager;
    protected readonly ModCollection        Collection = collection;

    public bool ApplyMod(IMod source, TIdentifier identifier, TEntry entry)
    {
        if (TryGetValue(identifier, out var pair) && pair.Source == source && EqualityComparer<TEntry>.Default.Equals(pair.Entry, entry))
            return false;

        this[identifier] = (source, entry);

        ApplyModInternal(identifier, entry);
        return true;
    }

    public bool RevertMod(TIdentifier identifier, [NotNullWhen(true)] out IMod? mod)
    {
        if (!Remove(identifier, out var pair))
        {
            mod = null;
            return false;
        }

        mod = pair.Source;

        RevertModInternal(identifier);
        return true;
    }


    protected virtual void ApplyModInternal(TIdentifier identifier, TEntry entry)
    { }

    protected virtual void RevertModInternal(TIdentifier identifier)
    { }
}
