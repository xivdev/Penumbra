using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Utility;
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
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ItemSwapTab : IDisposable, ITab
{
    private readonly CommunicatorService   _communicator;
    private readonly ItemService           _itemService;
    private readonly CollectionManager _collectionManager;
    private readonly ModManager           _modManager;
    private readonly Configuration         _config;

    public ItemSwapTab(CommunicatorService communicator, ItemService itemService, CollectionManager collectionManager,
        ModManager modManager, Configuration config)
    {
        _communicator      = communicator;
        _itemService       = itemService;
        _collectionManager = collectionManager;
        _modManager        = modManager;
        _config            = config;

        _selectors = new Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, string TextFrom, string TextTo)>
        {
            // @formatter:off
            [SwapType.Hat]      = (new ItemSelector(_itemService, FullEquipType.Head),   new ItemSelector(_itemService, FullEquipType.Head),   "Take this Hat",        "and put it on this one" ),
            [SwapType.Top]      = (new ItemSelector(_itemService, FullEquipType.Body),   new ItemSelector(_itemService, FullEquipType.Body),   "Take this Top",        "and put it on this one" ),
            [SwapType.Gloves]   = (new ItemSelector(_itemService, FullEquipType.Hands),  new ItemSelector(_itemService, FullEquipType.Hands),  "Take these Gloves",    "and put them on these"  ),
            [SwapType.Pants]    = (new ItemSelector(_itemService, FullEquipType.Legs),   new ItemSelector(_itemService, FullEquipType.Legs),   "Take these Pants",     "and put them on these"  ),
            [SwapType.Shoes]    = (new ItemSelector(_itemService, FullEquipType.Feet),   new ItemSelector(_itemService, FullEquipType.Feet),   "Take these Shoes",     "and put them on these"  ),
            [SwapType.Earrings] = (new ItemSelector(_itemService, FullEquipType.Ears),   new ItemSelector(_itemService, FullEquipType.Ears),   "Take these Earrings",  "and put them on these"  ),
            [SwapType.Necklace] = (new ItemSelector(_itemService, FullEquipType.Neck),   new ItemSelector(_itemService, FullEquipType.Neck),   "Take this Necklace",   "and put it on this one" ),
            [SwapType.Bracelet] = (new ItemSelector(_itemService, FullEquipType.Wrists), new ItemSelector(_itemService, FullEquipType.Wrists), "Take these Bracelets", "and put them on these"  ),
            [SwapType.Ring]     = (new ItemSelector(_itemService, FullEquipType.Finger), new ItemSelector(_itemService, FullEquipType.Finger), "Take this Ring",       "and put it on this one" ),
            // @formatter:on
        };

        _communicator.CollectionChange.Event         += OnCollectionChange;
        _collectionManager.Current.ModSettingChanged += OnSettingChange;
    }

    /// <summary> Update the currently selected mod or its settings. </summary>
    public void UpdateMod(Mod mod, ModSettings? settings)
    {
        if (mod == _mod && settings == _modSettings)
            return;

        var oldDefaultName = $"{_mod?.Name.Text ?? "Unknown"} (Swapped)";
        if (_newModName.Length == 0 || oldDefaultName == _newModName)
            _newModName = $"{mod.Name.Text} (Swapped)";

        _mod         = mod;
        _modSettings = settings;
        _swapData.LoadMod(_mod, _modSettings);
        UpdateOption();
        _dirty = true;
    }

    public ReadOnlySpan<byte> Label
        => "Item Swap (WIP)"u8;

    public void DrawContent()
    {
        ImGui.NewLine();
        DrawHeaderLine(300 * UiHelpers.Scale);
        ImGui.NewLine();

        DrawSwapBar();

        using var table = ImRaii.ListBox("##swaps", -Vector2.One);
        if (_loadException != null)
            ImGuiUtil.TextWrapped($"Could not load Customization Swap:\n{_loadException}");
        else if (_swapData.Loaded)
            foreach (var swap in _swapData.Swaps)
                DrawSwap(swap);
        else
            ImGui.TextUnformatted(NonExistentText());
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Event         -= OnCollectionChange;
        _collectionManager.Current.ModSettingChanged -= OnSettingChange;
    }

    private enum SwapType
    {
        Hat,
        Top,
        Gloves,
        Pants,
        Shoes,
        Earrings,
        Necklace,
        Bracelet,
        Ring,
        BetweenSlots,
        Hair,
        Face,
        Ears,
        Tail,
        Weapon,
    }

    private class ItemSelector : FilterComboCache<(string, Item)>
    {
        public ItemSelector(ItemService data, FullEquipType type)
            : base(() => data.AwaitedService[type].Select(i => (i.Name.ToDalamudString().TextValue, i)).ToArray())
        { }

        protected override string ToString((string, Item) obj)
            => obj.Item1;
    }

    private class WeaponSelector : FilterComboCache<FullEquipType>
    {
        public WeaponSelector()
            : base(FullEquipTypeExtensions.WeaponTypes.Concat(FullEquipTypeExtensions.ToolTypes))
        { }

        protected override string ToString(FullEquipType type)
            => type.ToName();
    }

    private readonly Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, string TextFrom, string TextTo)> _selectors;

    private          ItemSelector?     _weaponSource;
    private          ItemSelector?     _weaponTarget;
    private readonly WeaponSelector    _slotSelector = new();
    private readonly ItemSwapContainer _swapData     = new();

    private Mod?         _mod;
    private ModSettings? _modSettings;
    private bool         _dirty;

    private SwapType   _lastTab       = SwapType.Hair;
    private Gender     _currentGender = Gender.Male;
    private ModelRace  _currentRace   = ModelRace.Midlander;
    private int        _targetId;
    private int        _sourceId;
    private Exception? _loadException;
    private EquipSlot  _slotFrom = EquipSlot.Head;
    private EquipSlot  _slotTo   = EquipSlot.Ears;

    private string     _newModName    = string.Empty;
    private string     _newGroupName  = "Swaps";
    private string     _newOptionName = string.Empty;
    private IModGroup? _selectedGroup;
    private bool       _subModValid;
    private bool       _useFileSwaps = true;
    private bool       _useCurrentCollection;
    private bool       _useLeftRing  = true;
    private bool       _useRightRing = true;

    private Item[]? _affectedItems;

    private void UpdateState()
    {
        if (!_dirty)
            return;

        _swapData.Clear();
        _loadException = null;
        _affectedItems = null;
        try
        {
            switch (_lastTab)
            {
                case SwapType.Hat:
                case SwapType.Top:
                case SwapType.Gloves:
                case SwapType.Pants:
                case SwapType.Shoes:
                case SwapType.Earrings:
                case SwapType.Necklace:
                case SwapType.Bracelet:
                case SwapType.Ring:
                    var values = _selectors[_lastTab];
                    if (values.Source.CurrentSelection.Item2 != null && values.Target.CurrentSelection.Item2 != null)
                        _affectedItems = _swapData.LoadEquipment(values.Target.CurrentSelection.Item2, values.Source.CurrentSelection.Item2,
                            _useCurrentCollection ? _collectionManager.Current : null, _useRightRing, _useLeftRing);

                    break;
                case SwapType.BetweenSlots:
                    var (_, _, selectorFrom) = GetAccessorySelector(_slotFrom, true);
                    var (_, _, selectorTo)   = GetAccessorySelector(_slotTo,   false);
                    if (selectorFrom.CurrentSelection.Item2 != null && selectorTo.CurrentSelection.Item2 != null)
                        _affectedItems = _swapData.LoadTypeSwap(_slotTo, selectorTo.CurrentSelection.Item2, _slotFrom,
                            selectorFrom.CurrentSelection.Item2,
                            _useCurrentCollection ? _collectionManager.Current : null);
                    break;
                case SwapType.Hair when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(BodySlot.Hair, Names.CombinedRace(_currentGender, _currentRace), (SetId)_sourceId,
                        (SetId)_targetId,
                        _useCurrentCollection ? _collectionManager.Current : null);
                    break;
                case SwapType.Face when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(BodySlot.Face, Names.CombinedRace(_currentGender, _currentRace), (SetId)_sourceId,
                        (SetId)_targetId,
                        _useCurrentCollection ? _collectionManager.Current : null);
                    break;
                case SwapType.Ears when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(BodySlot.Zear, Names.CombinedRace(_currentGender, ModelRace.Viera), (SetId)_sourceId,
                        (SetId)_targetId,
                        _useCurrentCollection ? _collectionManager.Current : null);
                    break;
                case SwapType.Tail when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(BodySlot.Tail, Names.CombinedRace(_currentGender, _currentRace), (SetId)_sourceId,
                        (SetId)_targetId,
                        _useCurrentCollection ? _collectionManager.Current : null);
                    break;
                case SwapType.Weapon: break;
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not get Customization Data container for {_lastTab}:\n{e}");
            _loadException = e;
            _affectedItems = null;
            _swapData.Clear();
        }

        _dirty = false;
    }

    private static string SwapToString(Swap swap)
    {
        return swap switch
        {
            MetaSwap meta => $"{meta.SwapFrom}: {meta.SwapFrom.EntryToString()} -> {meta.SwapApplied.EntryToString()}",
            FileSwap file =>
                $"{file.Type}: {file.SwapFromRequestPath} -> {file.SwapToModded.FullName}{(file.DataWasChanged ? " (EDITED)" : string.Empty)}",
            _ => string.Empty,
        };
    }

    private string CreateDescription()
        => $"Created by swapping {_lastTab} {_sourceId} onto {_lastTab} {_targetId} for {_currentRace.ToName()} {_currentGender.ToName()}s in {_mod!.Name}.";

    private void UpdateOption()
    {
        _selectedGroup = _mod?.Groups.FirstOrDefault(g => g.Name == _newGroupName);
        _subModValid = _mod != null
         && _newGroupName.Length > 0
         && _newOptionName.Length > 0
         && (_selectedGroup?.All(o => o.Name != _newOptionName) ?? true);
    }

    private void CreateMod()
    {
        var newDir = Mod.Creator.CreateModFolder(_modManager.BasePath, _newModName);
        _modManager.DataEditor.CreateMeta(newDir, _newModName, _config.DefaultModAuthor, CreateDescription(), "1.0", string.Empty);
        Mod.Creator.CreateDefaultFiles(newDir);
        _modManager.AddMod(newDir);
        if (!_swapData.WriteMod(_modManager, _modManager.Last(),
                _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps))
            _modManager.DeleteMod(_modManager.Count - 1);
    }

    private void CreateOption()
    {
        if (_mod == null || !_subModValid)
            return;

        var            groupCreated     = false;
        var            dirCreated       = false;
        var            optionCreated    = false;
        DirectoryInfo? optionFolderName = null;
        try
        {
            optionFolderName =
                Mod.Creator.NewSubFolderName(new DirectoryInfo(Path.Combine(_mod.ModPath.FullName, _selectedGroup?.Name ?? _newGroupName)),
                    _newOptionName);
            if (optionFolderName?.Exists == true)
                throw new Exception($"The folder {optionFolderName.FullName} for the option already exists.");

            if (optionFolderName != null)
            {
                if (_selectedGroup == null)
                {
                    _modManager.OptionEditor.AddModGroup(_mod, GroupType.Multi, _newGroupName);
                    _selectedGroup = _mod.Groups.Last();
                    groupCreated   = true;
                }

                _modManager.OptionEditor.AddOption(_mod, _mod.Groups.IndexOf(_selectedGroup), _newOptionName);
                optionCreated    = true;
                optionFolderName = Directory.CreateDirectory(optionFolderName.FullName);
                dirCreated       = true;
                if (!_swapData.WriteMod(_modManager, _mod, _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps,
                        optionFolderName,
                        _mod.Groups.IndexOf(_selectedGroup), _selectedGroup.Count - 1))
                    throw new Exception("Failure writing files for mod swap.");
            }
        }
        catch (Exception e)
        {
            Penumbra.ChatService.NotificationMessage($"Could not create new Swap Option:\n{e}", "Error", NotificationType.Error);
            try
            {
                if (optionCreated && _selectedGroup != null)
                    _modManager.OptionEditor.DeleteOption(_mod, _mod.Groups.IndexOf(_selectedGroup), _selectedGroup.Count - 1);

                if (groupCreated)
                {
                    _modManager.OptionEditor.DeleteModGroup(_mod, _mod.Groups.IndexOf(_selectedGroup!));
                    _selectedGroup = null;
                }

                if (dirCreated && optionFolderName != null)
                    Directory.Delete(optionFolderName.FullName, true);
            }
            catch
            {
                // ignored
            }
        }

        UpdateOption();
    }

    private void DrawHeaderLine(float width)
    {
        var newModAvailable = _loadException == null && _swapData.Loaded;

        ImGui.SetNextItemWidth(width);
        if (ImGui.InputTextWithHint("##newModName", "New Mod Name...", ref _newModName, 64))
        { }

        ImGui.SameLine();
        var tt = !newModAvailable
            ? "No swap is currently loaded."
            : _newModName.Length == 0
                ? "Please enter a name for your mod."
                : "Create a new mod of the given name containing only the swap.";
        if (ImGuiUtil.DrawDisabledButton("Create New Mod", new Vector2(width / 2, 0), tt, !newModAvailable || _newModName.Length == 0))
            CreateMod();

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 20 * UiHelpers.Scale);
        ImGui.Checkbox("Use File Swaps", ref _useFileSwaps);
        ImGuiUtil.HoverTooltip("Instead of writing every single non-default file to the newly created mod or option,\n"
          + "even those available from game files, use File Swaps to default game files where possible.");

        ImGui.SetNextItemWidth((width - ImGui.GetStyle().ItemSpacing.X) / 2);
        if (ImGui.InputTextWithHint("##groupName", "Group Name...", ref _newGroupName, 32))
            UpdateOption();

        ImGui.SameLine();
        ImGui.SetNextItemWidth((width - ImGui.GetStyle().ItemSpacing.X) / 2);
        if (ImGui.InputTextWithHint("##optionName", "New Option Name...", ref _newOptionName, 32))
            UpdateOption();

        ImGui.SameLine();
        tt = !_subModValid
            ? "An option with that name already exists in that group, or no name is specified."
            : !newModAvailable
                ? "Create a new option inside this mod containing only the swap."
                : "Create a new option (and possibly Multi-Group) inside the currently selected mod containing the swap.";
        if (ImGuiUtil.DrawDisabledButton("Create New Option", new Vector2(width / 2, 0), tt, !newModAvailable || !_subModValid))
            CreateOption();

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 20 * UiHelpers.Scale);
        _dirty |= ImGui.Checkbox("Use Entire Collection", ref _useCurrentCollection);
        ImGuiUtil.HoverTooltip(
            "Use all applied mods from the Selected Collection with their current settings and respecting the enabled state of mods and inheritance,\n"
          + "instead of using only the selected mod with its current settings in the Selected collection or the default settings, ignoring the enabled state and inheritance.");
    }

    private void DrawSwapBar()
    {
        using var bar = ImRaii.TabBar("##swapBar", ImGuiTabBarFlags.None);

        DrawEquipmentSwap(SwapType.Hat);
        DrawEquipmentSwap(SwapType.Top);
        DrawEquipmentSwap(SwapType.Gloves);
        DrawEquipmentSwap(SwapType.Pants);
        DrawEquipmentSwap(SwapType.Shoes);
        DrawEquipmentSwap(SwapType.Earrings);
        DrawEquipmentSwap(SwapType.Necklace);
        DrawEquipmentSwap(SwapType.Bracelet);
        DrawEquipmentSwap(SwapType.Ring);
        DrawAccessorySwap();
        DrawHairSwap();
        DrawFaceSwap();
        DrawEarSwap();
        DrawTailSwap();
        DrawWeaponSwap();
    }

    private ImRaii.IEndObject DrawTab(SwapType newTab)
    {
        using var tab = ImRaii.TabItem(newTab is SwapType.BetweenSlots ? "Between Slots" : newTab.ToString());
        if (tab)
        {
            _dirty   |= _lastTab != newTab;
            _lastTab =  newTab;
        }

        UpdateState();

        return tab;
    }

    private void DrawAccessorySwap()
    {
        using var tab = DrawTab(SwapType.BetweenSlots);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 3, ImGuiTableFlags.SizingFixedFit);
        ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("and put them on these").X);

        var (article1, article2, selector) = GetAccessorySelector(_slotFrom, true);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"Take {article1}");

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
        using (var combo = ImRaii.Combo("##fromType", _slotFrom is EquipSlot.Head ? "Hat" : _slotFrom.ToName()))
        {
            if (combo)
                foreach (var slot in EquipSlotExtensions.AccessorySlots.Prepend(EquipSlot.Head))
                {
                    if (!ImGui.Selectable(slot is EquipSlot.Head ? "Hat" : slot.ToName(), slot == _slotFrom) || slot == _slotFrom)
                        continue;

                    _dirty    = true;
                    _slotFrom = slot;
                    if (slot == _slotTo)
                        _slotTo = EquipSlotExtensions.AccessorySlots.First(s => slot != s);
                }
        }

        ImGui.TableNextColumn();
        _dirty |= selector.Draw("##itemSource", selector.CurrentSelection.Item1 ?? string.Empty, string.Empty, InputWidth * 2,
            ImGui.GetTextLineHeightWithSpacing());

        (article1, _, selector) = GetAccessorySelector(_slotTo, false);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"and put {article2} on {article1}");

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
        using (var combo = ImRaii.Combo("##toType", _slotTo.ToName()))
        {
            if (combo)
                foreach (var slot in EquipSlotExtensions.AccessorySlots.Where(s => s != _slotFrom))
                {
                    if (!ImGui.Selectable(slot.ToName(), slot == _slotTo) || slot == _slotTo)
                        continue;

                    _dirty  = true;
                    _slotTo = slot;
                }
        }

        ImGui.TableNextColumn();

        _dirty |= selector.Draw("##itemTarget", selector.CurrentSelection.Item1 ?? string.Empty, string.Empty, InputWidth * 2,
            ImGui.GetTextLineHeightWithSpacing());
        if (_affectedItems is not { Length: > 1 })
            return;

        ImGui.SameLine();
        ImGuiUtil.DrawTextButton($"which will also affect {_affectedItems.Length - 1} other Items.", Vector2.Zero,
            Colors.PressEnterWarningBg);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join('\n', _affectedItems.Where(i => !ReferenceEquals(i, selector.CurrentSelection.Item2))
                .Select(i => i.Name.ToDalamudString().TextValue)));
    }

    private (string, string, ItemSelector) GetAccessorySelector(EquipSlot slot, bool source)
    {
        var (type, article1, article2) = slot switch
        {
            EquipSlot.Head    => (SwapType.Hat, "this", "it"),
            EquipSlot.Ears    => (SwapType.Earrings, "these", "them"),
            EquipSlot.Neck    => (SwapType.Necklace, "this", "it"),
            EquipSlot.Wrists  => (SwapType.Bracelet, "these", "them"),
            EquipSlot.RFinger => (SwapType.Ring, "this", "it"),
            EquipSlot.LFinger => (SwapType.Ring, "this", "it"),
            _                 => (SwapType.Ring, "this", "it"),
        };
        var (itemSelector, target, _, _) = _selectors[type];
        return (article1, article2, source ? itemSelector : target);
    }

    private void DrawEquipmentSwap(SwapType type)
    {
        using var tab = DrawTab(type);
        if (!tab)
            return;

        var (sourceSelector, targetSelector, text1, text2) = _selectors[type];
        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text1);
        ImGui.TableNextColumn();
        _dirty |= sourceSelector.Draw("##itemSource", sourceSelector.CurrentSelection.Item1 ?? string.Empty, string.Empty, InputWidth * 2,
            ImGui.GetTextLineHeightWithSpacing());

        if (type == SwapType.Ring)
        {
            ImGui.SameLine();
            _dirty |= ImGui.Checkbox("Swap Right Ring", ref _useRightRing);
        }

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text2);
        ImGui.TableNextColumn();
        _dirty |= targetSelector.Draw("##itemTarget", targetSelector.CurrentSelection.Item1 ?? string.Empty, string.Empty, InputWidth * 2,
            ImGui.GetTextLineHeightWithSpacing());
        if (type == SwapType.Ring)
        {
            ImGui.SameLine();
            _dirty |= ImGui.Checkbox("Swap Left Ring", ref _useLeftRing);
        }

        if (_affectedItems is not { Length: > 1 })
            return;

        ImGui.SameLine();
        ImGuiUtil.DrawTextButton($"which will also affect {_affectedItems.Length - 1} other Items.", Vector2.Zero,
            Colors.PressEnterWarningBg);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join('\n', _affectedItems.Where(i => !ReferenceEquals(i, targetSelector.CurrentSelection.Item2))
                .Select(i => i.Name.ToDalamudString().TextValue)));
    }

    private void DrawHairSwap()
    {
        using var tab = DrawTab(SwapType.Hair);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput("Take this Hairstyle");
        DrawSourceIdInput();
        DrawGenderInput();
    }

    private void DrawFaceSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab(SwapType.Face);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput("Take this Face Type");
        DrawSourceIdInput();
        DrawGenderInput();
    }

    private void DrawTailSwap()
    {
        using var tab = DrawTab(SwapType.Tail);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput("Take this Tail Type");
        DrawSourceIdInput();
        DrawGenderInput("for all", 2);
    }


    private void DrawEarSwap()
    {
        using var tab = DrawTab(SwapType.Ears);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        DrawTargetIdInput("Take this Ear Type");
        DrawSourceIdInput();
        DrawGenderInput("for all Viera", 0);
    }


    private void DrawWeaponSwap()
    {
        using var disabled = ImRaii.Disabled();
        using var tab      = DrawTab(SwapType.Weapon);
        if (!tab)
            return;

        using var table = ImRaii.Table("##settings", 2, ImGuiTableFlags.SizingFixedFit);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Select the weapon or tool you want");
        ImGui.TableNextColumn();
        if (_slotSelector.Draw("##weaponSlot", _slotSelector.CurrentSelection.ToName(), string.Empty, InputWidth * 2,
                ImGui.GetTextLineHeightWithSpacing()))
        {
            _dirty        = true;
            _weaponSource = new ItemSelector(_itemService, _slotSelector.CurrentSelection);
            _weaponTarget = new ItemSelector(_itemService, _slotSelector.CurrentSelection);
        }
        else
        {
            _dirty        =   _weaponSource == null || _weaponTarget == null;
            _weaponSource ??= new ItemSelector(_itemService, _slotSelector.CurrentSelection);
            _weaponTarget ??= new ItemSelector(_itemService, _slotSelector.CurrentSelection);
        }

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("and put this variant of it");
        ImGui.TableNextColumn();
        _dirty |= _weaponSource.Draw("##weaponSource", _weaponSource.CurrentSelection.Item1 ?? string.Empty, string.Empty, InputWidth * 2,
            ImGui.GetTextLineHeightWithSpacing());

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("onto this one");
        ImGui.TableNextColumn();
        _dirty |= _weaponTarget.Draw("##weaponTarget", _weaponTarget.CurrentSelection.Item1 ?? string.Empty, string.Empty, InputWidth * 2,
            ImGui.GetTextLineHeightWithSpacing());
    }

    private const float InputWidth = 120;

    private void DrawTargetIdInput(string text = "Take this ID")
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(InputWidth * UiHelpers.Scale);
        if (ImGui.InputInt("##targetId", ref _targetId, 0, 0))
            _targetId = Math.Clamp(_targetId, 0, byte.MaxValue);

        _dirty |= ImGui.IsItemDeactivatedAfterEdit();
    }

    private void DrawSourceIdInput(string text = "and put it on this one")
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(InputWidth * UiHelpers.Scale);
        if (ImGui.InputInt("##sourceId", ref _sourceId, 0, 0))
            _sourceId = Math.Clamp(_sourceId, 0, byte.MaxValue);

        _dirty |= ImGui.IsItemDeactivatedAfterEdit();
    }

    private void DrawGenderInput(string text = "for all", int drawRace = 1)
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);

        ImGui.TableNextColumn();
        _dirty |= Combos.Gender("##Gender", InputWidth, _currentGender, out _currentGender);
        if (drawRace == 1)
        {
            ImGui.SameLine();
            _dirty |= Combos.Race("##Race", InputWidth, _currentRace, out _currentRace);
        }
        else if (drawRace == 2)
        {
            ImGui.SameLine();
            if (_currentRace is not ModelRace.Miqote and not ModelRace.AuRa and not ModelRace.Hrothgar)
                _currentRace = ModelRace.Miqote;

            _dirty |= ImGuiUtil.GenericEnumCombo("##Race", InputWidth, _currentRace, out _currentRace, new[]
                {
                    ModelRace.Miqote,
                    ModelRace.AuRa,
                    ModelRace.Hrothgar,
                },
                RaceEnumExtensions.ToName);
        }
    }

    private string NonExistentText()
        => _lastTab switch
        {
            SwapType.Hat      => "One of the selected hats does not seem to exist.",
            SwapType.Top      => "One of the selected tops does not seem to exist.",
            SwapType.Gloves   => "One of the selected pairs of gloves does not seem to exist.",
            SwapType.Pants    => "One of the selected pants does not seem to exist.",
            SwapType.Shoes    => "One of the selected pairs of shoes does not seem to exist.",
            SwapType.Earrings => "One of the selected earrings does not seem to exist.",
            SwapType.Necklace => "One of the selected necklaces does not seem to exist.",
            SwapType.Bracelet => "One of the selected bracelets does not seem to exist.",
            SwapType.Ring     => "One of the selected rings does not seem to exist.",
            SwapType.Hair     => "One of the selected hairstyles does not seem to exist for this gender and race combo.",
            SwapType.Face     => "One of the selected faces does not seem to exist for this gender and race combo.",
            SwapType.Ears     => "One of the selected ear types does not seem to exist for this gender and race combo.",
            SwapType.Tail     => "One of the selected tails does not seem to exist for this gender and race combo.",
            SwapType.Weapon   => "One of the selected weapons or tools does not seem to exist.",
            _                 => string.Empty,
        };

    private static void DrawSwap(Swap swap)
    {
        var       flags = swap.ChildSwaps.Count == 0 ? ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf : ImGuiTreeNodeFlags.DefaultOpen;
        using var tree  = ImRaii.TreeNode(SwapToString(swap), flags);
        if (!tree)
            return;

        foreach (var child in swap.ChildSwaps)
            DrawSwap(child);
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? oldCollection,
        ModCollection? newCollection, string _)
    {
        if (collectionType != CollectionType.Current || _mod == null || newCollection == null)
            return;

        UpdateMod(_mod, _mod.Index < newCollection.Settings.Count ? newCollection.Settings[_mod.Index] : null);
        newCollection.ModSettingChanged += OnSettingChange;
        if (oldCollection != null)
            oldCollection.ModSettingChanged -= OnSettingChange;
    }

    private void OnSettingChange(ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool inherited)
    {
        if (modIdx != _mod?.Index)
            return;

        _swapData.LoadMod(_mod, _modSettings);
        _dirty = true;
    }
}
