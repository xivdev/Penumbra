using Dalamud.Hooking;
using Luna;
using Penumbra.GameData;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed unsafe class FileReadService : IDisposable, IRequiredService
{
    private readonly HookManager _hooks;

    public FileReadService(ResourceManagerService resourceManager, HookManager hooks)
    {
        _resourceManager = resourceManager;
        _hooks           = hooks;
        _readSqPackHook = hooks.CreateHook<ReadSqPackPrototype>("ReadSqPack", Sigs.ReadSqPack, ReadSqPackDetour,
            !HookOverrides.Instance.ResourceLoading.ReadSqPack)!;
        _readFile = (delegate* unmanaged<nint, SeFileDescriptor*, int, byte, byte>)hooks.SigScanner.ScanText(Sigs.ReadFile);
    }

    /// <summary> Invoked when a file is supposed to be read from SqPack. </summary>
    /// <param name="fileDescriptor">The file descriptor containing what file to read.</param>
    /// <param name="priority">The games priority. Should not generally be changed.</param>
    /// <param name="isSync">Whether the file needs to be loaded synchronously. Should not generally be changed.</param>
    /// <param name="returnValue">The return value. If this is set, original will not be called.</param>
    public delegate void ReadSqPackDelegate(SeFileDescriptor* fileDescriptor, ref int priority, ref bool isSync, ref byte? returnValue);

    /// <summary>
    /// <inheritdoc cref="ReadSqPackDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ReadSqPackDelegate? ReadSqPack;

    /// <summary>
    /// Use the games ReadFile function to read a file from the hard drive instead of an SqPack.
    /// </summary>
    /// <param name="fileDescriptor">The file to load.</param>
    /// <param name="priority">The games priority.</param>
    /// <param name="isSync">Whether the file needs to be loaded synchronously.</param>
    /// <returns>Unknown, not directly success/failure.</returns>
    public byte ReadFile(SeFileDescriptor* fileDescriptor, int priority, bool isSync)
        => _readFile(GetResourceManager(), fileDescriptor, priority, isSync ? (byte)1 : (byte)0);

    public byte ReadDefaultSqPack(SeFileDescriptor* fileDescriptor, int priority, bool isSync)
        => _readSqPackHook.Result.Original(GetResourceManager(), fileDescriptor, priority, isSync);

    public void Dispose()
        => _hooks.DisposeHook("ReadSqPack");

    private readonly ResourceManagerService _resourceManager;

    private delegate byte ReadSqPackPrototype(nint resourceManager, SeFileDescriptor* pFileDesc, int priority, bool isSync);

    private readonly Task<Hook<ReadSqPackPrototype>> _readSqPackHook;

    private byte ReadSqPackDetour(nint resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        byte? ret = null;
        _lastFileThreadResourceManager.Value = resourceManager;
        ReadSqPack?.Invoke(fileDescriptor, ref priority, ref isSync, ref ret);
        _lastFileThreadResourceManager.Value = nint.Zero;
        return ret ?? _readSqPackHook.Result.Original(resourceManager, fileDescriptor, priority, isSync);
    }

    /// We need to use the ReadFile function to load local, uncompressed files instead of loading them from the SqPacks.
    private readonly delegate* unmanaged<nint, SeFileDescriptor*, int, byte, byte> _readFile;

    private readonly ThreadLocal<nint> _lastFileThreadResourceManager = new(true);

    /// <summary>
    /// Usually files are loaded using the resource manager as a first pointer, but it seems some rare cases are using something else.
    /// So we keep track of them per thread and use them.
    /// </summary>
    private nint GetResourceManager()
        => !_lastFileThreadResourceManager.IsValueCreated || _lastFileThreadResourceManager.Value == nint.Zero
            ? (nint)_resourceManager.ResourceManager
            : _lastFileThreadResourceManager.Value;
}
