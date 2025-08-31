namespace Penumbra.Interop;

public static unsafe partial class CloudApi
{
    private const int CfSyncRootInfoBasic = 0;
    
    /// <summary> Determines whether a file or directory is cloud-synced using OneDrive or other providers that use the Cloud API. </summary>
    /// <remarks> Can be expensive. Callers should cache the result when relevant. </remarks>
    public static bool IsCloudSynced(string path)
    {
        var  buffer = stackalloc long[1];
        int  hr;
        uint length;
        try
        {
            hr = CfGetSyncRootInfoByPath(path, CfSyncRootInfoBasic, buffer, sizeof(long), out length);
        }
        catch (DllNotFoundException)
        {
            Penumbra.Log.Debug($"{nameof(CfGetSyncRootInfoByPath)} threw DllNotFoundException");
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            Penumbra.Log.Debug($"{nameof(CfGetSyncRootInfoByPath)} threw EntryPointNotFoundException");
            return false;
        }

        Penumbra.Log.Debug($"{nameof(CfGetSyncRootInfoByPath)} returned HRESULT 0x{hr:X8}");
        if (hr < 0)
            return false;
        
        if (length != sizeof(long))
        {
            Penumbra.Log.Debug($"Expected {nameof(CfGetSyncRootInfoByPath)} to return {sizeof(long)} bytes, got {length} bytes");
            return false;
        }
        
        Penumbra.Log.Debug($"{nameof(CfGetSyncRootInfoByPath)} returned {{ SyncRootFileId = 0x{*buffer:X16} }}");

        return true;
    }
    
    [LibraryImport("cldapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CfGetSyncRootInfoByPath(string filePath, int infoClass, void* infoBuffer, uint infoBufferLength,
        out uint returnedLength);
}
