namespace Penumbra.Interop;

public static class VolatileOffsets
{
    public static class ApricotListenerSoundPlayCaller
    {
        public const int PlayTimeOffset     = 0x254;
        public const int SomeIntermediate   = 0x1F8;
        public const int Flags              = 0x4A4;
        public const int IInstanceListenner = 0x270;
        public const int BitShift           = 13;
        public const int CasterVFunc        = 1;
    }

    public static class AnimationState
    {
        public const int TimeLinePtr = 0x50;
    }

    public static class UpdateModel
    {
        public const int ShortCircuit = 0xA2C;
    }

    public static class FontReloader
    {
        public const int ReloadFontsVFunc = 43;
    }

    public static class RedrawService
    {
        public const int EnableDrawVFunc  = 12;
        public const int DisableDrawVFunc = 13;
    }
}
