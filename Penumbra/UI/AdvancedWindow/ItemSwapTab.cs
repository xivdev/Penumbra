using Dalamud.Interface.ImGuiNotification;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Import.Structs;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.ItemSwap;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.AdvancedWindow;

public class ItemSwapTab : IDisposable, ITab, IUiService
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly CollectionManager   _collectionManager;
    private readonly ModManager          _modManager;
    private readonly MetaFileManager     _metaFileManager;

    public ItemSwapTab(CommunicatorService communicator, ItemData itemService, CollectionManager collectionManager,
        ModManager modManager, ModFileSystemSelector selector, ObjectIdentification identifier, MetaFileManager metaFileManager,
        Configuration config)
    {
        _communicator      = communicator;
        _collectionManager = collectionManager;
        _modManager        = modManager;
        _metaFileManager   = metaFileManager;
        _config            = config;
        _swapData          = new ItemSwapContainer(metaFileManager, identifier);

        _selectors = new Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, string TextFrom, string TextTo)>
        {
            // @formatter:off
            [SwapType.Hat]      = (new ItemSelector(itemService, selector, FullEquipType.Head),    new ItemSelector(itemService, null, FullEquipType.Head),    "Take this Hat",        "and put it on this one" ),
            [SwapType.Top]      = (new ItemSelector(itemService, selector, FullEquipType.Body),    new ItemSelector(itemService, null, FullEquipType.Body),    "Take this Top",        "and put it on this one" ),
            [SwapType.Gloves]   = (new ItemSelector(itemService, selector, FullEquipType.Hands),   new ItemSelector(itemService, null, FullEquipType.Hands),   "Take these Gloves",    "and put them on these"  ),
            [SwapType.Pants]    = (new ItemSelector(itemService, selector, FullEquipType.Legs),    new ItemSelector(itemService, null, FullEquipType.Legs),    "Take these Pants",     "and put them on these"  ),
            [SwapType.Shoes]    = (new ItemSelector(itemService, selector, FullEquipType.Feet),    new ItemSelector(itemService, null, FullEquipType.Feet),    "Take these Shoes",     "and put them on these"  ),
            [SwapType.Earrings] = (new ItemSelector(itemService, selector, FullEquipType.Ears),    new ItemSelector(itemService, null, FullEquipType.Ears),    "Take these Earrings",  "and put them on these"  ),
            [SwapType.Necklace] = (new ItemSelector(itemService, selector, FullEquipType.Neck),    new ItemSelector(itemService, null, FullEquipType.Neck),    "Take this Necklace",   "and put it on this one" ),
            [SwapType.Bracelet] = (new ItemSelector(itemService, selector, FullEquipType.Wrists),  new ItemSelector(itemService, null, FullEquipType.Wrists),  "Take these Bracelets", "and put them on these"  ),
            [SwapType.Ring]     = (new ItemSelector(itemService, selector, FullEquipType.Finger),  new ItemSelector(itemService, null, FullEquipType.Finger),  "Take this Ring",       "and put it on this one" ),
            [SwapType.Glasses]  = (new ItemSelector(itemService, selector, FullEquipType.Glasses), new ItemSelector(itemService, null, FullEquipType.Glasses), "Take these Glasses",   "and put them on these" ),
            // @formatter:on
        };

        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ItemSwapTab);
        _communicator.ModSettingChanged.Subscribe(OnSettingChange, ModSettingChanged.Priority.ItemSwapTab);
        _communicator.CollectionInheritanceChanged.Subscribe(OnInheritanceChange, CollectionInheritanceChanged.Priority.ItemSwapTab);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.ItemSwapTab);
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
        => "Item Swap"u8;

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
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ModSettingChanged.Unsubscribe(OnSettingChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
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
        Glasses,
    }

    private class ItemSelector(ItemData data, ModFileSystemSelector? selector, FullEquipType type)
        : FilterComboCache<(EquipItem Item, bool InMod)>(() =>
        {
            var list = data.ByType[type];
            if (selector?.Selected is { } mod && mod.ChangedItems.Values.Any(o => o is IdentifiedItem i && i.Item.Type == type))
                return list.Select(i => (i, mod.ChangedItems.ContainsKey(i.Name))).OrderByDescending(p => p.Item2).ToList();

            return list.Select(i => (i, false)).ToList();
        }, MouseWheelType.None, Penumbra.Log)
    {
        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.ResTreeLocalPlayer.Value(), Items[globalIdx].InMod);
            return base.DrawSelectable(globalIdx, selected);
        }

        protected override string ToString((EquipItem Item, bool InMod) obj)
            => obj.Item.Name;
    }

    private readonly Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, string TextFrom, string TextTo)> _selectors;
    private readonly ItemSwapContainer                                                                                _swapData;

    private Mod?         _mod;
    private ModSettings? _modSettings;
    private bool         _dirty;

    private SwapType         _lastTab       = SwapType.Hair;
    private Gender           _currentGender = Gender.Male;
    private ModelRace        _currentRace   = ModelRace.Midlander;
    private int              _targetId;
    private int              _sourceId;
    private Exception?       _loadException;
    private BetweenSlotTypes _slotFrom = BetweenSlotTypes.Hat;
    private BetweenSlotTypes _slotTo   = BetweenSlotTypes.Earrings;

    private string     _newModName    = string.Empty;
    private string     _newGroupName  = "Swaps";
    private string     _newOptionName = string.Empty;
    private IModGroup? _selectedGroup;
    private bool       _subModValid;
    private bool       _useFileSwaps = true;
    private bool       _useCurrentCollection;
    private bool       _useLeftRing  = true;
    private bool       _useRightRing = true;

    private HashSet<EquipItem>? _affectedItems;

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
                case SwapType.Glasses:
                    var values = _selectors[_lastTab];
                    if (values.Source.CurrentSelection.Item.Type != FullEquipType.Unknown
                     && values.Target.CurrentSelection.Item.Type != FullEquipType.Unknown)
                        _affectedItems = _swapData.LoadEquipment(values.Target.CurrentSelection.Item, values.Source.CurrentSelection.Item,
                            _useCurrentCollection ? _collectionManager.Active.Current : null, _useRightRing, _useLeftRing);
                    break;
                case SwapType.BetweenSlots:
                    var (_, _, selectorFrom) = GetAccessorySelector(_slotFrom, true);
                    var (_, _, selectorTo)   = GetAccessorySelector(_slotTo,   false);
                    if (selectorFrom.CurrentSelection.Item.Valid && selectorTo.CurrentSelection.Item.Valid)
                        _affectedItems = _swapData.LoadTypeSwap(ToEquipSlot(_slotTo), selectorTo.CurrentSelection.Item, ToEquipSlot(_slotFrom),
                            selectorFrom.CurrentSelection.Item,
                            _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.Hair when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Hair, Names.CombinedRace(_currentGender, _currentRace),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.Face when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Face, Names.CombinedRace(_currentGender, _currentRace),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.Ears when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Ear, Names.CombinedRace(_currentGender, ModelRace.Viera),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
                    break;
                case SwapType.Tail when _targetId > 0 && _sourceId > 0:
                    _swapData.LoadCustomization(_metaFileManager, BodySlot.Tail, Names.CombinedRace(_currentGender, _currentRace),
                        (PrimaryId)_sourceId,
                        (PrimaryId)_targetId,
                        _useCurrentCollection ? _collectionManager.Active.Current : null);
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
            IMetaSwap meta => $"{meta.SwapFromIdentifier}: {meta.SwapFromDefaultEntry} -> {meta.SwapToModdedEntry}",
            FileSwap file =>
                $"{file.Type}: {file.SwapFromRequestPath} -> {file.SwapToModded.FullName}{(file.DataWasChanged ? " (EDITED)" : string.Empty)}",
            _ => string.Empty,
        };
    }

    private string CreateDescription()
    {
        switch (_lastTab)
        {
            case SwapType.Ears:
            case SwapType.Face:
            case SwapType.Hair:
            case SwapType.Tail:
                return
                    $"Created by swapping {_lastTab} {_sourceId} onto {_lastTab} {_targetId} for {_currentRace.ToName()} {_currentGender.ToName()}s in {_mod!.Name}{OriginalAuthor()}";
            case SwapType.BetweenSlots:
                return
                    $"Created by swapping {GetAccessorySelector(_slotFrom, true).Item3.CurrentSelection.Item.Name} onto {GetAccessorySelector(_slotTo, false).Item3.CurrentSelection.Item.Name} in {_mod!.Name}{OriginalAuthor()}";
            default:
                return
                    $"Created by swapping {_selectors[_lastTab].Source.CurrentSelection.Item.Name} onto {_selectors[_lastTab].Target.CurrentSelection.Item.Name} in {_mod!.Name}{OriginalAuthor()}";
        }
    }

    private string OriginalAuthor()
    {
        if (_mod!.Author.IsEmpty || _mod!.Author.Text is "TexTools User" or DefaultTexToolsData.Author)
            return ".";

        return $" by {_mod!.Author}.";
    }

    private string CreateAuthor()
    {
        if (_mod!.Author.IsEmpty)
            return _config.DefaultModAuthor;
        if (_mod!.Author.Text == _config.DefaultModAuthor)
            return _config.DefaultModAuthor;
        if (_mod!.Author.Text is "TexTools User" or DefaultTexToolsData.Author)
            return _config.DefaultModAuthor;
        if (_config.DefaultModAuthor is DefaultTexToolsData.Author)
            return _mod!.Author;

        return $"{_mod!.Author} (Swap by {_config.DefaultModAuthor})";
    }

    private void UpdateOption()
    {
        _selectedGroup = _mod?.Groups.FirstOrDefault(g => g.Name == _newGroupName);
        _subModValid = _mod != null
         && _newGroupName.Length > 0
         && _newOptionName.Length > 0
         && (_selectedGroup?.Options.All(o => o.Name != _newOptionName) ?? true);
    }

    private void CreateMod()
    {
        var newDir = _modManager.Creator.CreateEmptyMod(_modManager.BasePath, _newModName, CreateDescription(), CreateAuthor());
        if (newDir == null)
            return;

        _modManager.AddMod(newDir, false);
        var mod = _modManager[^1];
        if (!_swapData.WriteMod(_modManager, mod, mod.Default,
                _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps))
            _modManager.DeleteMod(mod);
    }

    private void CreateOption()
    {
        if (_mod == null || !_subModValid)
            return;

        var            groupCreated     = false;
        var            dirCreated       = false;
        IModOption?    createdOption    = null;
        DirectoryInfo? optionFolderName = null;
        try
        {
            optionFolderName =
                ModCreator.NewSubFolderName(new DirectoryInfo(Path.Combine(_mod.ModPath.FullName, _selectedGroup?.Name ?? _newGroupName)),
                    _newOptionName, _config.ReplaceNonAsciiOnImport);
            if (optionFolderName?.Exists == true)
                throw new Exception($"The folder {optionFolderName.FullName} for the option already exists.");

            if (optionFolderName != null)
            {
                if (_selectedGroup == null)
                {
                    if (_modManager.OptionEditor.AddModGroup(_mod, GroupType.Multi, _newGroupName) is not { } group)
                        throw new Exception($"Failure creating option group.");

                    _selectedGroup = group;
                    groupCreated   = true;
                }

                if (_modManager.OptionEditor.AddOption(_selectedGroup, _newOptionName) is not { } option)
                    throw new Exception($"Failure creating mod option.");

                createdOption    = option;
                optionFolderName = Directory.CreateDirectory(optionFolderName.FullName);
                dirCreated       = true;
                // #TODO ModOption <> DataContainer
                if (!_swapData.WriteMod(_modManager, _mod, (IModDataContainer)option,
                        _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps, optionFolderName))
                    throw new Exception("Failure writing files for mod swap.");
            }
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, "Could not create new Swap Option.", NotificationType.Error, false);
            try
            {
                if (createdOption != null)
                    _modManager.OptionEditor.DeleteOption(createdOption);

                if (groupCreated)
                {
                    _modManager.OptionEditor.DeleteModGroup(_selectedGroup!);
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
        DrawEquipmentSwap(SwapType.Glasses);
        DrawAccessorySwap();
        DrawHairSwap();
        //DrawFaceSwap();
        DrawEarSwap();
        DrawTailSwap();
        //DrawWeaponSwap();
    }

    private ImRaii.IEndObject DrawTab(SwapType newTab)
    {
        var tab = ImRaii.TabItem(newTab is SwapType.BetweenSlots ? "Between Slots" : newTab.ToString());
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
        using (var combo = ImRaii.Combo("##fromType", ToName(_slotFrom)))
        {
            if (combo)
                foreach (var slot in Enum.GetValues<BetweenSlotTypes>())
                {
                    if (!ImGui.Selectable(ToName(slot), slot == _slotFrom) || slot == _slotFrom)
                        continue;

                    _dirty    = true;
                    _slotFrom = slot;
                    if (slot == _slotTo)
                        _slotTo = AvailableToTypes.First(s => slot != s);
                }
        }

        ImGui.TableNextColumn();
        _dirty |= selector.Draw("##itemSource", selector.CurrentSelection.Item.Name ?? string.Empty, string.Empty,
            InputWidth * 2 * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());

        (article1, _, selector) = GetAccessorySelector(_slotTo, false);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"and put {article2} on {article1}");

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
        using (var combo = ImRaii.Combo("##toType", ToName(_slotTo)))
        {
            if (combo)
                foreach (var slot in AvailableToTypes.Where(t => t != _slotFrom))
                {
                    if (!ImGui.Selectable(ToName(slot), slot == _slotTo) || slot == _slotTo)
                        continue;

                    _dirty  = true;
                    _slotTo = slot;
                }
        }

        ImGui.TableNextColumn();

        _dirty |= selector.Draw("##itemTarget", selector.CurrentSelection.Item.Name, string.Empty, InputWidth * 2 * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());
        if (_affectedItems is not { Count: > 1 })
            return;

        ImGui.SameLine();
        ImGuiUtil.DrawTextButton($"which will also affect {_affectedItems.Count - 1} other Items.", Vector2.Zero,
            Colors.PressEnterWarningBg);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join('\n', _affectedItems.Where(i => !ReferenceEquals(i.Name, selector.CurrentSelection.Item.Name))
                .Select(i => i.Name)));
    }

    private (string, string, ItemSelector) GetAccessorySelector(BetweenSlotTypes slot, bool source)
    {
        var (type, article1, article2) = slot switch
        {
            BetweenSlotTypes.Hat       => (SwapType.Hat, "this", "it"),
            BetweenSlotTypes.Earrings  => (SwapType.Earrings, "these", "them"),
            BetweenSlotTypes.Necklace  => (SwapType.Necklace, "this", "it"),
            BetweenSlotTypes.Bracelets => (SwapType.Bracelet, "these", "them"),
            BetweenSlotTypes.RightRing => (SwapType.Ring, "this", "it"),
            BetweenSlotTypes.LeftRing  => (SwapType.Ring, "this", "it"),
            BetweenSlotTypes.Glasses   => (SwapType.Glasses, "these", "them"),
            _                          => (SwapType.Ring, "this", "it"),
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
        _dirty |= sourceSelector.Draw("##itemSource", sourceSelector.CurrentSelection.Item.Name, string.Empty, InputWidth * 2 * UiHelpers.Scale,
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
        _dirty |= targetSelector.Draw("##itemTarget", targetSelector.CurrentSelection.Item.Name, string.Empty, InputWidth * 2 * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());
        if (type == SwapType.Ring)
        {
            ImGui.SameLine();
            _dirty |= ImGui.Checkbox("Swap Left Ring", ref _useLeftRing);
        }

        if (_affectedItems is not { Count: > 1 })
            return;

        ImGui.SameLine();
        ImGuiUtil.DrawTextButton($"which will also affect {_affectedItems.Count - 1} other Items.", Vector2.Zero,
            Colors.PressEnterWarningBg);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join('\n', _affectedItems.Where(i => !ReferenceEquals(i.Name, targetSelector.CurrentSelection.Item.Name))
                .Select(i => i.Name)));
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
        _dirty |= Combos.Gender("##Gender", _currentGender, out _currentGender, InputWidth);
        if (drawRace == 1)
        {
            ImGui.SameLine();
            _dirty |= Combos.Race("##Race", _currentRace, out _currentRace, InputWidth);
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
            SwapType.Glasses  => "One of the selected glasses does not seem to exist.",
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
        if (collectionType is not CollectionType.Current || _mod == null || newCollection == null)
            return;

        UpdateMod(_mod, _mod.Index < newCollection.Settings.Count ? newCollection.GetInheritedSettings(_mod.Index).Settings : null);
    }

    private void OnSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, Setting oldValue, int groupIdx, bool inherited)
    {
        if (collection != _collectionManager.Active.Current || mod != _mod || type is ModSettingChange.TemporarySetting)
            return;

        _swapData.LoadMod(_mod, _modSettings);
        _dirty = true;
    }

    private void OnInheritanceChange(ModCollection collection, bool _)
    {
        if (collection != _collectionManager.Active.Current || _mod == null)
            return;

        UpdateMod(_mod, collection.GetInheritedSettings(_mod.Index).Settings);
        _swapData.LoadMod(_mod, _modSettings);
        _dirty = true;
    }

    private void OnModOptionChange(ModOptionChangeType type, Mod mod, IModGroup? group, IModOption? option, IModDataContainer? container,
        int fromIdx)
    {
        if (type is ModOptionChangeType.PrepareChange or ModOptionChangeType.GroupAdded or ModOptionChangeType.OptionAdded || mod != _mod)
            return;

        _swapData.LoadMod(_mod, _modSettings);
        UpdateOption();
        _dirty = true;
    }

    private enum BetweenSlotTypes
    {
        Hat,
        Earrings,
        Necklace,
        Bracelets,
        RightRing,
        LeftRing,
        Glasses,
    }

    private static EquipSlot ToEquipSlot(BetweenSlotTypes type)
        => type switch
        {
            BetweenSlotTypes.Hat       => EquipSlot.Head,
            BetweenSlotTypes.Earrings  => EquipSlot.Ears,
            BetweenSlotTypes.Necklace  => EquipSlot.Neck,
            BetweenSlotTypes.Bracelets => EquipSlot.Wrists,
            BetweenSlotTypes.RightRing => EquipSlot.RFinger,
            BetweenSlotTypes.LeftRing  => EquipSlot.LFinger,
            BetweenSlotTypes.Glasses   => BonusItemFlag.Glasses.ToEquipSlot(),
            _                          => EquipSlot.Unknown,
        };

    private static string ToName(BetweenSlotTypes type)
        => type switch
        {
            BetweenSlotTypes.Hat       => "Hat",
            BetweenSlotTypes.Earrings  => "Earrings",
            BetweenSlotTypes.Necklace  => "Necklace",
            BetweenSlotTypes.Bracelets => "Bracelets",
            BetweenSlotTypes.RightRing => "Right Ring",
            BetweenSlotTypes.LeftRing  => "Left Ring",
            BetweenSlotTypes.Glasses   => "Glasses",
            _                          => "Unknown",
        };

    private static readonly IReadOnlyList<BetweenSlotTypes> AvailableToTypes =
        Enum.GetValues<BetweenSlotTypes>().Where(s => s is not BetweenSlotTypes.Hat).ToArray();
}
