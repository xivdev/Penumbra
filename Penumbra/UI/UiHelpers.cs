using System.Diagnostics;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.UI.Classes;

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

    /// <summary> Apply Changed Item Counters to the Name if necessary. </summary>
    public static string ChangedItemName(string name, object? data)
        => data is int counter ? $"{counter} Files Manipulating {name}s" : name;

    /// <summary>
    /// Draw a changed item, invoking the Api-Events for clicks and tooltips.
    /// Also draw the item Id in grey if requested.
    /// </summary>
    public static void DrawChangedItem(PenumbraApi api, string name, object? data, bool drawId)
    {
        name = ChangedItemName(name, data);
        var ret = ImGui.Selectable(name) ? MouseButton.Left : MouseButton.None;
        ret = ImGui.IsItemClicked(ImGuiMouseButton.Right) ? MouseButton.Right : ret;
        ret = ImGui.IsItemClicked(ImGuiMouseButton.Middle) ? MouseButton.Middle : ret;

        if (ret != MouseButton.None)
            api.InvokeClick(ret, data);

        if (api.HasTooltip && ImGui.IsItemHovered())
        {
            // We can not be sure that any subscriber actually prints something in any case.
            // Circumvent ugly blank tooltip with less-ugly useless tooltip.
            using var tt    = ImRaii.Tooltip();
            using var group = ImRaii.Group();
            api.InvokeTooltip(data);
            group.Dispose();
            if (ImGui.GetItemRectSize() == Vector2.Zero)
                ImGui.TextUnformatted("No actions available.");
        }

        if (!drawId || !GetChangedItemObject(data, out var text))
            return;

        ImGui.SameLine(ImGui.GetContentRegionAvail().X);
        ImGuiUtil.RightJustify(text, ColorId.ItemId.Value(Penumbra.Config));
    }

    /// <summary> Return more detailed object information in text, if it exists. </summary>
    public static bool GetChangedItemObject(object? obj, out string text)
    {
        switch (obj)
        {
            case Item it:
                var quad = (Quad)it.ModelMain;
                text = quad.C == 0 ? $"({quad.A}-{quad.B})" : $"({quad.A}-{quad.B}-{quad.C})";
                return true;
            case ModelChara m:
                text = $"({((CharacterBase.ModelType)m.Type).ToName()} {m.Model}-{m.Base}-{m.Variant})";
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    /// <summary> Draw a button to open the official discord server. </summary>
    /// <param name="width">The desired width of the button.</param>
    public static void DrawDiscordButton(float width)
    {
        const string address = @"https://discord.gg/kVva7DHV4r";
        using var    color   = ImRaii.PushColor(ImGuiCol.Button, Colors.DiscordColor);
        if (ImGui.Button("Join Discord for Support", new Vector2(width, 0)))
            try
            {
                var process = new ProcessStartInfo(address)
                {
                    UseShellExecute = true,
                };
                Process.Start(process);
            }
            catch
            {
                Penumbra.ChatService.NotificationMessage($"Unable to open Discord at {address}.", "Error", NotificationType.Error);
            }

        ImGuiUtil.HoverTooltip($"Open {address}");
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
        Penumbra.ChatService.NotificationMessage($"Copied Support Info to Clipboard.", "Success", NotificationType.Success);
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

    /// <summary> Draw the button that opens the ReniGuide. </summary>
    public static void DrawGuideButton(float width)
    {
        const string address = @"https://reniguide.info/";
        using var color = ImRaii.PushColor(ImGuiCol.Button, Colors.ReniColorButton)
            .Push(ImGuiCol.ButtonHovered, Colors.ReniColorHovered)
            .Push(ImGuiCol.ButtonActive,  Colors.ReniColorActive);
        if (ImGui.Button("Beginner's Guides", new Vector2(width, 0)))
            try
            {
                var process = new ProcessStartInfo(address)
                {
                    UseShellExecute = true,
                };
                Process.Start(process);
            }
            catch
            {
                Penumbra.ChatService.NotificationMessage($"Could not open guide at {address} in external browser.", "Error",
                    NotificationType.Error);
            }

        ImGuiUtil.HoverTooltip(
            $"Open {address}\nImage and text based guides for most functionality of Penumbra made by Serenity.\n"
          + "Not directly affiliated and potentially, but not usually out of date.");
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

        IconButtonSize       = new Vector2(ImGui.GetFrameHeight());
        InputTextMinusButton3 = InputTextWidth.X - IconButtonSize.X - ScaleX3;
        InputTextMinusButton = InputTextWidth.X - IconButtonSize.X - ImGui.GetStyle().ItemSpacing.X;
    }
}
