using ImSharp;
using ImSharp.ImNodes;
using Luna;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.UI.ModsTab;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupConditionCache : ConditionDrawerCache<ModSettingContext>
{
    private readonly CommunicatorService _communicator;
    private readonly ConditionCombo      _conditionCombo = new() { Flags = ComboFlags.NoArrowButton };

    public ModGroupConditionCache(CommunicatorService communicator, ModSettingContext context)
        : base(context)
    {
        _communicator = communicator;
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.LayoutManager);
    }

    protected override void Dispose(bool disposing)
    {
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
    }

    private void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        if (arguments.Mod != Context.Mod)
            return;

        Dirty |= IManagedCache.DirtyFlags.Custom;
    }

    protected override ICondition<ModSettingContext>? CreateNewCondition()
    {
        var option = Context.Mod.Groups.Where(g => g != Context.Object?.Group).SelectMany(g => g.Options).FirstOrDefault();
        return option is null ? null : new SettingCondition(option);
    }

    protected override CustomNode? HandleCustomCondition(Action<ICondition<ModSettingContext>?> setter,
        ICondition<ModSettingContext> condition, ParentConditionType parent, byte depth)
    {
        if (condition is not SettingCondition setting)
            return null;

        var node    = new SettingNode(IdCounter++, setting, setter, IdCounter++, parent, depth);
        var ownSize = node.GetOwnSize(this);
        AddNode(node, ownSize.X, false);
        node.SubtreeHeight = ownSize.Y;
        return node;
    }

    private sealed class SettingNode(
        NodeId id,
        SettingCondition condition,
        Action<ICondition<ModSettingContext>?> setter,
        AttributeId output,
        ParentConditionType parent,
        byte depth) : CustomNode(id, condition, setter, output, parent, depth)
    {
        public override Vector2 GetOwnSize(ConditionDrawerCache<ModSettingContext> drawerCache)
            => drawerCache.ConstantNodeSize with { X = drawerCache.ButtonSize.X * 10 };

        /// <inheritdoc/>
        public override Rgba32 TitleColor(ConditionDrawerCache<ModSettingContext> drawerCache)
            => Rgba32.Transparent;

        /// <inheritdoc/>
        public override Rgba32 BorderColor(ConditionDrawerCache<ModSettingContext> drawerCache)
            => drawerCache.NodeColors.CustomBorder;

        public override bool DrawContent(ConditionDrawerCache<ModSettingContext> drawerCache, ImSharp.ImNodes.Node node)
        {
            var actualSize = GetActualSize(drawerCache);
            DrawOutputConnector(node, actualSize, Output);
            var ret = false;

            var combo = ((ModGroupConditionCache)drawerCache)._conditionCombo;
            using (node.TitleBar())
            {
                if (combo.Draw("##combo"u8, drawerCache.Context.Object!, actualSize.X, condition, out var newCondition))
                {
                    Setter(newCondition);
                    ret = true;
                }
            }

            var buttonSize = new Vector2(MathF.Round(actualSize.X / (Parent is ParentConditionType.Not ? 3 : 4)), drawerCache.ButtonSize.Y - ImNodes.Style.NodeBorderThickness);
            Im.Cursor.X += ImNodes.Style.NodeBorderThickness;
            return ret | DefaultButtons(drawerCache, node, buttonSize);
        }
    }
}
