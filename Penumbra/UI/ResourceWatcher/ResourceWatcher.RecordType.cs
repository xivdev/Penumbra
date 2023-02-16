using System;

namespace Penumbra.UI;

public partial class ResourceWatcher
{
    [Flags]
    public enum RecordType : byte
    {
        Request      = 0x01,
        ResourceLoad = 0x02,
        FileLoad     = 0x04,
        Destruction  = 0x08,
    }

    public const RecordType AllRecords = RecordType.Request | RecordType.ResourceLoad | RecordType.FileLoad | RecordType.Destruction;
}