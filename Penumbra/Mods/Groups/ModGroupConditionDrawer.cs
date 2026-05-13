using Luna;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupConditionDrawer : ConditionDrawer<ModSettingContext>, IUiService
{
    protected override void DrawCustom(ICondition<ModSettingContext> condition, ModSettingContext context)
    {
        switch (condition)
        {
            case SingleSettingCondition single:
            { }
                break;
            case MultiSettingAllCondition all:
            { }
                break;
            case MultiSettingAnyCondition any:
            { }
                break;
        }
    }
}
