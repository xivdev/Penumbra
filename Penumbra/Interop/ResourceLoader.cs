using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.Structs;
using Penumbra.Util;
using FileMode = Penumbra.Structs.FileMode;

namespace Penumbra.Interop;

public class ResourceLoader : IDisposable
{
    public Penumbra Penumbra { get; set; }

    public bool IsEnabled { get; set; }

    public Crc32 Crc32 { get; }

    public static readonly IntPtr CustomFileFlag = new(0xDEADBEEF);

    // Delegate prototypes
    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public unsafe delegate byte ReadFilePrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public unsafe delegate byte ReadSqpackPrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public unsafe delegate void* GetResourceSyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType
        , uint* pResourceHash, char* pPath, void* pUnknown );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public unsafe delegate void* GetResourceAsyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType
        , uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public delegate IntPtr CheckFileStatePrototype( IntPtr unk1, ulong crc );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public delegate byte LoadTexFileExternPrototype( IntPtr resourceHandle, int unk1, IntPtr unk2, bool unk3, IntPtr unk4 );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public delegate byte LoadTexFileLocalPrototype( IntPtr resourceHandle, int unk1, IntPtr unk2, bool unk3 );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public delegate byte LoadMdlFileExternPrototype( IntPtr resourceHandle, IntPtr unk1, bool unk2, IntPtr unk3 );

    [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
    public delegate byte LoadMdlFileLocalPrototype( IntPtr resourceHandle, IntPtr unk1, bool unk2 );

    // Hooks
    public Hook< GetResourceSyncPrototype >? GetResourceSyncHook { get; private set; }
    public Hook< GetResourceAsyncPrototype >? GetResourceAsyncHook { get; private set; }
    public Hook< ReadSqpackPrototype >? ReadSqpackHook { get; private set; }
    public Hook< CheckFileStatePrototype >? CheckFileStateHook { get; private set; }
    public Hook< LoadTexFileExternPrototype >? LoadTexFileExternHook { get; private set; }
    public Hook< LoadMdlFileExternPrototype >? LoadMdlFileExternHook { get; private set; }

    // Unmanaged functions
    public ReadFilePrototype? ReadFile { get; private set; }
    public LoadTexFileLocalPrototype? LoadTexFileLocal { get; private set; }
    public LoadMdlFileLocalPrototype? LoadMdlFileLocal { get; private set; }

    public bool   LogAllFiles   = false;
    public Regex? LogFileFilter = null;


    public ResourceLoader( Penumbra penumbra )
    {
        Penumbra = penumbra;
        Crc32    = new Crc32();
    }

    public unsafe void Init()
    {
        var readFileAddress =
            Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3 BA 05" );
        GeneralUtil.PrintDebugAddress( "ReadFile", readFileAddress );

        var readSqpackAddress =
            Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3" );
        GeneralUtil.PrintDebugAddress( "ReadSqPack", readSqpackAddress );

        var getResourceSyncAddress =
            Dalamud.SigScanner.ScanText( "E8 ?? ?? 00 00 48 8D 8F ?? ?? 00 00 48 89 87 ?? ?? 00 00" );
        GeneralUtil.PrintDebugAddress( "GetResourceSync", getResourceSyncAddress );

        var getResourceAsyncAddress =
            Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00" );
        GeneralUtil.PrintDebugAddress( "GetResourceAsync", getResourceAsyncAddress );

        var checkFileStateAddress = Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? ?? 48 85 c0 74 ?? 45 0f b6 ce 48 89 44 24" );
        GeneralUtil.PrintDebugAddress( "CheckFileState", checkFileStateAddress );

        var loadTexFileLocalAddress =
            Dalamud.SigScanner.ScanText( "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 30 49 8B F0 44 88 4C 24 20" );
        GeneralUtil.PrintDebugAddress( "LoadTexFileLocal", loadTexFileLocalAddress );

        var loadTexFileExternAddress =
            Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? ?? 0F B6 E8 48 8B CB E8" );
        GeneralUtil.PrintDebugAddress( "LoadTexFileExtern", loadTexFileExternAddress );

        var loadMdlFileLocalAddress =
            Dalamud.SigScanner.ScanText( "40 55 53 56 57 41 56 41 57 48 8D 6C 24 D1 48 81 EC 98 00 00 00" );
        GeneralUtil.PrintDebugAddress( "LoadMdlFileLocal", loadMdlFileLocalAddress );

        var loadMdlFileExternAddress =
            Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? ?? EB 02 B0 F1" );
        GeneralUtil.PrintDebugAddress( "LoadMdlFileExtern", loadMdlFileExternAddress );

        ReadSqpackHook       = new Hook< ReadSqpackPrototype >( readSqpackAddress, ReadSqpackHandler );
        GetResourceSyncHook  = new Hook< GetResourceSyncPrototype >( getResourceSyncAddress, GetResourceSyncHandler );
        GetResourceAsyncHook = new Hook< GetResourceAsyncPrototype >( getResourceAsyncAddress, GetResourceAsyncHandler );

        ReadFile         = Marshal.GetDelegateForFunctionPointer< ReadFilePrototype >( readFileAddress );
        LoadTexFileLocal = Marshal.GetDelegateForFunctionPointer< LoadTexFileLocalPrototype >( loadTexFileLocalAddress );
        LoadMdlFileLocal = Marshal.GetDelegateForFunctionPointer< LoadMdlFileLocalPrototype >( loadMdlFileLocalAddress );

        CheckFileStateHook    = new Hook< CheckFileStatePrototype >( checkFileStateAddress, CheckFileStateDetour );
        LoadTexFileExternHook = new Hook< LoadTexFileExternPrototype >( loadTexFileExternAddress, LoadTexFileExternDetour );
        LoadMdlFileExternHook = new Hook< LoadMdlFileExternPrototype >( loadMdlFileExternAddress, LoadMdlFileExternDetour );
    }

    private IntPtr CheckFileStateDetour( IntPtr ptr, ulong crc64 )
    {
        var modManager = Service< ModManager >.Get();
        return modManager.CheckCrc64( crc64 ) ? CustomFileFlag : CheckFileStateHook!.Original( ptr, crc64 );
    }

    private byte LoadTexFileExternDetour( IntPtr resourceHandle, int unk1, IntPtr unk2, bool unk3, IntPtr ptr )
        => ptr.Equals( CustomFileFlag )
            ? LoadTexFileLocal!.Invoke( resourceHandle, unk1, unk2, unk3 )
            : LoadTexFileExternHook!.Original( resourceHandle, unk1, unk2, unk3, ptr );

    private byte LoadMdlFileExternDetour( IntPtr resourceHandle, IntPtr unk1, bool unk2, IntPtr ptr )
        => ptr.Equals( CustomFileFlag )
            ? LoadMdlFileLocal!.Invoke( resourceHandle, unk1, unk2 )
            : LoadMdlFileExternHook!.Original( resourceHandle, unk1, unk2, ptr );

    private unsafe void* GetResourceSyncHandler(
        IntPtr pFileManager,
        uint* pCategoryId,
        char* pResourceType,
        uint* pResourceHash,
        char* pPath,
        void* pUnknown
    )
        => GetResourceHandler( true, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, false );

    private unsafe void* GetResourceAsyncHandler(
        IntPtr pFileManager,
        uint* pCategoryId,
        char* pResourceType,
        uint* pResourceHash,
        char* pPath,
        void* pUnknown,
        bool isUnknown
    )
        => GetResourceHandler( false, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );

    private unsafe void* CallOriginalHandler(
        bool isSync,
        IntPtr pFileManager,
        uint* pCategoryId,
        char* pResourceType,
        uint* pResourceHash,
        char* pPath,
        void* pUnknown,
        bool isUnknown
    )
    {
        if( isSync )
        {
            if( GetResourceSyncHook == null )
            {
                PluginLog.Error( "[GetResourceHandler] GetResourceSync is null." );
                return null;
            }

            return GetResourceSyncHook.Original( pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown );
        }

        if( GetResourceAsyncHook == null )
        {
            PluginLog.Error( "[GetResourceHandler] GetResourceAsync is null." );
            return null;
        }

        return GetResourceAsyncHook.Original( pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
    }

    private unsafe void* GetResourceHandler(
        bool isSync,
        IntPtr pFileManager,
        uint* pCategoryId,
        char* pResourceType,
        uint* pResourceHash,
        char* pPath,
        void* pUnknown,
        bool isUnknown
    )
    {
        string file;
        var    modManager = Service< ModManager >.Get();

        if( !Penumbra.Config.IsEnabled || modManager == null )
        {
            if( LogAllFiles )
            {
                file = Marshal.PtrToStringAnsi( new IntPtr( pPath ) )!;
                if( LogFileFilter == null || LogFileFilter.IsMatch( file ) )
                {
                    PluginLog.Information( "[GetResourceHandler] {0}", file );
                }
            }

            return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
        }

        file = Marshal.PtrToStringAnsi( new IntPtr( pPath ) )!;
        var gameFsPath      = GamePath.GenerateUncheckedLower( file );
        var replacementPath = modManager.ResolveSwappedOrReplacementPath( gameFsPath );
        if( LogAllFiles && ( LogFileFilter == null || LogFileFilter.IsMatch( file ) ) )
        {
            PluginLog.Information( "[GetResourceHandler] {0}", file );
        }

        // path must be < 260 because statically defined array length :(
        if( replacementPath == null )
        {
            return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
        }

        var path = Encoding.ASCII.GetBytes( replacementPath );

        var bPath = stackalloc byte[path.Length + 1];
        Marshal.Copy( path, 0, new IntPtr( bPath ), path.Length );
        pPath = ( char* )bPath;

        Crc32.Init();
        Crc32.Update( path );
        *pResourceHash = Crc32.Checksum;

        PluginLog.Verbose( "[GetResourceHandler] resolved {GamePath} to {NewPath}", gameFsPath, replacementPath );

        return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
    }


    private unsafe byte ReadSqpackHandler( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync )
    {
        if( ReadFile == null || pFileDesc == null || pFileDesc->ResourceHandle == null )
        {
            PluginLog.Error( "THIS SHOULD NOT HAPPEN" );
            return ReadSqpackHook?.Original( pFileHandler, pFileDesc, priority, isSync ) ?? 0;
        }

        var gameFsPath = Marshal.PtrToStringAnsi( new IntPtr( pFileDesc->ResourceHandle->FileName() ) );

        var isRooted = Path.IsPathRooted( gameFsPath );

        if( gameFsPath == null || gameFsPath.Length >= 260 || !isRooted )
        {
            return ReadSqpackHook?.Original( pFileHandler, pFileDesc, priority, isSync ) ?? 0;
        }

        PluginLog.Debug( "loading modded file: {GameFsPath}", gameFsPath );

        pFileDesc->FileMode = FileMode.LoadUnpackedResource;

        // note: must be utf16
        var utfPath = Encoding.Unicode.GetBytes( gameFsPath );

        Marshal.Copy( utfPath, 0, new IntPtr( &pFileDesc->UtfFileName ), utfPath.Length );

        var fd = stackalloc byte[0x20 + utfPath.Length + 0x16];
        Marshal.Copy( utfPath, 0, new IntPtr( fd + 0x21 ), utfPath.Length );

        pFileDesc->FileDescriptor = fd;

        return ReadFile( pFileHandler, pFileDesc, priority, isSync );
    }

    public void Enable()
    {
        if( IsEnabled )
        {
            return;
        }

        if( ReadSqpackHook       == null
        || GetResourceSyncHook   == null
        || GetResourceAsyncHook  == null
        || CheckFileStateHook    == null
        || LoadTexFileExternHook == null
        || LoadMdlFileExternHook == null )
        {
            PluginLog.Error( "[GetResourceHandler] Could not activate hooks because at least one was not set." );
            return;
        }

        ReadSqpackHook.Enable();
        GetResourceSyncHook.Enable();
        GetResourceAsyncHook.Enable();
        CheckFileStateHook.Enable();
        LoadTexFileExternHook.Enable();
        LoadMdlFileExternHook.Enable();

        IsEnabled    = true;
    }

    public void Disable()
    {
        if( !IsEnabled )
        {
            return;
        }

        ReadSqpackHook?.Disable();
        GetResourceSyncHook?.Disable();
        GetResourceAsyncHook?.Disable();
        CheckFileStateHook?.Disable();
        LoadTexFileExternHook?.Disable();
        LoadMdlFileExternHook?.Disable();
        IsEnabled    = false;
    }

    public void Dispose()
    {
        Disable();
        ReadSqpackHook?.Dispose();
        GetResourceSyncHook?.Dispose();
        GetResourceAsyncHook?.Dispose();
        CheckFileStateHook?.Dispose();
        LoadTexFileExternHook?.Dispose();
        LoadMdlFileExternHook?.Dispose();
    }
}