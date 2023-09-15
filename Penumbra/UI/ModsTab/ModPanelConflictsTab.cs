using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections.Manager;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelConflictsTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly CollectionManager _collectionManager;

    public ModPanelConflictsTab(CollectionManager collectionManager, ModFileSystemSelector selector)
    {
        _collectionManager = collectionManager;
        _selector          = selector;
    }

    public ReadOnlySpan<byte> Label
        => "Conflicts"u8;

    public bool IsVisible
        => _collectionManager.Active.Current.Conflicts(_selector.Selected!).Count > 0;

    public void DrawContent()
    {
        using var box = ImRaii.ListBox("##conflicts", -Vector2.One);
        if (!box)
            return;

        // Can not be null because otherwise the tab bar is never drawn.
        var mod = _selector.Selected!;
        foreach (var conflict in _collectionManager.Active.Current.Conflicts(mod))
        {
            if (ImGui.Selectable(conflict.Mod2.Name) && conflict.Mod2 is Mod otherMod)
                _selector.SelectByValue(otherMod);

            ImGui.SameLine();
            using (var color = ImRaii.PushColor(ImGuiCol.Text,
                       conflict.HasPriority ? ColorId.HandledConflictMod.Value() : ColorId.ConflictingMod.Value()))
            {
                var priority = conflict.Mod2.Index < 0
                    ? conflict.Mod2.Priority
                    : _collectionManager.Active.Current[conflict.Mod2.Index].Settings!.Priority;
                ImGui.TextUnformatted($"(Priority {priority})");
            }

            using var indent = ImRaii.PushIndent(30f);
            foreach (var data in conflict.Conflicts)
            {
                unsafe
                {
                    var _ = data switch
                    {
                        Utf8GamePath p     => ImGuiNative.igSelectable_Bool(p.Path.Path, 0, ImGuiSelectableFlags.None, Vector2.Zero) > 0,
                        MetaManipulation m => ImGui.Selectable(m.Manipulation?.ToString() ?? string.Empty),
                        _                  => false,
                    };
                }
            }
        }
    }
}
