using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace Penumbra.GameData.Enums;

public static class ModelTypeExtensions
{
    public static string ToName(this CharacterBase.ModelType type)
        => type switch
        {
            CharacterBase.ModelType.DemiHuman => "Demihuman",
            CharacterBase.ModelType.Monster   => "Monster",
            CharacterBase.ModelType.Human     => "Human",
            CharacterBase.ModelType.Weapon    => "Weapon",
            _                                 => string.Empty,
        };

    public static CharacterBase.ModelType ToModelType(this ObjectType type)
        => type switch
        {
            ObjectType.DemiHuman => CharacterBase.ModelType.DemiHuman,
            ObjectType.Monster   => CharacterBase.ModelType.Monster,
            ObjectType.Character => CharacterBase.ModelType.Human,
            ObjectType.Weapon    => CharacterBase.ModelType.Weapon,
            _                    => 0,
        };
}
