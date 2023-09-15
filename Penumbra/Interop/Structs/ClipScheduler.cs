namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct ClipScheduler
{
    [FieldOffset( 0 )]
    public IntPtr* VTable;

    [FieldOffset( 0x38 )]
    public IntPtr SchedulerTimeline;
}