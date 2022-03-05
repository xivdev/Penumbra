using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Interop;

public unsafe class PathResolver : IDisposable
{
    //public delegate IntPtr ResolveMdlImcPathDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 );
    //public delegate IntPtr ResolveMtrlPathDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, IntPtr unk5 );
    //public delegate byte   LoadMtrlFilesDelegate( IntPtr mtrlResourceHandle );
    //public delegate IntPtr CharacterBaseCreateDelegate( uint a, IntPtr b, IntPtr c, byte d );
    //public delegate void   EnableDrawDelegate( IntPtr gameObject, IntPtr b, IntPtr c, IntPtr d );
    //public delegate void   CharacterBaseDestructorDelegate( IntPtr drawBase );
    //
    //[Signature( "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 41 ?? 48 83 ?? ?? 45 8B ?? 49 8B ?? 48 8B ?? 48 8B ?? 41",
    //    DetourName = "ResolveMdlPathDetour" )]
    //public Hook< ResolveMdlImcPathDelegate >? ResolveMdlPathHook;
    //
    //[Signature( "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 57 48 83 ?? ?? 49 8B ?? 48 8B ?? 48 8B ?? 41 83 ?? ?? 0F",
    //    DetourName = "ResolveMtrlPathDetour" )]
    //public Hook< ResolveMtrlPathDelegate >? ResolveMtrlPathHook;
    //
    //[Signature( "40 ?? 48 83 ?? ?? 4D 8B ?? 48 8B ?? 41", DetourName = "ResolveImcPathDetour" )]
    //public Hook< ResolveMdlImcPathDelegate >? ResolveImcPathHook;
    //
    //[Signature( "4C 8B ?? ?? 89 ?? ?? ?? 89 ?? ?? 55 57 41 ?? 41", DetourName = "LoadMtrlTexDetour" )]
    //public Hook< LoadMtrlFilesDelegate >? LoadMtrlTexHook;
    //
    //[Signature( "?? 89 ?? ?? ?? 57 48 81 ?? ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ?? 48 33 ?? ?? 89 ?? ?? ?? ?? ?? ?? 44 ?? ?? ?? ?? ?? ?? ?? 4C",
    //    DetourName = "LoadMtrlShpkDetour" )]
    //public Hook< LoadMtrlFilesDelegate >? LoadMtrlShpkHook;
    //
    //[Signature( "E8 ?? ?? ?? ?? 48 85 C0 74 21 C7 40" )]
    //public Hook< CharacterBaseCreateDelegate >? CharacterBaseCreateHook;
    //
    //[Signature(
    //    "40 ?? 48 81 ?? ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ?? 48 33 ?? ?? 89 ?? ?? ?? ?? ?? ?? 48 8B ?? 48 8B ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? ?? BB" )]
    //public Hook< EnableDrawDelegate >? EnableDrawHook;
    //
    //[Signature( "E8 ?? ?? ?? ?? 40 F6 C7 01 74 3A 40 F6 C7 04 75 27 48 85 DB 74 2F 48 8B 05 ?? ?? ?? ?? 48 8B D3 48 8B 48 30",
    //    DetourName = "CharacterBaseDestructorDetour" )]
    //public Hook< CharacterBaseDestructorDelegate >? CharacterBaseDestructorHook;
    //
    //public delegate void UpdateModelDelegate( IntPtr drawObject );
    //
    //[Signature( "48 8B ?? 56 48 83 ?? ?? ?? B9", DetourName = "UpdateModelsDetour" )]
    //public Hook< UpdateModelDelegate >? UpdateModelsHook;
    //
    //public delegate void SetupConnectorModelAttributesDelegate( IntPtr drawObject, IntPtr unk );
    //
    //[Signature( "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 57 41 ?? 41 ?? 41 ?? 41 ?? 48 83 ?? ?? 8B ?? ?? ?? 4C",
    //    DetourName = "SetupConnectorModelAttributesDetour" )]
    //public Hook< SetupConnectorModelAttributesDelegate >? SetupConnectorModelAttributesHook;
    //
    //public delegate void SetupModelAttributesDelegate( IntPtr drawObject );
    //
    //[Signature( "48 89 6C 24 ?? 56 57 41 54 41 55 41 56 48 83 EC 20", DetourName = "SetupModelAttributesDetour" )]
    //public Hook< SetupModelAttributesDelegate >? SetupModelAttributesHook;
    //
    //[Signature( "40 ?? 48 83 ?? ?? ?? 81 ?? ?? ?? ?? ?? 48 8B ?? 74 ?? ?? 83 ?? ?? ?? ?? ?? ?? 74 ?? 4C",
    //    DetourName = "GetSlotEqpFlagIndirectDetour" )]
    //public Hook< SetupModelAttributesDelegate >? GetSlotEqpFlagIndirectHook;
    //
    //public delegate void ApplyVisorStuffDelegate( IntPtr drawObject, IntPtr unk1, float unk2, IntPtr unk3, ushort unk4, char unk5 );
    //
    //[Signature( "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", DetourName = "ApplyVisorStuffDetour" )]
    //public Hook< ApplyVisorStuffDelegate >? ApplyVisorStuffHook;
    //
    //private readonly  ResourceLoader                          _loader;
    //private readonly  ResidentResourceManager                 _resident;
    //internal readonly Dictionary< IntPtr, int >               _drawObjectToObject = new();
    //internal readonly Dictionary< Utf8String, ModCollection > _pathCollections    = new();
    //
    //internal GameObject* _lastGameObject = null;
    //internal DrawObject* _lastDrawObject = null;
    //
    //private bool   EqpDataChanged = false;
    //private IntPtr DefaultEqpData;
    //private int    DefaultEqpLength;
    //
    //private void ApplyVisorStuffDetour( IntPtr drawObject, IntPtr unk1, float unk2, IntPtr unk3, ushort unk4, char unk5 )
    //{
    //    PluginLog.Information( $"{drawObject:X} {unk1:X} {unk2} {unk3:X} {unk4} {unk5} {( ulong )FindParent( drawObject ):X}" );
    //    ApplyVisorStuffHook!.Original( drawObject, unk1, unk2, unk3, unk4, unk5 );
    //}
    //
    //private void GetSlotEqpFlagIndirectDetour( IntPtr drawObject )
    //{
    //    if( ( *( byte* )( drawObject + 0xa30 ) & 1 ) == 0 || *( ulong* )( drawObject + 0xa28 ) == 0 )
    //    {
    //        return;
    //    }
    //
    //    ChangeEqp( drawObject );
    //    GetSlotEqpFlagIndirectHook!.Original( drawObject );
    //    RestoreEqp();
    //}
    //
    //private void ChangeEqp( IntPtr drawObject )
    //{
    //    var parent = FindParent( drawObject );
    //    if( parent == null )
    //    {
    //        return;
    //    }
    //
    //    var name = new Utf8String( parent->Name );
    //    if( name.Length == 0 )
    //    {
    //        return;
    //    }
    //
    //    var charName = name.ToString();
    //    if( !Service< ModManager >.Get().Collections.CharacterCollection.TryGetValue( charName, out var collection ) )
    //    {
    //        collection = Service< ModManager >.Get().Collections.DefaultCollection;
    //    }
    //
    //    if( collection.Cache == null )
    //    {
    //        collection = Service< ModManager >.Get().Collections.ForcedCollection;
    //    }
    //
    //    var data = collection.Cache?.MetaManipulations.EqpData;
    //    if( data == null || data.Length == 0 )
    //    {
    //        return;
    //    }
    //
    //    _resident.CharacterUtility->EqpResource->SetData( data );
    //    PluginLog.Information( $"Changed eqp data to {collection.Name}." );
    //    EqpDataChanged = true;
    //}
    //
    //private void RestoreEqp()
    //{
    //    if( !EqpDataChanged )
    //    {
    //        return;
    //    }
    //
    //    _resident.CharacterUtility->EqpResource->SetData( new ReadOnlySpan< byte >( ( void* )DefaultEqpData, DefaultEqpLength ) );
    //    PluginLog.Information( $"Changed eqp data back." );
    //    EqpDataChanged = false;
    //}
    //
    //private void SetupModelAttributesDetour( IntPtr drawObject )
    //{
    //    ChangeEqp( drawObject );
    //    SetupModelAttributesHook!.Original( drawObject );
    //    RestoreEqp();
    //}
    //
    //private void UpdateModelsDetour( IntPtr drawObject )
    //{
    //    if( *( int* )( drawObject + 0x90c ) == 0 )
    //    {
    //        return;
    //    }
    //
    //    ChangeEqp( drawObject );
    //    UpdateModelsHook!.Original.Invoke( drawObject );
    //    RestoreEqp();
    //}
    //
    //private void SetupConnectorModelAttributesDetour( IntPtr drawObject, IntPtr unk )
    //{
    //    ChangeEqp( drawObject );
    //    SetupConnectorModelAttributesHook!.Original.Invoke( drawObject, unk );
    //    RestoreEqp();
    //}
    //
    //private void EnableDrawDetour( IntPtr gameObject, IntPtr b, IntPtr c, IntPtr d )
    //{
    //    _lastGameObject = ( GameObject* )gameObject;
    //    EnableDrawHook!.Original.Invoke( gameObject, b, c, d );
    //    _lastGameObject = null;
    //}
    //
    //private IntPtr CharacterBaseCreateDetour( uint a, IntPtr b, IntPtr c, byte d )
    //{
    //    var ret = CharacterBaseCreateHook!.Original( a, b, c, d );
    //    if( _lastGameObject != null )
    //    {
    //        _drawObjectToObject[ ret ] = _lastGameObject->ObjectIndex;
    //    }
    //
    //    return ret;
    //}
    //
    //private void CharacterBaseDestructorDetour( IntPtr drawBase )
    //{
    //    _drawObjectToObject.Remove( drawBase );
    //    CharacterBaseDestructorHook!.Original.Invoke( drawBase );
    //}
    //
    //private bool VerifyEntry( IntPtr drawObject, int gameObjectIdx, out GameObject* gameObject )
    //{
    //    gameObject = ( GameObject* )( Dalamud.Objects[ gameObjectIdx ]?.Address ?? IntPtr.Zero );
    //    if( gameObject != null && gameObject->DrawObject == ( DrawObject* )drawObject )
    //    {
    //        return true;
    //    }
    //
    //    _drawObjectToObject.Remove( drawObject );
    //    return false;
    //}
    //
    //private GameObject* FindParent( IntPtr drawObject )
    //{
    //    if( _drawObjectToObject.TryGetValue( drawObject, out var gameObjectIdx ) )
    //    {
    //        if( VerifyEntry( drawObject, gameObjectIdx, out var gameObject ) )
    //        {
    //            return gameObject;
    //        }
    //
    //        _drawObjectToObject.Remove( drawObject );
    //    }
    //
    //    if( _lastGameObject != null && ( _lastGameObject->DrawObject == null || _lastGameObject->DrawObject == ( DrawObject* )drawObject ) )
    //    {
    //        return _lastGameObject;
    //    }
    //
    //    return null;
    //}
    //
    //private void SetCollection( Utf8String path, ModCollection? collection )
    //{
    //    if( collection == null )
    //    {
    //        _pathCollections.Remove( path );
    //    }
    //    else if( _pathCollections.ContainsKey( path ) )
    //    {
    //        _pathCollections[ path ] = collection;
    //    }
    //    else
    //    {
    //        _pathCollections[ path.Clone() ] = collection;
    //    }
    //}
    //
    //private void LoadMtrlTexHelper( IntPtr mtrlResourceHandle )
    //{
    //    if( mtrlResourceHandle == IntPtr.Zero )
    //    {
    //        return;
    //    }
    //
    //    var numTex = *( byte* )( mtrlResourceHandle + 0xFA );
    //    if( numTex == 0 )
    //    {
    //        return;
    //    }
    //
    //    var handle     = ( Structs.ResourceHandle* )mtrlResourceHandle;
    //    var mtrlName   = Utf8String.FromSpanUnsafe( handle->FileNameSpan(), true, null, true );
    //    var collection = _pathCollections.TryGetValue( mtrlName, out var c ) ? c : null;
    //    var texSpace   = *( byte** )( mtrlResourceHandle + 0xD0 );
    //    for( var i = 0; i < numTex; ++i )
    //    {
    //        var texStringPtr = ( byte* )( *( ulong* )( mtrlResourceHandle + 0xE0 ) + *( ushort* )( texSpace + 8 + i * 16 ) );
    //        var texString    = new Utf8String( texStringPtr );
    //        SetCollection( texString, collection );
    //    }
    //}
    //
    //private byte LoadMtrlTexDetour( IntPtr mtrlResourceHandle )
    //{
    //    LoadMtrlTexHelper( mtrlResourceHandle );
    //    return LoadMtrlTexHook!.Original( mtrlResourceHandle );
    //}
    //
    //private byte LoadMtrlShpkDetour( IntPtr mtrlResourceHandle )
    //    => LoadMtrlShpkHook!.Original( mtrlResourceHandle );
    //
    //private IntPtr ResolvePathDetour( IntPtr drawObject, IntPtr path )
    //{
    //    if( path == IntPtr.Zero )
    //    {
    //        return path;
    //    }
    //
    //    var p = new Utf8String( ( byte* )path );
    //
    //    var parent = FindParent( drawObject );
    //    if( parent == null )
    //    {
    //        return path;
    //    }
    //
    //    var name = new Utf8String( parent->Name );
    //    if( name.Length == 0 )
    //    {
    //        return path;
    //    }
    //
    //    var charName = name.ToString();
    //    var gamePath = new Utf8String( ( byte* )path );
    //    if( !Service< ModManager >.Get().Collections.CharacterCollection.TryGetValue( charName, out var collection ) )
    //    {
    //        SetCollection( gamePath, null );
    //        return path;
    //    }
    //
    //    SetCollection( gamePath, collection );
    //    return path;
    //}
    //
    //private IntPtr ResolveMdlPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
    //{
    //    ChangeEqp( drawObject );
    //    var ret = ResolvePathDetour( drawObject, ResolveMdlPathHook!.Original( drawObject, path, unk3, unk4 ) );
    //    RestoreEqp();
    //    return ret;
    //}
    //
    //private IntPtr ResolveImcPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
    //    => ResolvePathDetour( drawObject, ResolveImcPathHook!.Original( drawObject, path, unk3, unk4 ) );
    //
    //private IntPtr ResolveMtrlPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, IntPtr unk5 )
    //    => ResolvePathDetour( drawObject, ResolveMtrlPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );
    //
    //public PathResolver( ResourceLoader loader, ResidentResourceManager resident )
    //{
    //    _loader   = loader;
    //    _resident = resident;
    //    SignatureHelper.Initialise( this );
    //    var data = _resident.CharacterUtility->EqpResource->GetData();
    //    fixed( byte* ptr = data )
    //    {
    //        DefaultEqpData = ( IntPtr )ptr;
    //    }
    //
    //    DefaultEqpLength = data.Length;
    //    Enable();
    //    foreach( var gameObject in Dalamud.Objects )
    //    {
    //        var ptr = ( GameObject* )gameObject.Address;
    //        if( ptr->IsCharacter() && ptr->DrawObject != null )
    //        {
    //            _drawObjectToObject[ ( IntPtr )ptr->DrawObject ] = ptr->ObjectIndex;
    //        }
    //    }
    //}
    //
    //
    //private (FullPath?, object?) CharacterReplacer( NewGamePath path )
    //{
    //    var modManager = Service< ModManager >.Get();
    //    var gamePath   = new GamePath( path.ToString() );
    //    var nonDefault = _pathCollections.TryGetValue( path.Path, out var collection );
    //    if( !nonDefault )
    //    {
    //        collection = modManager.Collections.DefaultCollection;
    //    }
    //
    //    var resolved = collection!.ResolveSwappedOrReplacementPath( gamePath );
    //    if( resolved == null )
    //    {
    //        resolved = modManager.Collections.ForcedCollection.ResolveSwappedOrReplacementPath( gamePath );
    //        if( resolved == null )
    //        {
    //            return ( null, collection );
    //        }
    //
    //        collection = modManager.Collections.ForcedCollection;
    //    }
    //
    //    var fullPath = new FullPath( resolved );
    //    if( nonDefault )
    //    {
    //        SetCollection( fullPath.InternalName, nonDefault ? collection : null );
    //    }
    //
    //    return ( fullPath, collection );
    //}
    //
    //public void Enable()
    //{
    //    ResolveMdlPathHook?.Enable();
    //    ResolveMtrlPathHook?.Enable();
    //    ResolveImcPathHook?.Enable();
    //    LoadMtrlTexHook?.Enable();
    //    LoadMtrlShpkHook?.Enable();
    //    EnableDrawHook?.Enable();
    //    CharacterBaseCreateHook?.Enable();
    //    _loader.ResolvePath = CharacterReplacer;
    //    CharacterBaseDestructorHook?.Enable();
    //    SetupConnectorModelAttributesHook?.Enable();
    //    UpdateModelsHook?.Enable();
    //    SetupModelAttributesHook?.Enable();
    //    ApplyVisorStuffHook?.Enable();
    //}
    //
    //public void Disable()
    //{
    //    _loader.ResolvePath = ResourceLoader.DefaultReplacer;
    //    ResolveMdlPathHook?.Disable();
    //    ResolveMtrlPathHook?.Disable();
    //    ResolveImcPathHook?.Disable();
    //    LoadMtrlTexHook?.Disable();
    //    LoadMtrlShpkHook?.Disable();
    //    EnableDrawHook?.Disable();
    //    CharacterBaseCreateHook?.Disable();
    //    CharacterBaseDestructorHook?.Disable();
    //    SetupConnectorModelAttributesHook?.Disable();
    //    UpdateModelsHook?.Disable();
    //    SetupModelAttributesHook?.Disable();
    //    ApplyVisorStuffHook?.Disable();
    //}
    //
    public void Dispose()
    {
    //    Disable();
    //    ResolveMdlPathHook?.Dispose();
    //    ResolveMtrlPathHook?.Dispose();
    //    ResolveImcPathHook?.Dispose();
    //    LoadMtrlTexHook?.Dispose();
    //    LoadMtrlShpkHook?.Dispose();
    //    EnableDrawHook?.Dispose();
    //    CharacterBaseCreateHook?.Dispose();
    //    CharacterBaseDestructorHook?.Dispose();
    //    SetupConnectorModelAttributesHook?.Dispose();
    //    UpdateModelsHook?.Dispose();
    //    SetupModelAttributesHook?.Dispose();
    //    ApplyVisorStuffHook?.Dispose();
    }
}