using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Mods;
using Penumbra.Mods.ItemSwap;

namespace Penumbra.UI.Classes;

public class ItemSwapWindow : IDisposable
{
    private class ItemSelector : FilterComboCache< Item >
    {
        public ItemSelector()
            : base( Dalamud.GameData.GetExcelSheet< Item >()!.Where( i
                => ( ( EquipSlot )i.EquipSlotCategory.Row ).IsEquipmentPiece() && i.ModelMain != 0 && i.Name.RawData.Length > 0 ) )
        { }

        protected override string ToString( Item obj )
            => obj.Name.ToString();
    }

    public ItemSwapWindow()
    {
        Penumbra.CollectionManager.CollectionChanged         += OnCollectionChange;
        Penumbra.CollectionManager.Current.ModSettingChanged += OnSettingChange;
    }

    public void Dispose()
    {
        Penumbra.CollectionManager.CollectionChanged         += OnCollectionChange;
        Penumbra.CollectionManager.Current.ModSettingChanged -= OnSettingChange;
    }

    private readonly ItemSelector      _itemSelector = new();
    private readonly ItemSwapContainer _swapData     = new();

    private Mod?         _mod;
    private ModSettings? _modSettings;
    private bool         _dirty;

    private SwapType   _lastTab        = SwapType.Equipment;
    private Gender     _currentGender  = Gender.Male;
    private ModelRace  _currentRace    = ModelRace.Midlander;
    private int        _targetId       = 0;
    private int        _sourceId       = 0;
    private int        _currentVariant = 1;
    private Exception? _loadException  = null;

    private string _newModName    = string.Empty;
    private string _newGroupName  = "Swaps";
    private string _newOptionName = string.Empty;
    private bool   _useFileSwaps  = false;


    public void UpdateMod( Mod mod, ModSettings? settings )
    {
        if( mod == _mod && settings == _modSettings )
        {
            return;
        }

        var oldDefaultName = $"{_mod?.Name.Text ?? "Unknown"} (Swapped)";
        if( _newModName.Length == 0 || oldDefaultName == _newModName )
        {
            _newModName = $"{mod.Name.Text} (Swapped)";
        }

        _mod         = mod;
        _modSettings = settings;
        _swapData.LoadMod( _mod, _modSettings );
        _dirty = true;
    }

    private void UpdateState()
    {
        if( !_dirty )
        {
            return;
        }

        _swapData.Clear();
        _loadException = null;
        if( _targetId > 0 && _sourceId > 0 )
        {
            try
            {
                switch( _lastTab )
                {
                    case SwapType.Equipment: break;
                    case SwapType.Accessory: break;
                    case SwapType.Hair:

                        _swapData.LoadCustomization( BodySlot.Hair, Names.CombinedRace( _currentGender, _currentRace ), ( SetId )_sourceId, ( SetId )_targetId );
                        break;
                    case SwapType.Face:
                        _swapData.LoadCustomization( BodySlot.Face, Names.CombinedRace( _currentGender, _currentRace ), ( SetId )_sourceId, ( SetId )_targetId );
                        break;
                    case SwapType.Ears:
                        _swapData.LoadCustomization( BodySlot.Zear, Names.CombinedRace( _currentGender, ModelRace.Viera ), ( SetId )_sourceId, ( SetId )_targetId );
                        break;
                    case SwapType.Tail:
                        _swapData.LoadCustomization( BodySlot.Tail, Names.CombinedRace( _currentGender, _currentRace ), ( SetId )_sourceId, ( SetId )_targetId );
                        break;
                    case SwapType.Weapon: break;
                    case SwapType.Minion: break;
                    case SwapType.Mount:  break;
                }
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not get Customization Data container for {_lastTab}:\n{e}" );
                _loadException = e;
            }
        }

        _dirty = false;
    }

    private static string SwapToString( Swap swap )
    {
        return swap switch
        {
            MetaSwap meta => $"{meta.SwapFrom}: {meta.SwapFrom.EntryToString()} -> {meta.SwapApplied.EntryToString()}",
            FileSwap file => $"{file.Type}: {file.SwapFromRequestPath} -> {file.SwapToModded.FullName}{( file.DataWasChanged ? " (EDITED)" : string.Empty )}",
            _             => string.Empty,
        };
    }

    private string CreateDescription()
        => $"Created by swapping {_lastTab} {_sourceId} onto {_lastTab} {_targetId} for {_currentRace.ToName()} {_currentGender.ToName()}s in {_mod!.Name}.";

    private void DrawHeaderLine( float width )
    {
        var newModAvailable = _loadException == null && _swapData.Loaded;

        ImGui.SetNextItemWidth( width );
        if( ImGui.InputTextWithHint( "##newModName", "New Mod Name...", ref _newModName, 64 ) )
        { }

        ImGui.SameLine();
        var tt = "Create a new mod of the given name containing only the swap.";
        if( ImGuiUtil.DrawDisabledButton( "Create New Mod", new Vector2( width / 2, 0 ), tt, !newModAvailable || _newModName.Length == 0 ) )
        {
            var newDir = Mod.CreateModFolder( Penumbra.ModManager.BasePath, _newModName );
            Mod.CreateMeta( newDir, _newModName, Penumbra.Config.DefaultModAuthor, CreateDescription(), "1.0", string.Empty );
            Mod.CreateDefaultFiles( newDir );
            Penumbra.ModManager.AddMod( newDir );
            if( !_swapData.WriteMod( Penumbra.ModManager.Last(), _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps ) )
            {
                Penumbra.ModManager.DeleteMod( Penumbra.ModManager.Count - 1 );
            }
        }


        ImGui.SetNextItemWidth( ( width - ImGui.GetStyle().ItemSpacing.X ) / 2 );
        if( ImGui.InputTextWithHint( "##groupName", "Group Name...", ref _newGroupName, 32 ) )
        { }

        ImGui.SameLine();
        ImGui.SetNextItemWidth( ( width - ImGui.GetStyle().ItemSpacing.X ) / 2 );
        if( ImGui.InputTextWithHint( "##optionName", "New Option Name...", ref _newOptionName, 32 ) )
        { }

        ImGui.SameLine();
        tt = "Create a new option inside this mod containing only the swap.";
        if( ImGuiUtil.DrawDisabledButton( "Create New Option (WIP)", new Vector2( width / 2, 0 ), tt,
               true || (!newModAvailable || _newGroupName.Length == 0 || _newOptionName.Length == 0 || _mod == null || _mod.AllSubMods.Any( m => m.Name == _newOptionName ) )) )
        { }

        ImGui.SameLine();
        var newPos = new Vector2( ImGui.GetCursorPosX() + 10 * ImGuiHelpers.GlobalScale, ImGui.GetCursorPosY() - ( ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y ) / 2 );
        ImGui.SetCursorPos( newPos );
        ImGui.Checkbox( "Use File Swaps", ref _useFileSwaps );
        ImGuiUtil.HoverTooltip( "Use File Swaps." );
    }

    private enum SwapType
    {
        Equipment,
        Accessory,
        Hair,
        Face,
        Ears,
        Tail,
        Weapon,
        Minion,
        Mount,
    }

    private void DrawSwapBar()
    {
        using var bar = ImRaii.TabBar( "##swapBar", ImGuiTabBarFlags.None );

        DrawHairSwap();
        DrawFaceSwap();
        DrawEarSwap();
        DrawTailSwap();
        DrawArmorSwap();
        DrawAccessorySwap();
        DrawWeaponSwap();
        DrawMinionSwap();
        DrawMountSwap();
    }

    private ImRaii.IEndObject DrawTab( SwapType newTab )
    {
        using var tab = ImRaii.TabItem( newTab.ToString() );
        if( tab )
        {
            _dirty   |= _lastTab != newTab;
            _lastTab = newTab;
        }

        UpdateState();

        return tab;
    }

    private void DrawArmorSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab( SwapType.Equipment );
        if( !tab )
        {
            return;
        }
    }

    private void DrawAccessorySwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab( SwapType.Accessory );
        if( !tab )
        {
            return;
        }
    }

    private void DrawHairSwap()
    {
        using var tab = DrawTab( SwapType.Hair );
        if( !tab )
        {
            return;
        }

        using var table = ImRaii.Table( "##settings", 2, ImGuiTableFlags.SizingFixedFit );
        DrawTargetIdInput( "Take this Hairstyle" );
        DrawSourceIdInput();
        DrawGenderInput();
    }

    private void DrawFaceSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab( SwapType.Face );
        if( !tab )
        {
            return;
        }

        using var table = ImRaii.Table( "##settings", 2, ImGuiTableFlags.SizingFixedFit );
        DrawTargetIdInput( "Take this Face Type" );
        DrawSourceIdInput();
        DrawGenderInput();
    }

    private void DrawTailSwap()
    {
        using var tab = DrawTab( SwapType.Tail );
        if( !tab )
        {
            return;
        }

        using var table = ImRaii.Table( "##settings", 2, ImGuiTableFlags.SizingFixedFit );
        DrawTargetIdInput( "Take this Tail Type" );
        DrawSourceIdInput();
        DrawGenderInput("for all", 2);
    }


    private void DrawEarSwap()
    {
        using var tab = DrawTab( SwapType.Ears );
        if( !tab )
        {
            return;
        }

        using var table = ImRaii.Table( "##settings", 2, ImGuiTableFlags.SizingFixedFit );
        DrawTargetIdInput( "Take this Ear Type" );
        DrawSourceIdInput();
        DrawGenderInput( "for all Viera", 0 );
    }


    private void DrawWeaponSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab( SwapType.Weapon );
        if( !tab )
        {
            return;
        }
    }

    private void DrawMinionSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab( SwapType.Minion );
        if( !tab )
        {
            return;
        }
    }

    private void DrawMountSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab( SwapType.Mount );
        if( !tab )
        {
            return;
        }
    }

    private const float InputWidth = 120;

    private void DrawTargetIdInput( string text = "Take this ID" )
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted( text );

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( InputWidth * ImGuiHelpers.GlobalScale );
        if( ImGui.InputInt( "##targetId", ref _targetId, 0, 0 ) )
            _targetId = Math.Clamp( _targetId, 0, byte.MaxValue );
        _dirty |= ImGui.IsItemDeactivatedAfterEdit();
    }

    private void DrawSourceIdInput( string text = "and put it on this one" )
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted( text );

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( InputWidth * ImGuiHelpers.GlobalScale );
        if (ImGui.InputInt( "##sourceId", ref _sourceId, 0, 0 ))
            _sourceId = Math.Clamp( _sourceId, 0, byte.MaxValue );
        _dirty |= ImGui.IsItemDeactivatedAfterEdit();
    }

    private void DrawVariantInput( string text )
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted( text );

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( InputWidth * ImGuiHelpers.GlobalScale );
        if( ImGui.InputInt( "##variantId", ref _currentVariant, 0, 0 ) )
            _currentVariant = Math.Clamp( _currentVariant, 0, byte.MaxValue );
        _dirty |= ImGui.IsItemDeactivatedAfterEdit();
    }

    private void DrawGenderInput( string text = "for all", int drawRace = 1 )
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted( text );

        ImGui.TableNextColumn();
        _dirty |= Combos.Gender( "##Gender", InputWidth, _currentGender, out _currentGender );
        if( drawRace == 1 )
        {
            ImGui.SameLine();
            _dirty |= Combos.Race( "##Race", InputWidth, _currentRace, out _currentRace );
        }
        else if( drawRace == 2 )
        {
            ImGui.SameLine();
            if( _currentRace is not ModelRace.Miqote and not ModelRace.AuRa and not ModelRace.Hrothgar )
            {
                _currentRace = ModelRace.Miqote;
            }

            _dirty |= ImGuiUtil.GenericEnumCombo( "##Race", InputWidth, _currentRace, out _currentRace, new[] { ModelRace.Miqote, ModelRace.AuRa, ModelRace.Hrothgar },
                RaceEnumExtensions.ToName );
        }
    }

    private string NonExistentText()
        => _lastTab switch
        {
            SwapType.Equipment => "One of the selected pieces of equipment does not seem to exist.",
            SwapType.Accessory => "One of the selected accessories does not seem to exist.",
            SwapType.Hair      => "One of the selected hairstyles does not seem to exist for this gender and race combo.",
            SwapType.Face      => "One of the selected faces does not seem to exist for this gender and race combo.",
            SwapType.Ears      => "One of the selected ear types does not seem to exist for this gender and race combo.",
            SwapType.Tail      => "One of the selected tails does not seem to exist for this gender and race combo.",
            SwapType.Weapon    => "One of the selected weapons does not seem to exist.",
            SwapType.Minion    => "One of the selected minions does not seem to exist.",
            SwapType.Mount     => "One of the selected mounts does not seem to exist.",
            _                  => string.Empty,
        };


    public void DrawItemSwapPanel()
    {
        using var tab = ImRaii.TabItem( "Item Swap (WIP)" );
        if( !tab )
        {
            return;
        }

        ImGui.NewLine();
        DrawHeaderLine( 300 * ImGuiHelpers.GlobalScale );
        ImGui.NewLine();

        DrawSwapBar();

        using var table = ImRaii.ListBox( "##swaps", -Vector2.One );
        if( _loadException != null )
        {
            ImGuiUtil.TextWrapped( $"Could not load Customization Swap:\n{_loadException}" );
        }
        else if( _swapData.Loaded )
        {
            foreach( var swap in _swapData.Swaps )
            {
                DrawSwap( swap );
            }
        }
        else
        {
            ImGui.TextUnformatted( NonExistentText() );
        }
    }

    private static void DrawSwap( Swap swap )
    {
        var       flags = swap.ChildSwaps.Count == 0 ? ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf : ImGuiTreeNodeFlags.DefaultOpen;
        using var tree  = ImRaii.TreeNode( SwapToString( swap ), flags );
        if( !tree )
        {
            return;
        }

        foreach( var child in swap.ChildSwaps )
        {
            DrawSwap( child );
        }
    }

    private void OnCollectionChange( CollectionType collectionType, ModCollection? oldCollection,
        ModCollection? newCollection, string _ )
    {
        if( collectionType != CollectionType.Current || _mod == null || newCollection == null )
        {
            return;
        }

        UpdateMod( _mod, newCollection.Settings[ _mod.Index ] );
        newCollection.ModSettingChanged += OnSettingChange;
        if( oldCollection != null )
        {
            oldCollection.ModSettingChanged -= OnSettingChange;
        }
    }

    private void OnSettingChange( ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool inherited )
    {
        if( modIdx == _mod?.Index )
        {
            _swapData.LoadMod( _mod, _modSettings );
            _dirty = true;
        }
    }
}