using Iced.Intel;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public class MappedCodeReader(UnmanagedMemoryAccessor data, long offset) : CodeReader
{
    public override int ReadByte()
    {
        if (offset >= data.Capacity)
            return -1;

        return data.ReadByte(offset++);
    }
}
