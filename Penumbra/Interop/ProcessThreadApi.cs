namespace Penumbra.Interop;

public static partial class ProcessThreadApi
{
    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();
}
