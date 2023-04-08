using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Raii;
using Penumbra.Import.Structs;
using Penumbra.Mods.Manager;

namespace Penumbra.UI;

/// <summary> Draw the progress information for import. </summary>
public sealed class ImportPopup : Window
{
    private readonly ModImportManager _modImportManager;

    public ImportPopup(ModImportManager modImportManager)
        : base("Penumbra Import Status",
            ImGuiWindowFlags.Modal
          | ImGuiWindowFlags.Popup
          | ImGuiWindowFlags.NoCollapse
          | ImGuiWindowFlags.NoDecoration
          | ImGuiWindowFlags.NoBackground
          | ImGuiWindowFlags.NoMove
          | ImGuiWindowFlags.NoInputs
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoBringToFrontOnFocus, true)
    {
        _modImportManager = modImportManager;
        IsOpen = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = Vector2.Zero,
        };
    }

    public override void Draw()
    {
        _modImportManager.TryUnpacking();
        if (!_modImportManager.IsImporting(out var import))
            return;

        ImGui.OpenPopup("##importPopup");

        var display = ImGui.GetIO().DisplaySize;
        var height = Math.Max(display.Y / 4, 15 * ImGui.GetFrameHeightWithSpacing());
        var width = display.X / 8;
        var size = new Vector2(width * 2, height);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, Vector2.One / 2);
        ImGui.SetNextWindowSize(size);
        using var popup = ImRaii.Popup("##importPopup", ImGuiWindowFlags.Modal);
        using (var child = ImRaii.Child("##import", new Vector2(-1, size.Y - ImGui.GetFrameHeight() * 2)))
        {
            if (child)
                import.DrawProgressInfo(new Vector2(-1, ImGui.GetFrameHeight()));
        }

        if ((import.State != ImporterState.Done || !ImGui.Button("Close", -Vector2.UnitX))
         && (import.State == ImporterState.Done || !import.DrawCancelButton(-Vector2.UnitX)))
            return;

        _modImportManager.ClearImport();
    }
}
