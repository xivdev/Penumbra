using Penumbra.GameData.Enums;
using System.Text.RegularExpressions;
using Penumbra.GameData;

namespace Penumbra.Import.Structs;

/// <summary>
/// Obtain information what type of object is manipulated
/// by the given .meta file from TexTools, using its name.
/// </summary>
public partial struct MetaFileInfo
{
    // These are the valid regexes for .meta files that we are able to support at the moment.
    [GeneratedRegex(@"bgcommon/hou/(?'Type1'[a-z]*)/general/(?'Id1'\d{4})/\k'Id1'\.meta",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex HousingMeta();

    [GeneratedRegex(
        @"chara/(?'Type1'[a-z]*)/(?'Pre1'[a-z])(?'Id1'\d{4})(/obj/(?'Type2'[a-z]*)/(?'Pre2'[a-z])(?'Id2'\d{4}))?/\k'Pre1'\k'Id1'(\k'Pre2'\k'Id2')?(_(?'Slot'[a-z]{3}))?\.meta",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex CharaMeta();

    public readonly ObjectType        PrimaryType;
    public readonly BodySlot          SecondaryType;
    public readonly ushort            PrimaryId;
    public readonly ushort            SecondaryId;
    public readonly EquipSlot         EquipSlot         = EquipSlot.Unknown;
    public readonly CustomizationType CustomizationType = CustomizationType.Unknown;

    private static bool ValidType(ObjectType type)
        => type switch
        {
            ObjectType.Accessory     => true,
            ObjectType.Character     => true,
            ObjectType.Equipment     => true,
            ObjectType.DemiHuman     => true,
            ObjectType.Housing       => true,
            ObjectType.Monster       => true,
            ObjectType.Weapon        => true,
            ObjectType.Icon          => false,
            ObjectType.Font          => false,
            ObjectType.Interface     => false,
            ObjectType.LoadingScreen => false,
            ObjectType.Map           => false,
            ObjectType.Vfx           => false,
            ObjectType.Unknown       => false,
            ObjectType.World         => false,
            _                        => false,
        };

    public MetaFileInfo(IGamePathParser parser, string fileName)
    {
        // Set the primary type from the gamePath start.
        PrimaryType   = parser.PathToObjectType(fileName);
        PrimaryId     = 0;
        SecondaryType = BodySlot.Unknown;
        SecondaryId   = 0;
        // Not all types of objects can have valid meta data manipulation.
        if (!ValidType(PrimaryType))
        {
            PrimaryType = ObjectType.Unknown;
            return;
        }

        // Housing files have a separate regex that just contains the primary id.
        if (PrimaryType == ObjectType.Housing)
        {
            var housingMatch = HousingMeta().Match(fileName);
            if (housingMatch.Success)
                PrimaryId = ushort.Parse(housingMatch.Groups["Id1"].Value);

            return;
        }

        // Non-housing is in chara/.
        var match = CharaMeta().Match(fileName);
        if (!match.Success)
            return;

        // The primary ID has to be available for every object.
        PrimaryId = ushort.Parse(match.Groups["Id1"].Value);

        // Depending on slot, we can set equip slot or customization type.
        if (match.Groups["Slot"].Success)
            switch (PrimaryType)
            {
                case ObjectType.Equipment:
                case ObjectType.Accessory:
                    if (Names.SuffixToEquipSlot.TryGetValue(match.Groups["Slot"].Value, out var tmpSlot))
                        EquipSlot = tmpSlot;

                    break;
                case ObjectType.Character:
                    if (Names.SuffixToCustomizationType.TryGetValue(match.Groups["Slot"].Value, out var tmpCustom))
                        CustomizationType = tmpCustom;

                    break;
            }

        // Secondary type and secondary id are for weapons and demihumans.
        if (match.Groups["Type2"].Success && Names.StringToBodySlot.TryGetValue(match.Groups["Type2"].Value, out SecondaryType))
            SecondaryId = ushort.Parse(match.Groups["Id2"].Value);
    }
}
