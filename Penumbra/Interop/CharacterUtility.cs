using System;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop;

public unsafe class CharacterUtility : IDisposable
{
    // A static pointer to the CharacterUtility address.
    [Signature( "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2", ScanType = ScanType.StaticAddress )]
    private readonly Structs.CharacterUtility** _characterUtilityAddress = null;


    // Only required for migration anymore.
    public delegate void LoadResources( Structs.CharacterUtility* address );

    [Signature( "E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2" )]
    public readonly LoadResources? LoadCharacterResourcesFunc;

    public void LoadCharacterResources()
        => LoadCharacterResourcesFunc?.Invoke( Address );

    public Structs.CharacterUtility* Address
        => *_characterUtilityAddress;

    public bool Ready { get; private set; }
    public event Action LoadingFinished;

    // The relevant indices depend on which meta manipulations we allow for.
    // The defines are set in the project configuration.
    public static readonly int[] RelevantIndices
        = Array.Empty< int >()
#if USE_EQP
           .Append( Structs.CharacterUtility.EqpIdx )
#endif
#if USE_GMP
           .Append( Structs.CharacterUtility.GmpIdx )
#endif
#if USE_EQDP
           .Concat( Enumerable.Range( Structs.CharacterUtility.EqdpStartIdx, Structs.CharacterUtility.NumEqdpFiles )
               .Where( i => i != 17 ) ) // TODO: Female Hrothgar
#endif
#if USE_CMP
           .Append( Structs.CharacterUtility.HumanCmpIdx )
#endif
#if USE_EST
           .Concat( Enumerable.Range( Structs.CharacterUtility.FaceEstIdx, 4 ) )
#endif
           .ToArray();

    public static readonly int[] ReverseIndices
        = Enumerable.Range( 0, Structs.CharacterUtility.NumResources )
           .Select( i => Array.IndexOf( RelevantIndices, i ) ).ToArray();


    public readonly (IntPtr Address, int Size)[] DefaultResources = new (IntPtr, int)[RelevantIndices.Length];

    public (IntPtr Address, int Size) DefaultResource( int fullIdx )
        => DefaultResources[ ReverseIndices[ fullIdx ] ];

    public CharacterUtility()
    {
        SignatureHelper.Initialise( this );
        LoadingFinished += () => PluginLog.Debug( "Loading of CharacterUtility finished." );
        LoadDefaultResources( true );
    }

    // We store the default data of the resources so we can always restore them.
    private void LoadDefaultResources( bool repeat )
    {
        var missingCount = 0;
        if( Address == null )
        {
            return;
        }

        for( var i = 0; i < RelevantIndices.Length; ++i )
        {
            if( DefaultResources[ i ].Size == 0 )
            {
                var resource = ( Structs.ResourceHandle* )Address->Resources[ RelevantIndices[ i ] ];
                var data     = resource->GetData();
                if( data.Data != IntPtr.Zero && data.Length != 0 )
                {
                    DefaultResources[ i ] = data;
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
        }
        else if( repeat )
        {
            PluginLog.Debug( "Custom load of character resources triggered." );
            LoadCharacterResources();
            LoadDefaultResources( false );
        }
    }

    // Set the data of one of the stored resources to a given pointer and length.
    public bool SetResource( int resourceIdx, IntPtr data, int length )
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
    public void ResetResource( int resourceIdx )
    {
        if( !Ready )
        {
            PluginLog.Error( $"Can not reset {resourceIdx}: CharacterUtility not ready yet." );
            return;
        }

        var relevantIdx = ReverseIndices[ resourceIdx ];
        var (data, length) = DefaultResources[ relevantIdx ];
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