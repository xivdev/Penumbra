using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    public readonly struct MetaManagerImc : IDisposable
    {
        public readonly Dictionary< Utf8GamePath, ImcFile > Files         = new();
        public readonly Dictionary< ImcManipulation, IMod >  Manipulations = new();

        private readonly ModCollection _collection;
        private static   int           _imcManagerCount;

        public MetaManagerImc( ModCollection collection )
        {
            _collection = collection;
            SetupDelegate();
        }

        [Conditional( "USE_IMC" )]
        public void SetFiles()
        {
            if( !_collection.HasCache )
            {
                return;
            }

            foreach( var path in Files.Keys )
            {
                _collection.ForceFile( path, CreateImcPath( path ) );
            }
        }

        [Conditional( "USE_IMC" )]
        public void Reset()
        {
            if( _collection.HasCache )
            {
                foreach( var (path, file) in Files )
                {
                    _collection.RemoveFile( path );
                    file.Reset();
                }
            }
            else
            {
                foreach( var (_, file) in Files )
                {
                    file.Reset();
                }
            }

            Manipulations.Clear();
        }

        public bool ApplyMod( ImcManipulation m, IMod mod )
        {
#if USE_IMC
            Manipulations[ m ] = mod;
            var path = m.GamePath();
            try
            {
                if( !Files.TryGetValue( path, out var file ) )
                {
                    file = new ImcFile( path );
                }

                if( !m.Apply( file ) )
                {
                    return false;
                }

                Files[ path ] = file;
                var fullPath = CreateImcPath( path );
                if( _collection.HasCache )
                {
                    _collection.ForceFile( path, fullPath );
                }

                return true;
            }
            catch( Exception e )
            {
                ++Penumbra.ImcExceptions;
                PluginLog.Error( $"Could not apply IMC Manipulation:\n{e}" );
                return false;
            }
#else
            return false;
#endif
        }

        public bool RevertMod( ImcManipulation m )
        {
#if USE_IMC
            if( Manipulations.Remove( m ) )
            {
                var path = m.GamePath();
                if( !Files.TryGetValue( path, out var file ) )
                {
                    return false;
                }

                var def   = ImcFile.GetDefault( path, m.EquipSlot, m.Variant, out _ );
                var manip = m with { Entry = def };
                if( !manip.Apply( file ) )
                {
                    return false;
                }

                var fullPath = CreateImcPath( path );
                if( _collection.HasCache )
                {
                    _collection.ForceFile( path, fullPath );
                }

                return true;
            }
#endif
            return false;
        }

        public void Dispose()
        {
            foreach( var file in Files.Values )
            {
                file.Dispose();
            }

            Files.Clear();
            Manipulations.Clear();
            RestoreDelegate();
        }

        [Conditional( "USE_IMC" )]
        private static unsafe void SetupDelegate()
        {
            if( _imcManagerCount++ == 0 )
            {
                Penumbra.ResourceLoader.ResourceLoadCustomization += ImcLoadHandler;
                Penumbra.ResourceLoader.ResourceLoaded            += ImcResourceHandler;
            }
        }

        [Conditional( "USE_IMC" )]
        private static unsafe void RestoreDelegate()
        {
            if( --_imcManagerCount == 0 )
            {
                Penumbra.ResourceLoader.ResourceLoadCustomization -= ImcLoadHandler;
                Penumbra.ResourceLoader.ResourceLoaded            -= ImcResourceHandler;
            }
        }

        private FullPath CreateImcPath( Utf8GamePath path )
            => new($"|{_collection.Name}_{_collection.RecomputeCounter}|{path}");

        private static unsafe bool ImcLoadHandler( Utf8String split, Utf8String path, ResourceManager* resourceManager,
            SeFileDescriptor* fileDescriptor, int priority, bool isSync, out byte ret )
        {
            ret = 0;
            if( fileDescriptor->ResourceHandle->FileType != ResourceType.Imc )
            {
                return false;
            }

            PluginLog.Verbose( "Using ImcLoadHandler for path {$Path:l}.", path );
            ret = Penumbra.ResourceLoader.ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );

            var lastUnderscore = split.LastIndexOf( ( byte )'_' );
            var name           = lastUnderscore == -1 ? split.ToString() : split.Substring( 0, lastUnderscore ).ToString();
            if( Penumbra.CollectionManager.ByName( name, out var collection )
            && collection.HasCache
            && collection.MetaCache!.Imc.Files.TryGetValue(
                   Utf8GamePath.FromSpan( path.Span, out var p ) ? p : Utf8GamePath.Empty, out var file ) )
            {
                PluginLog.Debug( "Loaded {GamePath:l} from file and replaced with IMC from collection {Collection:l}.", path,
                    collection.Name );
                file.Replace( fileDescriptor->ResourceHandle, true );
                file.ChangesSinceLoad = false;
            }

            return true;
        }

        private static unsafe void ImcResourceHandler( ResourceHandle* resource, Utf8GamePath gamePath, FullPath? _2, object? resolveData )
        {
            // Only check imcs.
            if( resource->FileType != ResourceType.Imc
            || resolveData is not ModCollection { HasCache: true } collection
            || !collection.MetaCache!.Imc.Files.TryGetValue( gamePath, out var file )
            || !file.ChangesSinceLoad )
            {
                return;
            }

            PluginLog.Debug( "File {GamePath:l} was already loaded but IMC in collection {Collection:l} was changed, so reloaded.", gamePath,
                collection.Name );
            file.Replace( resource, false );
            file.ChangesSinceLoad = false;
        }
    }
}