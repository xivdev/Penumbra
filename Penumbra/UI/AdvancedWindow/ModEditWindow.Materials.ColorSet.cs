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
    private static bool DrawMaterialColorSetChange( MtrlFile file, bool disabled )
    {
        if( !file.ColorSets.Any( c => c.HasRows ) )
        {
            return false;
        }

        ColorSetCopyAllClipboardButton( file, 0 );
        ImGui.SameLine();
        var ret = ColorSetPasteAllClipboardButton( file, 0 );
        ImGui.SameLine();
        ImGui.Dummy( ImGuiHelpers.ScaledVector2( 20, 0 ) );
        ImGui.SameLine();
        ret |= DrawPreviewDye( file, disabled );

        using var table = ImRaii.Table( "##ColorSets", 11,
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
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Dye" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Dye Preview" );

        for( var j = 0; j < file.ColorSets.Length; ++j )
        {
            using var _ = ImRaii.PushId( j );
            for( var i = 0; i < MtrlFile.ColorSet.RowArray.NumRows; ++i )
            {
                ret |= DrawColorSetRow( file, j, i, disabled );
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

    private static bool DrawPreviewDye( MtrlFile file, bool disabled )
    {
        var (dyeId, (name, dyeColor, _)) = Penumbra.StainService.StainCombo.CurrentSelection;
        var tt = dyeId == 0 ? "Select a preview dye first." : "Apply all preview values corresponding to the dye template and chosen dye where dyeing is enabled.";
        if( ImGuiUtil.DrawDisabledButton( "Apply Preview Dye", Vector2.Zero, tt, disabled || dyeId == 0 ) )
        {
            var ret = false;
            for( var j = 0; j < file.ColorDyeSets.Length; ++j )
            {
                for( var i = 0; i < MtrlFile.ColorSet.RowArray.NumRows; ++i )
                {
                    ret |= file.ApplyDyeTemplate( Penumbra.StainService.StmFile, j, i, dyeId );
                }
            }

            return ret;
        }

        ImGui.SameLine();
        var label = dyeId == 0 ? "Preview Dye###previewDye" : $"{name} (Preview)###previewDye";
        Penumbra.StainService.StainCombo.Draw( label, dyeColor, string.Empty, true );
        return false;
    }

    private static unsafe bool ColorSetPasteAllClipboardButton( MtrlFile file, int colorSetIdx )
    {
        if( !ImGui.Button( "Import All Rows from Clipboard", ImGuiHelpers.ScaledVector2( 200, 0 ) ) || file.ColorSets.Length <= colorSetIdx )
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

            ref var rows = ref file.ColorSets[ colorSetIdx ].Rows;
            fixed( void* ptr = data, output = &rows )
            {
                MemoryUtility.MemCpyUnchecked( output, ptr, Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() );
                if( data.Length             >= Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() + Marshal.SizeOf< MtrlFile.ColorDyeSet.RowArray >()
                && file.ColorDyeSets.Length > colorSetIdx )
                {
                    ref var dyeRows = ref file.ColorDyeSets[ colorSetIdx ].Rows;
                    fixed( void* output2 = &dyeRows )
                    {
                        MemoryUtility.MemCpyUnchecked( output2, ( byte* )ptr + Marshal.SizeOf< MtrlFile.ColorSet.RowArray >(), Marshal.SizeOf< MtrlFile.ColorDyeSet.RowArray >() );
                    }
                }
            }

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

    private static unsafe bool ColorSetPasteFromClipboardButton( MtrlFile file, int colorSetIdx, int rowIdx, bool disabled )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Paste.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
               "Import an exported row from your clipboard onto this row.", disabled, true ) )
        {
            try
            {
                var text = ImGui.GetClipboardText();
                var data = Convert.FromBase64String( text );
                if( data.Length          != MtrlFile.ColorSet.Row.Size + 2
                || file.ColorSets.Length <= colorSetIdx )
                {
                    return false;
                }

                fixed( byte* ptr = data )
                {
                    file.ColorSets[ colorSetIdx ].Rows[ rowIdx ] = *( MtrlFile.ColorSet.Row* )ptr;
                    if( colorSetIdx < file.ColorDyeSets.Length )
                    {
                        file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ] = *( MtrlFile.ColorDyeSet.Row* )( ptr + MtrlFile.ColorSet.Row.Size );
                    }
                }

                return true;
            }
            catch
            {
                // ignored
            }
        }

        return false;
    }

    private static bool DrawColorSetRow( MtrlFile file, int colorSetIdx, int rowIdx, bool disabled )
    {
        static bool FixFloat( ref float val, float current )
        {
            val = ( float )( Half )val;
            return val != current;
        }

        using var id        = ImRaii.PushId( rowIdx );
        var       row       = file.ColorSets[ colorSetIdx ].Rows[ rowIdx ];
        var       hasDye    = file.ColorDyeSets.Length > colorSetIdx;
        var       dye       = hasDye ? file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ] : new MtrlFile.ColorDyeSet.Row();
        var       floatSize = 70 * UiHelpers.Scale;
        var       intSize   = 45 * UiHelpers.Scale;
        ImGui.TableNextColumn();
        ColorSetCopyClipboardButton( row, dye );
        ImGui.SameLine();
        var ret = ColorSetPasteFromClipboardButton( file, colorSetIdx, rowIdx, disabled );

        ImGui.TableNextColumn();
        ImGui.TextUnformatted( $"#{rowIdx + 1:D2}" );

        ImGui.TableNextColumn();
        using var dis = ImRaii.Disabled( disabled );
        ret |= ColorPicker( "##Diffuse", "Diffuse Color", row.Diffuse, c => file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Diffuse = c );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeDiffuse", "Apply Diffuse Color on Dye", dye.Diffuse,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Diffuse = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker( "##Specular", "Specular Color", row.Specular, c => file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Specular = c );
        ImGui.SameLine();
        var tmpFloat = row.SpecularStrength;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SpecularStrength", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.SpecularStrength ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].SpecularStrength = tmpFloat;
            ret                                                           = true;
        }

        ImGuiUtil.HoverTooltip( "Specular Strength", ImGuiHoveredFlags.AllowWhenDisabled );

        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeSpecular", "Apply Specular Color on Dye", dye.Specular,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Specular = b, ImGuiHoveredFlags.AllowWhenDisabled );
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeSpecularStrength", "Apply Specular Strength on Dye", dye.SpecularStrength,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].SpecularStrength = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker( "##Emissive", "Emissive Color", row.Emissive, c => file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Emissive = c );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeEmissive", "Apply Emissive Color on Dye", dye.Emissive,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Emissive = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        tmpFloat = row.GlossStrength;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##GlossStrength", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.GlossStrength ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].GlossStrength = tmpFloat;
            ret                                                        = true;
        }

        ImGuiUtil.HoverTooltip( "Gloss Strength", ImGuiHoveredFlags.AllowWhenDisabled );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeGloss", "Apply Gloss Strength on Dye", dye.Gloss,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Gloss = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        int tmpInt = row.TileSet;
        ImGui.SetNextItemWidth( intSize );
        if( ImGui.InputInt( "##TileSet", ref tmpInt, 0, 0 ) && tmpInt != row.TileSet && tmpInt is >= 0 and <= ushort.MaxValue )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].TileSet = ( ushort )tmpInt;
            ret                                                  = true;
        }

        ImGuiUtil.HoverTooltip( "Tile Set", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialRepeat.X;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##RepeatX", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialRepeat.X ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { X = tmpFloat };
            ret                                                         = true;
        }

        ImGuiUtil.HoverTooltip( "Repeat X", ImGuiHoveredFlags.AllowWhenDisabled );
        ImGui.SameLine();
        tmpFloat = row.MaterialRepeat.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##RepeatY", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialRepeat.Y ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { Y = tmpFloat };
            ret                                                         = true;
        }

        ImGuiUtil.HoverTooltip( "Repeat Y", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialSkew.X;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewX", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialSkew.X ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { X = tmpFloat };
            ret                                                       = true;
        }

        ImGuiUtil.HoverTooltip( "Skew X", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.SameLine();
        tmpFloat = row.MaterialSkew.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewY", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialSkew.Y ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { Y = tmpFloat };
            ret                                                       = true;
        }

        ImGuiUtil.HoverTooltip( "Skew Y", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        if( hasDye )
        {
            if( Penumbra.StainService.TemplateCombo.Draw( "##dyeTemplate", dye.Template.ToString(), string.Empty, intSize
                 + ImGui.GetStyle().ScrollbarSize / 2, ImGui.GetTextLineHeightWithSpacing(), ImGuiComboFlags.NoArrowButton ) )
            {
                file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Template = Penumbra.StainService.TemplateCombo.CurrentSelection;
                ret                                                      = true;
            }

            ImGuiUtil.HoverTooltip( "Dye Template", ImGuiHoveredFlags.AllowWhenDisabled );

            ImGui.TableNextColumn();
            ret |= DrawDyePreview( file, colorSetIdx, rowIdx, disabled, dye, floatSize );
        }
        else
        {
            ImGui.TableNextColumn();
        }


        return ret;
    }

    private static bool DrawDyePreview( MtrlFile file, int colorSetIdx, int rowIdx, bool disabled, MtrlFile.ColorDyeSet.Row dye, float floatSize )
    {
        var stain = Penumbra.StainService.StainCombo.CurrentSelection.Key;
        if( stain == 0 || !Penumbra.StainService.StmFile.Entries.TryGetValue( dye.Template, out var entry ) )
        {
            return false;
        }

        var       values = entry[ ( int )stain ];
        using var style  = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2 );

        var ret = ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2( ImGui.GetFrameHeight() ),
            "Apply the selected dye to this row.", disabled, true );

        ret = ret && file.ApplyDyeTemplate( Penumbra.StainService.StmFile, colorSetIdx, rowIdx, stain );

        ImGui.SameLine();
        ColorPicker( "##diffusePreview", string.Empty, values.Diffuse, _ => { }, "D" );
        ImGui.SameLine();
        ColorPicker( "##specularPreview", string.Empty, values.Specular, _ => { }, "S" );
        ImGui.SameLine();
        ColorPicker( "##emissivePreview", string.Empty, values.Emissive, _ => { }, "E" );
        ImGui.SameLine();
        using var dis = ImRaii.Disabled();
        ImGui.SetNextItemWidth( floatSize );
        ImGui.DragFloat( "##gloss", ref values.Gloss, 0, 0, 0, "%.2f G" );
        ImGui.SameLine();
        ImGui.SetNextItemWidth( floatSize );
        ImGui.DragFloat( "##specularStrength", ref values.SpecularPower, 0, 0, 0, "%.2f S" );

        return ret;
    }

    private static bool ColorPicker( string label, string tooltip, Vector3 input, Action< Vector3 > setter, string letter = "" )
    {
        var ret = false;
        var tmp = input;
        if( ImGui.ColorEdit3( label, ref tmp,
               ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.InputRGB | ImGuiColorEditFlags.NoTooltip )
        && tmp != input )
        {
            setter( tmp );
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