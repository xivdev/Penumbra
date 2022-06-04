using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Collections;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    // Probably used when the base idle animation gets loaded.
    // Make it aware of the correct collection to load the correct pap files.
    //[Signature( "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B CF 44 8B C2 E8 ?? ?? ?? ?? 48 8B 05", DetourName = "CharacterBaseLoadAnimationDetour" )]
    //public Hook< CharacterBaseDestructorDelegate >? CharacterBaseLoadAnimationHook;
    //
    //private ModCollection? _animationLoadCollection;
    //
    //private void CharacterBaseLoadAnimationDetour( IntPtr drawObject )
    //{
    //    _animationLoadCollection = _lastCreatedCollection
    //     ?? ( FindParent( drawObject, out var collection ) != null ? collection : Penumbra.CollectionManager.Default );
    //    CharacterBaseLoadAnimationHook!.Original( drawObject );
    //    _animationLoadCollection = null;
    //}

    // Probably used when action paps are loaded.
    // Make it aware of the correct collection to load the correct pap files.
    //public delegate void PapLoadFunction( IntPtr drawObject, IntPtr a2, uint a3, IntPtr a4, uint a5, uint a6, uint a7 );

    //[Signature( "E8 ?? ?? ?? ?? 0F 10 00 0F 11 06", DetourName = "RandomPapDetour" )]
    //public Hook< PapLoadFunction >? RandomPapHook;

    //private void RandomPapDetour( IntPtr drawObject, IntPtr a2, uint a3, IntPtr a4, uint a5, uint a6, uint a7 )
    //{
    //    _animationLoadCollection = _lastCreatedCollection
    //     ?? ( FindParent( drawObject, out var collection ) != null ? collection : Penumbra.CollectionManager.Default );
    //    RandomPapHook!.Original( drawObject, a2, a3, a4, a5, a6, a7 );
    //    _animationLoadCollection = null;
    //}

    //private void TestFunction()
    //{
    //    var p = Dalamud.Objects.FirstOrDefault( o => o.Name.ToString() == "Demi-Phoenix" );
    //    if( p != null )
    //    {
    //        var draw = ( ( GameObject* )p.Address )->DrawObject;
    //        PluginLog.Information( $"{p.Address:X} {( draw != null ? ( ( IntPtr )draw ).ToString( "X" ) : "NULL" )}" );
    //    }
    //}
    //
    //public delegate void TmbLoadFunction(IntPtr drawObject, ushort a2, uint a3, IntPtr a4, IntPtr a5 );
    //
    //[Signature( "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 44 38 75 ?? 74 ?? 44 89 B3 ", DetourName ="RandomTmbDetour" )]
    //public Hook< TmbLoadFunction > UnkHook = null;
    //
    //private void RandomTmbDetour( IntPtr drawObject, ushort a2, uint a3, IntPtr a4, IntPtr a5 )
    //{
    //    //PluginLog.Information($"{drawObject:X} {a2:X}, {a3:X} {a4:X} {a5:X}"  );
    //    //TestFunction();
    //    UnkHook!.Original( drawObject, a2, a3, a4, a5);
    //}
}