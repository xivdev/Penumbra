using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods;
using Penumbra.UI.CollectionTab;
using CollectionTuple =
    ImSharp.RefTuple<Penumbra.Collections.ModCollection?, ImSharp.Utf8StringHandler<ImSharp.LabelStringHandlerBuffer>,
        ImSharp.Utf8StringHandler<ImSharp.TextStringHandlerBuffer>, bool>;

namespace Penumbra.UI.Classes;

public class CollectionSelectHeader(
    CollectionManager collectionManager,
    TutorialService tutorial,
    ModSelection selection,
    CollectionResolver resolver,
    Configuration config,
    CollectionCombo combo)
    : IHeader
{
    private readonly        ActiveCollections _activeCollections = collectionManager.Active;
    private static readonly AwesomeIcon       Icon               = FontAwesomeIcon.Stopwatch;

    /// <summary> Draw the header line that can quick switch between collections. </summary>
    public void Draw(bool spacing)
    {
        using var style = ImStyleSingle.FrameRounding.Push(0)
            .Push(ImStyleDouble.ItemSpacing, new Vector2(0, spacing ? Im.Style.ItemSpacing.Y : 0));
        DrawTemporaryCheckbox();
        Im.Line.Same();
        var comboWidth = Im.ContentRegion.Available.X / 4f;
        var buttonSize = new Vector2(comboWidth * 3f / 4f, 0f);
        using (var _ = Im.Group())
        {
            DrawCollectionButton(buttonSize, GetDefaultCollectionInfo(),   1);
            DrawCollectionButton(buttonSize, GetInterfaceCollectionInfo(), 2);
            DrawCollectionButton(buttonSize, GetPlayerCollectionInfo(),    3);
            DrawCollectionButton(buttonSize, GetInheritedCollectionInfo(), 4);

            combo.Draw("##collectionSelector"u8, comboWidth, ColorId.SelectedCollection.Value());
        }

        tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImEx.TextFramed("The currently selected collection is not used in any way."u8, -Vector2.UnitX, Colors.PressEnterWarningBg);
    }

    private void DrawTemporaryCheckbox()
    {
        var hold = config.IncognitoModifier.IsActive();
        var tint = config.DefaultTemporaryMode
            ? Rgba32.TintColor(Im.Style[ImGuiColor.Text], ColorId.TemporaryModSettingsTint.Value().ToVector())
            : Im.Style[ImGuiColor.TextDisabled];
        var frameBg = Im.Style[ImGuiColor.FrameBackground];

        using (ImStyleBorder.Frame.Push(tint)
                   .Push(ImGuiColor.ButtonHovered, frameBg, !hold)
                   .Push(ImGuiColor.ButtonActive,  frameBg, !hold))
        {
            if (ImEx.Icon.Button(Icon, buttonColor: frameBg, textColor: tint) && hold)
            {
                config.DefaultTemporaryMode = !config.DefaultTemporaryMode;
                config.Save();
            }
        }

        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
            "Toggle the temporary settings mode, where all changes you do create temporary settings first and need to be made permanent if desired."u8);
        if (!hold)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {config.IncognitoModifier} while clicking to toggle.");
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
        if (collection is null)
            return CollectionState.Unavailable;
        if (collection == ModCollection.Empty)
            return CollectionState.Empty;
        if (collection == _activeCollections.Current)
            return inheritance ? CollectionState.Unavailable : CollectionState.Selected;

        return CollectionState.Available;
    }

    private CollectionTuple GetDefaultCollectionInfo()
    {
        var collection = _activeCollections.Default;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => new CollectionTuple(collection, "None"u8, "The base collection is configured to use no mods."u8, true),
            CollectionState.Selected => new CollectionTuple(collection, collection.Identity.Name,
                "The configured base collection is already selected as the current collection."u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection.Identity.Name,
                $"Select the configured base collection {collection.Identity.Name} as the current collection.", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private CollectionTuple GetPlayerCollectionInfo()
    {
        var collection = resolver.PlayerCollection();
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => new CollectionTuple(collection, "None"u8, "The loaded player character is configured to use no mods."u8,
                true),
            CollectionState.Selected => new CollectionTuple(collection, collection.Identity.Name,
                "The collection configured to apply to the loaded player character is already selected as the current collection."u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection.Identity.Name,
                $"Select the collection {collection.Identity.Name} that applies to the loaded player character as the current collection.",
                false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private CollectionTuple GetInterfaceCollectionInfo()
    {
        var collection = _activeCollections.Interface;
        return CheckCollection(collection) switch
        {
            CollectionState.Empty => new CollectionTuple(collection, "None"u8, "The interface collection is configured to use no mods."u8,
                true),
            CollectionState.Selected => new CollectionTuple(collection, collection.Identity.Name,
                "The configured interface collection is already selected as the current collection."u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection.Identity.Name,
                $"Select the configured interface collection {collection.Identity.Name} as the current collection.", false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private CollectionTuple GetInheritedCollectionInfo()
    {
        var collection = selection.Mod is null ? null : selection.Collection;
        return CheckCollection(collection, true) switch
        {
            CollectionState.Unavailable => new CollectionTuple(null, "Not Inherited"u8,
                "The settings of the selected mod are not inherited from another collection."u8, true),
            CollectionState.Available => new CollectionTuple(collection, collection!.Identity.Name,
                $"Select the collection {collection.Identity.Name} from which the selected mod inherits its settings as the current collection.",
                false),
            _ => throw new Exception("Can not happen."),
        };
    }

    private void DrawCollectionButton(Vector2 buttonWidth, in CollectionTuple tuple, int id)
    {
        var (collection, name, tooltip, disabled) = tuple;
        using var _ = Im.Id.Push(id);
        if (ImEx.Button(name, buttonWidth, tooltip, disabled))
            _activeCollections.SetCollection(collection!, CollectionType.Current);
        Im.Line.Same();
    }

    public bool Collapsed
        => false;

    public void Draw(Vector2 size)
    {
        using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero);
        DrawTemporaryCheckbox();
        Im.Line.Same();
        var comboWidth = (size.X - Im.Style.FrameHeight) / 4f;
        var buttonSize = new Vector2(comboWidth * 3f / 4f, 0f);
        using (var _ = Im.Group())
        {
            DrawCollectionButton(buttonSize, GetDefaultCollectionInfo(),   1);
            DrawCollectionButton(buttonSize, GetInterfaceCollectionInfo(), 2);
            DrawCollectionButton(buttonSize, GetPlayerCollectionInfo(),    3);
            DrawCollectionButton(buttonSize, GetInheritedCollectionInfo(), 4);

            combo.Draw("##collectionSelector"u8, comboWidth, ColorId.SelectedCollection.Value());
        }

        tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImEx.TextFramed("The currently selected collection is not used in any way."u8, -Vector2.UnitX, Colors.PressEnterWarningBg);
    }
}
