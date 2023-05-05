using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;
using SixLabors.ImageSharp.ColorSpaces;

namespace Penumbra.UI.AdvancedWindow;

public class ModMergeTab
{
    private readonly ModMerger _modMerger;
    private readonly ModCombo  _modCombo;

    private string _newModName = string.Empty;

    public ModMergeTab(ModMerger modMerger)
    {
        _modMerger = modMerger;
        _modCombo  = new ModCombo(() => _modMerger.ModsWithoutCurrent.ToList());
    }

    public void Draw()
    {
        if (_modMerger.MergeFromMod == null)
            return;

        using var tab = ImRaii.TabItem("Merge Mods");
        if (!tab)
            return;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"Merge {_modMerger.MergeFromMod.Name} into ");
        ImGui.SameLine();
        DrawCombo();

        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##optionGroupInput", "Target Option Group", ref _modMerger.OptionGroupName, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##optionInput", "Target Option Name", ref _modMerger.OptionName, 64);

        if (ImGuiUtil.DrawDisabledButton("Merge", Vector2.Zero, string.Empty, !_modMerger.CanMerge))
            _modMerger.Merge();

        ImGui.Dummy(Vector2.One);
        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        using (var table = ImRaii.Table("##options", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail() with { Y = 6 * ImGui.GetFrameHeightWithSpacing()}))
        {
            foreach (var (option, idx) in _modMerger.MergeFromMod.AllSubMods.WithIndex())
            {
                using var id       = ImRaii.PushId(idx);
                var       selected = _modMerger.SelectedOptions.Contains(option);
                ImGui.TableNextColumn();
                if (ImGui.Checkbox("##check", ref selected))
                {
                    if (selected)
                        _modMerger.SelectedOptions.Add(option);
                    else
                        _modMerger.SelectedOptions.Remove(option);
                }

                if (option.IsDefault)
                {
                    ImGuiUtil.DrawTableColumn(option.FullName);
                    ImGui.TableNextColumn();
                }
                else
                {
                    ImGuiUtil.DrawTableColumn(option.ParentMod.Groups[option.GroupIdx].Name);
                    ImGuiUtil.DrawTableColumn(option.Name);
                }

                ImGuiUtil.DrawTableColumn(option.FileData.Count.ToString());
                ImGuiUtil.DrawTableColumn(option.FileSwapData.Count.ToString());
                ImGuiUtil.DrawTableColumn(option.Manipulations.Count.ToString());

            }
        }
        ImGui.InputTextWithHint("##newModInput", "New Mod Name...", ref _newModName, 64);
        if (ImGuiUtil.DrawDisabledButton("Split Off", Vector2.Zero, string.Empty, _newModName.Length == 0 || _modMerger.SelectedOptions.Count == 0))
            _modMerger.SplitIntoMod(_newModName);

        if (_modMerger.Error != null)
        {
            ImGui.Separator();
            ImGui.Dummy(Vector2.One);
            using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
            ImGuiUtil.TextWrapped(_modMerger.Error.ToString());
        }
    }

    private void DrawCombo()
    {
        _modCombo.Draw("##ModSelection", _modCombo.CurrentSelection?.Name.Text ?? string.Empty, string.Empty,
            200 * ImGuiHelpers.GlobalScale, ImGui.GetTextLineHeight());
        _modMerger.MergeToMod = _modCombo.CurrentSelection;
    }
}
