using System;
using System.Collections.Generic;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.GameData.ByteString;
using Penumbra.Interop;
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

        public unsafe bool ApplyMod( ImcManipulation m, Mod.Mod mod )
        {
            const uint imcExt = 0x00696D63;
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

            var resource = ResourceLoader.FindResource( ResourceCategory.Chara, imcExt, ( uint )path.Path.Crc32 );
            if( resource != null )
            {
                file.Replace( ( ResourceHandle* )resource );
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
            Penumbra.ResourceLoader.ResourceLoadCustomization = ImcHandler;
        }

        [Conditional( "USE_IMC" )]
        private unsafe void RestoreDelegate()
        {
            if( Penumbra.ResourceLoader.ResourceLoadCustomization == ImcHandler )
            {
                Penumbra.ResourceLoader.ResourceLoadCustomization = _previousDelegate;
            }
        }

        private FullPath CreateImcPath( Utf8GamePath path )
            => new($"|{_collection.Name}|{path}");

        private static unsafe byte ImcHandler( Utf8GamePath gamePath, ResourceManager* resourceManager,
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
                file.Replace( fileDescriptor->ResourceHandle );
            }

            fileDescriptor->ResourceHandle->FileNameData   = gamePath.Path.Path;
            fileDescriptor->ResourceHandle->FileNameLength = gamePath.Path.Length;
            return ret;
        }
    }
}