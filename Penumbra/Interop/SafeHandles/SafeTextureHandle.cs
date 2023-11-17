using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Penumbra.Interop.SafeHandles;

public unsafe class SafeTextureHandle : SafeHandle
{
    public Texture* Texture
        => (Texture*)handle;

    public override bool IsInvalid
        => handle == 0;

    public SafeTextureHandle(Texture* handle, bool incRef, bool ownsHandle = true)
        : base(0, ownsHandle)
    {
        if (incRef && !ownsHandle)
            throw new ArgumentException("Non-owning SafeTextureHandle with IncRef is unsupported");

        if (incRef && handle != null)
            handle->IncRef();
        SetHandle((nint)handle);
    }

    public void Exchange(ref nint ppTexture)
    {
        lock (this)
        {
            handle = Interlocked.Exchange(ref ppTexture, handle);
        }
    }

    public static SafeTextureHandle CreateInvalid()
        => new(null, false);

    protected override bool ReleaseHandle()
    {
        nint handle;
        lock (this)
        {
            handle      = this.handle;
            this.handle = 0;
        }

        if (handle != 0)
            ((Texture*)handle)->DecRef();

        return true;
    }
}
