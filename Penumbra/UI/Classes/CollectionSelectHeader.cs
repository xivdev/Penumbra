using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Classes;

public class CollectionSelectHeader(
    CollectionManager collectionManager,
    TutorialService tutorial,
    ModSelection selection,
    CollectionResolver resolver,
    Configuration config,
    CollectionCombo combo)
    : Luna.IUiService
{
    private readonly ActiveCollections _activeCollections = collectionManager.Active;

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

            combo.Draw("##collectionSelector"u8, comboWidth, ColorId.SelectedCollection.Value());
        }

        tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImGuiUtil.DrawTextButton("The currently selected collection is not used in any way.", -Vector2.UnitX, Colors.PressEnterWarningBg);
    }

    private void DrawTemporaryCheckbox()
    {
        var hold = config.IncognitoModifier.IsActive();
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImUtf8.GlobalScale))
        {
            var tint = config.DefaultTemporaryMode
                ? ImGuiCol.Text.Tinted(ColorId.TemporaryModSettingsTint)
                : ImGui.GetColorU32(ImGuiCol.TextDisabled);
            using var color = ImRaii.PushColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.FrameBg), !hold)
                .Push(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.FrameBg), !hold)
                .Push(ImGuiCol.Border,       tint,                                config.DefaultTemporaryMode);
            if (ImUtf8.IconButton(FontAwesomeIcon.Stopwatch, ""u8, default, false, tint, ImGui.GetColorU32(ImGuiCol.FrameBg)) && hold)
            {
                config.DefaultTemporaryMode = !config.DefaultTemporaryMode;
                config.Save();
            }
        }

        ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
            "Toggle the temporary settings mode, where all changes you do create temporary settings first and need to be made permanent if desired."u8);
        if (!hold)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"\nHold {config.IncognitoModifier} while clicking to toggle.");
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
        var collection = resolver.PlayerCollection();
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
        var collection = selection.Mod == null ? null : selection.Collection;
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
