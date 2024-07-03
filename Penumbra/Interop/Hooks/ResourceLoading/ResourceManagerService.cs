using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public unsafe class ResourceManagerService : IRequiredService
{
    public ResourceManagerService(IGameInteropProvider interop)
        => interop.InitializeFromAttributes(this);

    /// <summary> The SE Resource Manager as pointer. </summary>
    public ResourceManager* ResourceManager
        => *ResourceManagerAddress;

    /// <summary> Find a resource in the resource manager by its category, extension and crc-hash. </summary>
    public ResourceHandle* FindResource(ResourceCategory cat, ResourceType ext, uint crc32)
    {
        ref var manager = ref *ResourceManager;
        var catIdx = (uint)cat >> 0x18;
        cat = (ResourceCategory)(ushort)cat;
        ref var category = ref manager.ResourceGraph->Containers[(int)cat];
        var extMap = FindInMap(category.CategoryMaps[(int)catIdx].Value, (uint)ext);
        if (extMap == null)
            return null;

        var ret = FindInMap(extMap->Value, crc32);
        return ret == null ? null : ret->Value;
    }

    public delegate void ExtMapAction(ResourceCategory category, StdMap<uint, Pointer<StdMap<uint, Pointer<ResourceHandle>>>>* graph, int idx);
    public delegate void ResourceMapAction(uint ext, StdMap<uint, Pointer<ResourceHandle>>* graph);
    public delegate void ResourceAction(uint crc32, ResourceHandle* graph);

    /// <summary>  Iterate through the entire graph calling an action on every ExtMap. </summary>
    public void IterateGraphs(ExtMapAction action)
    {
        ref var manager = ref *ResourceManager;
        foreach (var resourceType in Enum.GetValues<ResourceCategory>().SkipLast(1))
        {
            ref var graph = ref manager.ResourceGraph->Containers[(int)resourceType];
            for (var i = 0; i < 20; ++i)
            {
                var map = graph.CategoryMaps[i];
                if (map.Value != null)
                    action(resourceType, map, i);
            }
        }
    }

    /// <summary> Iterate through a specific ExtMap calling an action on every resource map. </summary>
    public void IterateExtMap(StdMap<uint, Pointer<StdMap<uint, Pointer<ResourceHandle>>>>* map, ResourceMapAction action)
        => IterateMap(map, (ext, m) => action(ext, m.Value));

    /// <summary> Iterate through a specific resource map calling an action on every resource. </summary>
    public void IterateResourceMap(StdMap<uint, Pointer<ResourceHandle>>* map, ResourceAction action)
        => IterateMap(map, (crc, r) => action(crc, r.Value));

    /// <summary> Iterate through the entire graph calling an action on every resource. </summary>
    public void IterateResources(ResourceAction action)
    {
        IterateGraphs((_, extMap, _)
            => IterateExtMap(extMap, (_, resourceMap)
                => IterateResourceMap(resourceMap, action)));
    }

    /// <summary> A static pointer to the SE Resource Manager. </summary>
    [Signature(Sigs.ResourceManager, ScanType = ScanType.StaticAddress)]
    internal readonly ResourceManager** ResourceManagerAddress = null;

    // Find a key in a StdMap.
    private static TValue* FindInMap<TKey, TValue>(StdMap<TKey, TValue>* map, in TKey key)
        where TKey : unmanaged, IComparable<TKey>
        where TValue : unmanaged
    {
        if (map == null)
            return null;

        return map->TryGetValuePointer(key, out var val) ? val : null;
    }

    // Iterate in tree-order through a map, applying action to each KeyValuePair.
    private static void IterateMap<TKey, TValue>(StdMap<TKey, TValue>* map, Action<TKey, TValue> action)
        where TKey : unmanaged
        where TValue : unmanaged
    {
        if (map == null)
            return;

        foreach (var (key, value) in *map)
            action(key, value);
    }
}
