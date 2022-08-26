using System;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop;

public unsafe class CharacterUtility : IDisposable
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

    // The relevant indices depend on which meta manipulations we allow for.
    // The defines are set in the project configuration.
    public static readonly Structs.CharacterUtility.Index[]
        RelevantIndices = Enum.GetValues< Structs.CharacterUtility.Index >();

    public static readonly InternalIndex[] ReverseIndices
        = Enumerable.Range( 0, Structs.CharacterUtility.TotalNumResources )
           .Select( i => new InternalIndex( Array.IndexOf( RelevantIndices, (Structs.CharacterUtility.Index) i ) ) )
           .ToArray();


    private readonly (IntPtr Address, int Size)[] _defaultResources = new (IntPtr, int)[RelevantIndices.Length];

    public (IntPtr Address, int Size) DefaultResource( Structs.CharacterUtility.Index idx )
        => _defaultResources[ ReverseIndices[ ( int )idx ].Value ];

    public (IntPtr Address, int Size) DefaultResource( InternalIndex idx )
        => _defaultResources[ idx.Value ];

    public CharacterUtility()
    {
        SignatureHelper.Initialise( this );
        LoadingFinished += () => PluginLog.Debug( "Loading of CharacterUtility finished." );
        LoadDefaultResources( null! );
        if( !Ready )
        {
            Dalamud.Framework.Update += LoadDefaultResources;
        }
    }

    // We store the default data of the resources so we can always restore them.
    private void LoadDefaultResources( object _ )
    {
        var missingCount = 0;
        if( Address == null )
        {
            return;
        }

        for( var i = 0; i < RelevantIndices.Length; ++i )
        {
            if( _defaultResources[ i ].Size == 0 )
            {
                var resource = Address->Resource( RelevantIndices[i] );
                var data     = resource->GetData();
                if( data.Data != IntPtr.Zero && data.Length != 0 )
                {
                    _defaultResources[ i ] = data;
                }
                else
                {
                    ++missingCount;
                }
            }
        }

        if( missingCount == 0 )
        {
            Ready = true;
            LoadingFinished.Invoke();
            Dalamud.Framework.Update -= LoadDefaultResources;
        }
    }

    // Set the data of one of the stored resources to a given pointer and length.
    public bool SetResource( Structs.CharacterUtility.Index resourceIdx, IntPtr data, int length )
    {
        if( !Ready )
        {
            PluginLog.Error( $"Can not set resource {resourceIdx}: CharacterUtility not ready yet." );
            return false;
        }

        var resource = Address->Resource( resourceIdx );
        var ret      = resource->SetData( data, length );
        PluginLog.Verbose( "Set resource {Idx} to 0x{NewData:X} ({NewLength} bytes).", resourceIdx, ( ulong )data, length );
        return ret;
    }

    // Reset the data of one of the stored resources to its default values.
    public void ResetResource( Structs.CharacterUtility.Index resourceIdx )
    {
        if( !Ready )
        {
            PluginLog.Error( $"Can not reset {resourceIdx}: CharacterUtility not ready yet." );
            return;
        }

        var (data, length) = DefaultResource( resourceIdx);
        var resource = Address->Resource( resourceIdx );
        PluginLog.Verbose( "Reset resource {Idx} to default at 0x{DefaultData:X} ({NewLength} bytes).", resourceIdx, ( ulong )data, length );
        resource->SetData( data, length );
    }

    // Return all relevant resources to the default resource.
    public void ResetAll()
    {
        if( !Ready )
        {
            PluginLog.Error( "Can not reset all resources: CharacterUtility not ready yet." );
            return;
        }

        foreach( var idx in RelevantIndices )
        {
            ResetResource( idx );
        }

        PluginLog.Debug( "Reset all CharacterUtility resources to default." );
    }

    public void Dispose()
    {
        ResetAll();
    }
}