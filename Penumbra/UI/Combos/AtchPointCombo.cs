using OtterGui.Widgets;
using Penumbra.GameData.Files.AtchStructs;

namespace Penumbra.UI.Combos;

internal sealed class AtchPointCombo(Func<IReadOnlyList<AtchType>> generator)
    : FilterComboCache<AtchType>(generator, MouseWheelType.Control, Penumbra.Log)
{
    protected override string ToString(AtchType obj)
        => obj.ToName();
}
