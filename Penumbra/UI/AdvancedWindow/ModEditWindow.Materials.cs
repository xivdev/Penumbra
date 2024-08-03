using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.UI.AdvancedWindow.Materials;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileEditor<MtrlTab> _materialTab;

    private bool DrawMaterialPanel(MtrlTab tab, bool disabled)
    {
        if (tab.DrawVersionUpdate(disabled))
            _materialTab.SaveFile();

        return tab.DrawPanel(disabled);
    }

    private void DrawMaterialReassignmentTab()
    {
        if (_editor.Files.Mdl.Count == 0)
            return;

        using var tab = ImRaii.TabItem("Material Reassignment");
        if (!tab)
            return;

        ImGui.NewLine();
        MaterialSuffix.Draw(_editor, ImGuiHelpers.ScaledVector2(175, 0));

        ImGui.NewLine();
        using var child = ImRaii.Child("##mdlFiles", -Vector2.One, true);
        if (!child)
            return;

        using var table = ImRaii.Table("##files", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.One);
        if (!table)
            return;

        var iconSize = ImGui.GetFrameHeight() * Vector2.One;
        foreach (var (info, idx) in _editor.MdlMaterialEditor.ModelFiles.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Save.ToIconString(), iconSize,
                    "Save the changed mdl file.\nUse at own risk!", !info.Changed, true))
                info.Save(_editor.Compactor);

            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Recycle.ToIconString(), iconSize,
                    "Restore current changes to default.", !info.Changed, true))
                info.Restore();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(info.Path.FullName[(Mod!.ModPath.FullName.Length + 1)..]);
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(400 * UiHelpers.Scale);
            var tmp = info.CurrentMaterials[0];
            if (ImGui.InputText("##0", ref tmp, 64))
                info.SetMaterial(tmp, 0);

            for (var i = 1; i < info.Count; ++i)
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(400 * UiHelpers.Scale);
                tmp = info.CurrentMaterials[i];
                if (ImGui.InputText($"##{i}", ref tmp, 64))
                    info.SetMaterial(tmp, i);
            }
        }
    }
}
