using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.UI.AdvancedWindow.Meta;

public sealed class GlobalEqpMetaDrawer(ModMetaEditor editor, MetaFileManager metaFiles)
    : MetaDrawer<GlobalEqpManipulation, byte>(editor, metaFiles), IService
{
    public override ReadOnlySpan<byte> Label
        => "Global Equipment Parameter Edits (Global EQP)###GEQP"u8;

    public override int NumColumns
        => 4;

    protected override void Initialize()
    {
        Identifier = new GlobalEqpManipulation()
        {
            Condition = 1,
            Type      = GlobalEqpType.DoNotHideEarrings,
        };
    }

    protected override void DrawNew()
    {
        ImGui.TableNextColumn();
        CopyToClipboardButton("Copy all current global EQP manipulations to clipboard."u8, MetaDictionary.SerializeTo([], Editor.GlobalEqp));

        ImGui.TableNextColumn();
        var canAdd = !Editor.Contains(Identifier);
        var tt     = canAdd ? "Stage this edit."u8 : "This entry is already edited."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, disabled: !canAdd))
            Editor.Changes |= Editor.TryAdd(Identifier);

        DrawIdentifierInput(ref Identifier);
    }

    protected override void DrawEntry(GlobalEqpManipulation identifier, byte _)
    {
        DrawMetaButtons(identifier, 0);
        DrawIdentifier(identifier);
    }

    protected override IEnumerable<(GlobalEqpManipulation, byte)> Enumerate()
        => Editor.GlobalEqp
            .OrderBy(identifier => identifier.Type)
            .ThenBy(identifier => identifier.Condition.Id)
            .Select(identifier => (identifier, (byte)0));

    protected override int Count
        => Editor.GlobalEqp.Count;

    private static void DrawIdentifierInput(ref GlobalEqpManipulation identifier)
    {
        ImGui.TableNextColumn();
        DrawType(ref identifier);

        ImGui.TableNextColumn();
        if (identifier.Type.HasCondition())
            DrawCondition(ref identifier);
        else
            ImUtf8.ScaledDummy(100);
    }

    private static void DrawIdentifier(GlobalEqpManipulation identifier)
    {
        ImGui.TableNextColumn();
        ImUtf8.TextFramed(identifier.Type.ToName(), FrameColor);
        ImUtf8.HoverTooltip("Global EQP Type"u8);

        ImGui.TableNextColumn();
        if (identifier.Type.HasCondition())
        {
            ImUtf8.TextFramed($"{identifier.Condition.Id}", FrameColor);
            ImUtf8.HoverTooltip("Conditional Model ID"u8);
        }
    }

    public static bool DrawType(ref GlobalEqpManipulation identifier, float unscaledWidth = 250)
    {
        ImGui.SetNextItemWidth(unscaledWidth * ImUtf8.GlobalScale);
        using var combo = ImUtf8.Combo("##geqpType"u8, identifier.Type.ToName());
        if (!combo)
            return false;

        var ret = false;
        foreach (var type in Enum.GetValues<GlobalEqpType>())
        {
            if (ImUtf8.Selectable(type.ToName(), type == identifier.Type))
            {
                identifier = new GlobalEqpManipulation
                {
                    Type      = type,
                    Condition = type.HasCondition() ? identifier.Type.HasCondition() ? identifier.Condition : 1 : 0,
                };
                ret = true;
            }

            ImUtf8.HoverTooltip(type.ToDescription());
        }

        return ret;
    }

    public static void DrawCondition(ref GlobalEqpManipulation identifier, float unscaledWidth = 100)
    {
        if (IdInput("##geqpCond"u8, unscaledWidth, identifier.Condition.Id, out var newId, 1, ushort.MaxValue,
                identifier.Condition.Id <= 1))
            identifier = identifier with { Condition = newId };
        ImUtf8.HoverTooltip("The Model ID for the item that should not be hidden."u8);
    }
}
