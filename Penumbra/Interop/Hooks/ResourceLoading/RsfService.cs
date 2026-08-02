using Dalamud.Hooking;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.Interop.Structs;
using Penumbra.String.Classes;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;
using TextureResourceHandle = Penumbra.Interop.Structs.TextureResourceHandle;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed unsafe class RsfService : IDisposable, IRequiredService
{
    private readonly HookManager _hooks;

    /// <summary>
    /// We need to be able to obtain the requested LoD level.
    /// This replicates the LoD behavior of a textures OnLoad function.
    /// </summary>
    private readonly struct LodService(HookManager interop)
    {
        private readonly nint _lodConfig = interop.SigScanner.GetStaticAddressFromSig(Sigs.LodConfig);

        public byte GetLod(TextureResourceHandle* handle)
        {
            if (handle->ChangeLod)
            {
                var config = *(byte*)_lodConfig + 0xE;
                if (config == byte.MaxValue)
                    return 2;
            }

            return 0;
        }
    }

    /// <summary>  Custom ulong flag to signal our files as opposed to SE files. </summary>
    public static readonly nint CustomFileFlag = new(0xDEADBEEF);

    private readonly LodService _lodService;

    public RsfService(HookManager hooks)
    {
        _hooks      = hooks;
        _lodService = new LodService(_hooks);
        _checkFileStateHook = _hooks.CreateHook<CheckFileStatePrototype>("CheckFileState", Sigs.CheckFileState, CheckFileStateDetour,
            !HookOverrides.Instance.ResourceLoading.CheckFileState)!;
        _loadMdlFileExternHook = _hooks.CreateHook<LoadMdlFileExternPrototype>("LoadMdlFileExtern", Sigs.LoadMdlFileExtern,
            LoadMdlFileExternDetour, !HookOverrides.Instance.ResourceLoading.LoadMdlFileExtern)!;
        _textureOnLoadHook = _hooks.CreateHook<TexResourceHandleOnLoadPrototype>("TextureOnLoad", Sigs.TexHandleOnLoad, OnTexLoadDetour,
            !HookOverrides.Instance.ResourceLoading.TexResourceHandleOnLoad)!;
        _soundOnLoadHook = _hooks.CreateHook<SoundOnLoadDelegate>("SoundOnLoad", Sigs.SoundOnLoad, OnScdLoadDetour,
            !HookOverrides.Instance.ResourceLoading.SoundOnLoad)!;
        _updateCategory = (delegate* unmanaged<TextureResourceHandle*, void>)_hooks.SigScanner.ScanText(Sigs.TexHandleUpdateCategory);
        _loadScdFileLocal =
            (delegate* unmanaged<ResourceHandle*, SeFileDescriptor*, byte, byte>)_hooks.SigScanner.ScanText(Sigs.LoadScdFileLocal);
        _loadTexFileLocal =
            (delegate* unmanaged<TextureResourceHandle*, int, SeFileDescriptor*, byte, byte>)_hooks.SigScanner.ScanText(Sigs.LoadTexFileLocal);
        _loadMdlFileLocal = (delegate* unmanaged<ResourceHandle*, nint, byte, byte>)_hooks.SigScanner.ScanText(Sigs.LoadMdlFileLocal);
        _rsfService       = (nint*)_hooks.SigScanner.GetStaticAddressFromSig(Sigs.RsfServiceAddress);
    }

    /// <summary> Add CRC64 if the given file is a model or texture file and has an associated path. </summary>
    public void AddCrc(ResourceType type, FullPath? path)
    {
        _ = type switch
        {
            ResourceType.Mdl when path.HasValue => _customFileCrc.TryAdd(path.Value.Crc64, ResourceType.Mdl),
            ResourceType.Tex when path.HasValue => _customFileCrc.TryAdd(path.Value.Crc64, ResourceType.Tex),
            ResourceType.Scd when path.HasValue => _customFileCrc.TryAdd(path.Value.Crc64, ResourceType.Scd),
            _                                   => false,
        };
    }

    public void Dispose()
    {
        _hooks.DisposeHook("CheckFileState");
        _hooks.DisposeHook("LoadMdlFileExtern");
        _hooks.DisposeHook("TextureOnLoad");
        _hooks.DisposeHook("SoundOnLoad");
    }

    /// <summary>
    /// We need to keep a list of all CRC64 hash values of our replaced Mdl and Tex files,
    /// i.e. CRC32 of filename in the lower bytes, CRC32 of parent path in the upper bytes.
    /// </summary>
    private readonly Dictionary<ulong, ResourceType> _customFileCrc = [];

    public IReadOnlyDictionary<ulong, ResourceType> CustomCache
        => _customFileCrc;

    private delegate nint CheckFileStatePrototype(nint unk1, ulong crc64);
    private readonly Task<Hook<CheckFileStatePrototype>> _checkFileStateHook;
    private readonly ThreadLocal<bool> _texReturnData = new(() => false);
    private readonly ThreadLocal<bool> _scdReturnData = new(() => false);
    private readonly delegate*unmanaged<TextureResourceHandle*, void> _updateCategory;
    private delegate byte SoundOnLoadDelegate(ResourceHandle* handle, SeFileDescriptor* descriptor, byte unk);
    private readonly Task<Hook<SoundOnLoadDelegate>> _soundOnLoadHook;
    private readonly delegate* unmanaged<ResourceHandle*, SeFileDescriptor*, byte, byte> _loadScdFileLocal;
    private readonly nint* _rsfService;

    private byte OnScdLoadDetour(ResourceHandle* handle, SeFileDescriptor* descriptor, byte unk)
    {
        byte ret;
        if (*_rsfService == nint.Zero)
        {
            Penumbra.Log.Debug(
                $"Resource load of {handle->FileName} before FFXIV RSF-service was instantiated, workaround by setting pointer.");
            *_rsfService = 1;
            ret          = _soundOnLoadHook.Result.Original(handle, descriptor, unk);
            *_rsfService = nint.Zero;
        }
        else
        {
            ret = _soundOnLoadHook.Result.Original(handle, descriptor, unk);
        }

        if (!_scdReturnData.Value)
            return ret;

        // Function failed on a replaced scd, call local.
        _scdReturnData.Value = false;
        ret                  = _loadScdFileLocal(handle, descriptor, unk);
        return ret;
    }

    /// <summary>
    /// The function that checks a files CRC64 to determine whether it is 'protected'.
    /// We use it to check against our stored CRC64s and if it corresponds, we return the custom flag for models.
    /// Since Dawntrail inlined the RSF function for textures, we can not use the flag method here.
    /// Instead, we signal the caller that this will fail and let it call the local function after intentionally failing.
    /// </summary>
    private nint CheckFileStateDetour(nint ptr, ulong crc64)
    {
        if (_customFileCrc.TryGetValue(crc64, out var type))
            switch (type)
            {
                case ResourceType.Mdl: return CustomFileFlag;
                case ResourceType.Tex:
                    _texReturnData.Value = true;
                    return nint.Zero;
                case ResourceType.Scd:
                    _scdReturnData.Value = true;
                    return nint.Zero;
            }

        var ret = _checkFileStateHook.Result.Original(ptr, crc64);
        Penumbra.Log.Excessive($"[CheckFileState] Called on 0x{ptr:X} with CRC {crc64:X16}, returned 0x{ret:X}.");
        return ret;
    }

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    private readonly delegate*unmanaged<TextureResourceHandle*, int, SeFileDescriptor*, byte, byte> _loadTexFileLocal;

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    private readonly delegate*unmanaged<ResourceHandle*, nint, byte, byte> _loadMdlFileLocal;

    private delegate byte TexResourceHandleOnLoadPrototype(TextureResourceHandle* handle, SeFileDescriptor* descriptor, byte unk2);

    private readonly Task<Hook<TexResourceHandleOnLoadPrototype>> _textureOnLoadHook;

    private byte OnTexLoadDetour(TextureResourceHandle* handle, SeFileDescriptor* descriptor, byte unk2)
    {
        var ret = _textureOnLoadHook.Result.Original(handle, descriptor, unk2);
        if (!_texReturnData.Value)
            return ret;

        // Function failed on a replaced texture, call local.
        _texReturnData.Value = false;
        ret                  = _loadTexFileLocal(handle, _lodService.GetLod(handle), descriptor, unk2);
        _updateCategory(handle);
        return ret;
    }

    private delegate byte LoadMdlFileExternPrototype(ResourceHandle* handle, nint unk1, byte unk2, nint unk3);

    private readonly Task<Hook<LoadMdlFileExternPrototype>> _loadMdlFileExternHook;

    /// <summary> We hook the extern functions to just return the local one if given the custom flag as last argument. </summary>
    private byte LoadMdlFileExternDetour(ResourceHandle* resourceHandle, nint unk1, byte unk2, nint ptr)
        => ptr.Equals(CustomFileFlag)
            ? _loadMdlFileLocal(resourceHandle, unk1, unk2)
            : _loadMdlFileExternHook.Result.Original(resourceHandle, unk1, unk2, ptr);
}
