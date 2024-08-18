using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Base;

namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ClipScheduler
{
    [FieldOffset(0)]
    public nint* VTable;

    [FieldOffset(0x38)]
    public SchedulerTimeline* SchedulerTimeline;
}
