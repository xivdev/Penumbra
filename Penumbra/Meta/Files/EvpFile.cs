using System;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Files;


// EVP file structure:
// [Identifier:3 bytes, EVP]
// [NumModels:ushort]
// NumModels x [ModelId:ushort]
//     Containing the relevant model IDs. Seems to be sorted.
// NumModels x [DataArray]:512 Byte]
// Containing Flags in each byte, 0x01 set for Body, 0x02 set for Helmet.
// Each flag corresponds to a mount row from the Mounts table and determines whether the mount disables the effect.
public unsafe class EvpFile : MetaBaseFile
{
    public const int FlagArraySize = 512;

    [Flags]
    public enum EvpFlag : byte
    {
        None = 0x00,
        Body = 0x01,
        Head = 0x02,
        Both = Body | Head,
    }

    public int NumModels
        => Data[ 3 ];

    public ReadOnlySpan< ushort > ModelSetIds
        => new(Data + 4, NumModels);

    public ushort ModelSetId( int idx )
        => idx >= 0 && idx < NumModels ? ( ( ushort* )( Data + 4 ) )[ idx ] : ushort.MaxValue;

    public ReadOnlySpan< EvpFlag > Flags( int idx )
        => new(Data + 4 + idx * FlagArraySize, FlagArraySize);

    public EvpFlag Flag( ushort modelSet, int arrayIndex )
    {
        if( arrayIndex is >= FlagArraySize or < 0 )
        {
            return EvpFlag.None;
        }

        var ids = ModelSetIds;
        for( var i = 0; i < ids.Length; ++i )
        {
            var model = ids[ i ];
            if( model < modelSet )
            {
                continue;
            }

            if( model > modelSet )
            {
                break;
            }

            return Flags( i )[ arrayIndex ];
        }

        return EvpFlag.None;
    }

    public EvpFile()
        : base( ( MetaIndex )1 ) // TODO: Name
    { }
}