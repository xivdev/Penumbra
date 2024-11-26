using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.Interop.Structs;
using Penumbra.String.Classes;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;
using TextureResourceHandle = Penumbra.Interop.Structs.TextureResourceHandle;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public unsafe class RsfService : IDisposable, IRequiredService
{
    /// <summary>
    /// We need to be able to obtain the requested LoD level.
    /// This replicates the LoD behavior of a textures OnLoad function.
    /// </summary>
    private readonly struct LodService
    {
        public LodService(IGameInteropProvider interop)
            => interop.InitializeFromAttributes(this);

        [Signature(Sigs.LodConfig)]
        private readonly nint _lodConfig = nint.Zero;

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

    public RsfService(IGameInteropProvider interop)
    {
        interop.InitializeFromAttributes(this);
        _lodService = new LodService(interop);
        if (!HookOverrides.Instance.ResourceLoading.CheckFileState)
            _checkFileStateHook.Enable();
        if (!HookOverrides.Instance.ResourceLoading.LoadMdlFileExtern)
            _loadMdlFileExternHook.Enable();
        if (!HookOverrides.Instance.ResourceLoading.TexResourceHandleOnLoad)
            _textureOnLoadHook.Enable();
        if (!HookOverrides.Instance.ResourceLoading.SoundOnLoad)
            _soundOnLoadHook.Enable();
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
        _checkFileStateHook.Dispose();
        _loadMdlFileExternHook.Dispose();
        _textureOnLoadHook.Dispose();
        _soundOnLoadHook.Dispose();
    }

    /// <summary>
    /// We need to keep a list of all CRC64 hash values of our replaced Mdl and Tex files,
    /// i.e. CRC32 of filename in the lower bytes, CRC32 of parent path in the upper bytes.
    /// </summary>
    private readonly Dictionary<ulong, ResourceType> _customFileCrc = [];

    public IReadOnlyDictionary<ulong, ResourceType> CustomCache
        => _customFileCrc;

    private delegate nint CheckFileStatePrototype(nint unk1, ulong crc64);

    [Signature(Sigs.CheckFileState, DetourName = nameof(CheckFileStateDetour))]
    private readonly Hook<CheckFileStatePrototype> _checkFileStateHook = null!;

    private readonly ThreadLocal<bool> _texReturnData = new(() => default);
    private readonly ThreadLocal<bool> _scdReturnData = new(() => default);

    private delegate void UpdateCategoryDelegate(TextureResourceHandle* resourceHandle);

    [Signature(Sigs.TexHandleUpdateCategory)]
    private readonly UpdateCategoryDelegate _updateCategory = null!;

    private delegate byte SoundOnLoadDelegate(ResourceHandle* handle, SeFileDescriptor* descriptor, byte unk);

    [Signature(Sigs.LoadScdFileLocal)]
    private readonly delegate* unmanaged<ResourceHandle*, SeFileDescriptor*, byte, byte> _loadScdFileLocal = null!;

    [Signature(Sigs.SoundOnLoad, DetourName = nameof(OnScdLoadDetour))]
    private readonly Hook<SoundOnLoadDelegate> _soundOnLoadHook = null!;

    [Signature(Sigs.RsfServiceAddress, ScanType = ScanType.StaticAddress)]
    private readonly nint* _rsfService = null;

    private byte OnScdLoadDetour(ResourceHandle* handle, SeFileDescriptor* descriptor, byte unk)
    {
        byte ret;
        if (*_rsfService == nint.Zero)
        {
            Penumbra.Log.Debug(
                $"Resource load of {handle->FileName} before FFXIV RSF-service was instantiated, workaround by setting pointer.");
            *_rsfService = 1;
            ret          = _soundOnLoadHook.Original(handle, descriptor, unk);
            *_rsfService = nint.Zero;
        }
        else
        {
            ret = _soundOnLoadHook.Original(handle, descriptor, unk);
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

        var ret = _checkFileStateHook.Original(ptr, crc64);
        Penumbra.Log.Excessive($"[CheckFileState] Called on 0x{ptr:X} with CRC {crc64:X16}, returned 0x{ret:X}.");
        return ret;
    }

    private delegate byte LoadTexFileLocalDelegate(TextureResourceHandle* handle, int unk1, SeFileDescriptor* unk2, bool unk3);

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    [Signature(Sigs.LoadTexFileLocal)]
    private readonly LoadTexFileLocalDelegate _loadTexFileLocal = null!;

    private delegate byte LoadMdlFileLocalPrototype(ResourceHandle* handle, nint unk1, bool unk2);

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    [Signature(Sigs.LoadMdlFileLocal)]
    private readonly LoadMdlFileLocalPrototype _loadMdlFileLocal = null!;

    private delegate byte TexResourceHandleOnLoadPrototype(TextureResourceHandle* handle, SeFileDescriptor* descriptor, byte unk2);

    [Signature(Sigs.TexHandleOnLoad, DetourName = nameof(OnTexLoadDetour))]
    private readonly Hook<TexResourceHandleOnLoadPrototype> _textureOnLoadHook = null!;

    private byte OnTexLoadDetour(TextureResourceHandle* handle, SeFileDescriptor* descriptor, byte unk2)
    {
        var ret = _textureOnLoadHook.Original(handle, descriptor, unk2);
        if (!_texReturnData.Value)
            return ret;

        // Function failed on a replaced texture, call local.
        _texReturnData.Value = false;
        ret                  = _loadTexFileLocal(handle, _lodService.GetLod(handle), descriptor, unk2 != 0);
        _updateCategory(handle);
        return ret;
    }

    private delegate byte LoadMdlFileExternPrototype(ResourceHandle* handle, nint unk1, bool unk2, nint unk3);

    [Signature(Sigs.LoadMdlFileExtern, DetourName = nameof(LoadMdlFileExternDetour))]
    private readonly Hook<LoadMdlFileExternPrototype> _loadMdlFileExternHook = null!;

    /// <summary> We hook the extern functions to just return the local one if given the custom flag as last argument. </summary>
    private byte LoadMdlFileExternDetour(ResourceHandle* resourceHandle, nint unk1, bool unk2, nint ptr)
        => ptr.Equals(CustomFileFlag)
            ? _loadMdlFileLocal.Invoke(resourceHandle, unk1, unk2)
            : _loadMdlFileExternHook.Original(resourceHandle, unk1, unk2, ptr);
}
