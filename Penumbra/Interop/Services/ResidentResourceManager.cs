using Luna;
using Penumbra.GameData;

namespace Penumbra.Interop.Services;

public unsafe class ResidentResourceManager(HookManager interop) : IService
{
    // A static pointer to the resident resource manager address.
    private readonly Structs.ResidentResourceManager** _residentResourceManagerAddress =
        (Structs.ResidentResourceManager**)interop.SigScanner.GetStaticAddressFromSig(Sigs.ResidentResourceManager);

    // Some attach and physics files are stored in the resident resource manager, and we need to manually trigger a reload of them to get them to apply.
    public readonly delegate* unmanaged<Structs.ResidentResourceManager*, void*> LoadPlayerResources =
        (delegate*unmanaged<Structs.ResidentResourceManager*, void*>)interop.SigScanner.ScanText(Sigs.LoadPlayerResources);

    public readonly delegate* unmanaged<Structs.ResidentResourceManager*, void*> UnloadPlayerResources =
        (delegate*unmanaged<Structs.ResidentResourceManager*, void*>)interop.SigScanner.ScanText(Sigs.UnloadPlayerResources);

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
}
