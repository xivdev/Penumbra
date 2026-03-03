using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData;
using Penumbra.Interop.PathResolving;
using Penumbra.String;

namespace Penumbra.Interop.Hooks.Animation;

public sealed unsafe class CreateApricotDocumentHook(CollectionManager collections, GameState state)
    : FastHook<CreateApricotDocumentHook.Delegate>
{
    public CreateApricotDocumentHook(CollectionManager collections, GameState state, HookManager hooks)
        : this(collections, state)
    {
        Task = hooks.CreateHook<Delegate>("Create Apricot Document", Sigs.CreateApricotDocument, Detour,
            !HookOverrides.Instance.Animation.CreateApricotDocument);
    }

    public delegate nint Delegate(nint singleton, nint vfxOwner, byte* avfxPath, byte* avfxData, uint avfxSize, ApricotResourceHandle* resource,
        nint document);

    private nint Detour(nint singleton, nint vfxOwner, byte* avfxPath, byte* avfxData, uint avfxSize, ApricotResourceHandle* resource,
        nint document)
    {
        var fullPath = new CiByteString(avfxPath);
        state.ApricotDocumentAvfx = state.AvfxData.Value.Valid
            ? state.AvfxData.Value
            : new ResolveData(avfxPath is not null
             && PathDataHandler.Split(fullPath.Span, out var path, out var additionalData)
             && PathDataHandler.Read(additionalData, out var data)
                    ? collections.Storage.ByLocalId(data.Collection)
                    : collections.Active.Default, nint.Zero);
        var ret = Task.Result.Original(singleton, vfxOwner, avfxPath, avfxData, avfxSize, resource, document);
        Penumbra.Log.Excessive(
            $"[CreateApricotDocument] Results with 0x{singleton:X}, 0x{vfxOwner:X}, 0x{(nint)avfxPath:X} ({fullPath}, {state.ApricotDocumentAvfx.ModCollection}) 0x{(nint)avfxData:X} ({avfxSize} bytes) 0x{(long)resource:X} 0x{document:X} -> 0x{ret:X}.");
        return ret;
    }
}
