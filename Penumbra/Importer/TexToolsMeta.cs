using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Logging;
using Lumina.Data.Files;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Util;
using ImcFile = Penumbra.Meta.Files.ImcFile;

namespace Penumbra.Importer;

// TexTools provices custom generated *.meta files for its modpacks, that contain changes to
//     - imc files
//     - eqp files
//     - gmp files
//     - est files
//     - eqdp files
// made by the mod. The filename determines to what the changes are applied, and the binary file itself contains changes.
// We parse every *.meta file in a mod and combine all actual changes that do not keep data on default values and that can be applied to the game in a .json.
// TexTools may also generate files that contain non-existing changes, e.g. *.imc files for weapon offhands, which will be ignored.
// TexTools also provides .rgsp files, that contain changes to the racial scaling parameters in the human.cmp file.
public class TexToolsMeta
{
    // The info class determines the files or table locations the changes need to apply to from the filename.
    public class Info
    {
        private const string Pt   = @"(?'PrimaryType'[a-z]*)";                                              // language=regex
        private const string Pp   = @"(?'PrimaryPrefix'[a-z])";                                             // language=regex
        private const string Pi   = @"(?'PrimaryId'\d{4})";                                                 // language=regex
        private const string Pir  = @"\k'PrimaryId'";                                                       // language=regex
        private const string St   = @"(?'SecondaryType'[a-z]*)";                                            // language=regex
        private const string Sp   = @"(?'SecondaryPrefix'[a-z])";                                           // language=regex
        private const string Si   = @"(?'SecondaryId'\d{4})";                                               // language=regex
        private const string File = @"\k'PrimaryPrefix'\k'PrimaryId'(\k'SecondaryPrefix'\k'SecondaryId')?"; // language=regex
        private const string Slot = @"(_(?'Slot'[a-z]{3}))?";                                               // language=regex
        private const string Ext  = @"\.meta";

        // These are the valid regexes for .meta files that we are able to support at the moment.
        private static readonly Regex HousingMeta = new($"bgcommon/hou/{Pt}/general/{Pi}/{Pir}{Ext}", RegexOptions.Compiled);
        private static readonly Regex CharaMeta   = new($"chara/{Pt}/{Pp}{Pi}(/obj/{St}/{Sp}{Si})?/{File}{Slot}{Ext}", RegexOptions.Compiled);

        public readonly ObjectType        PrimaryType;
        public readonly BodySlot          SecondaryType;
        public readonly ushort            PrimaryId;
        public readonly ushort            SecondaryId;
        public readonly EquipSlot         EquipSlot         = EquipSlot.Unknown;
        public readonly CustomizationType CustomizationType = CustomizationType.Unknown;

        private static bool ValidType( ObjectType type )
        {
            return type switch
            {
                ObjectType.Accessory     => true,
                ObjectType.Character     => true,
                ObjectType.Equipment     => true,
                ObjectType.DemiHuman     => true,
                ObjectType.Housing       => true,
                ObjectType.Monster       => true,
                ObjectType.Weapon        => true,
                ObjectType.Icon          => false,
                ObjectType.Font          => false,
                ObjectType.Interface     => false,
                ObjectType.LoadingScreen => false,
                ObjectType.Map           => false,
                ObjectType.Vfx           => false,
                ObjectType.Unknown       => false,
                ObjectType.World         => false,
                _                        => false,
            };
        }

        public Info( string fileName )
            : this( new GamePath( fileName ) )
        { }

        public Info( GamePath fileName )
        {
            PrimaryType   = GameData.GameData.GetGamePathParser().PathToObjectType( fileName );
            PrimaryId     = 0;
            SecondaryType = BodySlot.Unknown;
            SecondaryId   = 0;
            if( !ValidType( PrimaryType ) )
            {
                PrimaryType = ObjectType.Unknown;
                return;
            }

            if( PrimaryType == ObjectType.Housing )
            {
                var housingMatch = HousingMeta.Match( fileName );
                if( housingMatch.Success )
                {
                    PrimaryId = ushort.Parse( housingMatch.Groups[ "PrimaryId" ].Value );
                }

                return;
            }

            var match = CharaMeta.Match( fileName );
            if( !match.Success )
            {
                return;
            }

            PrimaryId = ushort.Parse( match.Groups[ "PrimaryId" ].Value );
            if( match.Groups[ "Slot" ].Success )
            {
                switch( PrimaryType )
                {
                    case ObjectType.Equipment:
                    case ObjectType.Accessory:
                        if( Names.SuffixToEquipSlot.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpSlot ) )
                        {
                            EquipSlot = tmpSlot;
                        }

                        break;
                    case ObjectType.Character:
                        if( Names.SuffixToCustomizationType.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpCustom ) )
                        {
                            CustomizationType = tmpCustom;
                        }

                        break;
                }
            }

            if( match.Groups[ "SecondaryType" ].Success
            && Names.StringToBodySlot.TryGetValue( match.Groups[ "SecondaryType" ].Value, out SecondaryType ) )
            {
                SecondaryId = ushort.Parse( match.Groups[ "SecondaryId" ].Value );
            }
        }
    }

    public readonly uint                     Version;
    public readonly string                   FilePath;
    public readonly List< EqpManipulation >  EqpManipulations  = new();
    public readonly List< GmpManipulation >  GmpManipulations  = new();
    public readonly List< EqdpManipulation > EqdpManipulations = new();
    public readonly List< EstManipulation >  EstManipulations  = new();
    public readonly List< RspManipulation >  RspManipulations  = new();
    public readonly List< ImcManipulation >  ImcManipulations  = new();

    private void DeserializeEqpEntry( Info info, byte[]? data )
    {
        if( data == null || !info.EquipSlot.IsEquipment() )
        {
            return;
        }

        var value = Eqp.FromSlotAndBytes( info.EquipSlot, data );
        var def   = new EqpManipulation( ExpandedEqpFile.GetDefault( info.PrimaryId ), info.EquipSlot, info.PrimaryId );
        var manip = new EqpManipulation( value, info.EquipSlot, info.PrimaryId );
        if( def.Entry != manip.Entry )
        {
            EqpManipulations.Add( manip );
        }
    }

    private void DeserializeEqdpEntries( Info info, byte[]? data )
    {
        if( data == null )
        {
            return;
        }

        var       num    = data.Length / 5;
        using var reader = new BinaryReader( new MemoryStream( data ) );
        for( var i = 0; i < num; ++i )
        {
            var gr        = ( GenderRace )reader.ReadUInt32();
            var byteValue = reader.ReadByte();
            if( !gr.IsValid() || !info.EquipSlot.IsEquipment() && !info.EquipSlot.IsAccessory() )
            {
                continue;
            }

            var value = Eqdp.FromSlotAndBits( info.EquipSlot, ( byteValue & 1 ) == 1, ( byteValue & 2 ) == 2 );
            var def = new EqdpManipulation( ExpandedEqdpFile.GetDefault( gr, info.EquipSlot.IsAccessory(), info.PrimaryId ), info.EquipSlot,
                gr.Split().Item1, gr.Split().Item2, info.PrimaryId );
            var manip = new EqdpManipulation( value, info.EquipSlot, gr.Split().Item1, gr.Split().Item2, info.PrimaryId );
            if( def.Entry != manip.Entry )
            {
                EqdpManipulations.Add( manip );
            }
        }
    }

    private void DeserializeGmpEntry( Info info, byte[]? data )
    {
        if( data == null )
        {
            return;
        }

        using var reader = new BinaryReader( new MemoryStream( data ) );
        var       value  = ( GmpEntry )reader.ReadUInt32();
        value.UnknownTotal = reader.ReadByte();
        var def = ExpandedGmpFile.GetDefault( info.PrimaryId );
        if( value != def )
        {
            GmpManipulations.Add( new GmpManipulation( value, info.PrimaryId ) );
        }
    }

    private void DeserializeEstEntries( Info info, byte[]? data )
    {
        if( data == null )
        {
            return;
        }

        var       num    = data.Length / 6;
        using var reader = new BinaryReader( new MemoryStream( data ) );
        for( var i = 0; i < num; ++i )
        {
            var gr    = ( GenderRace )reader.ReadUInt16();
            var id    = reader.ReadUInt16();
            var value = reader.ReadUInt16();
            var type = ( info.SecondaryType, info.EquipSlot ) switch
            {
                (BodySlot.Face, _)  => EstManipulation.EstType.Face,
                (BodySlot.Hair, _)  => EstManipulation.EstType.Hair,
                (_, EquipSlot.Head) => EstManipulation.EstType.Head,
                (_, EquipSlot.Body) => EstManipulation.EstType.Body,
                _                   => ( EstManipulation.EstType )0,
            };
            if( !gr.IsValid() || type == 0 )
            {
                continue;
            }

            var def = EstFile.GetDefault( type, gr, id );
            if( def != value )
            {
                EstManipulations.Add( new EstManipulation( gr.Split().Item1, gr.Split().Item2, type, id, value ) );
            }
        }
    }

    private void DeserializeImcEntries( Info info, byte[]? data )
    {
        if( data == null )
        {
            return;
        }

        var       num    = data.Length / 6;
        using var reader = new BinaryReader( new MemoryStream( data ) );
        var       values = reader.ReadStructures< ImcEntry >( num );
        ushort    i      = 0;
        try
        {
            if( info.PrimaryType is ObjectType.Equipment or ObjectType.Accessory )
            {
                var def     = new ImcFile( new ImcManipulation( info.EquipSlot, i, info.PrimaryId, new ImcEntry() ).GamePath() );
                var partIdx = ImcFile.PartIndex( info.EquipSlot );
                foreach( var value in values )
                {
                    if( !value.Equals( def.GetEntry( partIdx, i ) ) )
                    {
                        ImcManipulations.Add( new ImcManipulation( info.EquipSlot, i, info.PrimaryId, value ) );
                    }

                    ++i;
                }
            }
            else
            {
                var def = new ImcFile( new ImcManipulation( info.PrimaryType, info.SecondaryType, info.PrimaryId, info.SecondaryId, i,
                    new ImcEntry() ).GamePath() );
                foreach( var value in values.Where( v => true || !v.Equals( def.GetEntry( 0, i ) ) ) )
                {
                    ImcManipulations.Add( new ImcManipulation( info.PrimaryType, info.SecondaryType, info.PrimaryId, info.SecondaryId, i,
                        value ) );
                    ++i;
                }
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( "Could not compute IMC manipulation. This is in all likelihood due to TexTools corrupting your index files.\n"
              + $"If the following error looks like Lumina is having trouble to read an IMC file, please do a do-over in TexTools:\n{e}" );
        }
    }

    private static string ReadNullTerminated( BinaryReader reader )
    {
        var builder = new System.Text.StringBuilder();
        for( var c = reader.ReadChar(); c != 0; c = reader.ReadChar() )
        {
            builder.Append( c );
        }

        return builder.ToString();
    }

    public TexToolsMeta( byte[] data )
    {
        try
        {
            using var reader = new BinaryReader( new MemoryStream( data ) );
            Version  = reader.ReadUInt32();
            FilePath = ReadNullTerminated( reader );
            var metaInfo    = new Info( FilePath );
            var numHeaders  = reader.ReadUInt32();
            var headerSize  = reader.ReadUInt32();
            var headerStart = reader.ReadUInt32();
            reader.BaseStream.Seek( headerStart, SeekOrigin.Begin );

            List< (MetaManipulation.Type type, uint offset, int size) > entries = new();
            for( var i = 0; i < numHeaders; ++i )
            {
                var currentOffset = reader.BaseStream.Position;
                var type          = ( MetaManipulation.Type )reader.ReadUInt32();
                var offset        = reader.ReadUInt32();
                var size          = reader.ReadInt32();
                entries.Add( ( type, offset, size ) );
                reader.BaseStream.Seek( currentOffset + headerSize, SeekOrigin.Begin );
            }

            byte[]? ReadEntry( MetaManipulation.Type type )
            {
                var idx = entries.FindIndex( t => t.type == type );
                if( idx < 0 )
                {
                    return null;
                }

                reader.BaseStream.Seek( entries[ idx ].offset, SeekOrigin.Begin );
                return reader.ReadBytes( entries[ idx ].size );
            }

            DeserializeEqpEntry( metaInfo, ReadEntry( MetaManipulation.Type.Eqp ) );
            DeserializeGmpEntry( metaInfo, ReadEntry( MetaManipulation.Type.Gmp ) );
            DeserializeEqdpEntries( metaInfo, ReadEntry( MetaManipulation.Type.Eqdp ) );
            DeserializeEstEntries( metaInfo, ReadEntry( MetaManipulation.Type.Est ) );
            DeserializeImcEntries( metaInfo, ReadEntry( MetaManipulation.Type.Imc ) );
        }
        catch( Exception e )
        {
            FilePath = "";
            PluginLog.Error( $"Error while parsing .meta file:\n{e}" );
        }
    }

    private TexToolsMeta( string filePath, uint version )
    {
        FilePath = filePath;
        Version  = version;
    }

    public static TexToolsMeta Invalid = new(string.Empty, 0);

    public static TexToolsMeta FromRgspFile( string filePath, byte[] data )
    {
        if( data.Length != 45 && data.Length != 42 )
        {
            PluginLog.Error( "Error while parsing .rgsp file:\n\tInvalid number of bytes." );
            return Invalid;
        }

        using var s       = new MemoryStream( data );
        using var br      = new BinaryReader( s );
        var       flag    = br.ReadByte();
        var       version = flag != 255 ? ( uint )1 : br.ReadUInt16();

        var ret = new TexToolsMeta( filePath, version );

        var subRace = ( SubRace )( version == 1 ? flag + 1 : br.ReadByte() + 1 );
        if( !Enum.IsDefined( typeof( SubRace ), subRace ) || subRace == SubRace.Unknown )
        {
            PluginLog.Error( $"Error while parsing .rgsp file:\n\t{subRace} is not a valid SubRace." );
            return Invalid;
        }

        var gender = br.ReadByte();
        if( gender != 1 && gender != 0 )
        {
            PluginLog.Error( $"Error while parsing .rgsp file:\n\t{gender} is neither Male nor Female." );
            return Invalid;
        }

        void Add( RspAttribute attribute, float value )
        {
            var def = CmpFile.GetDefault( subRace, attribute );
            if( value != def )
            {
                ret!.RspManipulations.Add( new RspManipulation( subRace, attribute, value ) );
            }
        }

        if( gender == 1 )
        {
            Add( RspAttribute.FemaleMinSize, br.ReadSingle() );
            Add( RspAttribute.FemaleMaxSize, br.ReadSingle() );
            Add( RspAttribute.FemaleMinTail, br.ReadSingle() );
            Add( RspAttribute.FemaleMaxTail, br.ReadSingle() );

            Add( RspAttribute.BustMinX, br.ReadSingle() );
            Add( RspAttribute.BustMinY, br.ReadSingle() );
            Add( RspAttribute.BustMinZ, br.ReadSingle() );
            Add( RspAttribute.BustMaxX, br.ReadSingle() );
            Add( RspAttribute.BustMaxY, br.ReadSingle() );
            Add( RspAttribute.BustMaxZ, br.ReadSingle() );
        }
        else
        {
            Add( RspAttribute.MaleMinSize, br.ReadSingle() );
            Add( RspAttribute.MaleMaxSize, br.ReadSingle() );
            Add( RspAttribute.MaleMinTail, br.ReadSingle() );
            Add( RspAttribute.MaleMaxTail, br.ReadSingle() );
        }

        return ret;
    }
}