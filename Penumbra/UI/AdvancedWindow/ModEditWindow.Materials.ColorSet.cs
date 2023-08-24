using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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

    private bool DrawMaterialColorSetChange( MtrlTab tab, bool disabled )
    {
        if( !tab.SamplerIds.Contains( ShpkFile.TableSamplerId ) || !tab.Mtrl.ColorSets.Any( c => c.HasRows ) )
        {
            return false;
        }

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        if( !ImGui.CollapsingHeader( "Color Set", ImGuiTreeNodeFlags.DefaultOpen ) )
        {
            return false;
        }

        var hasAnyDye = tab.UseColorDyeSet;

        ColorSetCopyAllClipboardButton( tab.Mtrl, 0 );
        ImGui.SameLine();
        var ret = ColorSetPasteAllClipboardButton( tab, 0, disabled );
        if( !disabled )
        {
            ImGui.SameLine();
            ImGui.Dummy( ImGuiHelpers.ScaledVector2( 20, 0 ) );
            ImGui.SameLine();
            ret |= ColorSetDyeableCheckbox( tab, ref hasAnyDye );
        }
        if( hasAnyDye )
        {
            ImGui.SameLine();
            ImGui.Dummy( ImGuiHelpers.ScaledVector2( 20, 0 ) );
            ImGui.SameLine();
            ret |= DrawPreviewDye( tab, disabled );
        }

        using var table = ImRaii.Table( "##ColorSets", hasAnyDye ? 11 : 9,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV );
        if( !table )
        {
            return false;
        }

        ImGui.TableNextColumn();
        ImGui.TableHeader( string.Empty );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Row" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Diffuse" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Specular" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Emissive" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Gloss" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Tile" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Repeat" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Skew" );
        if( hasAnyDye )
        {
            ImGui.TableNextColumn();
            ImGui.TableHeader("Dye");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Dye Preview");
        }

        for( var j = 0; j < tab.Mtrl.ColorSets.Length; ++j )
        {
            using var _ = ImRaii.PushId( j );
            for( var i = 0; i < MtrlFile.ColorSet.RowArray.NumRows; ++i )
            {
                ret |= DrawColorSetRow( tab, j, i, disabled, hasAnyDye );
                ImGui.TableNextRow();
            }
        }

        return ret;
    }


    private static void ColorSetCopyAllClipboardButton( MtrlFile file, int colorSetIdx )
    {
        if( !ImGui.Button( "Export All Rows to Clipboard", ImGuiHelpers.ScaledVector2( 200, 0 ) ) )
        {
            return;
        }

        try
        {
            var data1 = file.ColorSets[ colorSetIdx ].Rows.AsBytes();
            var data2 = file.ColorDyeSets.Length > colorSetIdx ? file.ColorDyeSets[ colorSetIdx ].Rows.AsBytes() : ReadOnlySpan< byte >.Empty;
            var array = new byte[data1.Length + data2.Length];
            data1.TryCopyTo( array );
            data2.TryCopyTo( array.AsSpan( data1.Length ) );
            var text = Convert.ToBase64String( array );
            ImGui.SetClipboardText( text );
        }
        catch
        {
            // ignored
        }
    }

    private bool DrawPreviewDye( MtrlTab tab, bool disabled )
    {
        var (dyeId, (name, dyeColor, gloss)) = _stainService.StainCombo.CurrentSelection;
        var tt = dyeId == 0 ? "Select a preview dye first." : "Apply all preview values corresponding to the dye template and chosen dye where dyeing is enabled.";
        if( ImGuiUtil.DrawDisabledButton( "Apply Preview Dye", Vector2.Zero, tt, disabled || dyeId == 0 ) )
        {
            var ret = false;
            for( var j = 0; j < tab.Mtrl.ColorDyeSets.Length; ++j )
            {
                for( var i = 0; i < MtrlFile.ColorSet.RowArray.NumRows; ++i )
                {
                    ret |= tab.Mtrl.ApplyDyeTemplate( _stainService.StmFile, j, i, dyeId );
                }
            }

            tab.UpdateColorSetPreview();

            return ret;
        }

        ImGui.SameLine();
        var label = dyeId == 0 ? "Preview Dye###previewDye" : $"{name} (Preview)###previewDye";
        if (_stainService.StainCombo.Draw(label, dyeColor, string.Empty, true, gloss))
            tab.UpdateColorSetPreview();
        return false;
    }

    private static unsafe bool ColorSetPasteAllClipboardButton( MtrlTab tab, int colorSetIdx, bool disabled )
    {
        if( !ImGuiUtil.DrawDisabledButton( "Import All Rows from Clipboard", ImGuiHelpers.ScaledVector2( 200, 0 ), string.Empty, disabled ) || tab.Mtrl.ColorSets.Length <= colorSetIdx )
        {
            return false;
        }

        try
        {
            var text = ImGui.GetClipboardText();
            var data = Convert.FromBase64String( text );
            if( data.Length < Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() )
            {
                return false;
            }

            ref var rows = ref tab.Mtrl.ColorSets[ colorSetIdx ].Rows;
            fixed( void* ptr = data, output = &rows )
            {
                MemoryUtility.MemCpyUnchecked( output, ptr, Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() );
                if( data.Length             >= Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() + Marshal.SizeOf< MtrlFile.ColorDyeSet.RowArray >()
                && tab.Mtrl.ColorDyeSets.Length > colorSetIdx )
                {
                    ref var dyeRows = ref tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows;
                    fixed( void* output2 = &dyeRows )
                    {
                        MemoryUtility.MemCpyUnchecked( output2, ( byte* )ptr + Marshal.SizeOf< MtrlFile.ColorSet.RowArray >(), Marshal.SizeOf< MtrlFile.ColorDyeSet.RowArray >() );
                    }
                }
            }

            tab.UpdateColorSetPreview();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static unsafe void ColorSetCopyClipboardButton( MtrlFile.ColorSet.Row row, MtrlFile.ColorDyeSet.Row dye )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Clipboard.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
               "Export this row to your clipboard.", false, true ) )
        {
            try
            {
                var data = new byte[MtrlFile.ColorSet.Row.Size + 2];
                fixed( byte* ptr = data )
                {
                    MemoryUtility.MemCpyUnchecked( ptr, &row, MtrlFile.ColorSet.Row.Size );
                    MemoryUtility.MemCpyUnchecked( ptr + MtrlFile.ColorSet.Row.Size, &dye, 2 );
                }

                var text = Convert.ToBase64String( data );
                ImGui.SetClipboardText( text );
            }
            catch
            {
                // ignored
            }
        }
    }

    private static bool ColorSetDyeableCheckbox( MtrlTab tab, ref bool dyeable )
    {
        var ret = ImGui.Checkbox( "Dyeable", ref dyeable );

        if( ret )
        {
            tab.UseColorDyeSet = dyeable;
            if( dyeable )
                tab.Mtrl.FindOrAddColorDyeSet();
            tab.UpdateColorSetPreview();
        }

        return ret;
    }

    private static unsafe bool ColorSetPasteFromClipboardButton( MtrlTab tab, int colorSetIdx, int rowIdx, bool disabled )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Paste.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
               "Import an exported row from your clipboard onto this row.", disabled, true ) )
        {
            try
            {
                var text = ImGui.GetClipboardText();
                var data = Convert.FromBase64String( text );
                if( data.Length          != MtrlFile.ColorSet.Row.Size + 2
                || tab.Mtrl.ColorSets.Length <= colorSetIdx )
                {
                    return false;
                }

                fixed( byte* ptr = data )
                {
                    tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ] = *( MtrlFile.ColorSet.Row* )ptr;
                    if( colorSetIdx < tab.Mtrl.ColorDyeSets.Length )
                    {
                        tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ] = *( MtrlFile.ColorDyeSet.Row* )( ptr + MtrlFile.ColorSet.Row.Size );
                    }
                }

                tab.UpdateColorSetRowPreview(rowIdx);

                return true;
            }
            catch
            {
                // ignored
            }
        }

        return false;
    }

    private static void ColorSetHighlightButton( MtrlTab tab, int rowIdx, bool disabled )
    {
        ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Crosshairs.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
               "Highlight this row on your character, if possible.", disabled || tab.ColorSetPreviewers.Count == 0, true );

        if( ImGui.IsItemHovered() )
            tab.HighlightColorSetRow( rowIdx );
        else if( tab.HighlightedColorSetRow == rowIdx )
            tab.CancelColorSetHighlight();
    }

    private bool DrawColorSetRow( MtrlTab tab, int colorSetIdx, int rowIdx, bool disabled, bool hasAnyDye )
    {
        static bool FixFloat( ref float val, float current )
        {
            val = ( float )( Half )val;
            return val != current;
        }

        using var id        = ImRaii.PushId( rowIdx );
        var       row       = tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ];
        var       hasDye    = hasAnyDye && tab.Mtrl.ColorDyeSets.Length > colorSetIdx;
        var       dye       = hasDye ? tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ] : new MtrlFile.ColorDyeSet.Row();
        var       floatSize = 70 * UiHelpers.Scale;
        var       intSize   = 45 * UiHelpers.Scale;
        ImGui.TableNextColumn();
        ColorSetCopyClipboardButton( row, dye );
        ImGui.SameLine();
        var ret = ColorSetPasteFromClipboardButton( tab, colorSetIdx, rowIdx, disabled );
        ImGui.SameLine();
        ColorSetHighlightButton( tab, rowIdx, disabled );

        ImGui.TableNextColumn();
        ImGui.TextUnformatted( $"#{rowIdx + 1:D2}" );

        ImGui.TableNextColumn();
        using var dis = ImRaii.Disabled( disabled );
        ret |= ColorPicker( "##Diffuse", "Diffuse Color", row.Diffuse, c => { tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Diffuse = c; tab.UpdateColorSetRowPreview(rowIdx); } );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeDiffuse", "Apply Diffuse Color on Dye", dye.Diffuse,
                b => { tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Diffuse = b; tab.UpdateColorSetRowPreview(rowIdx); }, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker( "##Specular", "Specular Color", row.Specular, c => { tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Specular = c; tab.UpdateColorSetRowPreview(rowIdx); } );
        ImGui.SameLine();
        var tmpFloat = row.SpecularStrength;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SpecularStrength", ref tmpFloat, 0.1f, 0f, HalfMaxValue, "%.2f" ) && FixFloat( ref tmpFloat, row.SpecularStrength ) )
        {
            tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].SpecularStrength = tmpFloat;
            ret                                                               = true;
            tab.UpdateColorSetRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip( "Specular Strength", ImGuiHoveredFlags.AllowWhenDisabled );

        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeSpecular", "Apply Specular Color on Dye", dye.Specular,
                b => { tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Specular = b; tab.UpdateColorSetRowPreview(rowIdx); }, ImGuiHoveredFlags.AllowWhenDisabled );
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeSpecularStrength", "Apply Specular Strength on Dye", dye.SpecularStrength,
                b => { tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].SpecularStrength = b; tab.UpdateColorSetRowPreview(rowIdx); }, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker( "##Emissive", "Emissive Color", row.Emissive, c => { tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Emissive = c; tab.UpdateColorSetRowPreview(rowIdx); } );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeEmissive", "Apply Emissive Color on Dye", dye.Emissive,
                b => { tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Emissive = b; tab.UpdateColorSetRowPreview(rowIdx); }, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        tmpFloat = row.GlossStrength;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##GlossStrength", ref tmpFloat, Math.Max( 0.1f, tmpFloat * 0.025f ), HalfEpsilon, HalfMaxValue, "%.1f" ) && FixFloat( ref tmpFloat, row.GlossStrength ) )
        {
            tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].GlossStrength = Math.Max(tmpFloat, HalfEpsilon);
            ret                                                            = true;
            tab.UpdateColorSetRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip( "Gloss Strength", ImGuiHoveredFlags.AllowWhenDisabled );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeGloss", "Apply Gloss Strength on Dye", dye.Gloss,
                b => { tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Gloss = b; tab.UpdateColorSetRowPreview(rowIdx); }, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        int tmpInt = row.TileSet;
        ImGui.SetNextItemWidth( intSize );
        if( ImGui.DragInt( "##TileSet", ref tmpInt, 0.25f, 0, 63 ) && tmpInt != row.TileSet && tmpInt is >= 0 and <= ushort.MaxValue )
        {
            tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].TileSet = ( ushort )Math.Clamp(tmpInt, 0, 63);
            ret                                                      = true;
            tab.UpdateColorSetRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip( "Tile Set", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialRepeat.X;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##RepeatX", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f" ) && FixFloat( ref tmpFloat, row.MaterialRepeat.X ) )
        {
            tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { X = tmpFloat };
            ret                                                             = true;
            tab.UpdateColorSetRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip( "Repeat X", ImGuiHoveredFlags.AllowWhenDisabled );
        ImGui.SameLine();
        tmpFloat = row.MaterialRepeat.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##RepeatY", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f" ) && FixFloat( ref tmpFloat, row.MaterialRepeat.Y ) )
        {
            tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { Y = tmpFloat };
            ret                                                             = true;
            tab.UpdateColorSetRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip( "Repeat Y", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialSkew.X;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewX", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f" ) && FixFloat( ref tmpFloat, row.MaterialSkew.X ) )
        {
            tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { X = tmpFloat };
            ret                                                           = true;
            tab.UpdateColorSetRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip( "Skew X", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.SameLine();
        tmpFloat = row.MaterialSkew.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewY", ref tmpFloat, 0.1f, HalfMinValue, HalfMaxValue, "%.2f" ) && FixFloat( ref tmpFloat, row.MaterialSkew.Y ) )
        {
            tab.Mtrl.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { Y = tmpFloat };
            ret                                                           = true;
            tab.UpdateColorSetRowPreview(rowIdx);
        }

        ImGuiUtil.HoverTooltip( "Skew Y", ImGuiHoveredFlags.AllowWhenDisabled );

        if( hasDye )
        {
            ImGui.TableNextColumn();
            if (_stainService.TemplateCombo.Draw( "##dyeTemplate", dye.Template.ToString(), string.Empty, intSize
                 + ImGui.GetStyle().ScrollbarSize / 2, ImGui.GetTextLineHeightWithSpacing(), ImGuiComboFlags.NoArrowButton ) )
            {
                tab.Mtrl.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Template = _stainService.TemplateCombo.CurrentSelection;
                ret                                                          = true;
                tab.UpdateColorSetRowPreview(rowIdx);
            }

            ImGuiUtil.HoverTooltip( "Dye Template", ImGuiHoveredFlags.AllowWhenDisabled );

            ImGui.TableNextColumn();
            ret |= DrawDyePreview( tab, colorSetIdx, rowIdx, disabled, dye, floatSize );
        }
        else if ( hasAnyDye )
        {
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
        }


        return ret;
    }

    private bool DrawDyePreview( MtrlTab tab, int colorSetIdx, int rowIdx, bool disabled, MtrlFile.ColorDyeSet.Row dye, float floatSize )
    {
        var stain = _stainService.StainCombo.CurrentSelection.Key;
        if( stain == 0 || !_stainService.StmFile.Entries.TryGetValue( dye.Template, out var entry ) )
        {
            return false;
        }

        var       values = entry[ ( int )stain ];
        using var style  = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2 );

        var ret = ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2( ImGui.GetFrameHeight() ),
            "Apply the selected dye to this row.", disabled, true );

        ret = ret && tab.Mtrl.ApplyDyeTemplate(_stainService.StmFile, colorSetIdx, rowIdx, stain );
        if (ret)
            tab.UpdateColorSetRowPreview(rowIdx);

        ImGui.SameLine();
        ColorPicker( "##diffusePreview", string.Empty, values.Diffuse, _ => { }, "D" );
        ImGui.SameLine();
        ColorPicker( "##specularPreview", string.Empty, values.Specular, _ => { }, "S" );
        ImGui.SameLine();
        ColorPicker( "##emissivePreview", string.Empty, values.Emissive, _ => { }, "E" );
        ImGui.SameLine();
        using var dis = ImRaii.Disabled();
        ImGui.SetNextItemWidth( floatSize );
        ImGui.DragFloat( "##gloss", ref values.Gloss, 0, values.Gloss, values.Gloss, "%.1f G" );
        ImGui.SameLine();
        ImGui.SetNextItemWidth( floatSize );
        ImGui.DragFloat( "##specularStrength", ref values.SpecularPower, 0, values.SpecularPower, values.SpecularPower, "%.2f S" );

        return ret;
    }

    private static bool ColorPicker( string label, string tooltip, Vector3 input, Action< Vector3 > setter, string letter = "" )
    {
        var ret = false;
        var inputSqrt = Vector3.SquareRoot( input );
        var tmp = inputSqrt;
        if( ImGui.ColorEdit3( label, ref tmp,
               ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.InputRGB | ImGuiColorEditFlags.NoTooltip )
        && tmp != inputSqrt )
        {
            setter( tmp * tmp );
            ret = true;
        }

        if( letter.Length > 0 && ImGui.IsItemVisible() )
        {
            var textSize  = ImGui.CalcTextSize( letter );
            var center    = ImGui.GetItemRectMin() + ( ImGui.GetItemRectSize() - textSize ) / 2;
            var textColor = input.LengthSquared() < 0.25f ? 0x80FFFFFFu : 0x80000000u;
            ImGui.GetWindowDrawList().AddText( center, textColor, letter );
        }

        ImGuiUtil.HoverTooltip( tooltip, ImGuiHoveredFlags.AllowWhenDisabled );

        return ret;
    }
}