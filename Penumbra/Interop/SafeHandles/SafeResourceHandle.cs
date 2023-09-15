using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Penumbra.Interop.SafeHandles;

public unsafe class SafeResourceHandle : SafeHandle
{
    public ResourceHandle* ResourceHandle => (ResourceHandle*)handle;

    public override bool IsInvalid => handle == 0;

    public SafeResourceHandle(ResourceHandle* handle, bool incRef, bool ownsHandle = true) : base(0, ownsHandle)
    {
        if (incRef && !ownsHandle)
            throw new ArgumentException("Non-owning SafeResourceHandle with IncRef is unsupported");
        if (incRef && handle != null)
            handle->IncRef();
        SetHandle((nint)handle);
    }

    public static SafeResourceHandle CreateInvalid()
        => new(null, incRef: false);

    protected override bool ReleaseHandle()
    {
        var handle = Interlocked.Exchange(ref this.handle, 0);
        if (handle != 0)
            ((ResourceHandle*)handle)->DecRef();

        return true;
    }
}
