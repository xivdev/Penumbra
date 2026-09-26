using Luna;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.Services;

namespace Penumbra.Interop.Services;

public unsafe class ResidentResourceManager : IRequiredService, IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly CharacterUtility    _utility;

    // A static pointer to the resident resource manager address.
    private readonly Structs.ResidentResourceManager** _residentResourceManagerAddress;

    // Some attach and physics files are stored in the resident resource manager, and we need to manually trigger a reload of them to get them to apply.
    public readonly delegate* unmanaged<Structs.ResidentResourceManager*, void*> LoadPlayerResources;

    public readonly delegate* unmanaged<Structs.ResidentResourceManager*, void*> UnloadPlayerResources;

    public ResidentResourceManager(HookManager interop, CommunicatorService communicator, CharacterUtility utility)
    {
        _communicator = communicator;
        _utility      = utility;
        _residentResourceManagerAddress =
            (Structs.ResidentResourceManager**)interop.SigScanner.GetStaticAddressFromSig(Sigs.ResidentResourceManager);
        LoadPlayerResources =
            (delegate*unmanaged<Structs.ResidentResourceManager*, void*>)interop.SigScanner.ScanText(Sigs.LoadPlayerResources);
        UnloadPlayerResources =
            (delegate*unmanaged<Structs.ResidentResourceManager*, void*>)interop.SigScanner.ScanText(Sigs.UnloadPlayerResources);
        _communicator.EnabledChanged.Subscribe(OnEnabledChange, EnabledChanged.Priority.ResidentResourceManager);
    }

    private void OnEnabledChange(in EnabledChanged.Arguments arguments)
    {
        if (_utility.Ready)
            Reload();
    }

    public Structs.ResidentResourceManager* Address
        => *_residentResourceManagerAddress;

    // Reload certain player resources by force.
    public void Reload()
    {
        if (Address is null || Address->NumResources <= 0)
            return;

        Penumbra.Log.Debug("Reload of resident resources triggered.");
        UnloadPlayerResources(Address);
        LoadPlayerResources(Address);
    }

    public void Dispose()
    {
        _communicator.EnabledChanged.Unsubscribe(OnEnabledChange);
    }
}
