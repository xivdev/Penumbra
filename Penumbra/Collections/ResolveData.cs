namespace Penumbra.Collections;

public readonly struct ResolveData
{
    public static readonly ResolveData Invalid = new();

    private readonly ModCollection? _modCollection;

    public ModCollection ModCollection
        => _modCollection ?? ModCollection.Empty;

    public readonly nint AssociatedGameObject;

    public bool Valid
        => _modCollection != null;

    public ResolveData()
     : this(null!, nint.Zero)
    { }

    public ResolveData(ModCollection collection, nint gameObject)
    {
        _modCollection       = collection;
        AssociatedGameObject = gameObject;
    }

    public ResolveData(ModCollection collection)
        : this(collection, nint.Zero)
    { }

    public override string ToString()
        => ModCollection.Name;
}

public static class ResolveDataExtensions
{
    public static ResolveData ToResolveData(this ModCollection collection)
        => new(collection);

    public static ResolveData ToResolveData(this ModCollection collection, nint ptr)
        => new(collection, ptr);

    public static unsafe ResolveData ToResolveData(this ModCollection collection, void* ptr)
        => new(collection, (nint)ptr);
}
