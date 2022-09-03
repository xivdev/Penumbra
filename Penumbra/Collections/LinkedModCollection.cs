using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Penumbra.Collections;

public class LinkedModCollection
{
    private IntPtr? _associatedGameObject;
    public IntPtr AssociatedGameObject
    {
        get => _associatedGameObject ?? IntPtr.Zero;
        set => _associatedGameObject = value;
    }
    public ModCollection ModCollection;

    public LinkedModCollection(ModCollection modCollection)
    {
        ModCollection = modCollection;
    }

    public LinkedModCollection(IntPtr? gameObject, ModCollection collection)
    {
        AssociatedGameObject = gameObject ?? IntPtr.Zero;
        ModCollection = collection; 
    }

    public unsafe LinkedModCollection(GameObject* gameObject, ModCollection collection)
    {
        AssociatedGameObject = ( IntPtr )gameObject;
        ModCollection = collection;
    }
}
