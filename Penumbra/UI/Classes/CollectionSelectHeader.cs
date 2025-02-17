using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Text.Widget;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Classes;

public class CollectionSelectHeader : IUiService
{
    private readonly CollectionCombo     _collectionCombo;
    private readonly ActiveCollections   _activeCollections;
    private readonly TutorialService     _tutorial;
    private readonly ModSelection        _selection;
    private readonly CollectionResolver  _resolver;
    private readonly FontAwesomeCheckbox _temporaryCheckbox = new(FontAwesomeIcon.Stopwatch);
    private readonly Configuration       _config;

    public CollectionSelectHeader(CollectionManager collectionManager, TutorialService tutorial, ModSelection selection,
        CollectionResolver resolver, Configuration config)
    {
        _tutorial          = tutorial;
        _selection         = selection;
        _resolver          = resolver;
        _config            = config;
        _activeCollections = collectionManager.Active;
        _collectionCombo   = new CollectionCombo(collectionManager, () => collectionManager.Storage.OrderBy(c => c.Identity.Name).ToList());
    }

    /// <summary> Draw the header line that can quick switch between collections. </summary>
    public void Draw(bool spacing)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(0, spacing ? ImGui.GetStyle().ItemSpacing.Y : 0));
        DrawTemporaryCheckbox();
        ImGui.SameLine();
        var comboWidth = ImGui.GetContentRegionAvail().X / 4f;
        var buttonSize = new Vector2(comboWidth * 3f / 4f, 0f);
        using (var _ = ImRaii.Group())
        {
            DrawCollectionButton(buttonSize, GetDefaultCollectionInfo(),   1);
            DrawCollectionButton(buttonSize, GetInterfaceCollectionInfo(), 2);
            DrawCollectionButton(buttonSize, GetPlayerCollectionInfo(),    3);
            DrawCollectionButton(buttonSize, GetInheritedCollectionInfo(), 4);

            _collectionCombo.Draw("##collectionSelector", comboWidth, ColorId.SelectedCollection.Value());
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImGuiUtil.DrawTextButton("The currently selected collection is not used in any way.", -Vector2.UnitX, Colors.PressEnterWarningBg);
    }

    private void DrawTemporaryCheckbox()
    {
        var hold = _config.DeleteModModifier.IsActive();
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImUtf8.GlobalScale))
        {
            var tint = ImGuiCol.Text.Tinted(ColorId.TemporaryModSettingsTint);
            using var color = ImRaii.PushColor(ImGuiCol.FrameBgHovered, ImGui.GetColorU32(ImGuiCol.FrameBg), !hold)
                .Push(ImGuiCol.FrameBgActive, ImGui.GetColorU32(ImGuiCol.FrameBg), !hold)
                .Push(ImGuiCol.CheckMark,     tint)
                .Push(ImGuiCol.Border,        tint, _config.DefaultTemporaryMode);
            if (_temporaryCheckbox.Draw("##tempCheck"u8, _config.DefaultTemporaryMode, out var newValue) && hold)
            {
                _config.DefaultTemporaryMode = newValue;
                _config.Save();
            }
        }

        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
            "Toggle the temporary settings mode, where all changes you do create temporary settings first and need to be made permanent if desired.\n"u8);
        if (!hold)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"Hold {_config.DeleteModModifier} while clicking to toggle.");
    }

    private enum CollectionState
    {
        Empty,
        Selected,
        Unavailable,
        Available,
    }

    private CollectionState CheckCollection(ModCollection? collection, bool inheritance = false)
    {
        if (collection == null)
            return CollectionState.Unavailable;
        if (collection == ModCollection.Empty)
            return CollectionState.Empty;
        if (collection == _activeCollections.Current)
            return inheritance ? CollectionState.Unavailable : CollectionState.Selected;

        return CollectionState.Available;
    }

    private (ModCollection?, string, string, bool) GetDefaultCollectionInfo()
    {
        var collection = _activeCollections.Default;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "None", "The base collection is configured to use no mods.", true),
            CollectionState.Selected => (collection, collection.Identity.Name,
                "The configured base collection is already selected as the current collection.", true),
            CollectionState.Available => (collection, collection.Identity.Name,
                $"Select the configured base collection {collection.Identity.Name} as the current collection.", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private (ModCollection?, string, string, bool) GetPlayerCollectionInfo()
    {
        var collection = _resolver.PlayerCollection();
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "None", "The loaded player character is configured to use no mods.", true),
            CollectionState.Selected => (collection, collection.Identity.Name,
                "The collection configured to apply to the loaded player character is already selected as the current collection.", true),
            CollectionState.Available => (collection, collection.Identity.Name,
                $"Select the collection {collection.Identity.Name} that applies to the loaded player character as the current collection.",
                false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private (ModCollection?, string, string, bool) GetInterfaceCollectionInfo()
    {
        var collection = _activeCollections.Interface;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => (collection, "None", "The interface collection is configured to use no mods.", true),
            CollectionState.Selected => (collection, collection.Identity.Name,
                "The configured interface collection is already selected as the current collection.", true),
            CollectionState.Available => (collection, collection.Identity.Name,
                $"Select the configured interface collection {collection.Identity.Name} as the current collection.", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private (ModCollection?, string, string, bool) GetInheritedCollectionInfo()
    {
        var collection = _selection.Mod == null ? null : _selection.Collection;
        return CheckCollection(collection, true) switch
        {
            CollectionState.Unavailable => (null, "Not Inherited",
                "The settings of the selected mod are not inherited from another collection.", true),
            CollectionState.Available => (collection, collection!.Identity.Name,
                $"Select the collection {collection!.Identity.Name} from which the selected mod inherits its settings as the current collection.",
                false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private void DrawCollectionButton(Vector2 buttonWidth, (ModCollection?, string, string, bool) tuple, int id)
    {
        var (collection, name, tooltip, disabled) = tuple;
        using var _ = ImRaii.PushId(id);
        if (ImGuiUtil.DrawDisabledButton(name, buttonWidth, tooltip, disabled))
            _activeCollections.SetCollection(collection!, CollectionType.Current);
        ImGui.SameLine();
    }
}
