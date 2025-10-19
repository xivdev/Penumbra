using ImSharp;
using Luna;
using Penumbra.Import.Structs;
using Penumbra.Mods.Manager;

namespace Penumbra.UI;

/// <summary> Draw the progress information for import. </summary>
public sealed class ImportPopup : Window, IUiService
{
    public const string WindowLabel = "Penumbra Import Status";

    private readonly        ModImportManager _modImportManager;
    private static readonly Vector2          OneHalf = Vector2.One / 2;

    public bool WasDrawn      { get; private set; }
    public bool PopupWasDrawn { get; private set; }

    public ImportPopup(ModImportManager modImportManager)
        : base(WindowLabel,
            WindowFlags.NoCollapse
          | WindowFlags.NoDecoration
          | WindowFlags.NoBackground
          | WindowFlags.NoMove
          | WindowFlags.NoInputs
          | WindowFlags.NoNavFocus
          | WindowFlags.NoFocusOnAppearing
          | WindowFlags.NoBringToFrontOnFocus
          | WindowFlags.NoDocking
          | WindowFlags.NoTitleBar, true)
    {
        _modImportManager   = modImportManager;
        DisableWindowSounds = true;
        IsOpen              = true;
        RespectCloseHotkey  = false;
        Collapsed           = false;
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

        if (!Im.Popup.IsOpen("##PenumbraImportPopup"u8))
            Im.Popup.Open("##PenumbraImportPopup"u8);

        var display = Im.Io.DisplaySize;
        var height  = Math.Max(display.Y / 4, 15 * Im.Style.FrameHeightWithSpacing);
        var width   = display.X / 8;
        var size    = new Vector2(width * 2, height);
        Im.Window.SetNextPosition(Im.Viewport.Main.Center, Condition.Always, OneHalf);
        Im.Window.SetNextSize(size);
        using var popup = Im.Popup.Begin("##PenumbraImportPopup"u8, WindowFlags.Modal);
        PopupWasDrawn = true;
        var terminate = false;
        using (var child = Im.Child.Begin("##import"u8, new Vector2(-1, size.Y - Im.Style.FrameHeight * 2)))
        {
            if (child.Success && import.DrawProgressInfo(new Vector2(-1, Im.Style.FrameHeight)))
                if (!Im.Mouse.IsHoveringRectangle(Rectangle.FromSize(Im.Window.Position, Im.Window.Size))
                 && Im.Mouse.IsClicked(MouseButton.Left))
                    terminate = true;
        }

        terminate |= import.State == ImporterState.Done
            ? Im.Button("Close"u8, -Vector2.UnitX)
            : import.DrawCancelButton(-Vector2.UnitX);
        if (terminate)
            _modImportManager.ClearImport();
    }
}
