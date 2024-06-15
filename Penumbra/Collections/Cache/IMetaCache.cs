using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.Collections.Cache;

public abstract class MetaCacheBase<TIdentifier, TEntry>
    : Dictionary<TIdentifier, (IMod Source, TEntry Entry)>
    where TIdentifier : unmanaged, IMetaIdentifier
    where TEntry : unmanaged
{
    protected MetaCacheBase(MetaFileManager manager, ModCollection collection)
    {
        Manager    = manager;
        Collection = collection;
        if (!Manager.CharacterUtility.Ready)
            Manager.CharacterUtility.LoadingFinished += IncorporateChanges;
    }

    protected readonly MetaFileManager Manager;
    protected readonly ModCollection   Collection;

    public void Dispose()
    {
        Manager.CharacterUtility.LoadingFinished -= IncorporateChanges;
        Dispose(true);
    }

    public abstract void SetFiles();

    public bool ApplyMod(IMod source, TIdentifier identifier, TEntry entry)
    {
        lock (this)
        {
            if (TryGetValue(identifier, out var pair) && pair.Source == source && EqualityComparer<TEntry>.Default.Equals(pair.Entry, entry))
                return false;

            this[identifier] = (source, entry);
        }

        ApplyModInternal(identifier, entry);
        return true;
    }

    public bool RevertMod(TIdentifier identifier, [NotNullWhen(true)] out IMod? mod)
    {
        lock (this)
        {
            if (!Remove(identifier, out var pair))
            {
                mod = null;
                return false;
            }

            mod = pair.Source;
        }

        RevertModInternal(identifier);
        return true;
    }

    private void IncorporateChanges()
    {
        lock (this)
        {
            IncorporateChangesInternal();
        }

        if (Manager.ActiveCollections.Default == Collection && Manager.Config.EnableMods)
            SetFiles();
    }

    protected abstract void ApplyModInternal(TIdentifier identifier, TEntry entry);
    protected abstract void RevertModInternal(TIdentifier identifier);
    protected abstract void IncorporateChangesInternal();

    protected virtual void Dispose(bool _)
    { }
}
