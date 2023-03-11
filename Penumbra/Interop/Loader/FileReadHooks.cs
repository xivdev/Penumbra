using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.GameData;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Loader;

public unsafe class FileReadHooks : IDisposable
{
    private delegate byte ReadSqPackPrototype(ResourceManager* resourceManager, SeFileDescriptor* pFileDesc, int priority, bool isSync);

    [Signature(Sigs.ReadSqPack, DetourName = nameof(ReadSqPackDetour))]
    private readonly Hook<ReadSqPackPrototype> _readSqPackHook = null!;

    public FileReadHooks()
    {
        SignatureHelper.Initialise(this);
        _readSqPackHook.Enable();
    }

    /// <summary> Invoked when a file is supposed to be read from SqPack. </summary>
    /// <param name="fileDescriptor">The file descriptor containing what file to read.</param>
    /// <param name="priority">The games priority. Should not generally be changed.</param>
    /// <param name="isSync">Whether the file needs to be loaded synchronously. Should not generally be changed.</param>
    /// <param name="callOriginal">Whether to call the original function after the event is finished.</param>
    public delegate void ReadSqPackDelegate(ref SeFileDescriptor fileDescriptor, ref int priority, ref bool isSync, ref bool callOriginal);

    /// <summary>
    /// <inheritdoc cref="ReadSqPackDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ReadSqPackDelegate? ReadSqPack;

    private byte ReadSqPackDetour(ResourceManager* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        var callOriginal = true;
        ReadSqPack?.Invoke(ref *fileDescriptor, ref priority, ref isSync, ref callOriginal);
        return callOriginal
            ? _readSqPackHook.Original(resourceManager, fileDescriptor, priority, isSync)
            : (byte)1;
    }

    public void Dispose()
    {
        _readSqPackHook.Disable();
        _readSqPackHook.Dispose();
    }
}
