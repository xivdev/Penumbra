using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.String.Functions;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private static readonly float HalfMinValue = (float)Half.MinValue;
    private static readonly float HalfMaxValue = (float)Half.MaxValue;
    private static readonly float HalfEpsilon  = (float)Half.Epsilon;

    private bool DrawMaterialColorTableChange(MtrlTab tab, bool disabled)
    {
        if (!tab.SamplerIds.Contains(ShpkFile.TableSamplerId) || !tab.Mtrl.HasTable)
            return false;

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        if (!ImGui.CollapsingHeader("Color Table", ImGuiTreeNodeFlags.DefaultOpen))
            return false;

        ColorTableCopyAllClipboardButton(tab.Mtrl);
        ImGui.SameLine();
        var ret = ColorTablePasteAllClipboardButton(tab, disabled);
        if (!disabled)
        {
            ImGui.SameLine();
            ImGui.Dummy(ImGuiHelpers.ScaledVector2(20, 0));
            ImGui.SameLine();
            ret |= ColorTableDyeableCheckbox(tab);
        }

        var hasDyeTable = tab.Mtrl.HasDyeTable;
        if (hasDyeTable)
        {
            ImGui.SameLine();
            ImGui.Dummy(ImGuiHelpers.ScaledVector2(20, 0));
            ImGui.SameLine();
            ret |= DrawPreviewDye(tab, disabled);
        }

        using var table = ImRaii.Table("##ColorTable", hasDyeTable ? 11 : 9,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV);
        if (!table)
            return false;

        ImGui.TableNextColumn();
        ImGui.TableHeader(string.Empty);
        ImGui.TableNextColumn();
        ImGui.TableHeader("Row");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Diffuse");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Specular");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Emissive");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Gloss");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Tile");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Repeat");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Skew");
        if (hasDyeTable)
        {
            ImGui.TableNextColumn();
            ImGui.TableHeader("Dye");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Dye Preview");
        }

        for (var i = 0; i < MtrlFile.ColorTable.NumRows; ++i)
        {
            ret |= DrawColorTableRow(tab, i, disabled);
            ImGui.TableNextRow();
        }

        return ret;
    }


    private static void ColorTableCopyAllClipboardButton(MtrlFile file)
    {
        if (!ImGui.Button("Export All Rows to Clipboard", ImGuiHelpers.ScaledVector2(200, 0)))
            return;

        try
        {
            var data1 = file.Table.AsBytes();
            var data2 = file.HasDyeTable ? file.DyeTable.AsBytes() : ReadOnlySpan<byte>.Empty;
            var array = new byte[data1.Length + data2.Length];
            data1.TryCopyTo(array);
            data2.TryCopyTo(array.AsSpan(data1.Length));
            var text = Convert.ToBase64String(array);
            ImGui.SetClipboardText(text);
        }
        catch
        {
            // ignored
        }
    }

    private bool DrawPreviewDye(MtrlTab tab, bool disabled)
    {
        var (dyeId, (name, dyeColor, gloss)) = _stainService.StainCombo.CurrentSelection;
        var tt = dyeId == 0
            ? "Select a preview dye first."
            : "Apply all preview values corresponding to the dye template and chosen dye where dyeing is enabled.";
        if (ImGuiUtil.DrawDisabledButton("Apply Preview Dye", Vector2.Zero, tt, disabled || dyeId == 0))
        {
            var ret = false;
            if (tab.Mtrl.HasDyeTable)
            {
                for (var i = 0; i < MtrlFile.ColorTable.NumRows; ++i)
                    ret |= tab.Mtrl.ApplyDyeTemplate(_stainService.StmFile, i, dyeId);
            }

            tab.UpdateColorTablePreview();

            return ret;
        }

        ImGui.SameLine();
        var label = dyeId == 0 ? "Preview Dye###previewDye" : $"{name} (Preview)###previewDye";
        if (_stainService.StainCombo.Draw(label, dyeColor, string.Empty, true, gloss))
            tab.UpdateColorTablePreview();
        return false;
    }

    private static unsafe bool ColorTablePasteAllClipboardButton(MtrlTab tab, bool disabled)
    {
        if (!ImGuiUtil.DrawDisabledButton("Import All Rows from Clipboard", ImGuiHelpers.ScaledVector2(200, 0), string.Empty, disabled)
         || !tab.Mtrl.HasTable)
            return false;

        try
        {
            var text = ImGui.GetClipboardText();
            var data = Convert.FromBase64String(text);
            if (data.Length < Marshal.SizeOf<MtrlFile.ColorTable>())
                return false;

            ref var rows = ref tab.Mtrl.Table;
            fixed (void* ptr = data, output = &rows)
            {
                MemoryUtility.MemCpyUnchecked(output, ptr, Marshal.SizeOf<MtrlFile.ColorTable>());
                if (data.Length >= Marshal.SizeOf<MtrlFile.ColorTable>() + Marshal.SizeOf<MtrlFile.ColorDyeTable>()
                 && tab.Mtrl.HasDyeTable)
                {
                    ref var dyeRows = ref tab.Mtrl.DyeTable;
                    fixed (void* output2 = &dyeRows)
                    {
                        MemoryUtility.MemCpyUnchecked(output2, (byte*)ptr + Marshal.SizeOf<MtrlFile.ColorTable>(),
                            Marshal.SizeOf<MtrlFile.ColorDyeTable>());
                    }
                }
            }

            tab.UpdateColorTablePreview();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static unsafe void ColorTableCopyClipboardButton(MtrlFile.ColorTable.Row row, MtrlFile.ColorDyeTable.Row dye)
    {
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
                "Export this row to your clipboard.", false, true))
            return;

        try
        {
            var data = new byte[MtrlFile.ColorTable.Row.Size + 2];
            fixed (byte* ptr = data)
            {
                MemoryUtility.MemCpyUnchecked(ptr,                                &row, MtrlFile.ColorTable.Row.Size);
                MemoryUtility.MemCpyUnchecked(ptr + MtrlFile.ColorTable.Row.Size, &dye, 2);
            }

            var text = Convert.ToBase64String(data);
            ImGui.SetClipboardText(text);
        }
        catch
        {
            // ignored
        }
    }

    private static bool ColorTableDyeableCheckbox(MtrlTab tab)
    {
        var dyeable = tab.Mtrl.HasDyeTable;
        var ret     = ImGui.Checkbox("Dyeable", ref dyeable);

        if (ret)
        {
            tab.Mtrl.HasDyeTable = dyeable;
            tab.UpdateColorTablePreview();
        }

        return ret;
    }

    private static unsafe bool ColorTablePasteFromClipboardButton(MtrlTab tab, int rowIdx, bool disabled)
    {
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Paste.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
                "Import an exported row from your clipboard onto this row.", disabled, true))
            return false;

        try
        {
            var text = ImGui.GetClipboardText();
            var data = Convert.FromBase64String(text);
            if (data.Length != MtrlFile.ColorTable.Row.Size + 2
             || !tab.Mtrl.HasTable)
                return false;

            fixed (byte* ptr = data)
            {
                tab.Mtrl.Table[rowIdx] = *(MtrlFile.ColorTable.Row*)ptr;
                if (tab.Mtrl.HasDyeTable)
                    tab.Mtrl.DyeTable[rowIdx] = *(MtrlFile.ColorDyeTable.Row*)(ptr + MtrlFile.ColorTable.Row.Size);
            }

            tab.UpdateColorTableRowPreview(rowIdx);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ColorTableHighlightButton(MtrlTab tab, int rowIdx, bool disabled)
    {
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Crosshairs.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
            "Highlight this row on your character, if possible.", disabled || tab.ColorTablePreviewers.Count == 0, true);

        if (ImGui.IsItemHovered())
            tab.HighlightColorTableRow(rowIdx);
        else if (tab.HighlightedColorTableRow == rowIdx)
            tab.CancelColorTableHighlight();
    }

    private bool DrawColorTableRow(MtrlTab tab, int rowIdx, bool disabled)
    {
        static bool FixFloat(ref float val, float current)
        {
            val = (float)(Half)val;
            return val != current;
        }

        using var id        = ImRaii.PushId(rowIdx);
        ref   var row       = ref tab.Mtrl.Table[rowIdx];
        var       hasDye    = tab.Mtrl.HasDyeTable;
        ref   var dye       = ref tab.Mtrl.DyeTable[rowIdx];
        var       floatSize = 70 * UiHelpers.Scale;
        var       intSize   = 45 * UiHelpers.Scale;
        ImGui.TableNextColumn();
        ColorTableCopyClipboardButton(row, dye);
        ImGui.SameLine();
        var ret = ColorTablePasteFromClipboardButton(tab, rowIdx, disabled);
        ImGui.SameLine();
        ColorTableHighlightButton(tab, rowIdx, disabled);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"#{rowIdx + 1:D2}");

        ImGui.TableNextColumn();
        using var dis = ImRaii.Disabled(disabled);
        ret |= ColorPicker("##Diffuse", "Diffuse Color", row.Diffuse, c =>
        {
            tab.Mtrl.Table[rowIdx].Diffuse = c;
            tab.UpdateColorTableRowPreview(rowIdx);
        });
        if (hasDye)
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox("##dyeDiffuse", "Apply Diffuse Color on Dye", dye.Diffuse,
                b =>
                {
                    tab.Mtrl.DyeTable[rowIdx].Diffuse = b;
                    tab.UpdateColorTableRowPreview(rowIdx);
                }, ImGuiHoveredFlags.AllowWhenDisabled);
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker("##Specular", "Specular Color", row.Specular, c =>
        {
            tab.Mtrl.Table[rowIdx].Specular = c;
            tab.UpdateColorTableRowPreview(rowIdx);
        });
        ImGui.SameLine();
        var tmpFloat = row.SpecularStrength;
        ImGui.SetNextItemWidth(floatSize);
        if (ImGui.DragFloat("##SpecularStrength", ref tmpFloat, 0.01f, 0f, HalfMaxValue, "%.2f") && FixFloat(ref tmpFloat, row.SpecularStrength))
        {
            row.SpecularStrength = tmpFloat;
            ret                  = true;
            tab.UpdateColorTableRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip("Specular Strength", ImGuiHoveredFlags.AllowWhenDisabled);

        if (hasDye)
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox("##dyeSpecular", "Apply Specular Color on Dye", dye.Specular,
                b =>
                {
                    tab.Mtrl.DyeTable[rowIdx].Specular = b;
                    tab.UpdateColorTableRowPreview(rowIdx);
                }, ImGuiHoveredFlags.AllowWhenDisabled);
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox("##dyeSpecularStrength", "Apply Specular Strength on Dye", dye.SpecularStrength,
                b =>
                {
                    tab.Mtrl.DyeTable[rowIdx].SpecularStrength = b;
                    tab.UpdateColorTableRowPreview(rowIdx);
                }, ImGuiHoveredFlags.AllowWhenDisabled);
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker("##Emissive", "Emissive Color", row.Emissive, c =>
        {
            tab.Mtrl.Table[rowIdx].Emissive = c;
            tab.UpdateColorTableRowPreview(rowIdx);
        });
        if (hasDye)
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox("##dyeEmissive", "Apply Emissive Color on Dye", dye.Emissive,
                b =>
                {
                    tab.Mtrl.DyeTable[rowIdx].Emissive = b;
                    tab.UpdateColorTableRowPreview(rowIdx);
                }, ImGuiHoveredFlags.AllowWhenDisabled);
        }

        ImGui.TableNextColumn();
        tmpFloat = row.GlossStrength;
        ImGui.SetNextItemWidth(floatSize);
        if (ImGui.DragFloat("##GlossStrength", ref tmpFloat, Math.Max(0.1f, tmpFloat * 0.025f), HalfEpsilon, HalfMaxValue, "%.1f")
         && FixFloat(ref tmpFloat, row.GlossStrength))
        {
            row.GlossStrength = Math.Max(tmpFloat, HalfEpsilon);
            ret               = true;
            tab.UpdateColorTableRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip("Gloss Strength", ImGuiHoveredFlags.AllowWhenDisabled);
        if (hasDye)
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox("##dyeGloss", "Apply Gloss Strength on Dye", dye.Gloss,
                b =>
                {
                    tab.Mtrl.DyeTable[rowIdx].Gloss = b;
                    tab.UpdateColorTableRowPreview(rowIdx);
                }, ImGuiHoveredFlags.AllowWhenDisabled);
        }

        ImGui.TableNextColumn();
        int tmpInt = row.TileSet;
        ImGui.SetNextItemWidth(intSize);
        if (ImGui.DragInt("##TileSet", ref tmpInt, 0.25f, 0, 63) && tmpInt != row.TileSet && tmpInt is >= 0 and <= ushort.MaxValue)
        {
            row.TileSet = (ushort)Math.Clamp(tmpInt, 0, 63);
            ret         = true;
            tab.UpdateColorTableRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip("Tile Set", ImGuiHoveredFlags.AllowWhenDisabled);

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialRepeat.X;
        ImGui.SetNextItemWidth(floatSize);
        if (ImGui.DragFloat("##RepeatX", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f")
         && FixFloat(ref tmpFloat, row.MaterialRepeat.X))
        {
            row.MaterialRepeat = row.MaterialRepeat with { X = tmpFloat };
            ret                = true;
            tab.UpdateColorTableRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip("Repeat X", ImGuiHoveredFlags.AllowWhenDisabled);
        ImGui.SameLine();
        tmpFloat = row.MaterialRepeat.Y;
        ImGui.SetNextItemWidth(floatSize);
        if (ImGui.DragFloat("##RepeatY", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f")
         && FixFloat(ref tmpFloat, row.MaterialRepeat.Y))
        {
            row.MaterialRepeat = row.MaterialRepeat with { Y = tmpFloat };
            ret                = true;
            tab.UpdateColorTableRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip("Repeat Y", ImGuiHoveredFlags.AllowWhenDisabled);

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialSkew.X;
        ImGui.SetNextItemWidth(floatSize);
        if (ImGui.DragFloat("##SkewX", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f") && FixFloat(ref tmpFloat, row.MaterialSkew.X))
        {
            row.MaterialSkew = row.MaterialSkew with { X = tmpFloat };
            ret              = true;
            tab.UpdateColorTableRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip("Skew X", ImGuiHoveredFlags.AllowWhenDisabled);

        ImGui.SameLine();
        tmpFloat = row.MaterialSkew.Y;
        ImGui.SetNextItemWidth(floatSize);
        if (ImGui.DragFloat("##SkewY", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f") && FixFloat(ref tmpFloat, row.MaterialSkew.Y))
        {
            row.MaterialSkew = row.MaterialSkew with { Y = tmpFloat };
            ret              = true;
            tab.UpdateColorTableRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip("Skew Y", ImGuiHoveredFlags.AllowWhenDisabled);

        if (hasDye)
        {
            ImGui.TableNextColumn();
            if (_stainService.TemplateCombo.Draw("##dyeTemplate", dye.Template.ToString(), string.Empty, intSize
                  + ImGui.GetStyle().ScrollbarSize / 2, ImGui.GetTextLineHeightWithSpacing(), ImGuiComboFlags.NoArrowButton))
            {
                dye.Template = _stainService.TemplateCombo.CurrentSelection;
                ret          = true;
                tab.UpdateColorTableRowPreview(rowIdx);
            }

            ImGuiUtil.HoverTooltip("Dye Template", ImGuiHoveredFlags.AllowWhenDisabled);

            ImGui.TableNextColumn();
            ret |= DrawDyePreview(tab, rowIdx, disabled, dye, floatSize);
        }


        return ret;
    }

    private bool DrawDyePreview(MtrlTab tab, int rowIdx, bool disabled, MtrlFile.ColorDyeTable.Row dye, float floatSize)
    {
        var stain = _stainService.StainCombo.CurrentSelection.Key;
        if (stain == 0 || !_stainService.StmFile.Entries.TryGetValue(dye.Template, out var entry))
            return false;

        var       values = entry[(int)stain];
        using var style  = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2);

        var ret = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
            "Apply the selected dye to this row.", disabled, true);

        ret = ret && tab.Mtrl.ApplyDyeTemplate(_stainService.StmFile, rowIdx, stain);
        if (ret)
            tab.UpdateColorTableRowPreview(rowIdx);

        ImGui.SameLine();
        ColorPicker("##diffusePreview", string.Empty, values.Diffuse, _ => { }, "D");
        ImGui.SameLine();
        ColorPicker("##specularPreview", string.Empty, values.Specular, _ => { }, "S");
        ImGui.SameLine();
        ColorPicker("##emissivePreview", string.Empty, values.Emissive, _ => { }, "E");
        ImGui.SameLine();
        using var dis = ImRaii.Disabled();
        ImGui.SetNextItemWidth(floatSize);
        ImGui.DragFloat("##gloss", ref values.Gloss, 0, values.Gloss, values.Gloss, "%.1f G");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(floatSize);
        ImGui.DragFloat("##specularStrength", ref values.SpecularPower, 0, values.SpecularPower, values.SpecularPower, "%.2f S");

        return ret;
    }

    private static bool ColorPicker(string label, string tooltip, Vector3 input, Action<Vector3> setter, string letter = "")
    {
        var ret       = false;
        var inputSqrt = PseudoSqrtRgb(input);
        var tmp       = inputSqrt;
        if (ImGui.ColorEdit3(label, ref tmp,
                ImGuiColorEditFlags.NoInputs
              | ImGuiColorEditFlags.DisplayRGB
              | ImGuiColorEditFlags.InputRGB
              | ImGuiColorEditFlags.NoTooltip
              | ImGuiColorEditFlags.HDR)
         && tmp != inputSqrt)
        {
            setter(PseudoSquareRgb(tmp));
            ret = true;
        }

        if (letter.Length > 0 && ImGui.IsItemVisible())
        {
            var textSize  = ImGui.CalcTextSize(letter);
            var center    = ImGui.GetItemRectMin() + (ImGui.GetItemRectSize() - textSize) / 2;
            var textColor = input.LengthSquared() < 0.25f ? 0x80FFFFFFu : 0x80000000u;
            ImGui.GetWindowDrawList().AddText(center, textColor, letter);
        }

        ImGuiUtil.HoverTooltip(tooltip, ImGuiHoveredFlags.AllowWhenDisabled);

        return ret;
    }

    // Functions to deal with squared RGB values without making negatives useless.

    private static float PseudoSquareRgb(float x)
        => x < 0.0f ? -(x * x) : x * x;

    private static Vector3 PseudoSquareRgb(Vector3 vec)
        => new(PseudoSquareRgb(vec.X), PseudoSquareRgb(vec.Y), PseudoSquareRgb(vec.Z));

    private static Vector4 PseudoSquareRgb(Vector4 vec)
        => new(PseudoSquareRgb(vec.X), PseudoSquareRgb(vec.Y), PseudoSquareRgb(vec.Z), vec.W);

    private static float PseudoSqrtRgb(float x)
        => x < 0.0f ? -MathF.Sqrt(-x) : MathF.Sqrt(x);

    private static Vector3 PseudoSqrtRgb(Vector3 vec)
        => new(PseudoSqrtRgb(vec.X), PseudoSqrtRgb(vec.Y), PseudoSqrtRgb(vec.Z));

    private static Vector4 PseudoSqrtRgb(Vector4 vec)
        => new(PseudoSqrtRgb(vec.X), PseudoSqrtRgb(vec.Y), PseudoSqrtRgb(vec.Z), vec.W);
}
