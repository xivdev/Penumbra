using ImSharp;
using Luna;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupConditionDrawer : ConditionDrawer<ModSettingContext>, IUiService
{
    private string _group  = string.Empty;
    private string _option = string.Empty;

    protected override bool DrawCustom(ICondition<ModSettingContext>? condition, ModSettingContext context,
        out ICondition<ModSettingContext>? replaced)
    {
        var ret = false;
        replaced = null;
        switch (condition)
        {
            case null:
            {
                if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, "Add a new condition."u8, _group.Length is 0 && _option.Length is 0))
                {
                    replaced = new SingleSettingCondition(_group, _option);
                    _group   = string.Empty;
                    _option  = string.Empty;
                    ret      = true;
                }

                Im.Line.SameInner();
                Im.Item.SetNextWidth(Im.ContentRegion.Available.X * 0.4f);
                Im.Input.Text("##Group"u8, ref _group, "Group..."u8);
                Im.Line.SameInner();
                Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
                Im.Input.Text("##Options"u8, ref _option, "Options..."u8);
            }
                break;
            case SingleSettingCondition single:
            {
                Im.FrameDummy();
                Im.Line.SameInner();
                ImEx.TextFramed(single.Group, new Vector2(Im.ContentRegion.Available.X * 0.4f, 0));
                Im.Line.SameInner();
                ImEx.TextFramed(single.Option, Im.ContentRegion.Available with { Y = 0 });
            }
                break;
            case MultiSettingAllCondition all:
            { }
                break;
            case MultiSettingAnyCondition any:
            { }
                break;
        }

        return ret;
    }
}
