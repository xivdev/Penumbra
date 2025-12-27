using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
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

[NamedEnum(Utf16: false)]
public enum SwapType
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
    [Name("Between Slots")]
    BetweenSlots,
    Hair,
    Face,
    Ears,
    Tail,
    Weapon,
    Glasses,
}


[NamedEnum(Utf16: false)]
public enum BetweenSlotTypes
{
    Hat,
    Earrings,
    Necklace,
    Bracelets,
    [Name("Right Ring")]
    RightRing,
    [Name("Left Ring")]
    LeftRing,
    Glasses,
}

public class ItemSwapTab : IDisposable, ITab
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

        var a = collectionManager.Active;
        _selectors = new Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, StringU8 TextFrom, StringU8 TextTo)>
        {
            // @formatter:off
            [SwapType.Hat]      = (new ItemSelector(a, itemService, selector, FullEquipType.Head),    new ItemSelector(a, itemService, null, FullEquipType.Head),    new StringU8("Take this Hat"u8),        new StringU8("and put it on this one"u8) ),
            [SwapType.Top]      = (new ItemSelector(a, itemService, selector, FullEquipType.Body),    new ItemSelector(a, itemService, null, FullEquipType.Body),    new StringU8("Take this Top"u8),        new StringU8("and put it on this one"u8) ),
            [SwapType.Gloves]   = (new ItemSelector(a, itemService, selector, FullEquipType.Hands),   new ItemSelector(a, itemService, null, FullEquipType.Hands),   new StringU8("Take these Gloves"u8),    new StringU8("and put them on these"u8) ),
            [SwapType.Pants]    = (new ItemSelector(a, itemService, selector, FullEquipType.Legs),    new ItemSelector(a, itemService, null, FullEquipType.Legs),    new StringU8("Take these Pants"u8),     new StringU8("and put them on these"u8) ),
            [SwapType.Shoes]    = (new ItemSelector(a, itemService, selector, FullEquipType.Feet),    new ItemSelector(a, itemService, null, FullEquipType.Feet),    new StringU8("Take these Shoes"u8),     new StringU8("and put them on these"u8) ),
            [SwapType.Earrings] = (new ItemSelector(a, itemService, selector, FullEquipType.Ears),    new ItemSelector(a, itemService, null, FullEquipType.Ears),    new StringU8("Take these Earrings"u8),  new StringU8("and put them on these"u8) ),
            [SwapType.Necklace] = (new ItemSelector(a, itemService, selector, FullEquipType.Neck),    new ItemSelector(a, itemService, null, FullEquipType.Neck),    new StringU8("Take this Necklace"u8),   new StringU8("and put it on this one"u8) ),
            [SwapType.Bracelet] = (new ItemSelector(a, itemService, selector, FullEquipType.Wrists),  new ItemSelector(a, itemService, null, FullEquipType.Wrists),  new StringU8("Take these Bracelets"u8), new StringU8("and put them on these"u8) ),
            [SwapType.Ring]     = (new ItemSelector(a, itemService, selector, FullEquipType.Finger),  new ItemSelector(a, itemService, null, FullEquipType.Finger),  new StringU8("Take this Ring"u8),       new StringU8("and put it on this one"u8) ),
            [SwapType.Glasses]  = (new ItemSelector(a, itemService, selector, FullEquipType.Glasses), new ItemSelector(a, itemService, null, FullEquipType.Glasses), new StringU8("Take these Glasses"u8),   new StringU8("and put them on these"u8) ),
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

        var oldDefaultName = $"{_mod?.Name ?? "Unknown"} (Swapped)";
        if (_newModName.Length is 0 || oldDefaultName == _newModName)
            _newModName = $"{mod.Name} (Swapped)";

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
        Im.Line.New();
        DrawHeaderLine(300 * Im.Style.GlobalScale);
        Im.Line.New();

        DrawSwapBar();

        using var table = Im.ListBox.Begin("##swaps"u8, Im.ContentRegion.Available);
        if (_loadException is not null)
            Im.TextWrapped($"Could not load Customization Swap:\n{_loadException}");
        else if (_swapData.Loaded)
            foreach (var swap in _swapData.Swaps)
                DrawSwap(swap);
        else
            Im.Text(NonExistentText());
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ModSettingChanged.Unsubscribe(OnSettingChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
    }

    private readonly Dictionary<SwapType, (ItemSelector Source, ItemSelector Target, StringU8 TextFrom, StringU8 TextTo)> _selectors;
    private readonly ItemSwapContainer                                                                                    _swapData;

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
                    if (values.Source.CurrentSelection.Type is not FullEquipType.Unknown
                     && values.Target.CurrentSelection.Type is not FullEquipType.Unknown)
                        _affectedItems = _swapData.LoadEquipment(values.Target.CurrentSelection, values.Source.CurrentSelection,
                            _useCurrentCollection ? _collectionManager.Active.Current : null, _useRightRing, _useLeftRing);
                    break;
                case SwapType.BetweenSlots:
                    var (_, _, selectorFrom) = GetAccessorySelector(_slotFrom, true);
                    var (_, _, selectorTo)   = GetAccessorySelector(_slotTo,   false);
                    if (selectorFrom.CurrentSelection.Valid && selectorTo.CurrentSelection.Valid)
                        _affectedItems = _swapData.LoadTypeSwap(ToEquipSlot(_slotTo), selectorTo.CurrentSelection, ToEquipSlot(_slotFrom),
                            selectorFrom.CurrentSelection,
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
                    $"Created by swapping {GetAccessorySelector(_slotFrom, true).Item3.CurrentSelection.Name} onto {GetAccessorySelector(_slotTo, false).Item3.CurrentSelection.Name} in {_mod!.Name}{OriginalAuthor()}";
            default:
                return
                    $"Created by swapping {_selectors[_lastTab].Source.CurrentSelection.Name} onto {_selectors[_lastTab].Target.CurrentSelection.Name} in {_mod!.Name}{OriginalAuthor()}";
        }
    }

    private string OriginalAuthor()
    {
        if (_mod!.Author.Length is 0 || _mod!.Author is "TexTools User" or DefaultTexToolsData.Author)
            return ".";

        return $" by {_mod!.Author}.";
    }

    private string CreateAuthor()
    {
        if (_mod!.Author.Length is 0
         || _mod!.Author == _config.DefaultModAuthor
         || _mod!.Author is "TexTools User" or DefaultTexToolsData.Author)
            return _config.DefaultModAuthor;
        if (_config.DefaultModAuthor is DefaultTexToolsData.Author)
            return _mod!.Author;

        return $"{_mod!.Author} (Swap by {_config.DefaultModAuthor})";
    }

    private void UpdateOption()
    {
        _selectedGroup = _mod?.Groups.FirstOrDefault(g => g.Name == _newGroupName);
        _subModValid = _mod is not null
         && _newGroupName.Length > 0
         && _newOptionName.Length > 0
         && (_selectedGroup?.Options.All(o => o.Name != _newOptionName) ?? true);
    }

    private void CreateMod()
    {
        var newDir = _modManager.Creator.CreateEmptyMod(_modManager.BasePath, _newModName, CreateDescription(), CreateAuthor());
        if (newDir is null)
            return;

        _modManager.AddMod(newDir, false);
        var mod = _modManager[^1];
        if (!_swapData.WriteMod(_modManager, mod, mod.Default,
                _useFileSwaps ? ItemSwapContainer.WriteType.UseSwaps : ItemSwapContainer.WriteType.NoSwaps))
            _modManager.DeleteMod(mod);
    }

    private void CreateOption()
    {
        if (_mod is null || !_subModValid)
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

            if (optionFolderName is not null)
            {
                if (_selectedGroup is null)
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
        var newModAvailable = _loadException is null && _swapData.Loaded;

        Im.Item.SetNextWidth(width);
        if (Im.Input.Text("##newModName"u8, ref _newModName, "New Mod Name..."u8))
        { }

        Im.Line.Same();
        var tt = !newModAvailable
            ? "No swap is currently loaded."u8
            : _newModName.Length is 0
                ? "Please enter a name for your mod."u8
                : "Create a new mod of the given name containing only the swap."u8;
        if (ImEx.Button("Create New Mod"u8, new Vector2(width / 2, 0), tt, !newModAvailable || _newModName.Length is 0))
            CreateMod();

        Im.Line.Same();
        Im.Cursor.X += 20 * Im.Style.GlobalScale;
        Im.Checkbox("Use File Swaps"u8, ref _useFileSwaps);
        Im.Tooltip.OnHover("Instead of writing every single non-default file to the newly created mod or option,\n"u8
          + "even those available from game files, use File Swaps to default game files where possible."u8);

        Im.Item.SetNextWidth((width - Im.Style.ItemSpacing.X) / 2);
        if (Im.Input.Text("##groupName"u8, ref _newGroupName, "Group Name..."u8))
            UpdateOption();

        Im.Line.Same();
        Im.Item.SetNextWidth((width - Im.Style.ItemSpacing.X) / 2);
        if (Im.Input.Text("##optionName"u8, ref _newOptionName, "New Option Name..."u8))
            UpdateOption();

        Im.Line.Same();
        tt = !_subModValid
            ? "An option with that name already exists in that group, or no name is specified."u8
            : !newModAvailable
                ? "Create a new option inside this mod containing only the swap."u8
                : "Create a new option (and possibly Multi-Group) inside the currently selected mod containing the swap."u8;
        if (ImEx.Button("Create New Option"u8, new Vector2(width / 2, 0), tt, !newModAvailable || !_subModValid))
            CreateOption();

        Im.Line.Same();
        Im.Cursor.X += 20 * Im.Style.GlobalScale;
        _dirty |= Im.Checkbox("Use Entire Collection"u8, ref _useCurrentCollection);
        Im.Tooltip.OnHover("Use all applied mods from the Selected Collection with their current settings and respecting the enabled state of mods and inheritance,\n"u8
          + "instead of using only the selected mod with its current settings in the Selected collection or the default settings, ignoring the enabled state and inheritance."u8);
    }

    private void DrawSwapBar()
    {
        using var bar = Im.TabBar.Begin("##swapBar"u8);

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

    private Im.TabItemDisposable DrawTab(SwapType newTab)
    {
        var tab = Im.TabBar.BeginItem(newTab.ToNameU8());
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

        using var table = Im.Table.Begin("##settings"u8, 3, TableFlags.SizingFixedFit);
        table.SetupColumn("##text"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("and put them on these"u8).X);

        var (article1, article2, selector) = GetAccessorySelector(_slotFrom, true);
        table.DrawFrameColumn($"Take {article1}");

        table.NextColumn();
        Im.Item.SetNextWidthScaled(100);
        using (var combo = Im.Combo.Begin("##fromType"u8, _slotFrom.ToNameU8()))
        {
            if (combo)
                foreach (var slot in Enum.GetValues<BetweenSlotTypes>())
                {
                    if (!Im.Selectable(slot.ToNameU8(), slot == _slotFrom) || slot == _slotFrom)
                        continue;

                    _dirty    = true;
                    _slotFrom = slot;
                    if (slot == _slotTo)
                        _slotTo = AvailableToTypes.First(s => slot != s);
                }
        }

        table.NextColumn();
        _dirty |= selector.Draw("##itemSource"u8, selector.CurrentSelection.Name, StringU8.Empty, InputWidth * 2 * Im.Style.GlobalScale, out _);

        (article1, _, selector) = GetAccessorySelector(_slotTo, false);
        table.DrawFrameColumn($"and put {article2} on {article1}");

        table.NextColumn();
        Im.Item.SetNextWidthScaled(100);
        using (var combo = Im.Combo.Begin("##toType"u8, _slotTo.ToNameU8()))
        {
            if (combo)
                foreach (var slot in AvailableToTypes.Where(t => t != _slotFrom))
                {
                    if (!Im.Selectable(slot.ToNameU8(), slot == _slotTo) || slot == _slotTo)
                        continue;

                    _dirty  = true;
                    _slotTo = slot;
                }
        }

        table.NextColumn();
        _dirty |= selector.Draw("##itemTarget"u8, selector.CurrentSelection.Name, StringU8.Empty, InputWidth * 2 * Im.Style.GlobalScale, out _);
        if (_affectedItems is not { Count: > 1 })
            return;

        Im.Line.Same();
        ImEx.TextFramed($"which will also affect {_affectedItems.Count - 1} other Items.", Vector2.Zero, Colors.PressEnterWarningBg);
        if (Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            foreach (var item in _affectedItems.Where(i => !ReferenceEquals(i.Name, selector.CurrentSelection.Name)))
                Im.Text(item.Name);
        }
    }

    private RefTuple<ReadOnlySpan<byte>, ReadOnlySpan<byte>, ItemSelector> GetAccessorySelector(BetweenSlotTypes slot, bool source)
    {
        var (type, article1, article2) = slot switch
        {
            BetweenSlotTypes.Hat       => RefTuple.Create(SwapType.Hat,      "this"u8,  "it"u8),
            BetweenSlotTypes.Earrings  => RefTuple.Create(SwapType.Earrings, "these"u8, "them"u8),
            BetweenSlotTypes.Necklace  => RefTuple.Create(SwapType.Necklace, "this"u8,  "it"u8),
            BetweenSlotTypes.Bracelets => RefTuple.Create(SwapType.Bracelet, "these"u8, "them"u8),
            BetweenSlotTypes.RightRing => RefTuple.Create(SwapType.Ring,     "this"u8,  "it"u8),
            BetweenSlotTypes.LeftRing  => RefTuple.Create(SwapType.Ring,     "this"u8, "it"u8),
            BetweenSlotTypes.Glasses   => RefTuple.Create(SwapType.Glasses,  "these"u8, "them"u8),
            _                          => RefTuple.Create(SwapType.Ring,     "this"u8,  "it"u8),
        };
        var (itemSelector, target, _, _) = _selectors[type];
        return RefTuple.Create(article1, article2, source ? itemSelector : target);
    }

    private void DrawEquipmentSwap(SwapType type)
    {
        using var tab = DrawTab(type);
        if (!tab)
            return;

        var (sourceSelector, targetSelector, text1, text2) = _selectors[type];
        using var table = Im.Table.Begin("##settings"u8, 2, TableFlags.SizingFixedFit);
        if (!table)
            return;
        table.DrawFrameColumn(text1);
        table.NextColumn();
        _dirty |= sourceSelector.Draw("##itemSource"u8, sourceSelector.CurrentSelection.Name, StringU8.Empty, InputWidth * 2 * Im.Style.GlobalScale, out _);

        if (type is SwapType.Ring)
        {
            Im.Line.Same();
            _dirty |= Im.Checkbox("Swap Right Ring"u8, ref _useRightRing);
        }

        table.DrawFrameColumn(text2);
        table.NextColumn();
        _dirty |= targetSelector.Draw("##itemTarget"u8, targetSelector.CurrentSelection.Name, StringU8.Empty, InputWidth * 2 * Im.Style.GlobalScale, out _);
        if (type is SwapType.Ring)
        {
            Im.Line.Same();
            _dirty |= Im.Checkbox("Swap Left Ring"u8, ref _useLeftRing);
        }

        if (_affectedItems is not { Count: > 1 })
            return;

        Im.Line.Same();
        ImEx.TextFramed($"which will also affect {_affectedItems.Count - 1} other Items.", Vector2.Zero, Colors.PressEnterWarningBg);
        if (Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            foreach (var item in _affectedItems.Where(i => !ReferenceEquals(i.Name, targetSelector.CurrentSelection.Name)))
                Im.Text(item.Name);
        }
    }

    private void DrawHairSwap()
    {
        using var tab = DrawTab(SwapType.Hair);
        if (!tab)
            return;

        using var table = Im.Table.Begin("##settings"u8, 2, TableFlags.SizingFixedFit);
        DrawTargetIdInput(table, "Take this Hairstyle"u8);
        DrawSourceIdInput(table, "and put it on this one"u8);
        DrawGenderInput(table, "for all"u8);
    }

    private void DrawTailSwap()
    {
        using var tab = DrawTab(SwapType.Tail);
        if (!tab)
            return;

        using var table = Im.Table.Begin("##settings"u8, 2, TableFlags.SizingFixedFit);
        DrawTargetIdInput(table, "Take this Tail Type"u8);
        DrawSourceIdInput(table, "and put it on this one"u8);
        DrawGenderInput(table, "for all"u8, 2);
    }


    private void DrawEarSwap()
    {
        using var tab = DrawTab(SwapType.Ears);
        if (!tab)
            return;

        using var table = Im.Table.Begin("##settings"u8, 2, TableFlags.SizingFixedFit);
        DrawTargetIdInput(table, "Take this Ear Type"u8);
        DrawSourceIdInput(table, "and put it on this one"u8);
        DrawGenderInput(table, "for all Viera"u8, 0);
    }

    private const float InputWidth = 120;

    private void DrawTargetIdInput(in Im.TableDisposable table, ReadOnlySpan<byte> text)
    {
        table.DrawFrameColumn(text);
        table.NextColumn();
        Im.Item.SetNextWidthScaled(InputWidth);
        if (Im.Input.Scalar("##targetId"u8, ref _targetId))
            _targetId = Math.Clamp(_targetId, 0, byte.MaxValue);

        _dirty |= Im.Item.DeactivatedAfterEdit;
    }

    private void DrawSourceIdInput(in Im.TableDisposable table, ReadOnlySpan<byte> text)
    {
        table.DrawFrameColumn(text);

        table.NextColumn();
        Im.Item.SetNextWidthScaled(InputWidth);
        if (Im.Input.Scalar("##sourceId"u8, ref _sourceId))
            _sourceId = Math.Clamp(_sourceId, 0, byte.MaxValue);

        _dirty |= Im.Item.DeactivatedAfterEdit;
    }

    private void DrawGenderInput(in Im.TableDisposable table, ReadOnlySpan<byte> text, int drawRace = 1)
    {
        table.DrawFrameColumn(text);

        table.NextColumn();
        _dirty |= Combos.Gender.Draw("##Gender"u8, ref _currentGender, StringU8.Empty, 120 * Im.Style.GlobalScale);
        if (drawRace is 1)
        {
            Im.Line.Same();
            _dirty |= Combos.ModelRace.Draw("##Race"u8, ref _currentRace, StringU8.Empty, InputWidth);
        }
        else if (drawRace is 2)
        {
            Im.Line.Same();
            if (_currentRace is not ModelRace.Miqote and not ModelRace.AuRa and not ModelRace.Hrothgar)
                _currentRace = ModelRace.Miqote;

            _dirty |= Combos.TailedRace.Draw("##Race"u8, ref _currentRace, StringU8.Empty, InputWidth);
        }
    }

    private ReadOnlySpan<byte> NonExistentText()
        => _lastTab switch
        {
            SwapType.Hat      => "One of the selected hats does not seem to exist."u8,
            SwapType.Top      => "One of the selected tops does not seem to exist."u8,
            SwapType.Gloves   => "One of the selected pairs of gloves does not seem to exist."u8,
            SwapType.Pants    => "One of the selected pants does not seem to exist."u8,
            SwapType.Shoes    => "One of the selected pairs of shoes does not seem to exist."u8,
            SwapType.Earrings => "One of the selected earrings does not seem to exist."u8,
            SwapType.Necklace => "One of the selected necklaces does not seem to exist."u8,
            SwapType.Bracelet => "One of the selected bracelets does not seem to exist."u8,
            SwapType.Ring     => "One of the selected rings does not seem to exist."u8,
            SwapType.Glasses  => "One of the selected glasses does not seem to exist."u8,
            SwapType.Hair     => "One of the selected hairstyles does not seem to exist for this gender and race combo."u8,
            SwapType.Face     => "One of the selected faces does not seem to exist for this gender and race combo."u8,
            SwapType.Ears     => "One of the selected ear types does not seem to exist for this gender and race combo."u8,
            SwapType.Tail     => "One of the selected tails does not seem to exist for this gender and race combo."u8,
            SwapType.Weapon   => "One of the selected weapons or tools does not seem to exist."u8,
            _                 => StringU8.Empty,
        };

    private static void DrawSwap(Swap swap)
    {
        var       flags = swap.ChildSwaps.Count is 0 ? TreeNodeFlags.Bullet | TreeNodeFlags.Leaf : TreeNodeFlags.DefaultOpen;
        using var tree  = Im.Tree.Node(SwapToString(swap), flags);
        if (!tree)
            return;

        foreach (var child in swap.ChildSwaps)
            DrawSwap(child);
    }

    private void OnCollectionChange(in CollectionChange.Arguments arguments)
    {
        if (arguments.Type is not CollectionType.Current || _mod is null || arguments.NewCollection is null)
            return;

        UpdateMod(_mod,
            _mod.Index < arguments.NewCollection.Settings.Count ? arguments.NewCollection.GetInheritedSettings(_mod.Index).Settings : null);
    }

    private void OnSettingChange(in ModSettingChanged.Arguments arguments)
    {
        if (arguments.Collection != _collectionManager.Active.Current
         || arguments.Mod != _mod
         || arguments.Type is ModSettingChange.TemporarySetting)
            return;

        _swapData.LoadMod(_mod, _modSettings);
        _dirty = true;
    }

    private void OnInheritanceChange(in CollectionInheritanceChanged.Arguments arguments)
    {
        if (arguments.Collection != _collectionManager.Active.Current || _mod is null)
            return;

        UpdateMod(_mod, arguments.Collection.GetInheritedSettings(_mod.Index).Settings);
        _swapData.LoadMod(_mod, _modSettings);
        _dirty = true;
    }

    private void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        if (arguments.Type is ModOptionChangeType.PrepareChange or ModOptionChangeType.GroupAdded or ModOptionChangeType.OptionAdded
         || arguments.Mod != _mod)
            return;

        _swapData.LoadMod(_mod, _modSettings);
        UpdateOption();
        _dirty = true;
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

    private static readonly IReadOnlyList<BetweenSlotTypes> AvailableToTypes =
        Enum.GetValues<BetweenSlotTypes>().Where(s => s is not BetweenSlotTypes.Hat).ToArray();
}
