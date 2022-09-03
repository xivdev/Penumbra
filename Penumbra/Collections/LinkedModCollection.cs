using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Penumbra.Collections;

public class LinkedModCollection
{
    private IntPtr? _associatedGameObject;
    public IntPtr AssociatedGameObject = IntPtr.Zero;
    public ModCollection ModCollection;

    public LinkedModCollection(ModCollection modCollection)
    {
        ModCollection = modCollection;
    }

    public LinkedModCollection(IntPtr gameObject, ModCollection collection)
    {
        AssociatedGameObject = gameObject;
        ModCollection = collection; 
    }

    public unsafe LinkedModCollection(GameObject* gameObject, ModCollection collection) : this((IntPtr)gameObject, collection)
    {
    }
}
