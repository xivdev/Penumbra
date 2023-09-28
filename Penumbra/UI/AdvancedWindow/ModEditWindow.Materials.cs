using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileEditor<MtrlTab> _materialTab;

    private bool DrawMaterialPanel(MtrlTab tab, bool disabled)
    {
        DrawMaterialLivePreviewRebind(tab, disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        var ret = DrawBackFaceAndTransparency(tab, disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        ret |= DrawMaterialShader(tab, disabled);

        ret |= DrawMaterialTextureChange(tab, disabled);
        ret |= DrawMaterialColorTableChange(tab, disabled);
        ret |= DrawMaterialConstants(tab, disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawOtherMaterialDetails(tab.Mtrl, disabled);

        return !disabled && ret;
    }

    private static void DrawMaterialLivePreviewRebind(MtrlTab tab, bool disabled)
    {
        if (disabled)
            return;

        if (ImGui.Button("Reload live preview"))
            tab.BindToMaterialInstances();

        if (tab.MaterialPreviewers.Count != 0 || tab.ColorTablePreviewers.Count != 0)
            return;

        ImGui.SameLine();
        using var c = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
        ImGui.TextUnformatted(
            "The current material has not been found on your character. Please check the Import from Screen tab for more information.");
    }

    private static bool DrawMaterialTextureChange(MtrlTab tab, bool disabled)
    {
        if (tab.Textures.Count == 0)
            return false;

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        if (!ImGui.CollapsingHeader("Textures and Samplers", ImGuiTreeNodeFlags.DefaultOpen))
            return false;

        var       frameHeight = ImGui.GetFrameHeight();
        var       ret         = false;
        using var table       = ImRaii.Table("##Textures", 3);

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, frameHeight);
        ImGui.TableSetupColumn("Path",       ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Name",       ImGuiTableColumnFlags.WidthFixed, tab.TextureLabelWidth * UiHelpers.Scale);
        foreach (var (label, textureI, samplerI, description, monoFont) in tab.Textures)
        {
            using var _        = ImRaii.PushId(samplerI);
            var       tmp      = tab.Mtrl.Textures[textureI].Path;
            var       unfolded = tab.UnfoldedTextures.Contains(samplerI);
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton((unfolded ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString(),
                    new Vector2(frameHeight),
                    "Settings for this texture and the associated sampler", false, true))
            {
                unfolded = !unfolded;
                if (unfolded)
                    tab.UnfoldedTextures.Add(samplerI);
                else
                    tab.UnfoldedTextures.Remove(samplerI);
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText(string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                    disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)
             && tmp.Length > 0
             && tmp != tab.Mtrl.Textures[textureI].Path)
            {
                ret                              = true;
                tab.Mtrl.Textures[textureI].Path = tmp;
            }

            ImGui.TableNextColumn();
            using (var font = ImRaii.PushFont(UiBuilder.MonoFont, monoFont))
            {
                ImGui.AlignTextToFramePadding();
                if (description.Length > 0)
                    ImGuiUtil.LabeledHelpMarker(label, description);
                else
                    ImGui.TextUnformatted(label);
            }

            if (unfolded)
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ret |= DrawMaterialSampler(tab, disabled, textureI, samplerI);
                ImGui.TableNextColumn();
            }
        }

        return ret;
    }

    private static bool DrawBackFaceAndTransparency(MtrlTab tab, bool disabled)
    {
        const uint transparencyBit = 0x10;
        const uint backfaceBit     = 0x01;

        var ret = false;

        using var dis = ImRaii.Disabled(disabled);

        var tmp = (tab.Mtrl.ShaderPackage.Flags & transparencyBit) != 0;
        if (ImGui.Checkbox("Enable Transparency", ref tmp))
        {
            tab.Mtrl.ShaderPackage.Flags =
                tmp ? tab.Mtrl.ShaderPackage.Flags | transparencyBit : tab.Mtrl.ShaderPackage.Flags & ~transparencyBit;
            ret = true;
            tab.SetShaderPackageFlags(tab.Mtrl.ShaderPackage.Flags);
        }

        ImGui.SameLine(200 * UiHelpers.Scale + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X);
        tmp = (tab.Mtrl.ShaderPackage.Flags & backfaceBit) != 0;
        if (ImGui.Checkbox("Hide Backfaces", ref tmp))
        {
            tab.Mtrl.ShaderPackage.Flags = tmp ? tab.Mtrl.ShaderPackage.Flags | backfaceBit : tab.Mtrl.ShaderPackage.Flags & ~backfaceBit;
            ret                          = true;
            tab.SetShaderPackageFlags(tab.Mtrl.ShaderPackage.Flags);
        }

        return ret;
    }

    private static void DrawOtherMaterialDetails(MtrlFile file, bool _)
    {
        if (!ImGui.CollapsingHeader("Further Content"))
            return;

        using (var sets = ImRaii.TreeNode("UV Sets", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (sets)
                foreach (var set in file.UvSets)
                    ImRaii.TreeNode($"#{set.Index:D2} - {set.Name}", ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var sets = ImRaii.TreeNode("Color Sets", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (sets)
                foreach (var set in file.ColorSets)
                    ImRaii.TreeNode($"#{set.Index:D2} - {set.Name}", ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        if (file.AdditionalData.Length <= 0)
            return;

        using var t = ImRaii.TreeNode($"Additional Data (Size: {file.AdditionalData.Length})###AdditionalData");
        if (t)
            ImGuiUtil.TextWrapped(string.Join(' ', file.AdditionalData.Select(c => $"{c:X2}")));
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
            ImGui.TextUnformatted(info.Path.FullName[(_mod!.ModPath.FullName.Length + 1)..]);
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
