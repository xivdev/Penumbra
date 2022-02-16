using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Interop;

public unsafe class PathResolver : IDisposable
{
    public delegate IntPtr ResolveMdlPath( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 );
    public delegate IntPtr ResolveMtrlPath( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, IntPtr unk5 );

    [Signature( "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 41 ?? 48 83 ?? ?? 45 8B ?? 49 8B ?? 48 8B ?? 48 8B ?? 41" )]
    public Hook< ResolveMdlPath >? ResolveMdlPathHook;

    [Signature( "?? 89 ?? ?? ?? ?? 89 ?? ?? ?? ?? 89 ?? ?? ?? 57 48 83 ?? ?? 49 8B ?? 48 8B ?? 48 8B ?? 41 83 ?? ?? 0F" )]
    public Hook<ResolveMtrlPath>? ResolveMtrlPathHook;

    private global::Dalamud.Game.ClientState.Objects.Types.GameObject? FindParent( IntPtr drawObject )
        => Dalamud.Objects.FirstOrDefault( a => ( ( GameObject* )a.Address )->DrawObject == ( DrawObject* )drawObject );

    private readonly byte[] _data = new byte[512];

    private unsafe IntPtr ResolveMdlPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
    {
        var ret  = ResolveMdlPathHook!.Original( drawObject, path, unk3, unk4 );
        var n    = Marshal.PtrToStringAnsi( ret )!;
        var name = FindParent( drawObject )?.Name.ToString() ?? string.Empty;
        PluginLog.Information( $"{drawObject:X} {path:X} {unk3:X} {unk4}\n{n}\n{name}" );
        if( Service< ModManager >.Get().Collections.CharacterCollection.TryGetValue( name, out var collection ) )
        {
            var replacement = collection.ResolveSwappedOrReplacementPath( GamePath.GenerateUncheckedLower( n ) );
            if( replacement != null )
            {
                for( var i = 0; i < replacement.Length; ++i )
                {
                    _data[ i ] = ( byte )replacement[ i ];
                }

                _data[ replacement.Length ] = 0;
                fixed( byte* data = _data )
                {
                    return ( IntPtr )data;
                }
            }
        }

        return ret;
    }

    private unsafe IntPtr ResolveMtrlPathDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, IntPtr unk5 )
    {
        var ret  = ResolveMtrlPathHook!.Original( drawObject, path, unk3, unk4, unk5 );
        var n    = Marshal.PtrToStringAnsi( ret )!;
        var name = FindParent( drawObject )?.Name.ToString() ?? string.Empty;
        PluginLog.Information( $"{drawObject:X} {path:X} {unk3:X} {unk4} {unk5:X}\n{n}\n{name}" );
        if( Service<ModManager>.Get().Collections.CharacterCollection.TryGetValue( name, out var collection ) )
        {
            var replacement = collection.ResolveSwappedOrReplacementPath( GamePath.GenerateUncheckedLower( n ) );
            if( replacement != null )
            {
                for( var i = 0; i < replacement.Length; ++i )
                {
                    _data[i] = ( byte )replacement[i];
                }

                _data[replacement.Length] = 0;
                fixed( byte* data = _data )
                {
                    return ( IntPtr )data;
                }
            }
        }

        return ret;
    }

    public PathResolver()
    {
        SignatureHelper.Initialise( this );
        Enable();
    }

    public void Enable()
    {
        ResolveMdlPathHook?.Enable();
        ResolveMtrlPathHook?.Enable();
    }

    public void Disable()
    {
        ResolveMdlPathHook?.Disable();
        ResolveMtrlPathHook?.Disable();
    }

    public void Dispose()
    {
        ResolveMdlPathHook?.Dispose();
        ResolveMtrlPathHook?.Dispose();
    }
}