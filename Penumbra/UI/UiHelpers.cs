using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.Interop.Structs;
using Penumbra.String;

namespace Penumbra.UI;

public static class UiHelpers
{
    /// <summary> Draw text given by a ByteString. </summary>
    public static unsafe void Text(ByteString s)
        => ImGuiNative.igTextUnformatted(s.Path, s.Path + s.Length);

    /// <summary> Draw text given by a byte pointer and length. </summary>
    public static unsafe void Text(byte* s, int length)
        => ImGuiNative.igTextUnformatted(s, s + length);

    /// <summary> Draw the name of a resource file. </summary>
    public static unsafe void Text(ResourceHandle* resource)
        => Text(resource->FileName().Path, resource->FileNameLength);

    /// <summary> Draw a ByteString as a selectable. </summary>
    public static unsafe bool Selectable(ByteString s, bool selected)
    {
        var tmp = (byte)(selected ? 1 : 0);
        return ImGuiNative.igSelectable_Bool(s.Path, tmp, ImGuiSelectableFlags.None, Vector2.Zero) != 0;
    }

    /// <summary>
    /// A selectable that copies its text to clipboard on selection and provides a on-hover tooltip about that,
    /// using an ByteString.
    /// </summary>
    public static unsafe void CopyOnClickSelectable(ByteString text)
    {
        if (ImGuiNative.igSelectable_Bool(text.Path, 0, ImGuiSelectableFlags.None, Vector2.Zero) != 0)
            ImGuiNative.igSetClipboardText(text.Path);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Click to copy to clipboard.");
    }

    /// <summary> The longest support button text. </summary>
    public const string SupportInfoButtonText = "Copy Support Info to Clipboard";

    /// <summary>
    /// Draw a button that copies the support info to clipboards.
    /// </summary>
    /// <param name="penumbra"></param>
    public static void DrawSupportButton(Penumbra penumbra)
    {
        if (!ImGui.Button(SupportInfoButtonText))
            return;

        var text = penumbra.GatherSupportInformation();
        ImGui.SetClipboardText(text);
        Penumbra.Messager.NotificationMessage($"Copied Support Info to Clipboard.", NotificationType.Success, false);
    }

    /// <summary> Draw a button to open a specific directory in a file explorer.</summary>
    /// <param name="id">Specific ID for the given type of directory.</param>
    /// <param name="directory">The directory to open.</param>
    /// <param name="condition">Whether the button is available. </param>
    public static void DrawOpenDirectoryButton(int id, DirectoryInfo directory, bool condition)
    {
        using var _ = ImRaii.PushId(id);
        if (ImGuiUtil.DrawDisabledButton("Open Directory", Vector2.Zero, "Open this directory in your configured file explorer.",
                !condition || !Directory.Exists(directory.FullName)))
            Process.Start(new ProcessStartInfo(directory.FullName)
            {
                UseShellExecute = true,
            });
    }

    /// <summary> Draw default vertical space. </summary>
    public static void DefaultLineSpace()
        => ImGui.Dummy(DefaultSpace);

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
        if (ImGuiHelpers.GlobalScale != Scale)
        {
            Scale          = ImGuiHelpers.GlobalScale;
            DefaultSpace   = new Vector2(0,            10 * Scale);
            InputTextWidth = new Vector2(350f * Scale, 0);
            ScaleX2        = Scale * 2;
            ScaleX3        = Scale * 3;
            ScaleX4        = Scale * 4;
            ScaleX5        = Scale * 5;
        }

        IconButtonSize        = new Vector2(ImGui.GetFrameHeight());
        InputTextMinusButton3 = InputTextWidth.X - IconButtonSize.X - ScaleX3;
        InputTextMinusButton  = InputTextWidth.X - IconButtonSize.X - ImGui.GetStyle().ItemSpacing.X;
    }
}
