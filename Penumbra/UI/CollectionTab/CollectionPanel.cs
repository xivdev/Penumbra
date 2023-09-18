using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionPanel : IDisposable
{
    private readonly CollectionStorage      _collections;
    private readonly ActiveCollections      _active;
    private readonly CollectionSelector     _selector;
    private readonly ActorService           _actors;
    private readonly ITargetManager         _targets;
    private readonly IndividualAssignmentUi _individualAssignmentUi;
    private readonly InheritanceUi          _inheritanceUi;
    private readonly ModStorage             _mods;

    private readonly GameFontHandle _nameFont;

    private static readonly IReadOnlyDictionary<CollectionType, (string Name, uint Border)> Buttons      = CreateButtons();
    private static readonly IReadOnlyList<(CollectionType, bool, bool, string, uint)>       AdvancedTree = CreateTree();
    private readonly        List<(CollectionType Type, ActorIdentifier Identifier)>         _inUseCache  = new();

    private int _draggedIndividualAssignment = -1;

    public CollectionPanel(DalamudPluginInterface pi, CommunicatorService communicator, CollectionManager manager,
        CollectionSelector selector, ActorService actors, ITargetManager targets, ModStorage mods)
    {
        _collections            = manager.Storage;
        _active                 = manager.Active;
        _selector               = selector;
        _actors                 = actors;
        _targets                = targets;
        _mods                   = mods;
        _individualAssignmentUi = new IndividualAssignmentUi(communicator, actors, manager);
        _inheritanceUi          = new InheritanceUi(manager, _selector);
        _nameFont               = pi.UiBuilder.GetGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter23));
    }

    public void Dispose()
    {
        _individualAssignmentUi.Dispose();
        _nameFont.Dispose();
    }

    /// <summary> Draw the panel containing beginners information and simple assignments. </summary>
    public void DrawSimple()
    {
        ImGuiUtil.TextWrapped("A collection is a set of mod configurations. You can have as many collections as you desire.\n"
          + "The collection you are currently editing in the mod tab can be selected here and is highlighted.\n");
        ImGuiUtil.TextWrapped(
            "There are functions you can assign these collections to, so different mod configurations apply for different things.\n"
          + "You can assign an existing collection to such a function by clicking the function or dragging the collection over.");
        ImGui.Separator();

        var buttonWidth = new Vector2(200 * ImGuiHelpers.GlobalScale, 2 * ImGui.GetTextLineHeightWithSpacing());
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, 1 * ImGuiHelpers.GlobalScale);
        DrawSimpleCollectionButton(CollectionType.Default,                  buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Interface,                buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Yourself,                 buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MalePlayerCharacter,      buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemalePlayerCharacter,    buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MaleNonPlayerCharacter,   buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemaleNonPlayerCharacter, buttonWidth);

        ImGuiUtil.DrawColoredText(("Individual ", ColorId.NewMod.Value()),
            ("Assignments take precedence before anything else and only apply to one specific character or monster.", 0));
        ImGui.Dummy(Vector2.UnitX);

        var specialWidth = buttonWidth with { X = 275 * ImGuiHelpers.GlobalScale };
        DrawCurrentCharacter(specialWidth);
        ImGui.SameLine();
        DrawCurrentTarget(specialWidth);
        DrawIndividualCollections(buttonWidth);

        var first = true;

        void Button(CollectionType type)
        {
            var (name, border) = Buttons[type];
            var collection = _active.ByType(type);
            if (collection == null)
                return;

            if (first)
            {
                ImGui.Separator();
                ImGui.TextUnformatted("Currently Active Advanced Assignments");
                first = false;
            }

            DrawButton(name, type, buttonWidth, border, ActorIdentifier.Invalid, 's', collection);
            ImGui.SameLine();
            if (ImGui.GetContentRegionAvail().X < buttonWidth.X + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X)
                ImGui.NewLine();
        }

        Button(CollectionType.NonPlayerChild);
        Button(CollectionType.NonPlayerElderly);
        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male,   false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male,   true));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, true));
        }
    }

    /// <summary> Draw the panel containing new and existing individual assignments. </summary>
    public void DrawIndividualPanel()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, 1 * ImGuiHelpers.GlobalScale);
        var width = new Vector2(300 * ImGuiHelpers.GlobalScale, 2 * ImGui.GetTextLineHeightWithSpacing());

        ImGui.Dummy(Vector2.One);
        DrawCurrentCharacter(width);
        ImGui.SameLine();
        DrawCurrentTarget(width);
        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        style.Pop();
        _individualAssignmentUi.DrawWorldCombo(width.X / 2);
        ImGui.SameLine();
        _individualAssignmentUi.DrawNewPlayerCollection(width.X);

        _individualAssignmentUi.DrawObjectKindCombo(width.X / 2);
        ImGui.SameLine();
        _individualAssignmentUi.DrawNewNpcCollection(width.X);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "Battle- and Event NPCs may apply to more than one ID if they share the same name. This is language dependent. If you change your clients language, verify that your collections are still correctly assigned.");
        ImGui.Dummy(Vector2.One);
        ImGui.Separator();
        style.Push(ImGuiStyleVar.FrameBorderSize, 1 * ImGuiHelpers.GlobalScale);

        DrawNewPlayer(width);
        ImGui.SameLine();
        ImGuiUtil.TextWrapped("Also check General Settings for UI characters and inheritance through ownership.");
        ImGui.Separator();

        DrawNewRetainer(width);
        ImGui.SameLine();
        ImGuiUtil.TextWrapped("Bell Retainers apply to Mannequins, but not to outdoor retainers, since those only carry their owners name.");
        ImGui.Separator();

        DrawNewNpc(width);
        ImGui.SameLine();
        ImGuiUtil.TextWrapped("Some NPCs are available as Battle - and Event NPCs and need to be setup for both if desired.");
        ImGui.Separator();

        DrawNewOwned(width);
        ImGui.SameLine();
        ImGuiUtil.TextWrapped("Owned NPCs take precedence before unowned NPCs of the same type.");
        ImGui.Separator();

        DrawIndividualCollections(width with { X = 200 * ImGuiHelpers.GlobalScale });
    }

    /// <summary> Draw the panel containing all special group assignments. </summary>
    public void DrawGroupPanel()
    {
        ImGui.Dummy(Vector2.One);
        using var table = ImRaii.Table("##advanced", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameBorderSize, 1 * ImGuiHelpers.GlobalScale);

        var buttonWidth = new Vector2(150 * ImGuiHelpers.GlobalScale, 2 * ImGui.GetTextLineHeightWithSpacing());
        var dummy       = new Vector2(1,                              0);

        foreach (var (type, pre, post, name, border) in AdvancedTree)
        {
            ImGui.TableNextColumn();
            if (type is CollectionType.Inactive)
                continue;

            if (pre)
                ImGui.Dummy(dummy);
            DrawAssignmentButton(type, buttonWidth, name, border);
            if (post)
                ImGui.Dummy(dummy);
        }
    }

    /// <summary> Draw the collection detail panel with inheritance, visible mod settings and statistics. </summary>
    public void DrawDetailsPanel()
    {
        var collection = _active.Current;
        DrawCollectionName(collection);
        DrawStatistics(collection);
        _inheritanceUi.Draw();
        ImGui.Separator();
        DrawInactiveSettingsList(collection);
        DrawSettingsList(collection);
    }

    private void DrawContext(bool open, ModCollection? collection, CollectionType type, ActorIdentifier identifier, string text, char suffix)
    {
        var label = $"{type}{text}{suffix}";
        if (open)
            ImGui.OpenPopup(label);

        using var context = ImRaii.Popup(label);
        if (!context)
            return;

        using (var color = ImRaii.PushColor(ImGuiCol.Text, Colors.DiscordColor))
        {
            if (ImGui.MenuItem("Use no mods."))
                _active.SetCollection(ModCollection.Empty, type, _active.Individuals.GetGroup(identifier));
        }

        if (collection != null)
        {
            using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
            if (ImGui.MenuItem("Remove this assignment."))
                _active.SetCollection(null, type, _active.Individuals.GetGroup(identifier));
        }

        foreach (var coll in _collections.OrderBy(c => c.Name))
        {
            if (coll != collection && ImGui.MenuItem($"Use {coll.Name}."))
                _active.SetCollection(coll, type, _active.Individuals.GetGroup(identifier));
        }
    }

    private bool DrawButton(string text, CollectionType type, Vector2 width, uint borderColor, ActorIdentifier id, char suffix,
        ModCollection? collection = null)
    {
        using var group      = ImRaii.Group();
        var       invalid    = type == CollectionType.Individual && !id.IsValid;
        var       redundancy = _active.RedundancyCheck(type, id);
        collection ??= _active.ByType(type, id);
        using var color = ImRaii.PushColor(ImGuiCol.Button,
                collection == null
                    ? ColorId.NoAssignment.Value()
                    : redundancy.Length > 0
                        ? ColorId.RedundantAssignment.Value()
                        : collection == _active.Current
                            ? ColorId.SelectedCollection.Value()
                            : collection == ModCollection.Empty
                                ? ColorId.NoModsAssignment.Value()
                                : ImGui.GetColorU32(ImGuiCol.Button), !invalid)
            .Push(ImGuiCol.Border, borderColor == 0 ? ImGui.GetColorU32(ImGuiCol.TextDisabled) : borderColor);
        using var disabled = ImRaii.Disabled(invalid);
        var       button   = ImGui.Button(text, width) || ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var       hovered  = redundancy.Length > 0 && ImGui.IsItemHovered();
        DrawIndividualDragSource(text, id);
        DrawIndividualDragTarget(text, id);
        if (!invalid)
        {
            _selector.DragTargetAssignment(type, id);
            var name    = Name(collection);
            var size    = ImGui.CalcTextSize(name);
            var textPos = ImGui.GetItemRectMax() - size - ImGui.GetStyle().FramePadding;
            ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), name);
            DrawContext(button, collection, type, id, text, suffix);
        }

        if (hovered)
            ImGui.SetTooltip(redundancy);

        return button;
    }

    private void DrawIndividualDragSource(string text, ActorIdentifier id)
    {
        if (!id.IsValid)
            return;

        using var source = ImRaii.DragDropSource();
        if (!source)
            return;

        ImGui.SetDragDropPayload("DragIndividual", nint.Zero, 0);
        ImGui.TextUnformatted($"Re-ordering {text}...");
        _draggedIndividualAssignment = _active.Individuals.Index(id);
    }

    private void DrawIndividualDragTarget(string text, ActorIdentifier id)
    {
        if (!id.IsValid)
            return;

        using var target = ImRaii.DragDropTarget();
        if (!target || !ImGuiUtil.IsDropping("DragIndividual"))
            return;

        var currentIdx = _active.Individuals.Index(id);
        if (_draggedIndividualAssignment != -1 && currentIdx != -1)
            _active.MoveIndividualCollection(_draggedIndividualAssignment, currentIdx);
        _draggedIndividualAssignment = -1;
    }

    private void DrawSimpleCollectionButton(CollectionType type, Vector2 width)
    {
        DrawButton(type.ToName(), type, width, 0, ActorIdentifier.Invalid, 's');
        ImGui.SameLine();
        using (var group = ImRaii.Group())
        {
            ImGuiUtil.TextWrapped(type.ToDescription());
            switch (type)
            {
                case CollectionType.Default:
                    ImGui.TextUnformatted("Overruled by any other Assignment.");
                    break;
                case CollectionType.Yourself:
                    ImGuiUtil.DrawColoredText(("Overruled by ", 0), ("Individual ", ColorId.NewMod.Value()), ("Assignments.", 0));
                    break;
                case CollectionType.MalePlayerCharacter:
                    ImGuiUtil.DrawColoredText(("Overruled by ", 0), ("Male Racial Player", Colors.DiscordColor), (", ", 0),
                        ("Your Character", ColorId.HandledConflictMod.Value()), (", or ", 0),
                        ("Individual ", ColorId.NewMod.Value()), ("Assignments.", 0));
                    break;
                case CollectionType.FemalePlayerCharacter:
                    ImGuiUtil.DrawColoredText(("Overruled by ", 0), ("Female Racial Player", Colors.ReniColorActive), (", ", 0),
                        ("Your Character", ColorId.HandledConflictMod.Value()), (", or ", 0),
                        ("Individual ", ColorId.NewMod.Value()), ("Assignments.", 0));
                    break;
                case CollectionType.MaleNonPlayerCharacter:
                    ImGuiUtil.DrawColoredText(("Overruled by ", 0), ("Male Racial NPC", Colors.DiscordColor), (", ", 0),
                        ("Children", ColorId.FolderLine.Value()), (", ", 0), ("Elderly", Colors.MetaInfoText), (", or ", 0),
                        ("Individual ", ColorId.NewMod.Value()), ("Assignments.", 0));
                    break;
                case CollectionType.FemaleNonPlayerCharacter:
                    ImGuiUtil.DrawColoredText(("Overruled by ", 0), ("Female Racial NPC", Colors.ReniColorActive), (", ", 0),
                        ("Children", ColorId.FolderLine.Value()), (", ", 0), ("Elderly", Colors.MetaInfoText), (", or ", 0),
                        ("Individual ", ColorId.NewMod.Value()), ("Assignments.", 0));
                    break;
            }
        }

        ImGui.Separator();
    }

    private void DrawAssignmentButton(CollectionType type, Vector2 width, string name, uint color)
        => DrawButton(name, type, width, color, ActorIdentifier.Invalid, 's', _active.ByType(type));

    /// <summary> Respect incognito mode for names of identifiers. </summary>
    private string Name(ActorIdentifier id, string? name)
        => _selector.IncognitoMode && id.Type is IdentifierType.Player or IdentifierType.Owned
            ? id.Incognito(name)
            : name ?? id.ToString();

    /// <summary> Respect incognito mode for names of collections. </summary>
    private string Name(ModCollection? collection)
        => collection == null                 ? "Unassigned" :
            collection == ModCollection.Empty ? "Use No Mods" :
            _selector.IncognitoMode           ? collection.AnonymizedName : collection.Name;

    private void DrawIndividualButton(string intro, Vector2 width, string tooltip, char suffix, params ActorIdentifier[] identifiers)
    {
        if (identifiers.Length > 0 && identifiers[0].IsValid)
        {
            DrawButton($"{intro} ({Name(identifiers[0], null)})", CollectionType.Individual, width, 0, identifiers[0], suffix);
        }
        else
        {
            if (tooltip.Length == 0 && identifiers.Length > 0)
                tooltip = $"The current target {identifiers[0].PlayerName} is not valid for an assignment.";
            DrawButton($"{intro} (Unavailable)", CollectionType.Individual, width, 0, ActorIdentifier.Invalid, suffix);
        }

        ImGuiUtil.HoverTooltip(tooltip);
    }

    private void DrawCurrentCharacter(Vector2 width)
        => DrawIndividualButton("Current Character", width, string.Empty, 'c', _actors.AwaitedService.GetCurrentPlayer());

    private void DrawCurrentTarget(Vector2 width)
        => DrawIndividualButton("Current Target", width, string.Empty, 't',
            _actors.AwaitedService.FromObject(_targets.Target, false, true, true));

    private void DrawNewPlayer(Vector2 width)
        => DrawIndividualButton("New Player", width, _individualAssignmentUi.PlayerTooltip, 'p',
            _individualAssignmentUi.PlayerIdentifiers.FirstOrDefault());

    private void DrawNewRetainer(Vector2 width)
        => DrawIndividualButton("New Bell Retainer", width, _individualAssignmentUi.RetainerTooltip, 'r',
            _individualAssignmentUi.RetainerIdentifiers.FirstOrDefault());

    private void DrawNewNpc(Vector2 width)
        => DrawIndividualButton("New NPC", width, _individualAssignmentUi.NpcTooltip, 'n',
            _individualAssignmentUi.NpcIdentifiers.FirstOrDefault());

    private void DrawNewOwned(Vector2 width)
        => DrawIndividualButton("New Owned NPC", width, _individualAssignmentUi.OwnedTooltip, 'o',
            _individualAssignmentUi.OwnedIdentifiers.FirstOrDefault());

    private void DrawIndividualCollections(Vector2 width)
    {
        for (var i = 0; i < _active.Individuals.Count; ++i)
        {
            var (name, ids, coll) = _active.Individuals.Assignments[i];
            DrawButton(Name(ids[0], name), CollectionType.Individual, width, 0, ids[0], 'i', coll);

            ImGui.SameLine();
            if (ImGui.GetContentRegionAvail().X < width.X + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X
             && i < _active.Individuals.Count - 1)
                ImGui.NewLine();
        }

        if (_active.Individuals.Count > 0)
            ImGui.NewLine();
    }

    private void DrawCollectionName(ModCollection collection)
    {
        ImGui.Dummy(Vector2.One);
        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.MetaInfoText);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2 * UiHelpers.Scale);
        using var font  = ImRaii.PushFont(_nameFont.ImFont, _nameFont.Available);
        var       name  = Name(collection);
        var       size  = ImGui.CalcTextSize(name).X;
        var       pos   = ImGui.GetContentRegionAvail().X - size + ImGui.GetStyle().FramePadding.X * 2;
        if (pos > 0)
            ImGui.SetCursorPosX(pos / 2);
        ImGuiUtil.DrawTextButton(name, Vector2.Zero, 0);
        ImGui.Dummy(Vector2.One);
    }

    private void DrawStatistics(ModCollection collection)
    {
        GatherInUse(collection);
        ImGui.Separator();

        var buttonHeight = 2 * ImGui.GetTextLineHeightWithSpacing();
        if (_inUseCache.Count == 0 && collection.DirectParentOf.Count == 0)
        {
            ImGui.Dummy(Vector2.One);
            using var font = ImRaii.PushFont(_nameFont.ImFont, _nameFont.Available);
            ImGuiUtil.DrawTextButton("Collection is not used.", new Vector2(ImGui.GetContentRegionAvail().X, buttonHeight),
                Colors.PressEnterWarningBg);
            ImGui.Dummy(Vector2.One);
            ImGui.Separator();
        }
        else
        {
            var buttonWidth = new Vector2(175 * ImGuiHelpers.GlobalScale, buttonHeight);
            DrawInUseStatistics(collection, buttonWidth);
            DrawInheritanceStatistics(collection, buttonWidth);
        }
    }

    private void GatherInUse(ModCollection collection)
    {
        _inUseCache.Clear();
        foreach (var special in CollectionTypeExtensions.Special.Select(t => t.Item1)
                     .Prepend(CollectionType.Default)
                     .Prepend(CollectionType.Interface)
                     .Where(t => _active.ByType(t) == collection))
            _inUseCache.Add((special, ActorIdentifier.Invalid));

        foreach (var (_, id, coll) in _active.Individuals.Assignments.Where(t
                     => t.Collection == collection && t.Identifiers.FirstOrDefault().IsValid))
            _inUseCache.Add((CollectionType.Individual, id[0]));
    }

    private void DrawInUseStatistics(ModCollection collection, Vector2 buttonWidth)
    {
        if (_inUseCache.Count <= 0)
            return;

        using (var _ = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero))
        {
            ImGuiUtil.DrawTextButton("In Use By", ImGui.GetContentRegionAvail() with { Y = 0 }, 0);
        }

        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1 * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.ButtonTextAlign, Vector2.Zero);

        foreach (var ((type, id), idx) in _inUseCache.WithIndex())
        {
            var name  = type == CollectionType.Individual ? Name(id, null) : Buttons[type].Name;
            var color = Buttons.TryGetValue(type, out var p) ? p.Border : 0;
            DrawButton(name, type, buttonWidth, color, id, 's', collection);
            ImGui.SameLine();
            if (ImGui.GetContentRegionAvail().X < buttonWidth.X + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X
             && idx != _inUseCache.Count - 1)
                ImGui.NewLine();
        }

        ImGui.NewLine();
        ImGui.Dummy(Vector2.One);
        ImGui.Separator();
    }

    private void DrawInheritanceStatistics(ModCollection collection, Vector2 buttonWidth)
    {
        if (collection.DirectParentOf.Count <= 0)
            return;

        using (var _ = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero))
        {
            ImGuiUtil.DrawTextButton("Inherited by", ImGui.GetContentRegionAvail() with { Y = 0 }, 0);
        }

        using var font  = ImRaii.PushFont(_nameFont.ImFont, _nameFont.Available);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);
        using var color = ImRaii.PushColor(ImGuiCol.Border, Colors.MetaInfoText);
        ImGuiUtil.DrawTextButton(Name(collection.DirectParentOf[0]), Vector2.Zero, 0);
        var constOffset = (ImGui.GetStyle().FramePadding.X + ImGuiHelpers.GlobalScale) * 2
          + ImGui.GetStyle().ItemSpacing.X
          + ImGui.GetStyle().WindowPadding.X;
        foreach (var parent in collection.DirectParentOf.Skip(1))
        {
            var name = Name(parent);
            var size = ImGui.CalcTextSize(name).X;
            ImGui.SameLine();
            if (constOffset + size >= ImGui.GetContentRegionAvail().X)
                ImGui.NewLine();
            ImGuiUtil.DrawTextButton(name, Vector2.Zero, 0);
        }

        ImGui.Dummy(Vector2.One);
        ImGui.Separator();
    }

    private void DrawSettingsList(ModCollection collection)
    {
        ImGui.Dummy(Vector2.One);
        var       size  = new Vector2(ImGui.GetContentRegionAvail().X, 10 * ImGui.GetFrameHeightWithSpacing());
        using var table = ImRaii.Table("##activeSettings", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, size);
        if (!table)
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Mod Name",       ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Inherited From", ImGuiTableColumnFlags.WidthFixed, 5f * ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("State",          ImGuiTableColumnFlags.WidthFixed, 1.75f * ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Priority",       ImGuiTableColumnFlags.WidthFixed, 2.5f * ImGui.GetFrameHeight());
        ImGui.TableHeadersRow();
        foreach (var (mod, (settings, parent)) in _mods.Select(m => (m, collection[m.Index]))
                     .Where(t => t.Item2.Settings != null)
                     .OrderBy(t => t.m.Name))
        {
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable(mod.Name);
            ImGui.TableNextColumn();
            if (parent != collection)
                ImGui.TextUnformatted(Name(parent));
            ImGui.TableNextColumn();
            var enabled = settings!.Enabled;
            using (var dis = ImRaii.Disabled())
            {
                ImGui.Checkbox("##check", ref enabled);
            }

            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(settings.Priority.ToString(), ImGui.GetStyle().WindowPadding.X);
        }
    }

    private void DrawInactiveSettingsList(ModCollection collection)
    {
        if (collection.UnusedSettings.Count == 0)
            return;

        ImGui.Dummy(Vector2.One);
        var text = collection.UnusedSettings.Count > 1
            ? $"Clear all {collection.UnusedSettings.Count} unused settings from deleted mods."
            : "Clear the currently unused setting from a deleted mods.";
        if (ImGui.Button(text, new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            _collections.CleanUnavailableSettings(collection);

        ImGui.Dummy(Vector2.One);

        var size = new Vector2(ImGui.GetContentRegionAvail().X,
            Math.Min(10, collection.UnusedSettings.Count + 1) * ImGui.GetFrameHeightWithSpacing());
        using var table = ImRaii.Table("##inactiveSettings", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, size);
        if (!table)
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn(string.Empty,            ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
        ImGui.TableSetupColumn("Unused Mod Identifier", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("State",                 ImGuiTableColumnFlags.WidthFixed, 1.75f * ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Priority",              ImGuiTableColumnFlags.WidthFixed, 2.5f * ImGui.GetFrameHeight());
        ImGui.TableHeadersRow();
        string? delete = null;
        foreach (var (name, settings) in collection.UnusedSettings.OrderBy(n => n.Key))
        {
            using var id = ImRaii.PushId(name);
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize,
                    "Delete this unused setting.", false, true))
                delete = name;
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable(name);
            ImGui.TableNextColumn();
            var enabled = settings.Enabled;
            using (var dis = ImRaii.Disabled())
            {
                ImGui.Checkbox("##check", ref enabled);
            }

            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(settings.Priority.ToString(), ImGui.GetStyle().WindowPadding.X);
        }

        _collections.CleanUnavailableSetting(collection, delete);
        ImGui.Separator();
    }

    /// <summary> Create names and border colors for special assignments. </summary>
    private static IReadOnlyDictionary<CollectionType, (string Name, uint Border)> CreateButtons()
    {
        var ret = Enum.GetValues<CollectionType>().ToDictionary(t => t, t => (t.ToName(), 0u));

        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            var color = race switch
            {
                SubRace.Midlander       => 0xAA5C9FE4u,
                SubRace.Highlander      => 0xAA5C9FE4u,
                SubRace.Wildwood        => 0xAA5C9F49u,
                SubRace.Duskwight       => 0xAA5C9F49u,
                SubRace.Plainsfolk      => 0xAAEF8CB6u,
                SubRace.Dunesfolk       => 0xAAEF8CB6u,
                SubRace.SeekerOfTheSun  => 0xAA8CEFECu,
                SubRace.KeeperOfTheMoon => 0xAA8CEFECu,
                SubRace.Seawolf         => 0xAAEFE68Cu,
                SubRace.Hellsguard      => 0xAAEFE68Cu,
                SubRace.Raen            => 0xAAB5EF8Cu,
                SubRace.Xaela           => 0xAAB5EF8Cu,
                SubRace.Helion          => 0xAAFFFFFFu,
                SubRace.Lost            => 0xAAFFFFFFu,
                SubRace.Rava            => 0xAA607FA7u,
                SubRace.Veena           => 0xAA607FA7u,
                _                       => 0u,
            };

            ret[CollectionTypeExtensions.FromParts(race, Gender.Male,   false)] = ($"♂ {race.ToShortName()}", color);
            ret[CollectionTypeExtensions.FromParts(race, Gender.Female, false)] = ($"♀ {race.ToShortName()}", color);
            ret[CollectionTypeExtensions.FromParts(race, Gender.Male,   true)]  = ($"♂ {race.ToShortName()} (NPC)", color);
            ret[CollectionTypeExtensions.FromParts(race, Gender.Female, true)]  = ($"♀ {race.ToShortName()} (NPC)", color);
        }

        ret[CollectionType.MalePlayerCharacter]      = ("♂ Player", 0);
        ret[CollectionType.FemalePlayerCharacter]    = ("♀ Player", 0);
        ret[CollectionType.MaleNonPlayerCharacter]   = ("♂ NPC", 0);
        ret[CollectionType.FemaleNonPlayerCharacter] = ("♀ NPC", 0);
        return ret;
    }

    /// <summary> Create the special assignment tree in order and with free spaces. </summary>
    private static IReadOnlyList<(CollectionType, bool, bool, string, uint)> CreateTree()
    {
        var ret = new List<(CollectionType, bool, bool, string, uint)>(Buttons.Count);

        void Add(CollectionType type, bool pre, bool post)
        {
            var (name, border) = Buttons[type];
            ret.Add((type, pre, post, name, border));
        }

        Add(CollectionType.Default,                  false, false);
        Add(CollectionType.Interface,                false, false);
        Add(CollectionType.Inactive,                 false, false);
        Add(CollectionType.Inactive,                 false, false);
        Add(CollectionType.Yourself,                 false, true);
        Add(CollectionType.Inactive,                 false, true);
        Add(CollectionType.NonPlayerChild,           false, true);
        Add(CollectionType.NonPlayerElderly,         false, true);
        Add(CollectionType.MalePlayerCharacter,      true,  true);
        Add(CollectionType.FemalePlayerCharacter,    true,  true);
        Add(CollectionType.MaleNonPlayerCharacter,   true,  true);
        Add(CollectionType.FemaleNonPlayerCharacter, true,  true);
        var pre = true;
        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            Add(CollectionTypeExtensions.FromParts(race, Gender.Male,   false), pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Female, false), pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Male,   true),  pre, !pre);
            Add(CollectionTypeExtensions.FromParts(race, Gender.Female, true),  pre, !pre);
            pre = !pre;
        }

        return ret;
    }
}
