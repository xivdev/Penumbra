using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Api.Enums;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.SafeHandles;
using Penumbra.String.Classes;

namespace Penumbra.Collections.Cache;

/// <summary> A cache for resources owned by a collection. </summary>
public sealed class CustomResourceCache(ResourceLoader loader)
    : ConcurrentDictionary<Utf8GamePath, SafeResourceHandle>, IDisposable
{
    /// <summary> Invalidate an existing resource by clearing it from the cache and disposing it. </summary>
    public void Invalidate(Utf8GamePath path)
    {
        if (TryRemove(path, out var handle))
            handle.Dispose();
    }

    public void Dispose()
    {
        foreach (var handle in Values)
            handle.Dispose();
        Clear();
    }

    /// <summary> Get the requested resource either from the cached resource, or load a new one if it does not exist. </summary>
    public SafeResourceHandle Get(ResourceCategory category, ResourceType type, Utf8GamePath path, ResolveData data)
    {
        if (TryGetClonedValue(path, out var handle))
            return handle;

        handle = loader.LoadResolvedSafeResource(category, type, path.Path, data);
        var clone = handle.Clone();
        if (!TryAdd(path, clone))
            clone.Dispose();
        return handle;
    }

    /// <summary> Get a cloned cached resource if it exists. </summary>
    private bool TryGetClonedValue(Utf8GamePath path, [NotNullWhen(true)] out SafeResourceHandle? handle)
    {
        if (!TryGetValue(path, out handle))
            return false;

        handle = handle.Clone();
        return true;
    }
}
