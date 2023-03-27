using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Services;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Tabs;

public class CollectionsTab : IDisposable, ITab
{
    private readonly CommunicatorService   _communicator;
    private readonly Configuration         _config;
    private readonly CollectionManager _collectionManager;
    private readonly TutorialService       _tutorial;
    private readonly SpecialCombo          _specialCollectionCombo;

    private readonly CollectionSelector     _collectionsWithEmpty;
    private readonly CollectionSelector     _collectionSelector;
    private readonly InheritanceUi          _inheritance;
    private readonly IndividualCollectionUi _individualCollections;

    public CollectionsTab(ActorService actorService, CommunicatorService communicator, CollectionManager collectionManager,
        TutorialService tutorial, Configuration config)
    {
        _communicator           = communicator;
        _collectionManager      = collectionManager;
        _tutorial               = tutorial;
        _config                 = config;
        _specialCollectionCombo = new SpecialCombo(_collectionManager, "##NewSpecial", 350);
        _collectionsWithEmpty = new CollectionSelector(_collectionManager,
            () => _collectionManager.OrderBy(c => c.Name).Prepend(ModCollection.Empty).ToList());
        _collectionSelector    = new CollectionSelector(_collectionManager, () => _collectionManager.OrderBy(c => c.Name).ToList());
        _inheritance           = new InheritanceUi(_collectionManager);
        _individualCollections = new IndividualCollectionUi(actorService, _collectionManager, _collectionsWithEmpty);

        _communicator.CollectionChange.Event += _individualCollections.UpdateIdentifiers;
    }

    public ReadOnlySpan<byte> Label
        => "Collections"u8;

    /// <summary> Draw a collection selector of a certain width for a certain type. </summary>
    public void DrawCollectionSelector(string label, float width, CollectionType collectionType, bool withEmpty)
        => (withEmpty ? _collectionsWithEmpty : _collectionSelector).Draw(label, width, collectionType);

    public void Dispose()
        => _communicator.CollectionChange.Event -= _individualCollections.UpdateIdentifiers;

    /// <summary> Draw a tutorial step regardless of tab selection. </summary>
    public void DrawHeader()
        => _tutorial.OpenTutorial(BasicTutorialSteps.Collections);

    public void DrawContent()
    {
        using var child = ImRaii.Child("##collections", -Vector2.One);
        if (child)
        {
            DrawActiveCollectionSelectors();
            DrawMainSelectors();
        }
    }

    #region New Collections

    // Input text fields.
    private string _newCollectionName = string.Empty;
    private bool   _canAddCollection;

    /// <summary>
    /// Create a new collection that is either empty or a duplicate of the current collection.
    /// Resets the new collection name.
    /// </summary>
    private void CreateNewCollection(bool duplicate)
    {
        if (_collectionManager.AddCollection(_newCollectionName, duplicate ? _collectionManager.Current : null))
            _newCollectionName = string.Empty;
    }

    /// <summary> Draw the Clean Unused Settings button if there are any. </summary>
    private void DrawCleanCollectionButton(Vector2 width)
    {
        if (!_collectionManager.Current.HasUnusedSettings)
            return;

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(
                $"Clean {_collectionManager.Current.NumUnusedSettings} Unused Settings###CleanSettings", width
                , "Remove all stored settings for mods not currently available and fix invalid settings.\n\nUse at own risk."
                , false))
            _collectionManager.Current.CleanUnavailableSettings();
    }

    /// <summary> Draw the new collection input as well as its buttons. </summary>
    private void DrawNewCollectionInput(Vector2 width)
    {
        // Input for new collection name. Also checks for validity when changed.
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        if (ImGui.InputTextWithHint("##New Collection", "New Collection Name...", ref _newCollectionName, 64))
            _canAddCollection = _collectionManager.CanAddCollection(_newCollectionName, out _);

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "A collection is a set of settings for your installed mods, including their enabled status, their priorities and their mod-specific configuration.\n"
          + "You can use multiple collections to quickly switch between sets of enabled mods.");

        // Creation buttons.
        var tt = _canAddCollection
            ? string.Empty
            : "Please enter a unique name only consisting of symbols valid in a path but no '|' before creating a collection.";
        if (ImGuiUtil.DrawDisabledButton("Create Empty Collection", width, tt, !_canAddCollection))
            CreateNewCollection(false);

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton($"Duplicate {TutorialService.SelectedCollection}", width, tt, !_canAddCollection))
            CreateNewCollection(true);
    }

    #endregion

    #region Collection Selection

    /// <summary> Draw all collection assignment selections. </summary>
    private void DrawActiveCollectionSelectors()
    {
        UiHelpers.DefaultLineSpace();
        var open = ImGui.CollapsingHeader(TutorialService.ActiveCollections, ImGuiTreeNodeFlags.DefaultOpen);
        _tutorial.OpenTutorial(BasicTutorialSteps.ActiveCollections);
        if (!open)
            return;

        UiHelpers.DefaultLineSpace();

        DrawDefaultCollectionSelector();
        _tutorial.OpenTutorial(BasicTutorialSteps.DefaultCollection);
        DrawInterfaceCollectionSelector();
        _tutorial.OpenTutorial(BasicTutorialSteps.InterfaceCollection);
        UiHelpers.DefaultLineSpace();

        DrawSpecialAssignments();
        _tutorial.OpenTutorial(BasicTutorialSteps.SpecialCollections1);
        UiHelpers.DefaultLineSpace();

        _individualCollections.Draw();
        _tutorial.OpenTutorial(BasicTutorialSteps.SpecialCollections2);
        UiHelpers.DefaultLineSpace();
    }

    private void DrawCurrentCollectionSelector(Vector2 width)
    {
        using var group = ImRaii.Group();
        DrawCollectionSelector("##current", UiHelpers.InputTextWidth.X, CollectionType.Current, false);
        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(TutorialService.SelectedCollection,
            "This collection will be modified when using the Installed Mods tab and making changes.\nIt is not automatically assigned to anything.");

        // Deletion conditions.
        var deleteCondition = _collectionManager.Current.Name != ModCollection.DefaultCollection;
        var modifierHeld    = Penumbra.Config.DeleteModModifier.IsActive();
        var tt = deleteCondition
            ? modifierHeld ? string.Empty : $"Hold {_config.DeleteModModifier} while clicking to delete the collection."
            : $"You can not delete the collection {ModCollection.DefaultCollection}.";

        if (ImGuiUtil.DrawDisabledButton($"Delete {TutorialService.SelectedCollection}", width, tt, !deleteCondition || !modifierHeld))
            _collectionManager.RemoveCollection(_collectionManager.Current);

        DrawCleanCollectionButton(width);
    }

    /// <summary> Draw the selector for the default collection assignment. </summary>
    private void DrawDefaultCollectionSelector()
    {
        using var group = ImRaii.Group();
        DrawCollectionSelector("##default", UiHelpers.InputTextWidth.X, CollectionType.Default, true);
        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(TutorialService.DefaultCollection,
            $"Mods in the {TutorialService.DefaultCollection} are loaded for anything that is not associated with the user interface or a character in the game,"
          + "as well as any character for whom no more specific conditions from below apply.");
    }

    /// <summary> Draw the selector for the interface collection assignment. </summary>
    private void DrawInterfaceCollectionSelector()
    {
        using var group = ImRaii.Group();
        DrawCollectionSelector("##interface", UiHelpers.InputTextWidth.X, CollectionType.Interface, true);
        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(TutorialService.InterfaceCollection,
            $"Mods in the {TutorialService.InterfaceCollection} are loaded for any file that the game categorizes as an UI file. This is mostly icons as well as the tiles that generate the user interface windows themselves.");
    }

    /// <summary> Description for character groups used in multiple help markers. </summary>
    private const string CharacterGroupDescription =
        $"{TutorialService.CharacterGroups} apply to certain types of characters based on a condition.\n"
      + $"All of them take precedence before the {TutorialService.DefaultCollection},\n"
      + $"but all {TutorialService.IndividualAssignments} take precedence before them.";

    /// <summary> Draw the entire group assignment section. </summary>
    private void DrawSpecialAssignments()
    {
        using var _ = ImRaii.Group();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(TutorialService.CharacterGroups);
        ImGuiComponents.HelpMarker(CharacterGroupDescription);
        ImGui.Separator();
        DrawSpecialCollections();
        ImGui.Dummy(Vector2.Zero);
        DrawNewSpecialCollection();
    }

    /// <summary> Draw a new combo to select special collections as well as button to create it. </summary>
    private void DrawNewSpecialCollection()
    {
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X);
        if (_specialCollectionCombo.CurrentIdx == -1
         || _collectionManager.ByType(_specialCollectionCombo.CurrentType!.Value.Item1) != null)
        {
            _specialCollectionCombo.ResetFilter();
            _specialCollectionCombo.CurrentIdx = CollectionTypeExtensions.Special
                .IndexOf(t => _collectionManager.ByType(t.Item1) == null);
        }

        if (_specialCollectionCombo.CurrentType == null)
            return;

        _specialCollectionCombo.Draw();
        ImGui.SameLine();
        var disabled = _specialCollectionCombo.CurrentType == null;
        var tt = disabled
            ? $"Please select a condition for a {TutorialService.GroupAssignment} before creating the collection.\n\n"
          + CharacterGroupDescription
            : CharacterGroupDescription;
        if (!ImGuiUtil.DrawDisabledButton($"Assign {TutorialService.ConditionalGroup}", new Vector2(120 * UiHelpers.Scale, 0), tt, disabled))
            return;

        _collectionManager.CreateSpecialCollection(_specialCollectionCombo.CurrentType!.Value.Item1);
        _specialCollectionCombo.CurrentIdx = -1;
    }

    #endregion

    #region Current Collection Editing

    /// <summary> Draw the current collection selection, the creation of new collections and the inheritance block. </summary>
    private void DrawMainSelectors()
    {
        UiHelpers.DefaultLineSpace();
        var open = ImGui.CollapsingHeader("Collection Settings", ImGuiTreeNodeFlags.DefaultOpen);
        _tutorial.OpenTutorial(BasicTutorialSteps.EditingCollections);
        if (!open)
            return;

        var width = new Vector2((UiHelpers.InputTextWidth.X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        UiHelpers.DefaultLineSpace();

        DrawCurrentCollectionSelector(width);
        _tutorial.OpenTutorial(BasicTutorialSteps.CurrentCollection);
        UiHelpers.DefaultLineSpace();

        DrawNewCollectionInput(width);
        UiHelpers.DefaultLineSpace();

        _inheritance.Draw();
        _tutorial.OpenTutorial(BasicTutorialSteps.Inheritance);
    }

    /// <summary> Draw all currently set special collections. </summary>
    private void DrawSpecialCollections()
    {
        foreach (var (type, name, desc) in CollectionTypeExtensions.Special)
        {
            var collection = _collectionManager.ByType(type);
            if (collection == null)
                continue;

            using var id = ImRaii.PushId((int)type);
            DrawCollectionSelector("##SpecialCombo", UiHelpers.InputTextWidth.X, type, true);
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize, string.Empty,
                    false, true))
            {
                _collectionManager.RemoveSpecialCollection(type);
                _specialCollectionCombo.ResetFilter();
            }

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGuiUtil.LabeledHelpMarker(name, desc);
        }
    }

    #endregion
}
