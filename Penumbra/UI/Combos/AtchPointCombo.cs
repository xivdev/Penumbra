using ImSharp;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.UI.AdvancedWindow.Meta;

namespace Penumbra.UI.Combos;

internal sealed class AtchPointCombo(AtchMetaDrawer parent)
    : SimpleFilterCombo<AtchType>(SimpleFilterType.Text)
{
    public override StringU8 DisplayString(in AtchType value)
        => new(value.ToNameU8());

    public override string FilterString(in AtchType value)
        => value.ToString();

    public override IEnumerable<AtchType> GetBaseItems()
        => parent.GetPoints();
}
