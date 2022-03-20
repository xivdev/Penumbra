using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.GameData.ByteString;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    public readonly struct MetaManagerImc : IDisposable
    {
        public readonly Dictionary< Utf8GamePath, ImcFile >    Files         = new();
        public readonly Dictionary< ImcManipulation, Mod.Mod > Manipulations = new();

        private readonly ModCollection                                     _collection;
        private readonly ResourceLoader.ResourceLoadCustomizationDelegate? _previousDelegate;


        public MetaManagerImc( ModCollection collection )
        {
            _collection       = collection;
            _previousDelegate = Penumbra.ResourceLoader.ResourceLoadCustomization;
            SetupDelegate();
        }

        [Conditional( "USE_IMC" )]
        public void SetFiles()
        {
            if( _collection.Cache == null )
            {
                return;
            }

            foreach( var path in Files.Keys )
            {
                _collection.Cache.ResolvedFiles[ path ] = CreateImcPath( path );
            }
        }

        [Conditional( "USE_IMC" )]
        public void Reset()
        {
            foreach( var (path, file) in Files )
            {
                _collection.Cache?.ResolvedFiles.Remove( path );
                file.Reset();
            }

            Manipulations.Clear();
        }

        public bool ApplyMod( ImcManipulation m, Mod.Mod mod )
        {
#if USE_IMC
            if( !Manipulations.TryAdd( m, mod ) )
            {
                return false;
            }

            var path = m.GamePath();
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
            if( _collection.Cache != null )
            {
                _collection.Cache.ResolvedFiles[ path ] = fullPath;
            }

            return true;
#else
            return false;
#endif
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
        private unsafe void SetupDelegate()
        {
            Penumbra.ResourceLoader.ResourceLoadCustomization =  ImcLoadHandler;
            Penumbra.ResourceLoader.ResourceLoaded            += ImcResourceHandler;
        }

        [Conditional( "USE_IMC" )]
        private unsafe void RestoreDelegate()
        {
            if( Penumbra.ResourceLoader.ResourceLoadCustomization == ImcLoadHandler )
            {
                Penumbra.ResourceLoader.ResourceLoadCustomization = _previousDelegate;
            }

            Penumbra.ResourceLoader.ResourceLoaded -= ImcResourceHandler;
        }

        private FullPath CreateImcPath( Utf8GamePath path )
            => new($"|{_collection.Name}|{path}");

        private static unsafe byte ImcLoadHandler( Utf8GamePath gamePath, ResourceManager* resourceManager,
            SeFileDescriptor* fileDescriptor, int priority, bool isSync )
        {
            var split = gamePath.Path.Split( ( byte )'|', 2, true );
            fileDescriptor->ResourceHandle->FileNameData   = split[ 1 ].Path;
            fileDescriptor->ResourceHandle->FileNameLength = split[ 1 ].Length;

            var ret = Penumbra.ResourceLoader.ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
            if( Penumbra.ModManager.Collections.Collections.TryGetValue( split[ 0 ].ToString(), out var collection )
            && collection.Cache != null
            && collection.Cache.MetaManipulations.Imc.Files.TryGetValue(
                   Utf8GamePath.FromSpan( split[ 1 ].Span, out var p, false ) ? p : Utf8GamePath.Empty, out var file ) )
            {
                PluginLog.Debug( "Loaded {GamePath:l} from file and replaced with IMC from collection {Collection:l}.", gamePath,
                    collection.Name );
                file.Replace( fileDescriptor->ResourceHandle );
                file.ChangesSinceLoad = false;
            }

            fileDescriptor->ResourceHandle->FileNameData   = gamePath.Path.Path;
            fileDescriptor->ResourceHandle->FileNameLength = gamePath.Path.Length;
            return ret;
        }

        private static unsafe void ImcResourceHandler( ResourceHandle* resource, Utf8GamePath gamePath, FullPath? _2, object? resolveData )
        {
            // Only check imcs.
            if( resource->FileType != 0x00696D63
            || resolveData is not ModCollection collection
            || collection.Cache == null
            || !collection.Cache.MetaManipulations.Imc.Files.TryGetValue( gamePath, out var file )
            || !file.ChangesSinceLoad )
            {
                return;
            }

            PluginLog.Debug( "File {GamePath:l} was already loaded but IMC in collection {Collection:l} was changed, so reloaded.", gamePath,
                collection.Name );
            file.Replace( resource );
            file.ChangesSinceLoad = false;
        }
    }
}