using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Lumina.Data.Files;
using Penumbra.Game;
using Penumbra.MetaData;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Importer
{
    public class TexToolsMeta
    {
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
                            if( GameData.SuffixToEquipSlot.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpSlot ) )
                            {
                                EquipSlot = tmpSlot;
                            }

                            break;
                        case ObjectType.Character:
                            if( GameData.SuffixToCustomizationType.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpCustom ) )
                            {
                                CustomizationType = tmpCustom;
                            }

                            break;
                    }
                }

                if( match.Groups[ "SecondaryType" ].Success
                 && GameData.StringToBodySlot.TryGetValue( match.Groups[ "SecondaryType" ].Value, out SecondaryType ) )
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
    }
}