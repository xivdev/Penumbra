using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Lumina.Data;
using Lumina.Data.Files;
using Penumbra.Game;
using Penumbra.Game.Enums;
using Penumbra.Util;

namespace Penumbra.Meta.Files
{
    // This class manages the default meta files obtained via lumina from the game files themselves.
    // On first call, the default version of any supported file will be cached and can be returned without reparsing.
    public class MetaDefaults
    {
        private readonly DalamudPluginInterface _pi;

        private readonly Dictionary< GamePath, object > _defaultFiles = new();

        private object CreateNewFile( string path )
        {
            if( path.EndsWith( ".imc" ) )
            {
                return GetImcFile( path );
            }

            var rawFile = FetchFile( path );
            if( path.EndsWith( ".eqp" ) )
            {
                return new EqpFile( rawFile );
            }

            if( path.EndsWith( ".gmp" ) )
            {
                return new GmpFile( rawFile );
            }

            if( path.EndsWith( ".eqdp" ) )
            {
                return new EqdpFile( rawFile );
            }

            if( path.EndsWith( ".est" ) )
            {
                return new EstFile( rawFile );
            }

            if( path.EndsWith( ".cmp" ) )
            {
                return new CmpFile( rawFile.Data );
            }

            throw new NotImplementedException();
        }

        private T? GetDefaultFile< T >( GamePath path, string error = "" ) where T : class
        {
            try
            {
                if( _defaultFiles.TryGetValue( path, out var file ) )
                {
                    return ( T )file;
                }

                var newFile = CreateNewFile( path );
                _defaultFiles.Add( path, newFile );
                return ( T )_defaultFiles[ path ];
            }
            catch( Exception e )
            {
                PluginLog.Error( $"{error}{e}" );
                return null;
            }
        }

        private EqdpFile? GetDefaultEqdpFile( EquipSlot slot, GenderRace gr )
            => GetDefaultFile< EqdpFile >( MetaFileNames.Eqdp( slot, gr ),
                $"Could not obtain Eqdp file for {slot} {gr}:\n" );

        private GmpFile? GetDefaultGmpFile()
            => GetDefaultFile< GmpFile >( MetaFileNames.Gmp(), "Could not obtain Gmp file:\n" );

        private EqpFile? GetDefaultEqpFile()
            => GetDefaultFile< EqpFile >( MetaFileNames.Eqp(), "Could not obtain Eqp file:\n" );

        private EstFile? GetDefaultEstFile( ObjectType type, EquipSlot equip, BodySlot body )
            => GetDefaultFile< EstFile >( MetaFileNames.Est( type, equip, body ), $"Could not obtain Est file for {type} {equip} {body}:\n" );

        private ImcFile? GetDefaultImcFile( ObjectType type, ushort primarySetId, ushort secondarySetId = 0 )
            => GetDefaultFile< ImcFile >( MetaFileNames.Imc( type, primarySetId, secondarySetId ),
                $"Could not obtain Imc file for {type}, {primarySetId} {secondarySetId}:\n" );

        private CmpFile? GetDefaultCmpFile()
            => GetDefaultFile< CmpFile >( MetaFileNames.Cmp(), "Could not obtain Cmp file:\n" );

        public EqdpFile? GetNewEqdpFile( EquipSlot slot, GenderRace gr )
            => GetDefaultEqdpFile( slot, gr )?.Clone();

        public GmpFile? GetNewGmpFile()
            => GetDefaultGmpFile()?.Clone();

        public EqpFile? GetNewEqpFile()
            => GetDefaultEqpFile()?.Clone();

        public EstFile? GetNewEstFile( ObjectType type, EquipSlot equip, BodySlot body )
            => GetDefaultEstFile( type, equip, body )?.Clone();

        public ImcFile? GetNewImcFile( ObjectType type, ushort primarySetId, ushort secondarySetId = 0 )
            => GetDefaultImcFile( type, primarySetId, secondarySetId )?.Clone();

        public CmpFile? GetNewCmpFile()
            => GetDefaultCmpFile()?.Clone();

        public MetaDefaults( DalamudPluginInterface pi )
            => _pi = pi;

        private ImcFile GetImcFile( string path )
            => _pi.Data.GetFile< ImcFile >( path );

        private FileResource FetchFile( string name )
            => _pi.Data.GetFile( name );

        // Check that a given meta manipulation is an actual change to the default value. We don't need to keep changes to default.
        public bool CheckAgainstDefault( MetaManipulation m )
        {
            return m.Type switch
            {
                MetaType.Imc => GetDefaultImcFile( m.ImcIdentifier.ObjectType, m.ImcIdentifier.PrimaryId, m.ImcIdentifier.SecondaryId )
                      ?.GetValue( m ).Equal( m.ImcValue )
                 ?? true,
                MetaType.Gmp => GetDefaultGmpFile()?.GetEntry( m.GmpIdentifier.SetId )
                 == m.GmpValue,
                MetaType.Eqp => GetDefaultEqpFile()?.GetEntry( m.EqpIdentifier.SetId )
                       .Reduce( m.EqpIdentifier.Slot )
                 == m.EqpValue,
                MetaType.Eqdp => GetDefaultEqdpFile( m.EqdpIdentifier.Slot, m.EqdpIdentifier.GenderRace )?.GetEntry( m.EqdpIdentifier.SetId )
                       .Reduce( m.EqdpIdentifier.Slot )
                 == m.EqdpValue,
                MetaType.Est => GetDefaultEstFile( m.EstIdentifier.ObjectType, m.EstIdentifier.EquipSlot, m.EstIdentifier.BodySlot )
                      ?.GetEntry( m.EstIdentifier.GenderRace, m.EstIdentifier.PrimaryId )
                 == m.EstValue,
                MetaType.Rsp => GetDefaultCmpFile()?[ m.RspIdentifier.SubRace ][ m.RspIdentifier.Attribute ]
                 == m.RspValue,
                _ => throw new NotImplementedException(),
            };
        }

        // Create a deep copy of a default file as a new file.
        public object? CreateNewFile( MetaManipulation m )
        {
            return m.Type switch
            {
                MetaType.Imc  => GetNewImcFile( m.ImcIdentifier.ObjectType, m.ImcIdentifier.PrimaryId, m.ImcIdentifier.SecondaryId ),
                MetaType.Gmp  => GetNewGmpFile(),
                MetaType.Eqp  => GetNewEqpFile(),
                MetaType.Eqdp => GetNewEqdpFile( m.EqdpIdentifier.Slot, m.EqdpIdentifier.GenderRace ),
                MetaType.Est  => GetNewEstFile( m.EstIdentifier.ObjectType, m.EstIdentifier.EquipSlot, m.EstIdentifier.BodySlot ),
                MetaType.Rsp  => GetNewCmpFile(),
                _             => throw new NotImplementedException(),
            };
        }
    }
}