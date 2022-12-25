using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    // Materials and avfx do contain their own paths to textures and shader packages or atex respectively.
    // Those are loaded synchronously.
    // Thus, we need to ensure the correct files are loaded when a material is loaded.
    public class SubfileHelper : IDisposable
    {
        private readonly PathState _paths;

        private ResolveData _mtrlData = ResolveData.Invalid;
        private ResolveData _avfxData = ResolveData.Invalid;

        public SubfileHelper( PathState paths )
        {
            SignatureHelper.Initialise( this );
            _paths = paths;
        }

        // Check specifically for shpk and tex files whether we are currently in a material load.
        public bool HandleSubFiles( ResourceType type, out ResolveData collection )
        {
            switch( type )
            {
                case ResourceType.Tex:
                case ResourceType.Shpk:
                    if( _mtrlData.Valid )
                    {
                        collection = _mtrlData;
                        return true;
                    }

                    break;
                case ResourceType.Atex when _avfxData.Valid:
                    collection = _avfxData;
                    return true;
            }

            collection = ResolveData.Invalid;
            return false;
        }

        // Materials need to be set per collection so they can load their textures independently from each other.
        public static void HandleCollection( ResolveData resolveData, string path, bool nonDefault, ResourceType type, FullPath? resolved,
            out (FullPath?, ResolveData) data )
        {
            if( nonDefault )
            {
                switch( type )
                {
                    case ResourceType.Mtrl:
                    case ResourceType.Avfx:
                        var fullPath = new FullPath( $"|{resolveData.ModCollection.Name}_{resolveData.ModCollection.ChangeCounter}|{path}" );
                        data = ( fullPath, resolveData );
                        return;
                }
            }

            data = ( resolved, resolveData );
        }

        public void Enable()
        {
            _loadMtrlShpkHook.Enable();
            _loadMtrlTexHook.Enable();
            _apricotResourceLoadHook.Enable();
            Penumbra.ResourceLoader.ResourceLoadCustomization += SubfileLoadHandler;
        }

        public void Disable()
        {
            _loadMtrlShpkHook.Disable();
            _loadMtrlTexHook.Disable();
            _apricotResourceLoadHook.Disable();
            Penumbra.ResourceLoader.ResourceLoadCustomization -= SubfileLoadHandler;
        }

        public void Dispose()
        {
            Disable();
            _loadMtrlShpkHook.Dispose();
            _loadMtrlTexHook.Dispose();
            _apricotResourceLoadHook.Dispose();
        }

        // We need to set the correct collection for the actual material path that is loaded
        // before actually loading the file.
        public bool SubfileLoadHandler( ByteString split, ByteString path, ResourceManager* resourceManager,
            SeFileDescriptor* fileDescriptor, int priority, bool isSync, out byte ret )
        {
            ret = 0;
            switch( fileDescriptor->ResourceHandle->FileType )
            {
                case ResourceType.Mtrl:
                case ResourceType.Avfx:
                    break;
                default: return false;
            }

            var lastUnderscore = split.LastIndexOf( ( byte )'_' );
            var name           = lastUnderscore == -1 ? split.ToString() : split.Substring( 0, lastUnderscore ).ToString();
            if( Penumbra.TempMods.CollectionByName( name, out var collection )
            || Penumbra.CollectionManager.ByName( name, out collection ) )
            {
#if DEBUG
                Penumbra.Log.Verbose( $"Using {nameof(SubfileLoadHandler)} with collection {name} for path {path}." );
#endif
                _paths.SetCollection( IntPtr.Zero, path, collection );
            }
            else
            {
#if DEBUG
                Penumbra.Log.Verbose( $"Using {nameof( SubfileLoadHandler )}  with no collection for path {path}." );
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
            _mtrlData = LoadFileHelper( mtrlResourceHandle );
            var ret = _loadMtrlTexHook.Original( mtrlResourceHandle );
            _mtrlData = ResolveData.Invalid;
            return ret;
        }

        [Signature( "48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 0F B7 89",
            DetourName = nameof( LoadMtrlShpkDetour ) )]
        private readonly Hook< LoadMtrlFilesDelegate > _loadMtrlShpkHook = null!;

        private byte LoadMtrlShpkDetour( IntPtr mtrlResourceHandle )
        {
            _mtrlData = LoadFileHelper( mtrlResourceHandle );
            var ret = _loadMtrlShpkHook.Original( mtrlResourceHandle );
            _mtrlData = ResolveData.Invalid;
            return ret;
        }

        private ResolveData LoadFileHelper( IntPtr resourceHandle )
        {
            if( resourceHandle == IntPtr.Zero )
            {
                return ResolveData.Invalid;
            }

            var resource     = ( ResourceHandle* )resourceHandle;
            var filePath = ByteString.FromSpanUnsafe( resource->FileNameSpan(), true, null, true );
            return _paths.TryGetValue( filePath, out var c ) ? c : ResolveData.Invalid;
        }


        private delegate byte ApricotResourceLoadDelegate( IntPtr handle, IntPtr unk1, byte unk2 );

        [Signature( "48 89 74 24 ?? 57 48 83 EC ?? 41 0F B6 F0 48 8B F9", DetourName = nameof( ApricotResourceLoadDetour ) )]
        private readonly Hook<ApricotResourceLoadDelegate> _apricotResourceLoadHook = null!;


        private byte ApricotResourceLoadDetour( IntPtr handle, IntPtr unk1, byte unk2 )
        {
            _avfxData = LoadFileHelper( handle );
            var ret = _apricotResourceLoadHook.Original( handle, unk1, unk2 );
            _avfxData = ResolveData.Invalid;
            return ret;
        }
    }
}