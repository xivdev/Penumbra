using Dalamud.Game.ClientState.Objects.Enums;
using ImSharp;

namespace Penumbra.UI.CollectionTab;

public sealed class ObjectKindCombo(params IReadOnlyList<ObjectKind> kinds) : SimpleFilterCombo<ObjectKind>(SimpleFilterType.None)
{
    public override StringU8 DisplayString(in ObjectKind value)
        => value switch
        {
            ObjectKind.None      => new StringU8("Unknown"u8),
            ObjectKind.BattleNpc => new StringU8("Battle NPC"u8),
            ObjectKind.EventNpc  => new StringU8("Event NPC"u8),
            ObjectKind.MountType => new StringU8("Mount"u8),
            ObjectKind.Companion => new StringU8("Companion"u8),
            ObjectKind.Ornament  => new StringU8("Accessory"u8),
            _                    => new StringU8($"{value}"),
        };

    public override string FilterString(in ObjectKind value)
        => string.Empty;

    public override IEnumerable<ObjectKind> GetBaseItems()
        => kinds;
}
