using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;
using Penumbra.Interop.Structs;
using System.Collections.Generic;

namespace Penumbra.Meta.Files;

public sealed unsafe class CmpFile : MetaBaseFile
{
    private const int RacialScalingStart = 0x2A800;

    public float this[ SubRace subRace, RspAttribute attribute ]
    {
        get => *( float* )( Data + RacialScalingStart + subRace.ToRspIndex() * RspEntry.ByteSize + ( int )attribute * 4 );
        set => *( float* )( Data + RacialScalingStart + subRace.ToRspIndex() * RspEntry.ByteSize + ( int )attribute * 4 ) = value;
    }

    public override void Reset()
        => Functions.MemCpyUnchecked( Data, ( byte* )DefaultData.Data, DefaultData.Length );

    public void Reset( IEnumerable< (SubRace, RspAttribute) > entries )
    {
        foreach( var (r, a) in entries )
        {
            this[ r, a ] = GetDefault( r, a );
        }
    }

    public CmpFile()
        : base( CharacterUtility.HumanCmpIdx )
    {
        AllocateData( DefaultData.Length );
        Reset();
    }

    public static float GetDefault( SubRace subRace, RspAttribute attribute )
    {
        var data = ( byte* )Penumbra.CharacterUtility.DefaultResources[ CharacterUtility.HumanCmpIdx ].Address;
        return *( float* )( data + RacialScalingStart + subRace.ToRspIndex() * RspEntry.ByteSize + ( int )attribute * 4 );
    }
}