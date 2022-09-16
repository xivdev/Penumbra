namespace Penumbra.GameData.Enums;

public enum ObjectType : byte
{
    Unknown,
    Vfx,
    DemiHuman,
    Accessory,
    World,
    Housing,
    Monster,
    Icon,
    LoadingScreen,
    Map,
    Interface,
    Equipment,
    Character,
    Weapon,
    Font,
}

public static class ObjectTypeExtensions
{
    public static string ToName( this ObjectType type )
        => type switch
        {
            ObjectType.Vfx           => "Visual Effect",
            ObjectType.DemiHuman     => "Demi Human",
            ObjectType.Accessory     => "Accessory",
            ObjectType.World         => "Doodad",
            ObjectType.Housing       => "Housing Object",
            ObjectType.Monster       => "Monster",
            ObjectType.Icon          => "Icon",
            ObjectType.LoadingScreen => "Loading Screen",
            ObjectType.Map           => "Map",
            ObjectType.Interface     => "UI Element",
            ObjectType.Equipment     => "Equipment",
            ObjectType.Character     => "Character",
            ObjectType.Weapon        => "Weapon",
            ObjectType.Font          => "Font",
            _                        => "Unknown",
        };


    public static readonly ObjectType[] ValidImcTypes =
    {
        ObjectType.Equipment,
        ObjectType.Accessory,
        ObjectType.DemiHuman,
        ObjectType.Monster,
        ObjectType.Weapon,
    };
}