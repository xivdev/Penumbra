using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Mods;
using Penumbra.Util;
using static Penumbra.Mods.Mod;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow : Window, IDisposable
{
    private const string  WindowBaseLabel = "###SubModEdit";
    private       Editor? _editor;
    private       Mod?    _mod;
    private       Vector2 _iconSize = Vector2.Zero;

    public void ChangeMod( Mod mod )
    {
        if( mod == _mod )
        {
            return;
        }

        _editor?.Dispose();
        _editor = new Editor( mod, mod.Default );
        _mod    = mod;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = ImGuiHelpers.ScaledVector2( 1000, 600 ),
            MaximumSize = 4000 * Vector2.One,
        };
        _selectedFiles.Clear();
    }

    public void ChangeOption( ISubMod? subMod )
        => _editor?.SetSubMod( subMod );

    public override bool DrawConditions()
        => _editor != null;

    public override void PreDraw()
    {
        var sb = new StringBuilder( 256 );

        var redirections = 0;
        var unused       = 0;
        var size = _editor!.AvailableFiles.Sum( f =>
        {
            if( f.SubModUsage.Count > 0 )
            {
                redirections += f.SubModUsage.Count;
            }
            else
            {
                ++unused;
            }

            return f.FileSize;
        } );
        var manipulations = 0;
        var subMods       = 0;
        var swaps = _mod!.AllSubMods.Sum( m =>
        {
            ++subMods;
            manipulations += m.Manipulations.Count;
            return m.FileSwaps.Count;
        } );
        sb.Append( _mod!.Name );
        if( subMods > 1 )
        {
            sb.AppendFormat( "   |   {0} Options", subMods );
        }

        if( size > 0 )
        {
            sb.AppendFormat( "   |   {0} Files ({1})", _editor.AvailableFiles.Count, Functions.HumanReadableSize( size ) );
        }

        if( unused > 0 )
        {
            sb.AppendFormat( "   |   {0} Unused Files", unused );
        }

        if( _editor.MissingFiles.Count > 0 )
        {
            sb.AppendFormat( "   |   {0} Missing Files", _editor.MissingFiles.Count );
        }

        if( redirections > 0 )
        {
            sb.AppendFormat( "   |   {0} Redirections", redirections );
        }

        if( manipulations > 0 )
        {
            sb.AppendFormat( "   |   {0} Manipulations", manipulations );
        }

        if( swaps > 0 )
        {
            sb.AppendFormat( "   |   {0} Swaps", swaps );
        }

        sb.Append( WindowBaseLabel );
        WindowName = sb.ToString();
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar( "##tabs" );
        if( !tabBar )
        {
            return;
        }

        _iconSize = new Vector2( ImGui.GetFrameHeight() );
        DrawFileTab();
        DrawMetaTab();
        DrawSwapTab();
        DrawMissingFilesTab();
        DrawDuplicatesTab();
        DrawMaterialChangeTab();
        DrawTextureTab();
    }

    // A row of three buttonSizes and a help marker that can be used for material suffix changing.
    private static class MaterialSuffix
    {
        private static string     _materialSuffixFrom = string.Empty;
        private static string     _materialSuffixTo   = string.Empty;
        private static GenderRace _raceCode           = GenderRace.Unknown;

        private static string RaceCodeName( GenderRace raceCode )
        {
            if( raceCode == GenderRace.Unknown )
            {
                return "All Races and Genders";
            }

            var (gender, race) = raceCode.Split();
            return $"({raceCode.ToRaceCode()}) {race.ToName()} {gender.ToName()} ";
        }

        private static void DrawRaceCodeCombo( Vector2 buttonSize )
        {
            ImGui.SetNextItemWidth( buttonSize.X );
            using var combo = ImRaii.Combo( "##RaceCode", RaceCodeName( _raceCode ) );
            if( !combo )
            {
                return;
            }

            foreach( var raceCode in Enum.GetValues< GenderRace >() )
            {
                if( ImGui.Selectable( RaceCodeName( raceCode ), _raceCode == raceCode ) )
                {
                    _raceCode = raceCode;
                }
            }
        }

        public static void Draw( Editor editor, Vector2 buttonSize )
        {
            DrawRaceCodeCombo( buttonSize );
            ImGui.SameLine();
            ImGui.SetNextItemWidth( buttonSize.X );
            ImGui.InputTextWithHint( "##suffixFrom", "From...", ref _materialSuffixFrom, 32 );
            ImGui.SameLine();
            ImGui.SetNextItemWidth( buttonSize.X );
            ImGui.InputTextWithHint( "##suffixTo", "To...", ref _materialSuffixTo, 32 );
            ImGui.SameLine();
            var disabled = !Editor.ValidString( _materialSuffixTo );
            var tt = _materialSuffixTo.Length == 0
                ? "Please enter a target suffix."
                : _materialSuffixFrom == _materialSuffixTo
                    ? "The source and target are identical."
                    : disabled
                        ? "The suffix is invalid."
                        : _materialSuffixFrom.Length == 0
                            ? _raceCode == GenderRace.Unknown
                                ? "Convert all skin material suffices to the target."
                                : "Convert all skin material suffices for the given race code to the target."
                            : _raceCode == GenderRace.Unknown
                                ? $"Convert all skin material suffices that are currently '{_materialSuffixFrom}' to '{_materialSuffixTo}'."
                                : $"Convert all skin material suffices for the given race code that are currently '{_materialSuffixFrom}' to '{_materialSuffixTo}'.";
            if( ImGuiUtil.DrawDisabledButton( "Change Material Suffix", buttonSize, tt, disabled ) )
            {
                editor.ReplaceAllMaterials( _materialSuffixTo, _materialSuffixFrom, _raceCode );
            }

            var anyChanges = editor.ModelFiles.Any( m => m.Changed );
            if( ImGuiUtil.DrawDisabledButton( "Save All Changes", buttonSize,
                   anyChanges ? "Irreversibly rewrites all currently applied changes to model files." : "No changes made yet.", !anyChanges ) )
            {
                editor.SaveAllModels();
            }

            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "Revert All Changes", buttonSize,
                   anyChanges ? "Revert all currently made and unsaved changes." : "No changes made yet.", !anyChanges ) )
            {
                editor.RestoreAllModels();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Model files refer to the skin material they should use. This skin material is always the same, but modders have started using different suffices to differentiate between body types.\n"
              + "This option allows you to switch the suffix of all model files to another. This changes the files, so you do this on your own risk.\n"
              + "If you do not know what the currently used suffix of this mod is, you can leave 'From' blank and it will replace all suffices with 'To', instead of only the matching ones." );
        }
    }

    private void DrawMaterialChangeTab()
    {
        using var tab = ImRaii.TabItem( "Model Materials" );
        if( !tab )
        {
            return;
        }

        if( _editor!.ModelFiles.Count == 0 )
        {
            ImGui.NewLine();
            ImGui.TextUnformatted( "No .mdl files detected." );
        }
        else
        {
            ImGui.NewLine();
            MaterialSuffix.Draw( _editor, ImGuiHelpers.ScaledVector2( 175, 0 ) );
            ImGui.NewLine();
            using var child = ImRaii.Child( "##mdlFiles", -Vector2.One, true );
            if( !child )
            {
                return;
            }

            using var table = ImRaii.Table( "##files", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.One );
            if( !table )
            {
                return;
            }

            var iconSize = ImGui.GetFrameHeight() * Vector2.One;
            foreach( var (info, idx) in _editor.ModelFiles.WithIndex() )
            {
                using var id = ImRaii.PushId( idx );
                ImGui.TableNextColumn();
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Save.ToIconString(), iconSize,
                       "Save the changed mdl file.\nUse at own risk!", !info.Changed, true ) )
                {
                    info.Save();
                }

                ImGui.TableNextColumn();
                if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Recycle.ToIconString(), iconSize,
                       "Restore current changes to default.", !info.Changed, true ) )
                {
                    info.Restore();
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted( info.Path.FullName[ ( _mod!.ModPath.FullName.Length + 1 ).. ] );
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth( 400 * ImGuiHelpers.GlobalScale );
                var tmp = info.CurrentMaterials[ 0 ];
                if( ImGui.InputText( "##0", ref tmp, 64 ) )
                {
                    info.SetMaterial( tmp, 0 );
                }

                for( var i = 1; i < info.Count; ++i )
                {
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth( 400 * ImGuiHelpers.GlobalScale );
                    tmp = info.CurrentMaterials[ i ];
                    if( ImGui.InputText( $"##{i}", ref tmp, 64 ) )
                    {
                        info.SetMaterial( tmp, i );
                    }
                }
            }
        }
    }

    private void DrawMissingFilesTab()
    {
        using var tab = ImRaii.TabItem( "Missing Files" );
        if( !tab )
        {
            return;
        }

        if( _editor!.MissingFiles.Count == 0 )
        {
            ImGui.NewLine();
            ImGui.TextUnformatted( "No missing files detected." );
        }
        else
        {
            if( ImGui.Button( "Remove Missing Files from Mod" ) )
            {
                _editor.RemoveMissingPaths();
            }

            using var child = ImRaii.Child( "##unusedFiles", -Vector2.One, true );
            if( !child )
            {
                return;
            }

            using var table = ImRaii.Table( "##missingFiles", 1, ImGuiTableFlags.RowBg, -Vector2.One );
            if( !table )
            {
                return;
            }

            foreach( var path in _editor.MissingFiles )
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( path.FullName );
            }
        }
    }

    private void DrawDuplicatesTab()
    {
        using var tab = ImRaii.TabItem( "Duplicates" );
        if( !tab )
        {
            return;
        }

        var buttonText = _editor!.DuplicatesFinished ? "Scan for Duplicates###ScanButton" : "Scanning for Duplicates...###ScanButton";
        if( ImGuiUtil.DrawDisabledButton( buttonText, Vector2.Zero, "Search for identical files in this mod. This may take a while.",
               !_editor.DuplicatesFinished ) )
        {
            _editor.StartDuplicateCheck();
        }

        if( !_editor.DuplicatesFinished )
        {
            ImGui.SameLine();
            if( ImGui.Button( "Cancel" ) )
            {
                _editor.Cancel();
            }

            return;
        }

        if( _editor.Duplicates.Count == 0 )
        {
            ImGui.NewLine();
            ImGui.TextUnformatted( "No duplicates found." );
            return;
        }

        if( ImGui.Button( "Delete and Redirect Duplicates" ) )
        {
            _editor.DeleteDuplicates();
        }

        if( _editor.SavedSpace > 0 )
        {
            ImGui.SameLine();
            ImGui.TextUnformatted( $"Frees up {Functions.HumanReadableSize( _editor.SavedSpace )} from your hard drive." );
        }

        using var child = ImRaii.Child( "##duptable", -Vector2.One, true );
        if( !child )
        {
            return;
        }

        using var table = ImRaii.Table( "##duplicates", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.One );
        if( !table )
        {
            return;
        }

        var width = ImGui.CalcTextSize( "NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN " ).X;
        ImGui.TableSetupColumn( "file", ImGuiTableColumnFlags.WidthStretch );
        ImGui.TableSetupColumn( "size", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize( "NNN.NNN  " ).X );
        ImGui.TableSetupColumn( "hash", ImGuiTableColumnFlags.WidthFixed,
            ImGui.GetWindowWidth() > 2 * width ? width : ImGui.CalcTextSize( "NNNNNNNN... " ).X );
        foreach( var (set, size, hash) in _editor.Duplicates.Where( s => s.Paths.Length > 1 ) )
        {
            ImGui.TableNextColumn();
            using var tree = ImRaii.TreeNode( set[ 0 ].FullName[ ( _mod!.ModPath.FullName.Length + 1 ).. ],
                ImGuiTreeNodeFlags.NoTreePushOnOpen );
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign( Functions.HumanReadableSize( size ) );
            ImGui.TableNextColumn();
            using( var _ = ImRaii.PushFont( UiBuilder.MonoFont ) )
            {
                if( ImGui.GetWindowWidth() > 2 * width )
                {
                    ImGuiUtil.RightAlign( string.Concat( hash.Select( b => b.ToString( "X2" ) ) ) );
                }
                else
                {
                    ImGuiUtil.RightAlign( string.Concat( hash.Take( 4 ).Select( b => b.ToString( "X2" ) ) ) + "..." );
                }
            }

            if( !tree )
            {
                continue;
            }

            using var indent = ImRaii.PushIndent();
            foreach( var duplicate in set.Skip( 1 ) )
            {
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor( ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint );
                using var node = ImRaii.TreeNode( duplicate.FullName[ ( _mod!.ModPath.FullName.Length + 1 ).. ], ImGuiTreeNodeFlags.Leaf );
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor( ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint );
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor( ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint );
            }
        }
    }

    private void DrawOptionSelectHeader()
    {
        const string defaultOption = "Default Option";
        using var    style         = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, Vector2.Zero ).Push( ImGuiStyleVar.FrameRounding, 0 );
        var          width         = new Vector2( ImGui.GetWindowWidth() / 3, 0 );
        if( ImGuiUtil.DrawDisabledButton( defaultOption, width, "Switch to the default option for the mod.\nThis resets unsaved changes.",
               _editor!.CurrentOption.IsDefault ) )
        {
            _editor.SetSubMod( _mod!.Default );
        }

        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( "Refresh Data", width, "Refresh data for the current option.\nThis resets unsaved changes.", false ) )
        {
            _editor.SetSubMod( _editor.CurrentOption );
        }

        ImGui.SameLine();

        using var combo = ImRaii.Combo( "##optionSelector", _editor.CurrentOption.FullName, ImGuiComboFlags.NoArrowButton );
        if( !combo )
        {
            return;
        }

        foreach( var option in _mod!.AllSubMods )
        {
            if( ImGui.Selectable( option.FullName, option == _editor.CurrentOption ) )
            {
                _editor.SetSubMod( option );
            }
        }
    }

    private string _newSwapKey   = string.Empty;
    private string _newSwapValue = string.Empty;

    private void DrawSwapTab()
    {
        using var tab = ImRaii.TabItem( "File Swaps" );
        if( !tab )
        {
            return;
        }

        DrawOptionSelectHeader();

        var setsEqual = _editor!.CurrentSwaps.SetEquals( _editor.CurrentOption.FileSwaps );
        var tt        = setsEqual ? "No changes staged." : "Apply the currently staged changes to the option.";
        ImGui.NewLine();
        if( ImGuiUtil.DrawDisabledButton( "Apply Changes", Vector2.Zero, tt, setsEqual ) )
        {
            _editor.ApplySwaps();
        }

        ImGui.SameLine();
        tt = setsEqual ? "No changes staged." : "Revert all currently staged changes.";
        if( ImGuiUtil.DrawDisabledButton( "Revert Changes", Vector2.Zero, tt, setsEqual ) )
        {
            _editor.RevertSwaps();
        }

        using var child = ImRaii.Child( "##swaps", -Vector2.One, true );
        if( !child )
        {
            return;
        }

        using var list = ImRaii.Table( "##table", 3, ImGuiTableFlags.RowBg, -Vector2.One );
        if( !list )
        {
            return;
        }

        var idx      = 0;
        var iconSize = ImGui.GetFrameHeight() * Vector2.One;
        var pathSize = ImGui.GetContentRegionAvail().X / 2 - iconSize.X;
        ImGui.TableSetupColumn( "button", ImGuiTableColumnFlags.WidthFixed, iconSize.X );
        ImGui.TableSetupColumn( "source", ImGuiTableColumnFlags.WidthFixed, pathSize );
        ImGui.TableSetupColumn( "value", ImGuiTableColumnFlags.WidthFixed, pathSize );

        foreach( var (gamePath, file) in _editor!.CurrentSwaps.ToList() )
        {
            using var id = ImRaii.PushId( idx++ );
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this swap.", false, true ) )
            {
                _editor.CurrentSwaps.Remove( gamePath );
            }

            ImGui.TableNextColumn();
            var tmp = gamePath.Path.ToString();
            ImGui.SetNextItemWidth( -1 );
            if( ImGui.InputText( "##key", ref tmp, Utf8GamePath.MaxGamePathLength )
            && Utf8GamePath.FromString( tmp, out var path )
            && !_editor.CurrentSwaps.ContainsKey( path ) )
            {
                _editor.CurrentSwaps.Remove( gamePath );
                if( path.Length > 0 )
                {
                    _editor.CurrentSwaps[ path ] = file;
                }
            }

            ImGui.TableNextColumn();
            tmp = file.FullName;
            ImGui.SetNextItemWidth( -1 );
            if( ImGui.InputText( "##value", ref tmp, Utf8GamePath.MaxGamePathLength ) && tmp.Length > 0 )
            {
                _editor.CurrentSwaps[ gamePath ] = new FullPath( tmp );
            }
        }

        ImGui.TableNextColumn();
        var addable = Utf8GamePath.FromString( _newSwapKey, out var newPath )
         && newPath.Length       > 0
         && _newSwapValue.Length > 0
         && _newSwapValue        != _newSwapKey
         && !_editor.CurrentSwaps.ContainsKey( newPath );
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconSize, "Add a new file swap to this option.", !addable,
               true ) )
        {
            _editor.CurrentSwaps[ newPath ] = new FullPath( _newSwapValue );
            _newSwapKey                     = string.Empty;
            _newSwapValue                   = string.Empty;
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( -1 );
        ImGui.InputTextWithHint( "##swapKey", "New Swap Source...", ref _newSwapKey, Utf8GamePath.MaxGamePathLength );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( -1 );
        ImGui.InputTextWithHint( "##swapValue", "New Swap Target...", ref _newSwapValue, Utf8GamePath.MaxGamePathLength );
    }

    public ModEditWindow()
        : base( WindowBaseLabel )
    { }

    public void Dispose()
    {
        _editor?.Dispose();
    }
}