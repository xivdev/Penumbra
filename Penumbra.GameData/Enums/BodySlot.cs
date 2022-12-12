using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Penumbra.GameData.Enums;

public enum BodySlot : byte
{
    Unknown,
    Hair,
    Face,
    Tail,
    Body,
    Zear,
}

public static class BodySlotEnumExtension
{
    public static string ToSuffix( this BodySlot value )
        => value switch
        {
            BodySlot.Zear => "zear",
            BodySlot.Face => "face",
            BodySlot.Hair => "hair",
            BodySlot.Body => "body",
            BodySlot.Tail => "tail",
            _             => throw new InvalidEnumArgumentException(),
        };

    public static char ToAbbreviation(this BodySlot value)
        => value switch
        {
            BodySlot.Hair => 'h',
            BodySlot.Face => 'f',
            BodySlot.Tail => 't',
            BodySlot.Body => 'b',
            BodySlot.Zear => 'z',
            _             => throw new InvalidEnumArgumentException(),
        };

    public static CustomizationType ToCustomizationType(this BodySlot value)
        => value switch
        {
            BodySlot.Hair => CustomizationType.Hair,
            BodySlot.Face => CustomizationType.Face,
            BodySlot.Tail => CustomizationType.Tail,
            BodySlot.Body => CustomizationType.Body,
            BodySlot.Zear => CustomizationType.Zear,
            _             => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
}

public static partial class Names
{
    public static readonly Dictionary< string, BodySlot > StringToBodySlot = new()
    {
        { BodySlot.Zear.ToSuffix(), BodySlot.Zear },
        { BodySlot.Face.ToSuffix(), BodySlot.Face },
        { BodySlot.Hair.ToSuffix(), BodySlot.Hair },
        { BodySlot.Body.ToSuffix(), BodySlot.Body },
        { BodySlot.Tail.ToSuffix(), BodySlot.Tail },
    };
}