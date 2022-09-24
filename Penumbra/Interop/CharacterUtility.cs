using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop;

public unsafe partial class CharacterUtility : IDisposable
{
    public record struct InternalIndex( int Value );

    // A static pointer to the CharacterUtility address.
    [Signature( "48 8B 05 ?? ?? ?? ?? 83 B9", ScanType = ScanType.StaticAddress )]
    private readonly Structs.CharacterUtility** _characterUtilityAddress = null;

    // Only required for migration anymore.
    public delegate void LoadResources( Structs.CharacterUtility* address );

    [Signature( "E8 ?? ?? ?? ?? 48 8D 8F ?? ?? ?? ?? E8 ?? ?? ?? ?? 33 D2 45 33 C0" )]
    public readonly LoadResources LoadCharacterResourcesFunc = null!;

    public void LoadCharacterResources()
        => LoadCharacterResourcesFunc.Invoke( Address );

    public Structs.CharacterUtility* Address
        => *_characterUtilityAddress;

    public bool Ready { get; private set; }
    public event Action LoadingFinished;
    private IntPtr _defaultTransparentResource;
    private IntPtr _defaultDecalResource;

    // The relevant indices depend on which meta manipulations we allow for.
    // The defines are set in the project configuration.
    public static readonly Structs.CharacterUtility.Index[]
        RelevantIndices = Enum.GetValues< Structs.CharacterUtility.Index >();

    public static readonly InternalIndex[] ReverseIndices
        = Enumerable.Range( 0, Structs.CharacterUtility.TotalNumResources )
           .Select( i => new InternalIndex( Array.IndexOf( RelevantIndices, ( Structs.CharacterUtility.Index )i ) ) )
           .ToArray();

    private readonly List[] _lists = Enumerable.Range( 0, RelevantIndices.Length )
       .Select( idx => new List( new InternalIndex( idx ) ) )
       .ToArray();

    public IReadOnlyList< List > Lists
        => _lists;

    public (IntPtr Address, int Size) DefaultResource( InternalIndex idx )
        => _lists[ idx.Value ].DefaultResource;

    public CharacterUtility()
    {
        SignatureHelper.Initialise( this );
        LoadingFinished += () => Penumbra.Log.Debug( "Loading of CharacterUtility finished." );
        LoadDefaultResources( null! );
        if( !Ready )
        {
            Dalamud.Framework.Update += LoadDefaultResources;
        }
    }

    // We store the default data of the resources so we can always restore them.
    private void LoadDefaultResources( object _ )
    {
        if( Address == null )
        {
            return;
        }

        var anyMissing = false;
        for( var i = 0; i < RelevantIndices.Length; ++i )
        {
            var list = _lists[ i ];
            if( !list.Ready )
            {
                var resource = Address->Resource( RelevantIndices[ i ] );
                var (data, length) = resource->GetData();
                list.SetDefaultResource( data, length );
                anyMissing |= !_lists[ i ].Ready;
            }
        }

        if( _defaultTransparentResource == IntPtr.Zero )
        {
            _defaultTransparentResource =  ( IntPtr )Address->TransparentTexResource;
            anyMissing                  |= _defaultTransparentResource == IntPtr.Zero;
        }

        if( _defaultDecalResource == IntPtr.Zero )
        {
            _defaultDecalResource =  ( IntPtr )Address->DecalTexResource;
            anyMissing            |= _defaultDecalResource == IntPtr.Zero;
        }

        if( !anyMissing )
        {
            Ready                    =  true;
            Dalamud.Framework.Update -= LoadDefaultResources;
            LoadingFinished.Invoke();
        }
    }

    public void SetResource( Structs.CharacterUtility.Index resourceIdx, IntPtr data, int length )
    {
        var idx  = ReverseIndices[ ( int )resourceIdx ];
        var list = _lists[ idx.Value ];
        list.SetResource( data, length );
    }

    public void ResetResource( Structs.CharacterUtility.Index resourceIdx )
    {
        var idx  = ReverseIndices[ ( int )resourceIdx ];
        var list = _lists[ idx.Value ];
        list.ResetResource();
    }

    public List.MetaReverter TemporarilySetResource( Structs.CharacterUtility.Index resourceIdx, IntPtr data, int length )
    {
        var idx  = ReverseIndices[ ( int )resourceIdx ];
        var list = _lists[ idx.Value ];
        return list.TemporarilySetResource( data, length );
    }

    public List.MetaReverter TemporarilyResetResource( Structs.CharacterUtility.Index resourceIdx )
    {
        var idx  = ReverseIndices[ ( int )resourceIdx ];
        var list = _lists[ idx.Value ];
        return list.TemporarilyResetResource();
    }

    // Return all relevant resources to the default resource.
    public void ResetAll()
    {
        foreach( var list in _lists )
        {
            list.Dispose();
        }

        Address->TransparentTexResource = ( Structs.TextureResourceHandle* )_defaultTransparentResource;
        Address->DecalTexResource       = ( Structs.TextureResourceHandle* )_defaultDecalResource;
    }

    public void Dispose()
    {
        ResetAll();
    }
}