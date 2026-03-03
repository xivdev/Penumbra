using ImSharp;
using Luna;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs;

public enum CollectionPanelMode
{
    SimpleAssignment,
    IndividualAssignment,
    GroupAssignment,
    Details,
};

public sealed class CollectionModeHeader(Configuration config, TutorialService tutorial, IncognitoService incognito) : IHeader
{
    public bool Collapsed
        => false;

    private CollectionPanelMode Mode
    {
        get => config.Ephemeral.CollectionPanel;
        set
        {
            config.Ephemeral.CollectionPanel = value;
            config.Ephemeral.Save();
        }
    }

    public void Draw(Vector2 size)
    {
        var withSpacing = Im.Style.FrameHeightWithSpacing;
        var buttonSize  = new Vector2((Im.ContentRegion.Available.X - withSpacing) / 4f, Im.Style.FrameHeight);

        var       tabSelectedColor = Im.Style[ImGuiColor.TabSelected];
        using var color            = ImGuiColor.Button.Push(tabSelectedColor, Mode is CollectionPanelMode.SimpleAssignment);
        if (Im.Button("Simple Assignments"u8, buttonSize))
            Mode = CollectionPanelMode.SimpleAssignment;
        color.Pop();
        tutorial.OpenTutorial(BasicTutorialSteps.SimpleAssignments);
        Im.Line.NoSpacing();

        color.Push(ImGuiColor.Button, tabSelectedColor, Mode is CollectionPanelMode.IndividualAssignment);
        if (Im.Button("Individual Assignments"u8, buttonSize))
            Mode = CollectionPanelMode.IndividualAssignment;
        color.Pop();
        tutorial.OpenTutorial(BasicTutorialSteps.IndividualAssignments);
        Im.Line.NoSpacing();

        color.Push(ImGuiColor.Button, tabSelectedColor, Mode is CollectionPanelMode.GroupAssignment);
        if (Im.Button("Group Assignments"u8, buttonSize))
            Mode = CollectionPanelMode.GroupAssignment;
        color.Pop();
        tutorial.OpenTutorial(BasicTutorialSteps.GroupAssignments);
        Im.Line.NoSpacing();

        color.Push(ImGuiColor.Button, tabSelectedColor, Mode is CollectionPanelMode.Details);
        if (Im.Button("Collection Details"u8, buttonSize))
            Mode = CollectionPanelMode.Details;
        color.Pop();
        tutorial.OpenTutorial(BasicTutorialSteps.CollectionDetails);
        Im.Line.NoSpacing();

        incognito.DrawToggle(withSpacing);
    }
}
