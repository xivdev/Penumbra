using Luna;
using Penumbra.Collections;

namespace Penumbra.Communication;

public sealed class CollectionRename(Logger log)
    : EventBase<CollectionRename.Arguments, CollectionRename.Priority>(nameof(CollectionRename), log)
{
    public enum Priority
    {
        /// <seealso cref="UI.CollectionTab.CollectionSelector.Cache.OnCollectionRename" />
        CollectionSelectorCache = int.MinValue,
    }

    /// <summary> The arguments for a collection rename event. </summary>
    /// <param name="Collection"> The renamed collection. </param>
    /// <param name="OldName"> The old name of the collection. </param>
    /// <param name="NewName"> The new name of the collection. </param>
    public readonly record struct Arguments(ModCollection Collection, string OldName, string NewName);
}
