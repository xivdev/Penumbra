using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.Util;
using String = FFXIVClientStructs.STD.String;

namespace Penumbra.Interop;

public unsafe class PathResolver : IDisposable
{
    public delegate IntPtr ResolveMdlImcPath( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 );
    public delegate IntPtr ResolveMtrlPath( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, IntPtr unk5 );
    public delegate void   LoadMtrlTex( IntPtr mtrlResourceHandle );

    [Signature( "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 41 ?? 48 83 ?? ?? 45 8B ?? 49 8B ?? 48 8B ?? 48 8B ?? 41",
        DetourName = "ResolveMdlPathDetour" )]
    public Hook< ResolveMdlImcPath >? ResolveMdlPathHook;

    [Signature( "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 57 48 83 ?? ?? 49 8B ?? 48 8B ?? 48 8B ?? 41 83 ?? ?? 0F",
        DetourName = "ResolveMtrlPathDetour" )]
    public Hook< ResolveMtrlPath >? ResolveMtrlPathHook;

    [Signature( "40 ?? 48 83 ?? ?? 4D 8B ?? 48 8B ?? 41", DetourName = "ResolveImcPathDetour" )]
    public Hook< ResolveMdlImcPath >? ResolveImcPathHook;

    [Signature( "4C 8B ?? ?? 89 ?? ?? ?? 89 ?? ?? 55 57 41 ?? 41" )]
    public Hook< LoadMtrlTex >? LoadMtrlTexHook;

    private global::Dalamud.Game.ClientState.Objects.Types.GameObject? FindParent( IntPtr drawObject )
        => Dalamud.Objects.FirstOrDefault( a => ( ( GameObject* )a.Address )->DrawObject == ( DrawObject* )drawObject );

    private readonly byte[] _data = new byte[512];

    public static Dictionary< string, ModCollection > Dict = new();

    private IntPtr WriteData( string characterName, string path )
    {
        _data[ 0 ] = ( byte )'|';
        var i = 1;
        foreach( var c in characterName )
        {
            _data[ i++ ] = ( byte )c;
        }

        _data[ i++ ] = ( byte )'|';

        foreach( var c in path )
        {
            _data[ i++ ] = ( byte )c;
        }

        _data[ i ] = 0;
        fixed( byte* data = _data )
        {
            return ( IntPtr )data;
        }
    }

    private void LoadMtrlTexDetour( IntPtr mtrlResourceHandle )
    {
        var handle   = ( ResourceHandle* )mtrlResourceHandle;
        var mtrlName = handle->FileName.ToString();
        if( Dict.TryGetValue( mtrlName, out var collection ) )
        {
            var numTex = *( byte* )( mtrlResourceHandle + 0xFA );
            if( numTex != 0 )
            {
                PluginLog.Information( $"{mtrlResourceHandle:X} -> {mtrlName} ({collection.Name}), {numTex} Texes" );
                var texSpace = *( byte** )( mtrlResourceHandle + 0xD0 );
                for( var i = 0; i < numTex; ++i )
                {
                    var texStringPtr = ( IntPtr )( *( ulong* )( mtrlResourceHandle + 0xE0 ) + *( ushort* )( texSpace + 8 + i * 16 ) );
                    var texString    = Marshal.PtrToStringAnsi( texStringPtr ) ?? string.Empty;
                    PluginLog.Information( $"{texStringPtr:X}: {texString}" );
                    Dict[ texString ] = collection;
                }
            }
        }

        LoadMtrlTexHook!.Original( mtrlResourceHandle );
    }

    private IntPtr ResolvePathDetour( IntPtr drawObject, IntPtr path )
    {
        if( path == IntPtr.Zero )
        {
            return path;
        }

        var n = Marshal.PtrToStringAnsi( path );
        if( n == null )
        {
            return path;
        }

        var name = FindParent( drawObject )?.Name.ToString() ?? string.Empty;
        PluginLog.Information( $"{drawObject:X} {path:X}\n{n}\n{name}" );
        if( Service< ModManager >.Get().Collections.CharacterCollection.TryGetValue( name, out var value ) )
        {
            Dict[ n ] = value;
        }
        else
        {
            Dict.Remove( n );
        }

        return path;
    }

    private unsafe IntPtr ResolveMdlPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveMdlPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private unsafe IntPtr ResolveImcPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveImcPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private unsafe IntPtr ResolveMtrlPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, IntPtr unk5 )
        => ResolvePathDetour( drawObject, ResolveMtrlPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    public PathResolver()
    {
        SignatureHelper.Initialise( this );
        Enable();
    }

    public void Enable()
    {
        ResolveMdlPathHook?.Enable();
        ResolveMtrlPathHook?.Enable();
        ResolveImcPathHook?.Enable();
        LoadMtrlTexHook?.Enable();
    }

    public void Disable()
    {
        ResolveMdlPathHook?.Disable();
        ResolveMtrlPathHook?.Disable();
        ResolveImcPathHook?.Disable();
        LoadMtrlTexHook?.Disable();
    }

    public void Dispose()
    {
        ResolveMdlPathHook?.Dispose();
        ResolveMtrlPathHook?.Dispose();
        ResolveImcPathHook?.Dispose();
        LoadMtrlTexHook?.Dispose();
    }
}