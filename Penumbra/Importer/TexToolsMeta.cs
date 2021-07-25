using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Lumina.Data.Files;
using Penumbra.Game;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Util;

namespace Penumbra.Importer
{
    // TexTools provices custom generated *.meta files for its modpacks, that contain changes to
    //     - imc files
    //     - eqp files
    //     - gmp files
    //     - est files
    //     - eqdp files
    // made by the mod. The filename determines to what the changes are applied, and the binary file itself contains changes.
    // We parse every *.meta file in a mod and combine all actual changes that do not keep data on default values and that can be applied to the game in a .json.
    // TexTools may also generate files that contain non-existing changes, e.g. *.imc files for weapon offhands, which will be ignored.
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
            private static readonly Regex HousingMeta = new( $"bgcommon/hou/{Pt}/general/{Pi}/{Pir}{Ext}" );
            private static readonly Regex CharaMeta   = new( $"chara/{Pt}/{Pp}{Pi}(/obj/{St}/{Sp}{Si})?/{File}{Slot}{Ext}" );

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
                PrimaryType   = GamePathParser.PathToObjectType( fileName );
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
                            if( GameData.Enums.GameData.SuffixToEquipSlot.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpSlot ) )
                            {
                                EquipSlot = tmpSlot;
                            }

                            break;
                        case ObjectType.Character:
                            if( GameData.Enums.GameData.SuffixToCustomizationType.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpCustom ) )
                            {
                                CustomizationType = tmpCustom;
                            }

                            break;
                    }
                }

                if( match.Groups[ "SecondaryType" ].Success
                 && GameData.Enums.GameData.StringToBodySlot.TryGetValue( match.Groups[ "SecondaryType" ].Value, out SecondaryType ) )
                {
                    SecondaryId = ushort.Parse( match.Groups[ "SecondaryId" ].Value );
                }
            }
        }

        public readonly uint                     Version;
        public readonly string                   FilePath;
        public readonly List< MetaManipulation > Manipulations = new();

        private static string ReadNullTerminated( BinaryReader reader )
        {
            var builder = new System.Text.StringBuilder();
            for( var c = reader.ReadChar(); c != 0; c = reader.ReadChar() )
            {
                builder.Append( c );
            }

            return builder.ToString();
        }

        private void AddIfNotDefault( MetaManipulation manipulation )
        {
            try
            {
                if( !Service< MetaDefaults >.Get().CheckAgainstDefault( manipulation ) )
                {
                    Manipulations.Add( manipulation );
                }
            }
            catch( Exception e )
            {
                PluginLog.Debug( "Skipped {Type}-manipulation:\n{e:l}", manipulation.Type, e );
            }
        }

        private void DeserializeEqpEntry( Info info, byte[]? data )
        {
            if( data == null || !info.EquipSlot.IsEquipment() )
            {
                return;
            }

            try
            {
                var value = Eqp.FromSlotAndBytes( info.EquipSlot, data );

                AddIfNotDefault( MetaManipulation.Eqp( info.EquipSlot, info.PrimaryId, value ) );
            }
            catch( ArgumentException )
            { }
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
                AddIfNotDefault( MetaManipulation.Eqdp( info.EquipSlot, gr, info.PrimaryId, value ) );
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
            AddIfNotDefault( MetaManipulation.Gmp( info.PrimaryId, value ) );
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
                if( !gr.IsValid()
                 || info.PrimaryType == ObjectType.Character && info.SecondaryType != BodySlot.Face  && info.SecondaryType != BodySlot.Hair
                 || info.PrimaryType == ObjectType.Equipment && info.EquipSlot     != EquipSlot.Head && info.EquipSlot     != EquipSlot.Body )
                {
                    continue;
                }

                AddIfNotDefault( MetaManipulation.Est( info.PrimaryType, info.EquipSlot, gr, info.SecondaryType, id, value ) );
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
            for( var i = 0; i < num; ++i )
            {
                var value = ImcFile.ImageChangeData.Read( reader );
                if( info.PrimaryType == ObjectType.Equipment || info.PrimaryType == ObjectType.Accessory )
                {
                    AddIfNotDefault( MetaManipulation.Imc( info.EquipSlot, info.PrimaryId, ( ushort )i, value ) );
                }
                else
                {
                    AddIfNotDefault( MetaManipulation.Imc( info.PrimaryType, info.SecondaryType, info.PrimaryId
                        , info.SecondaryId, ( ushort )i, value ) );
                }
            }
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

                List< (MetaType type, uint offset, int size) > entries = new();
                for( var i = 0; i < numHeaders; ++i )
                {
                    var currentOffset = reader.BaseStream.Position;
                    var type          = ( MetaType )reader.ReadUInt32();
                    var offset        = reader.ReadUInt32();
                    var size          = reader.ReadInt32();
                    entries.Add( ( type, offset, size ) );
                    reader.BaseStream.Seek( currentOffset + headerSize, SeekOrigin.Begin );
                }

                byte[]? ReadEntry( MetaType type )
                {
                    var idx = entries.FindIndex( t => t.type == type );
                    if( idx < 0 )
                    {
                        return null;
                    }

                    reader.BaseStream.Seek( entries[ idx ].offset, SeekOrigin.Begin );
                    return reader.ReadBytes( entries[ idx ].size );
                }

                DeserializeEqpEntry( metaInfo, ReadEntry( MetaType.Eqp ) );
                DeserializeGmpEntry( metaInfo, ReadEntry( MetaType.Gmp ) );
                DeserializeEqdpEntries( metaInfo, ReadEntry( MetaType.Eqdp ) );
                DeserializeEstEntries( metaInfo, ReadEntry( MetaType.Est ) );
                DeserializeImcEntries( metaInfo, ReadEntry( MetaType.Imc ) );
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

        public static TexToolsMeta Invalid = new( string.Empty, 0 );

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

            if( gender == 1 )
            {
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.FemaleMinSize, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.FemaleMaxSize, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.FemaleMinTail, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.FemaleMaxTail, br.ReadSingle() ) );

                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.BustMinX, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.BustMinY, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.BustMinZ, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.BustMaxX, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.BustMaxY, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.BustMaxZ, br.ReadSingle() ) );
            }
            else
            {
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.MaleMinSize, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.MaleMaxSize, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.MaleMinTail, br.ReadSingle() ) );
                ret.AddIfNotDefault( MetaManipulation.Rsp( subRace, RspAttribute.MaleMaxTail, br.ReadSingle() ) );
            }

            return ret;
        }
    }
}