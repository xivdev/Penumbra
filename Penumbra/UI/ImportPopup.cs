using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Raii;
using Penumbra.Import.Structs;
using Penumbra.Mods.Manager;

namespace Penumbra.UI;

/// <summary> Draw the progress information for import. </summary>
public sealed class ImportPopup : Window
{
    public const string WindowLabel = "Penumbra Import Status";

    private readonly ModImportManager _modImportManager;

    public bool WasDrawn      { get; private set; }
    public bool PopupWasDrawn { get; private set; }

    public ImportPopup(ModImportManager modImportManager)
        : base(WindowLabel,
            ImGuiWindowFlags.NoCollapse
          | ImGuiWindowFlags.NoDecoration
          | ImGuiWindowFlags.NoBackground
          | ImGuiWindowFlags.NoMove
          | ImGuiWindowFlags.NoInputs
          | ImGuiWindowFlags.NoNavFocus
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoBringToFrontOnFocus
          | ImGuiWindowFlags.NoDocking
          | ImGuiWindowFlags.NoTitleBar, true)
    {
        _modImportManager  = modImportManager;
        IsOpen             = true;
        RespectCloseHotkey = false;
        Collapsed          = false;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = Vector2.Zero,
        };
    }

    public override void PreOpenCheck()
    {
        WasDrawn      = false;
        PopupWasDrawn = false;
        _modImportManager.TryUnpacking();
        IsOpen = true;
    }

    public override void Draw()
    {
        WasDrawn = true;
        if (!_modImportManager.IsImporting(out var import))
            return;

        const string importPopup = "##PenumbraImportPopup";
        if (!ImGui.IsPopupOpen(importPopup))
            ImGui.OpenPopup(importPopup);

        var display = ImGui.GetIO().DisplaySize;
        var height  = Math.Max(display.Y / 4, 15 * ImGui.GetFrameHeightWithSpacing());
        var width   = display.X / 8;
        var size    = new Vector2(width * 2, height);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, Vector2.One / 2);
        ImGui.SetNextWindowSize(size);
        using var popup = ImRaii.Popup(importPopup, ImGuiWindowFlags.Modal);
        PopupWasDrawn = true;
        using (var child = ImRaii.Child("##import", new Vector2(-1, size.Y - ImGui.GetFrameHeight() * 2)))
        {
            if (child)
                import.DrawProgressInfo(new Vector2(-1, ImGui.GetFrameHeight()));
        }

        var terminate = import.State == ImporterState.Done
            ? ImGui.Button("Close", -Vector2.UnitX)
            : import.DrawCancelButton(-Vector2.UnitX);
        if (terminate)
            _modImportManager.ClearImport();
    }
}
