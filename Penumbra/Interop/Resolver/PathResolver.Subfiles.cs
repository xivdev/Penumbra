using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    // Materials and avfx do contain their own paths to textures and shader packages or atex respectively.
    // Those are loaded synchronously.
    // Thus, we need to ensure the correct files are loaded when a material is loaded.
    public class SubfileHelper : IDisposable, IReadOnlyCollection<KeyValuePair<IntPtr, ResolveData>>
    {
        private readonly ResourceLoader   _loader;
        private readonly GameEventManager _events;

        private readonly ThreadLocal<ResolveData> _mtrlData = new(() => ResolveData.Invalid);
        private readonly ThreadLocal<ResolveData> _avfxData = new(() => ResolveData.Invalid);

        private readonly ConcurrentDictionary<IntPtr, ResolveData> _subFileCollection = new();

        public SubfileHelper(ResourceLoader loader, GameEventManager events)
        {
            SignatureHelper.Initialise(this);

            _loader = loader;
            _events = events;
        }

        // Check specifically for shpk and tex files whether we are currently in a material load.
        public bool HandleSubFiles(ResourceType type, out ResolveData collection)
        {
            switch (type)
            {
                case ResourceType.Tex when _mtrlData.Value.Valid:
                case ResourceType.Shpk when _mtrlData.Value.Valid:
                    collection = _mtrlData.Value;
                    return true;
                case ResourceType.Scd when _avfxData.Value.Valid:
                    collection = _avfxData.Value;
                    return true;
                case ResourceType.Atex when _avfxData.Value.Valid:
                    collection = _avfxData.Value;
                    return true;
            }

            collection = ResolveData.Invalid;
            return false;
        }

        // Materials need to be set per collection so they can load their textures independently from each other.
        public static void HandleCollection(ResolveData resolveData, ByteString path, bool nonDefault, ResourceType type, FullPath? resolved,
            out (FullPath?, ResolveData) data)
        {
            if (nonDefault)
                switch (type)
                {
                    case ResourceType.Mtrl:
                    case ResourceType.Avfx:
                        var fullPath = new FullPath($"|{resolveData.ModCollection.Name}_{resolveData.ModCollection.ChangeCounter}|{path}");
                        data = (fullPath, resolveData);
                        return;
                }

            data = (resolved, resolveData);
        }

        public void Enable()
        {
            _loadMtrlShpkHook.Enable();
            _loadMtrlTexHook.Enable();
            _apricotResourceLoadHook.Enable();
            _loader.ResourceLoaded           += SubfileContainerRequested;
            _events.ResourceHandleDestructor += ResourceDestroyed;
        }

        public void Disable()
        {
            _loadMtrlShpkHook.Disable();
            _loadMtrlTexHook.Disable();
            _apricotResourceLoadHook.Disable();
            _loader.ResourceLoaded           -= SubfileContainerRequested;
            _events.ResourceHandleDestructor -= ResourceDestroyed;
        }

        public void Dispose()
        {
            Disable();
            _loadMtrlShpkHook.Dispose();
            _loadMtrlTexHook.Dispose();
            _apricotResourceLoadHook.Dispose();
        }

        private void SubfileContainerRequested(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
            ResolveData resolveData)
        {
            switch (handle->FileType)
            {
                case ResourceType.Mtrl:
                case ResourceType.Avfx:
                    if (handle->FileSize == 0)
                        _subFileCollection[(nint)handle] = resolveData;

                    break;
            }
        }

        private void ResourceDestroyed(ResourceHandle* handle)
            => _subFileCollection.TryRemove((IntPtr)handle, out _);

        private delegate byte LoadMtrlFilesDelegate(IntPtr mtrlResourceHandle);

        [Signature(Sigs.LoadMtrlTex, DetourName = nameof(LoadMtrlTexDetour))]
        private readonly Hook<LoadMtrlFilesDelegate> _loadMtrlTexHook = null!;

        private byte LoadMtrlTexDetour(IntPtr mtrlResourceHandle)
        {
            using var performance = Penumbra.Performance.Measure(PerformanceType.LoadTextures);
            var       old         = _mtrlData.Value;
            _mtrlData.Value = LoadFileHelper(mtrlResourceHandle);
            var ret = _loadMtrlTexHook.Original(mtrlResourceHandle);
            _mtrlData.Value = old;
            return ret;
        }

        [Signature(Sigs.LoadMtrlShpk, DetourName = nameof(LoadMtrlShpkDetour))]
        private readonly Hook<LoadMtrlFilesDelegate> _loadMtrlShpkHook = null!;

        private byte LoadMtrlShpkDetour(IntPtr mtrlResourceHandle)
        {
            using var performance = Penumbra.Performance.Measure(PerformanceType.LoadShaders);
            var       old         = _mtrlData.Value;
            _mtrlData.Value = LoadFileHelper(mtrlResourceHandle);
            var ret = _loadMtrlShpkHook.Original(mtrlResourceHandle);
            _mtrlData.Value = old;
            return ret;
        }

        private ResolveData LoadFileHelper(IntPtr resourceHandle)
        {
            if (resourceHandle == IntPtr.Zero)
                return ResolveData.Invalid;

            return _subFileCollection.TryGetValue(resourceHandle, out var c) ? c : ResolveData.Invalid;
        }


        private delegate byte ApricotResourceLoadDelegate(IntPtr handle, IntPtr unk1, byte unk2);

        [Signature(Sigs.ApricotResourceLoad, DetourName = nameof(ApricotResourceLoadDetour))]
        private readonly Hook<ApricotResourceLoadDelegate> _apricotResourceLoadHook = null!;


        private byte ApricotResourceLoadDetour(IntPtr handle, IntPtr unk1, byte unk2)
        {
            using var performance = Penumbra.Performance.Measure(PerformanceType.LoadApricotResources);
            var       old         = _avfxData.Value;
            _avfxData.Value = LoadFileHelper(handle);
            var ret = _apricotResourceLoadHook.Original(handle, unk1, unk2);
            _avfxData.Value = old;
            return ret;
        }

        public IEnumerator<KeyValuePair<IntPtr, ResolveData>> GetEnumerator()
            => _subFileCollection.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public int Count
            => _subFileCollection.Count;

        internal ResolveData MtrlData
            => _mtrlData.Value;

        internal ResolveData AvfxData
            => _avfxData.Value;
    }
}
