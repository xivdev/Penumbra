using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionPanel(
    IDalamudPluginInterface pi,
    CommunicatorService communicator,
    CollectionManager manager,
    CollectionSelector selector,
    ActorManager actors,
    ITargetManager targets,
    ModStorage mods,
    SaveService saveService,
    IncognitoService incognito)
    : IDisposable
{
    private readonly CollectionStorage _collections = manager.Storage;
    private readonly ActiveCollections _active = manager.Active;
    private readonly IndividualAssignmentUi _individualAssignmentUi = new(communicator, actors, manager);
    private readonly InheritanceUi _inheritanceUi = new(manager, incognito);
    private readonly IFontHandle _nameFont = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter23));

    private static readonly IReadOnlyDictionary<CollectionType, (StringU8 Name, Vector4 Border)> Buttons      = CreateButtons();
    private static readonly IReadOnlyList<(CollectionType, bool, bool, StringU8, Vector4)>       AdvancedTree = CreateTree();
    private readonly        List<(CollectionType Type, ActorIdentifier Identifier)>              _inUseCache  = [];

    private int _draggedIndividualAssignment = -1;

    public void Dispose()
    {
        _individualAssignmentUi.Dispose();
        _nameFont.Dispose();
    }

    /// <summary> Draw the panel containing beginners information and simple assignments. </summary>
    public void DrawSimple()
    {
        Im.TextWrapped("A collection is a set of mod configurations. You can have as many collections as you desire.\n"u8
          + "The collection you are currently editing in the mod tab can be selected here and is highlighted.\n"u8);
        Im.TextWrapped(
            "There are functions you can assign these collections to, so different mod configurations apply for different things.\n"u8
          + "You can assign an existing collection to such a function by clicking the function or dragging the collection over."u8);
        Im.Separator();

        var buttonWidth = new Vector2(200 * Im.Style.GlobalScale, 2 * Im.Style.FrameHeightWithSpacing);
        using var style = Im.Style.Push(ImStyleDouble.ButtonTextAlign, Vector2.Zero)
            .Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);
        DrawSimpleCollectionButton(CollectionType.Default,                  buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Interface,                buttonWidth);
        DrawSimpleCollectionButton(CollectionType.Yourself,                 buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MalePlayerCharacter,      buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemalePlayerCharacter,    buttonWidth);
        DrawSimpleCollectionButton(CollectionType.MaleNonPlayerCharacter,   buttonWidth);
        DrawSimpleCollectionButton(CollectionType.FemaleNonPlayerCharacter, buttonWidth);

        ImEx.TextMultiColored("Individual"u8, ColorId.NewMod.Value())
            .Then("Assignments take precedence before anything else and only apply to one specific character or monster."u8)
            .End();
        Im.Dummy(1);

        var specialWidth = buttonWidth with { X = 275 * Im.Style.GlobalScale };
        DrawCurrentCharacter(specialWidth);
        Im.Line.Same();
        DrawCurrentTarget(specialWidth);
        DrawIndividualCollections(buttonWidth);

        var first = true;

        Button(CollectionType.NonPlayerChild);
        Button(CollectionType.NonPlayerElderly);
        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male,   false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, false));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Male,   true));
            Button(CollectionTypeExtensions.FromParts(race, Gender.Female, true));
        }

        return;

        void Button(CollectionType type)
        {
            var (name, border) = Buttons[type];
            var collection = _active.ByType(type);
            if (collection == null)
                return;

            if (first)
            {
                Im.Separator();
                Im.Text("Currently Active Advanced Assignments"u8);
                first = false;
            }

            DrawButton(name, type, buttonWidth, border, ActorIdentifier.Invalid, 's', collection);
            Im.Line.Same();
            if (Im.ContentRegion.Available.X < buttonWidth.X + Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X)
                Im.Line.New();
        }
    }

    /// <summary> Draw the panel containing new and existing individual assignments. </summary>
    public void DrawIndividualPanel()
    {
        using var style = ImStyleDouble.ButtonTextAlign.Push(Vector2.Zero)
            .Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);
        var width = new Vector2(300 * Im.Style.GlobalScale, 2 * Im.Style.TextHeightWithSpacing);

        Im.Dummy(Vector2.One);
        DrawCurrentCharacter(width);
        Im.Line.Same();
        DrawCurrentTarget(width);
        Im.Separator();
        Im.Dummy(Vector2.One);
        style.Pop();
        _individualAssignmentUi.DrawWorldCombo(width.X / 2);
        Im.Line.Same();
        _individualAssignmentUi.DrawNewPlayerCollection(width.X);

        _individualAssignmentUi.DrawObjectKindCombo(width.X / 2);
        Im.Line.Same();
        _individualAssignmentUi.DrawNewNpcCollection(width.X);
        Im.Line.Same();
        ImGuiComponents.HelpMarker(
            "Battle- and Event NPCs may apply to more than one ID if they share the same name. This is language dependent. If you change your clients language, verify that your collections are still correctly assigned.");
        Im.Dummy(Vector2.One);
        Im.Separator();
        style.Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);

        DrawNewPlayer(width);
        Im.Line.Same();
        Im.TextWrapped("Also check General Settings for UI characters and inheritance through ownership."u8);
        Im.Separator();

        DrawNewRetainer(width);
        Im.Line.Same();
        Im.TextWrapped("Bell Retainers apply to Mannequins, but not to outdoor retainers, since those only carry their owners name."u8);
        Im.Separator();

        DrawNewNpc(width);
        Im.Line.Same();
        Im.TextWrapped("Some NPCs are available as Battle - and Event NPCs and need to be setup for both if desired."u8);
        Im.Separator();

        DrawNewOwned(width);
        Im.Line.Same();
        Im.TextWrapped("Owned NPCs take precedence before unowned NPCs of the same type."u8);
        Im.Separator();

        DrawIndividualCollections(width with { X = 200 * Im.Style.GlobalScale });
    }

    /// <summary> Draw the panel containing all special group assignments. </summary>
    public void DrawGroupPanel()
    {
        Im.Dummy(Vector2.One);
        using var table = Im.Table.Begin("##advanced"u8, 4, TableFlags.SizingFixedFit | TableFlags.RowBackground);
        if (!table)
            return;

        using var style = ImStyleDouble.ButtonTextAlign.Push(Vector2.Zero)
            .Push(ImStyleSingle.FrameBorderThickness, Im.Style.GlobalScale);

        var buttonWidth = new Vector2(150 * Im.Style.GlobalScale, 2 * Im.Style.TextHeightWithSpacing);
        var dummy       = new Vector2(1,                          0);

        foreach (var (type, pre, post, name, border) in AdvancedTree)
        {
            table.NextColumn();
            if (type is CollectionType.Inactive)
                continue;

            if (pre)
                Im.Dummy(dummy);
            DrawAssignmentButton(type, buttonWidth, name, border);
            if (post)
                Im.Dummy(dummy);
        }
    }

    /// <summary> Draw the collection detail panel with inheritance, visible mod settings and statistics. </summary>
    public void DrawDetailsPanel()
    {
        var collection = _active.Current;
        DrawCollectionName(collection);
        DrawStatistics(collection);
        DrawCollectionData(collection);
        _inheritanceUi.Draw();
        Im.Separator();
        DrawInactiveSettingsList(collection);
        DrawSettingsList(collection);
    }

    private void DrawCollectionData(ModCollection collection)
    {
        Im.Dummy(Vector2.Zero);
        using (Im.Group())
        {
            ImEx.TextFrameAligned("Name"u8);
            ImEx.TextFrameAligned("Identifier"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            var width = Im.ContentRegion.Available.X;
            using (Im.Disabled(_collections.DefaultNamed == collection))
            {
                using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));
                Im.Item.SetNextWidth(width);
                if (ImEx.InputOnDeactivation.Text("##name"u8, collection.Identity.Name, out string newName)
                 && newName != collection.Identity.Name)
                {
                    collection.Identity.Name = newName;
                    saveService.QueueSave(new ModCollectionSave(mods, collection));
                    selector.RestoreCollections();
                }
            }

            if (_collections.DefaultNamed == collection)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "The Default collection can not be renamed."u8);

            var identifier = collection.Identity.Identifier;
            var fileName   = saveService.FileNames.CollectionFile(collection);
            using (Im.Font.PushMono())
            {
                if (Im.Button(collection.Identity.Identifier, new Vector2(width, 0)))
                    try
                    {
                        Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Penumbra.Messager.NotificationMessage(ex, $"Could not open file {fileName}.", $"Could not open file {fileName}",
                            NotificationType.Warning);
                    }
            }

            if (Im.Item.RightClicked())
                Im.Clipboard.Set(identifier);

            Im.Tooltip.OnHover(
                $"Open the file\n\t{fileName}\ncontaining this design in the .json-editor of your choice.\n\nRight-Click to copy identifier to clipboard.");
        }

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);
    }

    private void DrawContext(bool open, ModCollection? collection, CollectionType type, ActorIdentifier identifier, StringU8 text, char suffix)
    {
        var label = $"{type}{text}{suffix}";
        if (open)
            Im.Popup.Open(label);

        using var context = Im.Popup.Begin(label);
        if (!context)
            return;

        using (ImGuiColor.Text.Push(LunaStyle.DiscordColor))
        {
            if (Im.Menu.Item("Use no mods."u8))
                _active.SetCollection(ModCollection.Empty, type, _active.Individuals.GetGroup(identifier));
        }

        if (collection is not null && type.CanBeRemoved())
        {
            using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
            if (Im.Menu.Item("Remove this assignment."u8))
                _active.SetCollection(null, type, _active.Individuals.GetGroup(identifier));
        }

        foreach (var coll in _collections.OrderBy(c => c.Identity.Name))
        {
            if (coll != collection && Im.Menu.Item($"Use {coll.Identity.Name}."))
                _active.SetCollection(coll, type, _active.Individuals.GetGroup(identifier));
        }
    }

    private void DrawButton(StringU8 text, CollectionType type, Vector2 width, Rgba32 borderColor, ActorIdentifier id, char suffix,
        ModCollection? collection = null)
    {
        using var group      = Im.Group();
        var       invalid    = type is CollectionType.Individual && !id.IsValid;
        var       redundancy = _active.RedundancyCheck(type, id);
        collection ??= _active.ByType(type, id);
        using var color = ImGuiColor.Button.Push(
                collection is null
                    ? ColorId.NoAssignment.Value()
                    : redundancy.Length > 0
                        ? ColorId.RedundantAssignment.Value()
                        : collection == _active.Current
                            ? ColorId.SelectedCollection.Value()
                            : collection == ModCollection.Empty
                                ? ColorId.NoModsAssignment.Value()
                                : ImGuiColor.Button.Get(), !invalid)
            .Push(ImGuiColor.Border, borderColor == 0 ? ImGuiColor.TextDisabled.Get().Color : borderColor);
        using var disabled = Im.Disabled(invalid);
        var       button   = Im.Button(text, width) || Im.Item.RightClicked();
        var       hovered  = redundancy.Length > 0 && Im.Item.Hovered();
        DrawIndividualDragSource(text, id);
        DrawIndividualDragTarget(id);
        if (!invalid)
        {
            selector.DragTargetAssignment(type, id);
            var name    = Name(collection);
            var size    = Im.Font.CalculateSize(name);
            var textPos = Im.Item.LowerRightCorner - size - Im.Style.FramePadding;
            Im.Window.DrawList.Text(textPos, ImGuiColor.Text.Get().Color, name);
            DrawContext(button, collection, type, id, text, suffix);
        }

        if (hovered)
            Im.Tooltip.Set(redundancy);
    }

    private void DrawIndividualDragSource(ReadOnlySpan<byte> text, ActorIdentifier id)
    {
        if (!id.IsValid)
            return;

        using var source = Im.DragDrop.Source();
        if (!source)
            return;

        Im.DragDrop.SetPayload("DragIndividual"u8);
        Im.Text($"Re-ordering {text}...");
        _draggedIndividualAssignment = _active.Individuals.Index(id);
    }

    private void DrawIndividualDragTarget(ActorIdentifier id)
    {
        if (!id.IsValid)
            return;

        using var target = Im.DragDrop.Target();
        if (!target || !target.IsDropping("DragIndividual"u8))
            return;

        var currentIdx = _active.Individuals.Index(id);
        if (_draggedIndividualAssignment != -1 && currentIdx != -1)
            _active.MoveIndividualCollection(_draggedIndividualAssignment, currentIdx);
        _draggedIndividualAssignment = -1;
    }

    private void DrawSimpleCollectionButton(CollectionType type, Vector2 width)
    {
        DrawButton(new StringU8(type.ToName()), type, width, 0, ActorIdentifier.Invalid, 's');
        Im.Line.Same();
        using (Im.Group())
        {
            Im.TextWrapped(type.ToDescription());
            switch (type)
            {
                case CollectionType.Default: Im.Text("Overruled by any other Assignment."u8); break;
                case CollectionType.Yourself:
                    ImEx.TextMultiColored("Overruled by "u8)
                        .Then("Individual "u8, ColorId.NewMod.Value().Color)
                        .Then("Assignments."u8)
                        .End();
                    break;
                case CollectionType.MalePlayerCharacter:
                    ImEx.TextMultiColored("Overruled by "u8)
                        .Then("Male Racial Player"u8, LunaStyle.DiscordColor)
                        .Then(", "u8)
                        .Then("Your Character"u8, ColorId.HandledConflictMod.Value().Color)
                        .Then(", or "u8)
                        .Then("Individual "u8, ColorId.NewMod.Value().Color)
                        .Then("Assignments."u8)
                        .End();
                    break;
                case CollectionType.FemalePlayerCharacter:
                    ImEx.TextMultiColored("Overruled by "u8)
                        .Then("Female Racial Player"u8, LunaStyle.ReniColorActive)
                        .Then(", "u8)
                        .Then("Your Character"u8, ColorId.HandledConflictMod.Value().Color)
                        .Then(", or "u8)
                        .Then("Individual "u8, ColorId.NewMod.Value().Color)
                        .Then("Assignments."u8)
                        .End();
                    break;
                case CollectionType.MaleNonPlayerCharacter:
                    ImEx.TextMultiColored("Overruled by "u8)
                        .Then("Male Racial NPC"u8, LunaStyle.DiscordColor)
                        .Then(", "u8)
                        .Then("Children"u8, ColorId.FolderLine.Value().Color)
                        .Then(", "u8)
                        .Then("Elderly"u8, Colors.MetaInfoText)
                        .Then(", or "u8)
                        .Then("Individual "u8, ColorId.NewMod.Value().Color)
                        .Then("Assignments."u8)
                        .End();
                    break;
                case CollectionType.FemaleNonPlayerCharacter:
                    ImEx.TextMultiColored("Overruled by "u8)
                        .Then("Female Racial NPC"u8, LunaStyle.ReniColorActive)
                        .Then(", "u8)
                        .Then("Children"u8, ColorId.FolderLine.Value().Color)
                        .Then(", "u8)
                        .Then("Elderly"u8, Colors.MetaInfoText)
                        .Then(", or "u8)
                        .Then("Individual "u8, ColorId.NewMod.Value().Color)
                        .Then("Assignments."u8)
                        .End();
                    break;
            }
        }

        Im.Separator();
    }

    private void DrawAssignmentButton(CollectionType type, Vector2 width, StringU8 name, Vector4 color)
        => DrawButton(name, type, width, color, ActorIdentifier.Invalid, 's', _active.ByType(type));

    /// <summary> Respect incognito mode for names of identifiers. </summary>
    private StringU8 Name(ActorIdentifier id, string? name)
        => incognito.IncognitoMode && id.Type is IdentifierType.Player or IdentifierType.Owned
            ? new StringU8(id.Incognito(name))
            : name is not null
                ? new StringU8(name)
                : new StringU8($"{id}");

    /// <summary> Respect incognito mode for names of collections. </summary>
    private string Name(ModCollection? collection)
        => collection is null                 ? "Unassigned" :
            collection == ModCollection.Empty ? "Use No Mods" :
            incognito.IncognitoMode           ? collection.Identity.AnonymizedName : collection.Identity.Name;

    private void DrawIndividualButton(string intro, Vector2 width, string tooltip, char suffix, params ActorIdentifier[] identifiers)
    {
        if (identifiers.Length > 0 && identifiers[0].IsValid)
        {
            DrawButton(new StringU8($"{intro} ({Name(identifiers[0], null)})"), CollectionType.Individual, width, 0, identifiers[0], suffix);
        }
        else
        {
            if (tooltip.Length == 0 && identifiers.Length > 0)
                tooltip = $"The current target {identifiers[0].PlayerName} is not valid for an assignment.";
            DrawButton(new StringU8($"{intro} (Unavailable)"), CollectionType.Individual, width, 0, ActorIdentifier.Invalid, suffix);
        }

        Im.Tooltip.OnHover(tooltip);
    }

    private void DrawCurrentCharacter(Vector2 width)
        => DrawIndividualButton("Current Character", width, string.Empty, 'c', actors.GetCurrentPlayer());

    private void DrawCurrentTarget(Vector2 width)
        => DrawIndividualButton("Current Target", width, string.Empty, 't',
            actors.FromObject(targets.Target, false, true, true));

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

            Im.Line.Same();
            if (Im.ContentRegion.Available.X < width.X + Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X
             && i < _active.Individuals.Count - 1)
                Im.Line.New();
        }

        if (_active.Individuals.Count > 0)
            Im.Line.New();
    }

    private void DrawCollectionName(ModCollection collection)
    {
        Im.Dummy(Vector2.One);
        using var style = ImStyleBorder.Frame.Push(Colors.MetaInfoText, 2 * Im.Style.GlobalScale);
        using var f     = _nameFont.Push();
        var       name  = Name(collection);
        var       size  = Im.Font.CalculateSize(name).X;
        var       pos   = Im.ContentRegion.Available.X - size + Im.Style.FramePadding.X * 2;
        if (pos > 0)
            Im.Cursor.X = pos / 2;
        ImEx.TextFramed(name, Vector2.Zero, 0);
        Im.Dummy(Vector2.One);
    }

    private void DrawStatistics(ModCollection collection)
    {
        GatherInUse(collection);
        Im.Separator();

        var buttonHeight = 2 * Im.Style.TextHeightWithSpacing;
        if (_inUseCache.Count == 0 && collection.Inheritance.DirectlyInheritedBy.Count == 0)
        {
            Im.Dummy(Vector2.One);
            using var f = _nameFont.Push();
            ImEx.TextFramed("Collection is not used."u8, Im.ContentRegion.Available with { Y = buttonHeight },
                Colors.PressEnterWarningBg);
            Im.Dummy(Vector2.One);
            Im.Separator();
        }
        else
        {
            var buttonWidth = new Vector2(175 * Im.Style.GlobalScale, buttonHeight);
            DrawInUseStatistics(collection, buttonWidth);
            DrawInheritanceStatistics(collection);
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

        foreach (var (_, id, _) in _active.Individuals.Assignments.Where(t
                     => t.Collection == collection && t.Identifiers.Count > 0 && t.Identifiers[0].IsValid))
            _inUseCache.Add((CollectionType.Individual, id[0]));
    }

    private void DrawInUseStatistics(ModCollection collection, Vector2 buttonWidth)
    {
        if (_inUseCache.Count <= 0)
            return;

        using (ImStyleDouble.FramePadding.Push(Vector2.Zero))
        {
            ImEx.TextFramed("In Use By"u8, Im.ContentRegion.Available with { Y = 0 }, 0);
        }

        using var style = ImStyleSingle.FrameBorderThickness.Push(Im.Style.GlobalScale)
            .Push(ImStyleDouble.ButtonTextAlign, Vector2.Zero);

        foreach (var (idx, (type, id)) in _inUseCache.Index())
        {
            var name  = type is CollectionType.Individual ? Name(id, null) : Buttons[type].Name;
            var color = Buttons.TryGetValue(type, out var p) ? p.Border : Vector4.Zero;
            DrawButton(name, type, buttonWidth, color, id, 's', collection);
            Im.Line.Same();
            if (Im.ContentRegion.Available.X < buttonWidth.X + Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X
             && idx != _inUseCache.Count - 1)
                Im.Line.New();
        }

        Im.Line.New();
        Im.Dummy(Vector2.One);
        Im.Separator();
    }

    private void DrawInheritanceStatistics(ModCollection collection)
    {
        if (collection.Inheritance.DirectlyInheritedBy.Count <= 0)
            return;

        using (ImStyleDouble.FramePadding.Push(Vector2.Zero))
        {
            ImEx.TextFramed("Inherited by"u8, Im.ContentRegion.Available with { Y = 0 }, 0);
        }

        using var f     = _nameFont.Push();
        using var style = ImStyleBorder.Frame.Push(Colors.MetaInfoText);
        ImEx.TextFramed(Name(collection.Inheritance.DirectlyInheritedBy[0]), Vector2.Zero, 0);
        var constOffset = (Im.Style.FramePadding.X + Im.Style.GlobalScale) * 2
          + Im.Style.ItemSpacing.X
          + Im.Style.WindowPadding.X;
        foreach (var parent in collection.Inheritance.DirectlyInheritedBy.Skip(1))
        {
            var name = Name(parent);
            var size = Im.Font.CalculateSize(name).X;
            Im.Line.Same();
            if (constOffset + size >= Im.ContentRegion.Available.X)
                Im.Line.New();
            ImEx.TextFramed(name, Vector2.Zero, 0);
        }

        Im.Dummy(Vector2.One);
        Im.Separator();
    }

    private void DrawSettingsList(ModCollection collection)
    {
        Im.Dummy(Vector2.One);
        var       size  = Im.ContentRegion.Available with { Y = 10 * Im.Style.FrameHeightWithSpacing };
        using var table = Im.Table.Begin("##activeSettings"u8, 4, TableFlags.ScrollY | TableFlags.RowBackground, size);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 1);
        table.SetupColumn("Mod Name"u8,       TableColumnFlags.WidthStretch);
        table.SetupColumn("Inherited From"u8, TableColumnFlags.WidthFixed, 5f * Im.Style.FrameHeight);
        table.SetupColumn("State"u8,          TableColumnFlags.WidthFixed, 1.75f * Im.Style.FrameHeight);
        table.SetupColumn("Priority"u8,       TableColumnFlags.WidthFixed, 2.5f * Im.Style.FrameHeight);
        table.HeaderRow();

        foreach (var (mod, (settings, parent)) in mods.Select(m => (m, collection.GetInheritedSettings(m.Index)))
                     .Where(t => t.Item2.Settings != null)
                     .OrderBy(t => t.m.Name))
        {
            table.NextColumn();
            ImEx.CopyOnClickSelectable(mod.Name);
            table.NextColumn();
            if (parent != collection)
                Im.Text(Name(parent));
            table.NextColumn();
            var enabled = settings!.Enabled;
            using (Im.Disabled())
            {
                Im.Checkbox("##check"u8, ref enabled);
            }

            table.NextColumn();
            ImEx.TextRightAligned($"{settings.Priority}", Im.Style.WindowPadding.X);
        }
    }

    private void DrawInactiveSettingsList(ModCollection collection)
    {
        if (collection.Settings.Unused.Count is 0)
            return;

        Im.Dummy(Vector2.One);
        if (Im.Button(collection.Settings.Unused.Count > 1
                ? $"Clear all {collection.Settings.Unused.Count} unused settings from deleted mods."
                : "Clear the currently unused setting from a deleted mods."u8, Im.ContentRegion.Available with { Y = 0 }))
            _collections.CleanUnavailableSettings(collection);

        Im.Dummy(Vector2.One);

        var size = Im.ContentRegion.Available with { Y = Math.Min(10, collection.Settings.Unused.Count + 1) * Im.Style.FrameHeightWithSpacing };
        using var table = Im.Table.Begin("##inactiveSettings"u8, 4, TableFlags.ScrollY | TableFlags.RowBackground, size);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 1);
        table.SetupColumn(StringU8.Empty,            TableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
        table.SetupColumn("Unused Mod Identifier"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("State"u8,                 TableColumnFlags.WidthFixed, 1.75f * Im.Style.FrameHeight);
        table.SetupColumn("Priority"u8,              TableColumnFlags.WidthFixed, 2.5f * Im.Style.FrameHeight);
        table.HeaderRow();
        string? delete = null;
        foreach (var (name, settings) in collection.Settings.Unused.OrderBy(n => n.Key))
        {
            using var id = Im.Id.Push(name);
            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "Delete this unused setting."u8))
                delete = name;
            table.NextColumn();
            ImEx.CopyOnClickSelectable(name);
            table.NextColumn();
            var enabled = settings.Enabled;
            using (Im.Disabled())
            {
                Im.Checkbox("##check"u8, ref enabled);
            }

            table.NextColumn();
            ImEx.TextRightAligned($"{settings.Priority}", Im.Style.WindowPadding.X);
        }

        _collections.CleanUnavailableSetting(collection, delete);
        Im.Separator();
    }

    /// <summary> Create names and border colors for special assignments. </summary>
    private static IReadOnlyDictionary<CollectionType, (StringU8 Name, Vector4 Border)> CreateButtons()
    {
        var ret = Enum.GetValues<CollectionType>().ToDictionary(t => t, t => (new StringU8(t.ToName()), Vector4.Zero));

        foreach (var race in Enum.GetValues<SubRace>().Skip(1))
        {
            Rgba32 color = race switch
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

            ret[CollectionTypeExtensions.FromParts(race, Gender.Male,   false)] = (new StringU8($"♂ {race.ToShortName()}"), color.ToVector());
            ret[CollectionTypeExtensions.FromParts(race, Gender.Female, false)] = (new StringU8($"♀ {race.ToShortName()}"), color.ToVector());
            ret[CollectionTypeExtensions.FromParts(race, Gender.Male, true)] =
                (new StringU8($"♂ {race.ToShortName()} (NPC)"), color.ToVector());
            ret[CollectionTypeExtensions.FromParts(race, Gender.Female, true)] =
                (new StringU8($"♀ {race.ToShortName()} (NPC)"), color.ToVector());
        }

        ret[CollectionType.MalePlayerCharacter]      = (new StringU8("♂ Player"), Vector4.Zero);
        ret[CollectionType.FemalePlayerCharacter]    = (new StringU8("♀ Player"), Vector4.Zero);
        ret[CollectionType.MaleNonPlayerCharacter]   = (new StringU8("♂ NPC"), Vector4.Zero);
        ret[CollectionType.FemaleNonPlayerCharacter] = (new StringU8("♀ NPC"), Vector4.Zero);
        return ret;
    }

    /// <summary> Create the special assignment tree in order and with free spaces. </summary>
    private static List<(CollectionType, bool, bool, StringU8, Vector4)> CreateTree()
    {
        var ret = new List<(CollectionType, bool, bool, StringU8, Vector4)>(Buttons.Count);

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

        void Add(CollectionType type, bool localPre, bool post)
        {
            var (name, border) = Buttons[type];
            ret.Add((type, localPre, post, name, border));
        }
    }
}
