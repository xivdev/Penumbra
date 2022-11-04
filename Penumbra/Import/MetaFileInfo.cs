using Penumbra.GameData.Enums;
using System.Text.RegularExpressions;

namespace Penumbra.Import;

// Obtain information what type of object is manipulated
// by the given .meta file from TexTools, using its name.
public class MetaFileInfo
{
    private const string Pt   = @"(?'PrimaryType'[a-z]*)";                                              // language=regex
    private const string Pp   = @"(?'PrimaryPrefix'[a-z])";                                             // language=regex
    private const string Pi   = @"(?'PrimaryId'\d{4})";                                                 // language=regex
    private const string Pir  = @"\k'PrimaryId'";                                                       // language=regex
    private const string St   = @"(?'SecondaryType'[a-z]*)";                                            // language=regex
    private const string Sp   = @"(?'SecondaryPrefix'[a-z])";                                           // language=regex
    private const string Si   = @"(?'SecondaryId'\d{4})";                                               // language=regex
    private const string File = @"\k'PrimaryPrefix'\k'PrimaryId'(\k'SecondaryPrefix'\k'SecondaryId')?"; // language=regex
    private const string Slot = @"(_(?'Slot'[a-z]{3}))?";                                               // language=regex
    private const string Ext  = @"\.meta";

    // These are the valid regexes for .meta files that we are able to support at the moment.
    private static readonly Regex HousingMeta = new($"bgcommon/hou/{Pt}/general/{Pi}/{Pir}{Ext}", RegexOptions.Compiled);
    private static readonly Regex CharaMeta   = new($"chara/{Pt}/{Pp}{Pi}(/obj/{St}/{Sp}{Si})?/{File}{Slot}{Ext}", RegexOptions.Compiled);

    public readonly ObjectType        PrimaryType;
    public readonly BodySlot          SecondaryType;
    public readonly ushort            PrimaryId;
    public readonly ushort            SecondaryId;
    public readonly EquipSlot         EquipSlot         = EquipSlot.Unknown;
    public readonly CustomizationType CustomizationType = CustomizationType.Unknown;

    private static bool ValidType( ObjectType type )
    {
        return type switch
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
    }

    public MetaFileInfo( string fileName )
    {
        // Set the primary type from the gamePath start.
        PrimaryType   = Penumbra.GamePathParser.PathToObjectType( fileName );
        PrimaryId     = 0;
        SecondaryType = BodySlot.Unknown;
        SecondaryId   = 0;
        // Not all types of objects can have valid meta data manipulation.
        if( !ValidType( PrimaryType ) )
        {
            PrimaryType = ObjectType.Unknown;
            return;
        }

        // Housing files have a separate regex that just contains the primary id.
        if( PrimaryType == ObjectType.Housing )
        {
            var housingMatch = HousingMeta.Match( fileName );
            if( housingMatch.Success )
            {
                PrimaryId = ushort.Parse( housingMatch.Groups[ "PrimaryId" ].Value );
            }

            return;
        }

        // Non-housing is in chara/.
        var match = CharaMeta.Match( fileName );
        if( !match.Success )
        {
            return;
        }

        // The primary ID has to be available for every object.
        PrimaryId = ushort.Parse( match.Groups[ "PrimaryId" ].Value );

        // Depending on slot, we can set equip slot or customization type.
        if( match.Groups[ "Slot" ].Success )
        {
            switch( PrimaryType )
            {
                case ObjectType.Equipment:
                case ObjectType.Accessory:
                    if( Names.SuffixToEquipSlot.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpSlot ) )
                    {
                        EquipSlot = tmpSlot;
                    }

                    break;
                case ObjectType.Character:
                    if( Names.SuffixToCustomizationType.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpCustom ) )
                    {
                        CustomizationType = tmpCustom;
                    }

                    break;
            }
        }

        // Secondary type and secondary id are for weapons and demihumans.
        if( match.Groups[ "SecondaryType" ].Success
        && Names.StringToBodySlot.TryGetValue( match.Groups[ "SecondaryType" ].Value, out SecondaryType ) )
        {
            SecondaryId = ushort.Parse( match.Groups[ "SecondaryId" ].Value );
        }
    }
}