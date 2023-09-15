using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.UI.Classes;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.ModsTab;
using ModFileSystemSelector = Penumbra.UI.ModsTab.ModFileSystemSelector;
using Penumbra.Collections.Manager;

namespace Penumbra.UI.Tabs;

public class ModsTab : ITab
{
    private readonly ModFileSystemSelector  _selector;
    private readonly ModPanel               _panel;
    private readonly TutorialService        _tutorial;
    private readonly ModManager             _modManager;
    private readonly ActiveCollections      _activeCollections;
    private readonly RedrawService          _redrawService;
    private readonly Configuration          _config;
    private readonly IClientState           _clientState;
    private readonly CollectionSelectHeader _collectionHeader;

    public ModsTab(ModManager modManager, CollectionManager collectionManager, ModFileSystemSelector selector, ModPanel panel,
        TutorialService tutorial, RedrawService redrawService, Configuration config, IClientState clientState,
        CollectionSelectHeader collectionHeader)
    {
        _modManager        = modManager;
        _activeCollections = collectionManager.Active;
        _selector          = selector;
        _panel             = panel;
        _tutorial          = tutorial;
        _redrawService     = redrawService;
        _config            = config;
        _clientState       = clientState;
        _collectionHeader  = collectionHeader;
    }

    public bool IsVisible
        => _modManager.Valid;

    public ReadOnlySpan<byte> Label
        => "Mods"u8;

    public void DrawHeader()
        => _tutorial.OpenTutorial(BasicTutorialSteps.Mods);

    public Mod SelectMod
    {
        set => _selector.SelectByValue(value);
    }

    public void DrawContent()
    {
        try
        {
            _selector.Draw(GetModSelectorSize(_config));
            ImGui.SameLine();
            using var group = ImRaii.Group();
            _collectionHeader.Draw(false);

            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            using (var child = ImRaii.Child("##ModsTabMod", new Vector2(-1, _config.HideRedrawBar ? 0 : -ImGui.GetFrameHeight()),
                       true, ImGuiWindowFlags.HorizontalScrollbar))
            {
                style.Pop();
                if (child)
                    _panel.Draw();

                style.Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            }

            style.Push(ImGuiStyleVar.FrameRounding, 0);
            DrawRedrawLine();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Exception thrown during ModPanel Render:\n{e}");
            Penumbra.Log.Error($"{_modManager.Count} Mods\n"
              + $"{_activeCollections.Current.AnonymizedName} Current Collection\n"
              + $"{_activeCollections.Current.Settings.Count} Settings\n"
              + $"{_selector.SortMode.Name} Sort Mode\n"
              + $"{_selector.SelectedLeaf?.Name ?? "NULL"} Selected Leaf\n"
              + $"{_selector.Selected?.Name ?? "NULL"} Selected Mod\n"
              + $"{string.Join(", ", _activeCollections.Current.DirectlyInheritsFrom.Select(c => c.AnonymizedName))} Inheritances\n"
              + $"{_selector.SelectedSettingCollection.AnonymizedName} Collection\n");
        }
    }

    /// <summary> Get the correct size for the mod selector based on current config. </summary>
    public static float GetModSelectorSize(Configuration config)
    {
        var absoluteSize = Math.Clamp(config.ModSelectorAbsoluteSize, Configuration.Constants.MinAbsoluteSize,
            Math.Min(Configuration.Constants.MaxAbsoluteSize, ImGui.GetContentRegionAvail().X - 100));
        var relativeSize = config.ScaleModSelector
            ? Math.Clamp(config.ModSelectorScaledSize, Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize)
            : 0;
        return !config.ScaleModSelector
            ? absoluteSize
            : Math.Max(absoluteSize, relativeSize * ImGui.GetContentRegionAvail().X / 100);
    }

    private void DrawRedrawLine()
    {
        if (_config.HideRedrawBar)
        {
            _tutorial.SkipTutorial(BasicTutorialSteps.Redrawing);
            return;
        }

        var frameHeight = new Vector2(0, ImGui.GetFrameHeight());
        var frameColor  = ImGui.GetColorU32(ImGuiCol.FrameBg);
        using (var _ = ImRaii.Group())
        {
            using (var font = ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGuiUtil.DrawTextButton(FontAwesomeIcon.InfoCircle.ToIconString(), frameHeight, frameColor);
                ImGui.SameLine();
            }

            ImGuiUtil.DrawTextButton("Redraw:        ", frameHeight, frameColor);
        }

        var hovered = ImGui.IsItemHovered();
        _tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (hovered)
            ImGui.SetTooltip($"The supported modifiers for '/penumbra redraw' are:\n{TutorialService.SupportedRedrawModifiers}");

        void DrawButton(Vector2 size, string label, string lower)
        {
            if (ImGui.Button(label, size))
            {
                if (lower.Length > 0)
                    _redrawService.RedrawObject(lower, RedrawType.Redraw);
                else
                    _redrawService.RedrawAll(RedrawType.Redraw);
            }

            ImGuiUtil.HoverTooltip(lower.Length > 0 ? $"Execute '/penumbra redraw {lower}'." : $"Execute '/penumbra redraw'.");
        }

        using var disabled = ImRaii.Disabled(_clientState.LocalPlayer == null);
        ImGui.SameLine();
        var buttonWidth = frameHeight with { X = ImGui.GetContentRegionAvail().X / 4 };
        DrawButton(buttonWidth, "All", string.Empty);
        ImGui.SameLine();
        DrawButton(buttonWidth, "Self", "self");
        ImGui.SameLine();
        DrawButton(buttonWidth, "Target", "target");
        ImGui.SameLine();
        DrawButton(frameHeight with { X = ImGui.GetContentRegionAvail().X - 1 }, "Focus", "focus");
    }
}
