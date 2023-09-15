using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using OtterGui.Classes;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.Services;

namespace Penumbra.Interop.Services;

public sealed unsafe class SkinFixer : IDisposable
{
    public static ReadOnlySpan<byte> SkinShpkName
        => "skin.shpk"u8;

    [Signature(Sigs.HumanVTable, ScanType = ScanType.StaticAddress)]
    private readonly nint* _humanVTable = null!;

    private delegate nint OnRenderMaterialDelegate(nint drawObject, OnRenderMaterialParams* param);

    [StructLayout(LayoutKind.Explicit)]
    private struct OnRenderMaterialParams
    {
        [FieldOffset(0x0)]
        public Model* Model;

        [FieldOffset(0x8)]
        public uint MaterialIndex;
    }

    private readonly Hook<OnRenderMaterialDelegate> _onRenderMaterialHook;

    private readonly GameEventManager    _gameEvents;
    private readonly CommunicatorService _communicator;
    private readonly CharacterUtility    _utility;

    // MaterialResourceHandle set
    private readonly ConcurrentSet<nint> _moddedSkinShpkMaterials = new();

    private readonly object _lock = new();

    // ConcurrentDictionary.Count uses a lock in its current implementation.
    private int   _moddedSkinShpkCount;
    private ulong _slowPathCallDelta;

    public bool Enabled { get; internal set; } = true;

    public int ModdedSkinShpkCount
        => _moddedSkinShpkCount;

    public SkinFixer(GameEventManager gameEvents, CharacterUtility utility, CommunicatorService communicator)
    {
        SignatureHelper.Initialise(this);
        _gameEvents           = gameEvents;
        _utility              = utility;
        _communicator         = communicator;
        _onRenderMaterialHook = Hook<OnRenderMaterialDelegate>.FromAddress(_humanVTable[62], OnRenderHumanMaterial);
        _communicator.MtrlShpkLoaded.Subscribe(OnMtrlShpkLoaded, MtrlShpkLoaded.Priority.SkinFixer);
        _gameEvents.ResourceHandleDestructor += OnResourceHandleDestructor;
        _onRenderMaterialHook.Enable();
    }

    public void Dispose()
    {
        _onRenderMaterialHook.Dispose();
        _communicator.MtrlShpkLoaded.Unsubscribe(OnMtrlShpkLoaded);
        _gameEvents.ResourceHandleDestructor -= OnResourceHandleDestructor;
        _moddedSkinShpkMaterials.Clear();
        _moddedSkinShpkCount = 0;
    }

    public ulong GetAndResetSlowPathCallDelta()
        => Interlocked.Exchange(ref _slowPathCallDelta, 0);

    private static bool IsSkinMaterial(Structs.MtrlResource* mtrlResource)
    {
        if (mtrlResource == null)
            return false;

        var shpkName = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(mtrlResource->ShpkString);
        return SkinShpkName.SequenceEqual(shpkName);
    }

    private void OnMtrlShpkLoaded(nint mtrlResourceHandle, nint gameObject)
    {
        var mtrl = (Structs.MtrlResource*)mtrlResourceHandle;
        var shpk = mtrl->ShpkResourceHandle;
        if (shpk == null)
            return;

        if (!IsSkinMaterial(mtrl) || (nint)shpk == _utility.DefaultSkinShpkResource)
            return;

        if (_moddedSkinShpkMaterials.TryAdd(mtrlResourceHandle))
            Interlocked.Increment(ref _moddedSkinShpkCount);
    }

    private void OnResourceHandleDestructor(Structs.ResourceHandle* handle)
    {
        if (_moddedSkinShpkMaterials.TryRemove((nint)handle))
            Interlocked.Decrement(ref _moddedSkinShpkCount);
    }

    private nint OnRenderHumanMaterial(nint human, OnRenderMaterialParams* param)
    {
        // If we don't have any on-screen instances of modded skin.shpk, we don't need the slow path at all.
        if (!Enabled || _moddedSkinShpkCount == 0)
            return _onRenderMaterialHook.Original(human, param);

        var material     = param->Model->Materials[param->MaterialIndex];
        var mtrlResource = (Structs.MtrlResource*)material->MaterialResourceHandle;
        if (!IsSkinMaterial(mtrlResource))
            return _onRenderMaterialHook.Original(human, param);

        Interlocked.Increment(ref _slowPathCallDelta);

        // Performance considerations:
        // - This function is called from several threads simultaneously, hence the need for synchronization in the swapping path ;
        // - Function is called each frame for each material on screen, after culling, i. e. up to thousands of times a frame in crowded areas ;
        // - Swapping path is taken up to hundreds of times a frame.
        // At the time of writing, the lock doesn't seem to have a noticeable impact in either framerate or CPU usage, but the swapping path shall still be avoided as much as possible.
        lock (_lock)
        {
            try
            {
                _utility.Address->SkinShpkResource = (Structs.ResourceHandle*)mtrlResource->ShpkResourceHandle;
                return _onRenderMaterialHook.Original(human, param);
            }
            finally
            {
                _utility.Address->SkinShpkResource = (Structs.ResourceHandle*)_utility.DefaultSkinShpkResource;
            }
        }
    }
}
