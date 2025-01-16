namespace Penumbra.Collections;

public readonly struct ResolveData(ModCollection collection, nint gameObject)
{
    public static readonly ResolveData Invalid = new();

    private readonly ModCollection? _modCollection = collection;

    public ModCollection ModCollection
        => _modCollection ?? ModCollection.Empty;

    public readonly nint AssociatedGameObject = gameObject;

    public bool Valid
        => _modCollection != null;

    public ResolveData()
        : this(null!, nint.Zero)
    { }

    public ResolveData(ModCollection collection)
        : this(collection, nint.Zero)
    { }

    public override string ToString()
        => ModCollection.Identity.Name;
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
