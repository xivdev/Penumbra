using OtterGui;
using Penumbra.Collections.Manager;

namespace Penumbra.Collections;

public struct ModCollectionIdentity(Guid id, LocalCollectionId localId)
{
    public const string DefaultCollectionName = "Default";
    public const string EmptyCollectionName   = "None";

    public static readonly ModCollectionIdentity Empty = new(Guid.Empty, LocalCollectionId.Zero, EmptyCollectionName, 0);

    public string            Name    { get; set; } = string.Empty;
    public Guid              Id      { get; }      = id;
    public LocalCollectionId LocalId { get; }      = localId;

    /// <summary> The index of the collection is set and kept up-to-date by the CollectionManager. </summary>
    public int Index { get; internal set; }

    public string Identifier
        => Id.ToString();

    public string ShortIdentifier
        => Id.ShortGuid();

    /// <summary> Get the short identifier of a collection unless it is a well-known collection name. </summary>
    public string AnonymizedName
        => Id == Guid.Empty ? EmptyCollectionName : Name == DefaultCollectionName ? Name : ShortIdentifier;

    public override string ToString()
        => Name.Length > 0 ? Name : ShortIdentifier;

    public ModCollectionIdentity(Guid id, LocalCollectionId localId, string name, int index)
        : this(id, localId)
    {
        Name  = name;
        Index = index;
    }

    public static ModCollectionIdentity New(string name, LocalCollectionId id, int index)
        => new(Guid.NewGuid(), id, name, index);
}
