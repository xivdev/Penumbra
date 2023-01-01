using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

public static class MaterialHandling
{
    public static GenderRace GetGameGenderRace(GenderRace actualGr, SetId hairId)
    {
        // Hrothgar do not share hairstyles.
        if (actualGr is GenderRace.HrothgarFemale or GenderRace.HrothgarMale)
            return actualGr;

        // Some hairstyles are miqo'te specific but otherwise shared.
        if (hairId.Value is >= 101 and <= 115)
        {
            if (actualGr is GenderRace.MiqoteFemale or GenderRace.MiqoteMale)
                return actualGr;

            return actualGr.Split().Item1 == Gender.Female ? GenderRace.MidlanderFemale : GenderRace.MidlanderMale;
        }

        // All hairstyles above 116 are shared except for Hrothgar
        if (hairId.Value is >= 116 and <= 200)
            return actualGr.Split().Item1 == Gender.Female ? GenderRace.MidlanderFemale : GenderRace.MidlanderMale;

        return actualGr;
    }

    public static bool IsSpecialCase(GenderRace gr, SetId hairId)
        => gr is GenderRace.MidlanderMale or GenderRace.MidlanderFemale && hairId.Value is >= 101 and <= 200;
}
