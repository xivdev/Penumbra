using System;
using Penumbra.Api.Enums;

namespace Penumbra.Api;

// Delegates used by different events.
public delegate void ChangedItemHover( object? item );
public delegate void ChangedItemClick( MouseButton button, object? item );
public delegate void GameObjectRedrawn( IntPtr objectPtr, int objectTableIndex );
public delegate void ModSettingChanged( ModSettingChange type, string collectionName, string modDirectory, bool inherited );

public delegate void CreatingCharacterBaseDelegate( IntPtr gameObject, string collectionName, IntPtr modelId, IntPtr customize,
    IntPtr equipData );

public delegate void CreatedCharacterBaseDelegate( IntPtr gameObject, string collectionName, IntPtr drawObject );
public delegate void GameObjectResourceResolvedDelegate( IntPtr gameObject, string gamePath, string localPath );