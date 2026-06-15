using ImSharp;
using Luna;
using Penumbra.UI.ModsTab;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupConditionDrawer : ConditionDrawer<ModSettingContext>, IUiService
{
    private readonly ConditionCombo _conditionCombo = new();

    private float _deleteSize;

    protected override bool UpdateSizes()
    {
        if (!base.UpdateSizes())
            return false;

        _deleteSize = Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight;
        return true;
    }

    private bool DrawSetting(SettingCondition s, ModSettingContext context, ref ICondition<ModSettingContext>? replaced, bool not)
    {
        var size = Im.ContentRegion.Available.X - _deleteSize;
        var ret  = false;
        if (_conditionCombo.Draw("##combo"u8, context.Object!, size, s, out var newCondition))
        {
            replaced = newCondition;
            ret      = true;
        }
        Im.Line.SameInner();
        ret |= DrawDeleteConditionButton(ref replaced);
        return ret;
    }

    protected override bool DrawCustom(ICondition<ModSettingContext>? condition, ModSettingContext context,
        out ICondition<ModSettingContext>? replaced)
    {
        var ret = false;
        replaced = condition;

        if (context.Object is null)
            return ret;

        switch (condition)
        {
            case null:
            {
                var size = Im.ContentRegion.Available.X - _deleteSize;
                if (_conditionCombo.Draw("##comboNew"u8, context.Object, size, NewCondition as SettingCondition, out var n))
                    NewCondition = n;
                Im.Line.SameInner();
                ret = DrawAddConditionButton(ref replaced);
                break;
            }
            case SettingCondition s: return DrawSetting(s, context, ref replaced, false);
        }

        return ret;
    }
}
