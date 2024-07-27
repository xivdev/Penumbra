using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileEditor<MtrlTab> _materialTab;

    private bool DrawMaterialPanel(MtrlTab tab, bool disabled)
    {
        DrawVersionUpdate(tab, disabled);
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

    private void DrawVersionUpdate(MtrlTab tab, bool disabled)
    {
        if (disabled || tab.Mtrl.IsDawnTrail)
            return;

        if (!ImUtf8.ButtonEx("Update MTRL Version to Dawntrail"u8,
                "Try using this if the material can not be loaded or should use legacy shaders.\n\nThis is not revertible."u8,
                new Vector2(-0.1f, 0), false, 0, Colors.PressEnterWarningBg))
            return;

        tab.Mtrl.MigrateToDawntrail();
        _materialTab.SaveFile();
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
            using (ImRaii.PushFont(UiBuilder.MonoFont, monoFont))
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
            Widget.DrawHexViewer(file.AdditionalData);
    }
}
