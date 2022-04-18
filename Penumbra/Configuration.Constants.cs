namespace Penumbra;

public partial class Configuration
{
    // Contains some default values or boundaries for config values.
    public static class Constants
    {
        public const int   CurrentVersion      = 3;
        public const float MaxAbsoluteSize     = 600;
        public const int   DefaultAbsoluteSize = 250;
        public const float MinAbsoluteSize     = 50;
        public const int   MaxScaledSize       = 80;
        public const int   DefaultScaledSize   = 20;
        public const int   MinScaledSize       = 5;
    }
}