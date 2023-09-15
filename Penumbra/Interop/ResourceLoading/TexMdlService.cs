using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceLoading;

public unsafe class TexMdlService
{
    /// <summary>  Custom ulong flag to signal our files as opposed to SE files. </summary>
    public static readonly IntPtr CustomFileFlag = new(0xDEADBEEF);

    /// <summary>
    /// We need to keep a list of all CRC64 hash values of our replaced Mdl and Tex files,
    /// i.e. CRC32 of filename in the lower bytes, CRC32 of parent path in the upper bytes.
    /// </summary>
    public IReadOnlySet<ulong> CustomFileCrc
        => _customFileCrc;

    public TexMdlService()
    {
        SignatureHelper.Initialise(this);
        _checkFileStateHook.Enable();
        _loadTexFileExternHook.Enable();
        _loadMdlFileExternHook.Enable();
    }

    /// <summary> Add CRC64 if the given file is a model or texture file and has an associated path. </summary>
    public void AddCrc(ResourceType type, FullPath? path)
    {
        if (path.HasValue && type is ResourceType.Mdl or ResourceType.Tex)
            _customFileCrc.Add(path.Value.Crc64);
    }

    /// <summary> Add a fixed CRC64 value. </summary>
    public void AddCrc(ulong crc64)
        => _customFileCrc.Add(crc64);

    public void Dispose()
    {
        _checkFileStateHook.Dispose();
        _loadTexFileExternHook.Dispose();
        _loadMdlFileExternHook.Dispose();
    }

    private readonly HashSet<ulong> _customFileCrc = new();

    private delegate IntPtr CheckFileStatePrototype(IntPtr unk1, ulong crc64);

    [Signature(Sigs.CheckFileState, DetourName = nameof(CheckFileStateDetour))]
    private readonly Hook<CheckFileStatePrototype> _checkFileStateHook = null!;

    /// <summary>
    /// The function that checks a files CRC64 to determine whether it is 'protected'.
    /// We use it to check against our stored CRC64s and if it corresponds, we return the custom flag.
    /// </summary>
    private IntPtr CheckFileStateDetour(IntPtr ptr, ulong crc64)
        => _customFileCrc.Contains(crc64) ? CustomFileFlag : _checkFileStateHook.Original(ptr, crc64);


    private delegate byte LoadTexFileLocalDelegate(ResourceHandle* handle, int unk1, IntPtr unk2, bool unk3);

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    [Signature(Sigs.LoadTexFileLocal)]
    private readonly LoadTexFileLocalDelegate _loadTexFileLocal = null!;

    private delegate byte LoadMdlFileLocalPrototype(ResourceHandle* handle, IntPtr unk1, bool unk2);

    /// <summary> We use the local functions for our own files in the extern hook. </summary>
    [Signature(Sigs.LoadMdlFileLocal)]
    private readonly LoadMdlFileLocalPrototype _loadMdlFileLocal = null!;


    private delegate byte LoadTexFileExternPrototype(ResourceHandle* handle, int unk1, IntPtr unk2, bool unk3, IntPtr unk4);

    [Signature(Sigs.LoadTexFileExtern, DetourName = nameof(LoadTexFileExternDetour))]
    private readonly Hook<LoadTexFileExternPrototype> _loadTexFileExternHook = null!;

    /// <summary> We hook the extern functions to just return the local one if given the custom flag as last argument. </summary>
    private byte LoadTexFileExternDetour(ResourceHandle* resourceHandle, int unk1, IntPtr unk2, bool unk3, IntPtr ptr)
        => ptr.Equals(CustomFileFlag)
            ? _loadTexFileLocal.Invoke(resourceHandle, unk1, unk2, unk3)
            : _loadTexFileExternHook.Original(resourceHandle, unk1, unk2, unk3, ptr);

    public delegate byte LoadMdlFileExternPrototype(ResourceHandle* handle, IntPtr unk1, bool unk2, IntPtr unk3);


    [Signature(Sigs.LoadMdlFileExtern, DetourName = nameof(LoadMdlFileExternDetour))]
    private readonly Hook<LoadMdlFileExternPrototype> _loadMdlFileExternHook = null!;

    /// <summary> We hook the extern functions to just return the local one if given the custom flag as last argument. </summary>
    private byte LoadMdlFileExternDetour(ResourceHandle* resourceHandle, IntPtr unk1, bool unk2, IntPtr ptr)
        => ptr.Equals(CustomFileFlag)
            ? _loadMdlFileLocal.Invoke(resourceHandle, unk1, unk2)
            : _loadMdlFileExternHook.Original(resourceHandle, unk1, unk2, ptr);
}
