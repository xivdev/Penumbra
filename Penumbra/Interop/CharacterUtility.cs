using System;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using ImGuiScene;

namespace Penumbra.Interop;

public unsafe class CharacterUtility : IDisposable
{
    // A static pointer to the CharacterUtility address.
    [Signature( "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2", ScanType = ScanType.StaticAddress )]
    private readonly Structs.CharacterUtility** _characterUtilityAddress = null;

    // The initial function in which all the character resources get loaded.
    public delegate void LoadDataFilesDelegate( Structs.CharacterUtility* characterUtility );

    [Signature( "E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2" )]
    public Hook< LoadDataFilesDelegate > LoadDataFilesHook = null!;

    public Structs.CharacterUtility* Address
        => *_characterUtilityAddress;

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
           .Concat( Enumerable.Range( Structs.CharacterUtility.EqdpStartIdx, Structs.CharacterUtility.NumEqdpFiles ).Where( i => i != 17 ) ) // TODO: Female Hrothgar
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


    public (IntPtr Address, int Size)[] DefaultResources = new (IntPtr, int)[RelevantIndices.Length];

    public (IntPtr Address, int Size) DefaultResource( int fullIdx )
        => DefaultResources[ ReverseIndices[ fullIdx ] ];

    public CharacterUtility()
    {
        SignatureHelper.Initialise( this );

        if( Address->EqpResource != null )
        {
            LoadDefaultResources();
        }
        else
        {
            LoadDataFilesHook.Enable();
        }
    }

    // Self-disabling hook to set default resources after loading them.
    private void LoadDataFilesDetour( Structs.CharacterUtility* characterUtility )
    {
        LoadDataFilesHook.Original( characterUtility );
        LoadDefaultResources();
        PluginLog.Debug( "Character Utility resources loaded and defaults stored, disabling hook." );
        LoadDataFilesHook.Disable();
    }

    // We store the default data of the resources so we can always restore them.
    private void LoadDefaultResources()
    {
        for( var i = 0; i < RelevantIndices.Length; ++i )
        {
            var resource = ( Structs.ResourceHandle* )Address->Resources[ RelevantIndices[ i ] ];
            DefaultResources[ i ] = resource->GetData();
        }
    }

    // Set the data of one of the stored resources to a given pointer and length.
    public bool SetResource( int resourceIdx, IntPtr data, int length )
    {
        var resource = Address->Resource( resourceIdx );
        var ret      = resource->SetData( data, length );
        PluginLog.Verbose( "Set resource {Idx} to 0x{NewData:X} ({NewLength} bytes).", resourceIdx, ( ulong )data, length );
        return ret;
    }

    // Reset the data of one of the stored resources to its default values.
    public void ResetResource( int resourceIdx )
    {
        var relevantIdx = ReverseIndices[ resourceIdx ];
        var (data, length) = DefaultResources[ relevantIdx ];
        var resource = Address->Resource( resourceIdx );
        PluginLog.Verbose( "Reset resource {Idx} to default at 0x{DefaultData:X} ({NewLength} bytes).", resourceIdx, ( ulong )data, length );
        resource->SetData( data, length );
    }

    // Return all relevant resources to the default resource.
    public void ResetAll()
    {
        foreach( var idx in RelevantIndices )
        {
            ResetResource( idx );
        }
    }

    public void Dispose()
    {
        ResetAll();
        LoadDataFilesHook.Dispose();
    }
}