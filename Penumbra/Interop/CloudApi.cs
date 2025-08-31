namespace Penumbra.Interop;

public static unsafe partial class CloudApi
{
    private const int CfSyncRootInfoBasic = 0;
    
    public static bool IsCloudSynced(string path)
    {
        var buffer = stackalloc long[1];
        var hr = CfGetSyncRootInfoByPath(path, CfSyncRootInfoBasic, buffer, sizeof(long), out var length);
        Penumbra.Log.Information($"{nameof(CfGetSyncRootInfoByPath)} returned HRESULT {hr}");
        if (hr < 0)
            return false;
        
        if (length != sizeof(long))
        {
            Penumbra.Log.Warning($"Expected {nameof(CfGetSyncRootInfoByPath)} to return {sizeof(long)} bytes, got {length} bytes");
            return false;
        }
        
        Penumbra.Log.Information($"{nameof(CfGetSyncRootInfoByPath)} returned {{ SyncRootFileId = 0x{*buffer:X16} }}");

        return true;
    }
    
    [LibraryImport("cldapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CfGetSyncRootInfoByPath(string filePath, int infoClass, void* infoBuffer, uint infoBufferLength,
        out uint returnedLength);
}
