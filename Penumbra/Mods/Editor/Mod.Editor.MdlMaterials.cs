using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Logging;
using OtterGui;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor
    {
        private static readonly Regex MaterialRegex = new(@"/mt_c(?'RaceCode'\d{4})b0001_(?'Suffix'.*?)\.mtrl", RegexOptions.Compiled);
        private readonly List< MaterialInfo > _modelFiles = new();
        public IReadOnlyList< MaterialInfo > ModelFiles
            => _modelFiles;

        // Non-ASCII encoding can not be used.
        public static bool ValidString( string to )
            => to.Length                         != 0
             && to.Length                        < 16
             && Encoding.UTF8.GetByteCount( to ) == to.Length;

        public void SaveAllModels()
        {
            foreach( var info in _modelFiles )
            {
                info.Save();
            }
        }

        public void RestoreAllModels()
        {
            foreach( var info in _modelFiles )
            {
                info.Restore();
            }
        }

        // Go through the currently loaded files and replace all appropriate suffices.
        // Does nothing if toSuffix is invalid.
        // If raceCode is Unknown, apply to all raceCodes.
        // If fromSuffix is empty, apply to all suffices.
        public void ReplaceAllMaterials( string toSuffix, string fromSuffix = "", GenderRace raceCode = GenderRace.Unknown )
        {
            if( !ValidString( toSuffix ) )
                return;

            foreach( var info in _modelFiles )
            {
                for( var i = 0; i < info.Count; ++i )
                {
                    var (_, def) = info[ i ];
                    var match = MaterialRegex.Match( def );
                    if( match.Success
                    && ( raceCode          == GenderRace.Unknown || raceCode.ToRaceCode() == match.Groups[ "RaceCode" ].Value )
                    && ( fromSuffix.Length == 0                  || fromSuffix            == match.Groups[ "Suffix" ].Value ) )
                    {
                        info.SetMaterial( $"/mt_c{match.Groups["RaceCode"].Value}b0001_{toSuffix}.mtrl", i );
                    }
                }
            }
        }

        // Find all model files in the mod that contain skin materials.
        private void ScanModels()
        {
            _modelFiles.Clear();
            foreach( var file in AvailableFiles.Where( f => f.File.Extension == ".mdl" ) )
            {
                try
                {
                    var bytes   = File.ReadAllBytes( file.File.FullName );
                    var mdlFile = new MdlFile( bytes );
                    var materials = mdlFile.Materials.WithIndex().Where( p => MaterialRegex.IsMatch( p.Item1 ) )
                       .Select( p => p.Item2 ).ToArray();
                    if( materials.Length > 0 )
                    {
                        _modelFiles.Add( new MaterialInfo( file.File, mdlFile, materials ) );
                    }
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Unexpected error scanning {_mod.Name}'s {file.File.FullName} for materials:\n{e}" );
                }
            }
        }

        // A class that collects information about skin materials in a model file and handle changes on them.
        public class MaterialInfo
        {
            public readonly FullPath Path;
            private readonly MdlFile _file;
            private readonly string[] _currentMaterials;
            private readonly IReadOnlyList<int> _materialIndices;
            public bool Changed { get; private set; }

            public IReadOnlyList<string> CurrentMaterials
                => _currentMaterials;

            private IEnumerable<string> DefaultMaterials
                => _materialIndices.Select( i => _file.Materials[i] );

            public (string Current, string Default) this[int idx]
                => (_currentMaterials[idx], _file.Materials[_materialIndices[idx]]);

            public int Count
                => _materialIndices.Count;

            // Set the skin material to a new value and flag changes appropriately.
            public void SetMaterial( string value, int materialIdx )
            {
                var mat = _file.Materials[_materialIndices[materialIdx]];
                _currentMaterials[materialIdx] = value;
                if( mat != value )
                {
                    Changed = true;
                }
                else
                {
                    Changed = !_currentMaterials.SequenceEqual( DefaultMaterials );
                }
            }

            // Save a changed .mdl file.
            public void Save()
            {
                if( !Changed )
                {
                    return;
                }

                foreach( var (idx, i) in _materialIndices.WithIndex() )
                {
                    _file.Materials[idx] = _currentMaterials[i];
                }

                try
                {
                    File.WriteAllBytes( Path.FullName, _file.Write() );
                    Changed = false;
                }
                catch( Exception e )
                {
                    Restore();
                    PluginLog.Error( $"Could not write manipulated .mdl file {Path.FullName}:\n{e}" );
                }
            }

            // Revert all current changes.
            public void Restore()
            {
                if( !Changed )
                    return;

                foreach( var (idx, i) in _materialIndices.WithIndex() )
                {
                    _currentMaterials[i] = _file.Materials[idx];
                }
                Changed = false;
            }

            public MaterialInfo( FullPath path, MdlFile file, IReadOnlyList<int> indices )
            {
                Path = path;
                _file = file;
                _materialIndices = indices;
                _currentMaterials = DefaultMaterials.ToArray();
            }
        }

    }
}