using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using ImSharp;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Tabs;

public sealed class CollectionsTab : IDisposable, ITab, Luna.IUiService
{
    private readonly EphemeralConfig    _config;
    private readonly CollectionSelector _selector;
    private readonly CollectionPanel    _panel;
    private readonly TutorialService    _tutorial;
    private readonly IncognitoService   _incognito;

    public enum PanelMode
    {
        SimpleAssignment,
        IndividualAssignment,
        GroupAssignment,
        Details,
    };

    public PanelMode Mode
    {
        get => _config.CollectionPanel;
        set
        {
            _config.CollectionPanel = value;
            _config.Save();
        }
    }

    public CollectionsTab(IDalamudPluginInterface pi, Configuration configuration, CommunicatorService communicator, IncognitoService incognito,
        CollectionManager collectionManager, ModStorage modStorage, ActorManager actors, ITargetManager targets, TutorialService tutorial, SaveService saveService)
    {
        _config    = configuration.Ephemeral;
        _tutorial  = tutorial;
        _incognito = incognito;
        _selector  = new CollectionSelector(configuration, communicator, collectionManager.Storage, collectionManager.Active, _tutorial, incognito);
        _panel     = new CollectionPanel(pi, communicator, collectionManager, _selector, actors, targets, modStorage, saveService, incognito);
    }

    public void Dispose()
    {
        _selector.Dispose();
        _panel.Dispose();
    }

    public ReadOnlySpan<byte> Label
        => "Collections"u8;

    public void DrawContent()
    {
        var width = ImGui.CalcTextSize("nnnnnnnnnnnnnnnnnnnnnnnnnn").X;
        using (var group = ImRaii.Group())
        {
            _selector.Draw(width);
        }

        _tutorial.OpenTutorial(BasicTutorialSteps.EditingCollections);

        Im.Line.Same();
        using (var group = ImRaii.Group())
        {
            DrawHeaderLine();
            DrawPanel();
        }
    }

    public void DrawHeader()
    {
        _tutorial.OpenTutorial(BasicTutorialSteps.Collections);
    }

    private void DrawHeaderLine()
    {
        var       withSpacing = Im.Style.FrameHeightWithSpacing;
        using var style       = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0).Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        var       buttonSize  = new Vector2((Im.ContentRegion.Available.X - withSpacing) / 4f, Im.Style.FrameHeight);

        using var _                = ImRaii.Group();
        var       tabSelectedColor = Im.Style[ImGuiColor.TabSelected];
        using var color            = ImGuiColor.Button.Push(tabSelectedColor, Mode is PanelMode.SimpleAssignment);
        if (ImGui.Button("Simple Assignments", buttonSize))
            Mode = PanelMode.SimpleAssignment;
        color.Pop();
        _tutorial.OpenTutorial(BasicTutorialSteps.SimpleAssignments);
        Im.Line.Same();

        color.Push(ImGuiColor.Button, tabSelectedColor, Mode is PanelMode.IndividualAssignment);
        if (ImGui.Button("Individual Assignments", buttonSize))
            Mode = PanelMode.IndividualAssignment;
        color.Pop();
        _tutorial.OpenTutorial(BasicTutorialSteps.IndividualAssignments);
        Im.Line.Same();

        color.Push(ImGuiColor.Button, tabSelectedColor, Mode is PanelMode.GroupAssignment);
        if (ImGui.Button("Group Assignments", buttonSize))
            Mode = PanelMode.GroupAssignment;
        color.Pop();
        _tutorial.OpenTutorial(BasicTutorialSteps.GroupAssignments);
        Im.Line.Same();

        color.Push(ImGuiColor.Button, tabSelectedColor, Mode is PanelMode.Details);
        if (ImGui.Button("Collection Details", buttonSize))
            Mode = PanelMode.Details;
        color.Pop();
        _tutorial.OpenTutorial(BasicTutorialSteps.CollectionDetails);
        Im.Line.Same();

        _incognito.DrawToggle(withSpacing);
    }

    private void DrawPanel()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        using var child = ImRaii.Child("##CollectionSettings", new Vector2(Im.ContentRegion.Available.X, 0), true);
        if (!child)
            return;

        style.Pop();
        switch (Mode)
        {
            case PanelMode.SimpleAssignment:
                _panel.DrawSimple();
                break;
            case PanelMode.IndividualAssignment:
                _panel.DrawIndividualPanel();
                break;
            case PanelMode.GroupAssignment:
                _panel.DrawGroupPanel();
                break;
            case PanelMode.Details:
                _panel.DrawDetailsPanel();
                break;
        }

        style.Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
    }
}
