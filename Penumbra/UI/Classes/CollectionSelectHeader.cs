using System.Linq;
using System.Numerics;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.UI.CollectionTab;
using Penumbra.UI.ModsTab;

namespace Penumbra.UI.Classes;

public class CollectionSelectHeader
{
    private readonly CollectionCombo       _collectionCombo;
    private readonly ActiveCollections     _activeCollections;
    private readonly TutorialService       _tutorial;
    private readonly ModFileSystemSelector _selector;

    public CollectionSelectHeader(CollectionManager collectionManager, TutorialService tutorial, ModFileSystemSelector selector)
    {
        _tutorial          = tutorial;
        _selector          = selector;
        _activeCollections = collectionManager.Active;
        _collectionCombo   = new CollectionCombo(collectionManager, () => collectionManager.Storage.OrderBy(c => c.Name).ToList());
    }

    /// <summary> Draw the header line that can quick switch between collections. </summary>
    public void Draw(bool spacing)
    {
        using var style      = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0).Push(ImGuiStyleVar.ItemSpacing, new Vector2(0, spacing ? ImGui.GetStyle().ItemSpacing.Y : 0));
        var       buttonSize = new Vector2(ImGui.GetContentRegionAvail().X / 8f, 0);

        using (var _ = ImRaii.Group())
        {
            DrawDefaultCollectionButton(3 * buttonSize);
            ImGui.SameLine();
            DrawInheritedCollectionButton(3 * buttonSize);
            ImGui.SameLine();
            _collectionCombo.Draw("##collectionSelector", 2 * buttonSize.X, ColorId.SelectedCollection.Value());
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.CollectionSelectors);

        if (!_activeCollections.CurrentCollectionInUse)
            ImGuiUtil.DrawTextButton("The currently selected collection is not used in any way.", -Vector2.UnitX, Colors.PressEnterWarningBg);
    }

    private void DrawDefaultCollectionButton(Vector2 width)
    {
        var name      = $"{TutorialService.DefaultCollection} ({_activeCollections.Default.Name})";
        var isCurrent = _activeCollections.Default == _activeCollections.Current;
        var isEmpty   = _activeCollections.Default == ModCollection.Empty;
        var tt = isCurrent ? $"The current collection is already the configured {TutorialService.DefaultCollection}."
            : isEmpty      ? $"The {TutorialService.DefaultCollection} is configured to be empty."
                             : $"Set the {TutorialService.SelectedCollection} to the configured {TutorialService.DefaultCollection}.";
        if (ImGuiUtil.DrawDisabledButton(name, width, tt, isCurrent || isEmpty))
            _activeCollections.SetCollection(_activeCollections.Default, CollectionType.Current);
    }

    private void DrawInheritedCollectionButton(Vector2 width)
    {
        var noModSelected = _selector.Selected == null;
        var collection    = _selector.SelectedSettingCollection;
        var modInherited  = collection != _activeCollections.Current;
        var (name, tt) = (noModSelected, modInherited) switch
        {
            (true, _) => ("Inherited Collection", "No mod selected."),
            (false, true) => ($"Inherited Collection ({collection.Name})",
                "Set the current collection to the collection the selected mod inherits its settings from."),
            (false, false) => ("Not Inherited", "The selected mod does not inherit its settings."),
        };
        if (ImGuiUtil.DrawDisabledButton(name, width, tt, noModSelected || !modInherited))
            _activeCollections.SetCollection(collection, CollectionType.Current);
    }
}
