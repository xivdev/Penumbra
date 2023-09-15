using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.SafeHandles;

public unsafe class SafeTextureHandle : SafeHandle
{
    public Texture* Texture => (Texture*)handle;

    public override bool IsInvalid => handle == 0;

    public SafeTextureHandle(Texture* handle, bool incRef, bool ownsHandle = true) : base(0, ownsHandle)
    {
        if (incRef && !ownsHandle)
            throw new ArgumentException("Non-owning SafeTextureHandle with IncRef is unsupported");
        if (incRef && handle != null)
            TextureUtility.IncRef(handle);
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
        => new(null, incRef: false);

    protected override bool ReleaseHandle()
    {
        nint handle;
        lock (this)
        {
            handle = this.handle;
            this.handle = 0;
        }
        if (handle != 0)
            TextureUtility.DecRef((Texture*)handle);

        return true;
    }
}
