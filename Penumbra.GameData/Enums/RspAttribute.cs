namespace Penumbra.GameData.Enums
{
    public enum RspAttribute : byte
    {
        MaleMinSize,
        MaleMaxSize,
        MaleMinTail,
        MaleMaxTail,
        FemaleMinSize,
        FemaleMaxSize,
        FemaleMinTail,
        FemaleMaxTail,
        BustMinX,
        BustMinY,
        BustMinZ,
        BustMaxX,
        BustMaxY,
        BustMaxZ,
        NumAttributes,
    }

    public static class RspAttributeExtensions
    {
        public static Gender ToGender( this RspAttribute attribute )
        {
            return attribute switch
            {
                RspAttribute.MaleMinSize   => Gender.Male,
                RspAttribute.MaleMaxSize   => Gender.Male,
                RspAttribute.MaleMinTail   => Gender.Male,
                RspAttribute.MaleMaxTail   => Gender.Male,
                RspAttribute.FemaleMinSize => Gender.Female,
                RspAttribute.FemaleMaxSize => Gender.Female,
                RspAttribute.FemaleMinTail => Gender.Female,
                RspAttribute.FemaleMaxTail => Gender.Female,
                RspAttribute.BustMinX      => Gender.Female,
                RspAttribute.BustMinY      => Gender.Female,
                RspAttribute.BustMinZ      => Gender.Female,
                RspAttribute.BustMaxX      => Gender.Female,
                RspAttribute.BustMaxY      => Gender.Female,
                RspAttribute.BustMaxZ      => Gender.Female,
                _                          => Gender.Unknown,
            };
        }

        public static string ToUngenderedString( this RspAttribute attribute )
        {
            return attribute switch
            {
                RspAttribute.MaleMinSize   => "MinSize",
                RspAttribute.MaleMaxSize   => "MaxSize",
                RspAttribute.MaleMinTail   => "MinTail",
                RspAttribute.MaleMaxTail   => "MaxTail",
                RspAttribute.FemaleMinSize => "MinSize",
                RspAttribute.FemaleMaxSize => "MaxSize",
                RspAttribute.FemaleMinTail => "MinTail",
                RspAttribute.FemaleMaxTail => "MaxTail",
                RspAttribute.BustMinX      => "BustMinX",
                RspAttribute.BustMinY      => "BustMinY",
                RspAttribute.BustMinZ      => "BustMinZ",
                RspAttribute.BustMaxX      => "BustMaxX",
                RspAttribute.BustMaxY      => "BustMaxY",
                RspAttribute.BustMaxZ      => "BustMaxZ",
                _                          => "",
            };
        }
    }
}