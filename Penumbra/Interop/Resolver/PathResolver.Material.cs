using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    // Materials do contain their own paths to textures and shader packages.
    // Those are loaded synchronously.
    // Thus, we need to ensure the correct files are loaded when a material is loaded.
    public class MaterialState : IDisposable
    {
        private readonly PathState _paths;

        private ResolveData _mtrlData = ResolveData.Invalid;

        public MaterialState( PathState paths )
        {
            SignatureHelper.Initialise( this );
            _paths = paths;
        }

        // Check specifically for shpk and tex files whether we are currently in a material load.
        public bool HandleSubFiles( ResourceType type, out ResolveData collection )
        {
            if( _mtrlData.Valid && type is ResourceType.Tex or ResourceType.Shpk )
            {
                collection = _mtrlData;
                return true;
            }

            collection = ResolveData.Invalid;
            return false;
        }

        // Materials need to be set per collection so they can load their textures independently from each other.
        public static void HandleCollection( ResolveData resolveData, string path, bool nonDefault, ResourceType type, FullPath? resolved,
            out (FullPath?, ResolveData) data )
        {
            if( nonDefault && type == ResourceType.Mtrl )
            {
                var fullPath = new FullPath( $"|{resolveData.ModCollection.Name}_{resolveData.ModCollection.ChangeCounter}|{path}" );
                data = ( fullPath, resolveData );
            }
            else
            {
                data = ( resolved, resolveData );
            }
        }

        public void Enable()
        {
            _loadMtrlShpkHook.Enable();
            _loadMtrlTexHook.Enable();
            Penumbra.ResourceLoader.ResourceLoadCustomization += MtrlLoadHandler;
        }

        public void Disable()
        {
            _loadMtrlShpkHook.Disable();
            _loadMtrlTexHook.Disable();
            Penumbra.ResourceLoader.ResourceLoadCustomization -= MtrlLoadHandler;
        }

        public void Dispose()
        {
            Disable();
            _loadMtrlShpkHook.Dispose();
            _loadMtrlTexHook.Dispose();
        }

        // We need to set the correct collection for the actual material path that is loaded
        // before actually loading the file.
        public bool MtrlLoadHandler( Utf8String split, Utf8String path, ResourceManager* resourceManager,
            SeFileDescriptor* fileDescriptor, int priority, bool isSync, out byte ret )
        {
            ret = 0;
            if( fileDescriptor->ResourceHandle->FileType != ResourceType.Mtrl )
            {
                return false;
            }

            var lastUnderscore = split.LastIndexOf( ( byte )'_' );
            var name           = lastUnderscore == -1 ? split.ToString() : split.Substring( 0, lastUnderscore ).ToString();
            if( Penumbra.TempMods.CollectionByName( name, out var collection )
            || Penumbra.CollectionManager.ByName( name, out collection ) )
            {
#if DEBUG
                PluginLog.Verbose( "Using MtrlLoadHandler with collection {$Split:l} for path {$Path:l}.", name, path );
#endif

                var objFromObjTable = Dalamud.Objects.FirstOrDefault( f => f.Name.TextValue == name );
                var gameObjAddr     = objFromObjTable?.Address ?? IntPtr.Zero;
                _paths.SetCollection( gameObjAddr, path, collection );
            }
            else
            {
#if DEBUG
                PluginLog.Verbose( "Using MtrlLoadHandler with no collection for path {$Path:l}.", path );
#endif
            }

            // Force isSync = true for this call. I don't really understand why,
            // or where the difference even comes from.
            // Was called with True on my client and with false on other peoples clients,
            // which caused problems.
            ret = Penumbra.ResourceLoader.DefaultLoadResource( path, resourceManager, fileDescriptor, priority, true );
            _paths.Consume( path, out _ );
            return true;
        }

        private delegate byte LoadMtrlFilesDelegate( IntPtr mtrlResourceHandle );

        [Signature( "4C 8B DC 49 89 5B ?? 49 89 73 ?? 55 57 41 55", DetourName = nameof( LoadMtrlTexDetour ) )]
        private readonly Hook< LoadMtrlFilesDelegate > _loadMtrlTexHook = null!;

        private byte LoadMtrlTexDetour( IntPtr mtrlResourceHandle )
        {
            LoadMtrlHelper( mtrlResourceHandle );
            var ret = _loadMtrlTexHook.Original( mtrlResourceHandle );
            _mtrlData = ResolveData.Invalid;
            return ret;
        }

        [Signature( "48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 0F B7 89",
            DetourName = nameof( LoadMtrlShpkDetour ) )]
        private readonly Hook< LoadMtrlFilesDelegate > _loadMtrlShpkHook = null!;

        private byte LoadMtrlShpkDetour( IntPtr mtrlResourceHandle )
        {
            LoadMtrlHelper( mtrlResourceHandle );
            var ret = _loadMtrlShpkHook.Original( mtrlResourceHandle );
            _mtrlData = ResolveData.Invalid;
            return ret;
        }

        private void LoadMtrlHelper( IntPtr mtrlResourceHandle )
        {
            if( mtrlResourceHandle == IntPtr.Zero )
            {
                return;
            }

            var mtrl     = ( MtrlResource* )mtrlResourceHandle;
            var mtrlPath = Utf8String.FromSpanUnsafe( mtrl->Handle.FileNameSpan(), true, null, true );
            _mtrlData = _paths.TryGetValue( mtrlPath, out var c ) ? c : ResolveData.Invalid;
        }
    }
}