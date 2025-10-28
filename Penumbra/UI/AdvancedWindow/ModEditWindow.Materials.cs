using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui.Raii;
using OtterGui.Text;
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

        using var tab = ImUtf8.TabItem("Material Reassignment"u8);
        if (!tab)
            return;

        Im.Line.New();
        MaterialSuffix.Draw(_editor, ImEx.ScaledVector(175, 0));

        Im.Line.New();
        using var child = ImUtf8.Child("##mdlFiles"u8, -Vector2.One, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##files"u8, 4, TableFlags.RowBackground | TableFlags.SizingFixedFit, -Vector2.One);
        if (!table)
            return;

        foreach (var (idx, info) in _editor.MdlMaterialEditor.ModelFiles.Index())
        {
            using var id = ImRaii.PushId(idx);
            ImGui.TableNextColumn();
            if (ImUtf8.IconButton(FontAwesomeIcon.Save, "Save the changed mdl file.\nUse at own risk!"u8, disabled: !info.Changed))
                info.Save(_editor.Compactor);

            ImGui.TableNextColumn();
            if (ImUtf8.IconButton(FontAwesomeIcon.Recycle, "Restore current changes to default."u8, disabled: !info.Changed))
                info.Restore();

            ImGui.TableNextColumn();
            ImUtf8.Text(info.Path.InternalName.Span[(Mod!.ModPath.FullName.Length + 1)..]);
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(400 * Im.Style.GlobalScale);
            var tmp = info.CurrentMaterials[0];
            if (ImUtf8.InputText("##0"u8, ref tmp))
                info.SetMaterial(tmp, 0);

            for (var i = 1; i < info.Count; ++i)
            {
                using var id2 = ImUtf8.PushId(i);
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(400 * Im.Style.GlobalScale);
                tmp = info.CurrentMaterials[i];
                if (ImUtf8.InputText(""u8, ref tmp))
                    info.SetMaterial(tmp, i);
            }
        }
    }
}
