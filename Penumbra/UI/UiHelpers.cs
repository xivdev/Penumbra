using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using ImGuiId = ImSharp.ImGuiId;

namespace Penumbra.UI;

public static class UiHelpers
{
    /// <summary> The longest support button text. </summary>
    public static ReadOnlySpan<byte> SupportInfoButtonText
        => "Copy Support Info to Clipboard"u8;

    /// <summary>
    /// Draw a button that copies the support info to clipboards.
    /// </summary>
    /// <param name="penumbra"></param>
    public static void DrawSupportButton(Penumbra penumbra)
    {
        if (!Im.Button(SupportInfoButtonText))
            return;

        var text = penumbra.GatherSupportInformation();
        Im.Clipboard.Set(text);
        Penumbra.Messager.NotificationMessage("Copied Support Info to Clipboard.", NotificationType.Success, false);
    }

    /// <summary> Draw a button to open a specific directory in a file explorer.</summary>
    /// <param name="id">Specific ID for the given type of directory.</param>
    /// <param name="directory">The directory to open.</param>
    /// <param name="condition">Whether the button is available. </param>
    public static void DrawOpenDirectoryButton(ImGuiId id, DirectoryInfo directory, bool condition)
    {
        using var _ = Im.Id.Push(id);
        if (ImEx.Button("Open Directory"u8, Vector2.Zero, "Open this directory in your configured file explorer."u8,
                !condition || !Directory.Exists(directory.FullName)))
            Process.Start(new ProcessStartInfo(directory.FullName)
            {
                UseShellExecute = true,
            });
    }

    /// <summary> Draw default vertical space. </summary>
    public static void DefaultLineSpace()
        => Im.Dummy(DefaultSpace);

    /// <summary> Vertical spacing between groups. </summary>
    public static Vector2 DefaultSpace;

    /// <summary> Width of most input fields. </summary>
    public static Vector2 InputTextWidth;

    /// <summary> Frame Height for square icon buttons. </summary>
    public static Vector2 IconButtonSize;

    /// <summary> Input Text Width with space for an additional button with spacing of 3 between them. </summary>
    public static float InputTextMinusButton3;

    /// <summary> Input Text Width with space for an additional button with spacing of default item spacing between them. </summary>
    public static float InputTextMinusButton;

    /// <summary> Multiples of the current Global Scale </summary>
    public static float Scale;

    public static float ScaleX2;
    public static float ScaleX3;
    public static float ScaleX4;
    public static float ScaleX5;

    public static void SetupCommonSizes()
    {
        if (Im.Style.GlobalScale != Scale)
        {
            Scale          = Im.Style.GlobalScale;
            DefaultSpace   = new Vector2(0,            10 * Scale);
            InputTextWidth = new Vector2(350f * Scale, 0);
            ScaleX2        = Scale * 2;
            ScaleX3        = Scale * 3;
            ScaleX4        = Scale * 4;
            ScaleX5        = Scale * 5;
        }

        IconButtonSize        = new Vector2(Im.Style.FrameHeight);
        InputTextMinusButton3 = InputTextWidth.X - IconButtonSize.X - ScaleX3;
        InputTextMinusButton  = InputTextWidth.X - IconButtonSize.X - Im.Style.ItemSpacing.X;
    }
}
