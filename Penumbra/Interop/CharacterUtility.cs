using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;

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

    public (IntPtr Address, int Size)[] DefaultResources = new (IntPtr, int)[Structs.CharacterUtility.NumResources];

    public CharacterUtility()
    {
        SignatureHelper.Initialise( this );
        LoadDataFilesHook.Enable();
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
        for( var i = 0; i < Structs.CharacterUtility.NumResources; ++i )
        {
            var resource = ( Structs.ResourceHandle* )Address->Resources[ i ];
            DefaultResources[ i ] = resource->GetData();
        }
    }

    // Set the data of one of the stored resources to a given pointer and length.
    public bool SetResource( int idx, IntPtr data, int length )
    {
        var resource = ( Structs.ResourceHandle* )Address->Resources[ idx ];
        var ret      = resource->SetData( data, length );
        PluginLog.Verbose( "Set resource {Idx} to 0x{NewData:X} ({NewLength} bytes).", idx, ( ulong )data, length );
        return ret;
    }

    // Reset the data of one of the stored resources to its default values.
    public void ResetResource( int idx )
    {
        var resource = ( Structs.ResourceHandle* )Address->Resources[ idx ];
        resource->SetData( DefaultResources[ idx ].Address, DefaultResources[ idx ].Size );
    }

    public void Dispose()
    {
        for( var i = 0; i < Structs.CharacterUtility.NumResources; ++i )
        {
            ResetResource( i );
        }

        LoadDataFilesHook.Dispose();
    }
}