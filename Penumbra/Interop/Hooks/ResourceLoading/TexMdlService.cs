using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets2;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.Interop.Structs;
using Penumbra.String.Classes;
using FileMode = Penumbra.Interop.Structs.FileMode;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public unsafe class TexMdlService : IDisposable, IRequiredService
{
    /// <summary>  Custom ulong flag to signal our files as opposed to SE files. </summary>
    public static readonly nint CustomFileFlag = new(0xDEADBEEF);

    /// <summary>
    /// We need to keep a list of all CRC64 hash values of our replaced Mdl and Tex files,
    /// i.e. CRC32 of filename in the lower bytes, CRC32 of parent path in the upper bytes.
    /// </summary>
    public IReadOnlySet<ulong> CustomFileCrc
        => _customFileCrc;

    public TexMdlService(IGameInteropProvider interop)
    {
        interop.InitializeFromAttributes(this);
        if (HookSettings.ReplacementHooks)
        {
            _checkFileStateHook.Enable();
            _loadMdlFileExternHook.Enable();
            _textureSomethingHook.Enable();
            _vf32Hook.Enable();
            //_loadTexFileExternHook.Enable();
        }
    }

    /// <summary> Add CRC64 if the given file is a model or texture file and has an associated path. </summary>
    public void AddCrc(ResourceType type, FullPath? path)
    {
        if (path.HasValue && type is ResourceType.Mdl)
            _customFileCrc.Add(path.Value.Crc64);
    }

    /// <summary> Add a fixed CRC64 value. </summary>
    public void AddCrc(ulong crc64)
        => _customFileCrc.Add(crc64);

    public void Dispose()
    {
        _checkFileStateHook.Dispose();
        //_loadTexFileExternHook.Dispose();
        _textureSomethingHook.Dispose();
        _loadMdlFileExternHook.Dispose();
        _vf32Hook.Dispose();
    }

    private readonly HashSet<ulong> _customFileCrc = [];

    private delegate nint CheckFileStatePrototype(nint unk1, ulong crc64);

    private delegate nint TextureSomethingDelegate(TextureResourceHandle* handle, int lod, SeFileDescriptor* descriptor);

    [Signature(Sigs.CheckFileState, DetourName = nameof(CheckFileStateDetour))]
    private readonly Hook<CheckFileStatePrototype> _checkFileStateHook = null!;

    [Signature("E8 ?? ?? ?? ?? 0F B6 C8 EB ?? 4C 8B 83", DetourName = nameof(TextureSomethingDetour))]
    private readonly Hook<TextureSomethingDelegate> _textureSomethingHook = null!;

    private nint TextureSomethingDetour(TextureResourceHandle* handle, int lod, SeFileDescriptor* descriptor)
    {
        //Penumbra.Log.Information($"SomethingDetour {handle->Handle.FileName()}");
        //if (!handle->Handle.GamePath(out var path) || !path.IsRooted())
            return _textureSomethingHook.Original(handle, lod, descriptor);

        descriptor->FileMode = FileMode.LoadUnpackedResource;
        return _loadTexFileLocal.Invoke((ResourceHandle*)handle, lod, (nint)descriptor, true);
    }

    /// <summary>
    /// The function that checks a files CRC64 to determine whether it is 'protected'.
    /// We use it to check against our stored CRC64s and if it corresponds, we return the custom flag.
    /// </summary>
    private nint CheckFileStateDetour(nint ptr, ulong crc64)
        => _customFileCrc.Contains(crc64) ? CustomFileFlag : _checkFileStateHook.Original(ptr, crc64);


    private delegate byte LoadTexFileLocalDelegate(ResourceHandle* handle, int unk1, nint unk2, bool unk3);

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    [Signature(Sigs.LoadTexFileLocal)]
    private readonly LoadTexFileLocalDelegate _loadTexFileLocal = null!;

    private delegate byte LoadMdlFileLocalPrototype(ResourceHandle* handle, nint unk1, bool unk2);

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    [Signature(Sigs.LoadMdlFileLocal)]
    private readonly LoadMdlFileLocalPrototype _loadMdlFileLocal = null!;


    private delegate byte LoadTexFileExternPrototype(ResourceHandle* handle, int unk1, nint unk2, bool unk3, nint unk4);

    private delegate byte TexResourceHandleVf32Prototype(TextureResourceHandle* handle, SeFileDescriptor* descriptor, byte unk2);

    [Signature("40 53 55 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B D9", DetourName = nameof(Vf32Detour))]
    private readonly Hook<TexResourceHandleVf32Prototype> _vf32Hook = null!;
    
    private byte Vf32Detour(TextureResourceHandle* handle, SeFileDescriptor* descriptor, byte unk2)
    {
        //if (handle->Handle.GamePath(out var path) && path.IsRooted())
        //{
        //    Penumbra.Log.Information($"Replacing {descriptor->FileMode} with {FileMode.LoadSqPackResource} in VF32 for {path}.");
        //    descriptor->FileMode = FileMode.LoadSqPackResource;
        //}

        var ret = _vf32Hook.Original(handle, descriptor, unk2);
        return ret;
    }

    //[Signature(Sigs.LoadTexFileExtern, DetourName = nameof(LoadTexFileExternDetour))]
    //private readonly Hook<LoadTexFileExternPrototype> _loadTexFileExternHook = null!;

    /// <summary> We hook the extern functions to just return the local one if given the custom flag as last argument. </summary>
    //private byte LoadTexFileExternDetour(ResourceHandle* resourceHandle, int unk1, nint unk2, bool unk3, nint ptr)
    //    => ptr.Equals(CustomFileFlag)
    //        ? _loadTexFileLocal.Invoke(resourceHandle, unk1, unk2, unk3)
    //        : _loadTexFileExternHook.Original(resourceHandle, unk1, unk2, unk3, ptr);
    public delegate byte LoadMdlFileExternPrototype(ResourceHandle* handle, nint unk1, bool unk2, nint unk3);


    [Signature(Sigs.LoadMdlFileExtern, DetourName = nameof(LoadMdlFileExternDetour))]
    private readonly Hook<LoadMdlFileExternPrototype> _loadMdlFileExternHook = null!;

    /// <summary> We hook the extern functions to just return the local one if given the custom flag as last argument. </summary>
    private byte LoadMdlFileExternDetour(ResourceHandle* resourceHandle, nint unk1, bool unk2, nint ptr)
        => ptr.Equals(CustomFileFlag)
            ? _loadMdlFileLocal.Invoke(resourceHandle, unk1, unk2)
            : _loadMdlFileExternHook.Original(resourceHandle, unk1, unk2, ptr);
}
