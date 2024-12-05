using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Interop.Services;

namespace Penumbra.UI.Tabs.Debug;

public unsafe class GlobalVariablesDrawer(CharacterUtility characterUtility, ResidentResourceManager residentResources) : IUiService
{
    /// <summary> Draw information about some game global variables. </summary>
    public void Draw()
    {
        var header = ImUtf8.CollapsingHeader("Global Variables"u8);
        ImUtf8.HoverTooltip("Draw information about global variables. Can provide useful starting points for a memory viewer."u8);
        if (!header)
            return;

        DebugTab.DrawCopyableAddress("CharacterUtility"u8,        characterUtility.Address);
        DebugTab.DrawCopyableAddress("ResidentResourceManager"u8, residentResources.Address);
        DebugTab.DrawCopyableAddress("Device"u8,                  Device.Instance());
        DrawCharacterUtility();
        DrawResidentResources();
    }

    /// <summary>
    /// Draw information about the character utility class from SE,
    /// displaying all files, their sizes, the default files and the default sizes.
    /// </summary>
    private void DrawCharacterUtility()
    {
        using var tree = ImUtf8.TreeNode("Character Utility"u8);
        if (!tree)
            return;

        using var table = ImUtf8.Table("##CharacterUtility"u8, 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var idx = 0; idx < CharacterUtility.ReverseIndices.Length; ++idx)
        {
            var intern   = CharacterUtility.ReverseIndices[idx];
            var resource = characterUtility.Address->Resource(idx);
            ImUtf8.DrawTableColumn($"[{idx}]");
            ImGui.TableNextColumn();
            ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource:X}");
            if (resource == null)
            {
                ImGui.TableNextRow();
                continue;
            }

            ImUtf8.DrawTableColumn(resource->CsHandle.FileName.AsSpan());
            ImGui.TableNextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            if (ImUtf8.Selectable($"0x{data:X}"))
                if (data != nint.Zero && length > 0)
                    ImUtf8.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)data, (int)length).ToArray().Select(b => b.ToString("X2"))));

            ImUtf8.HoverTooltip("Click to copy bytes to clipboard."u8);
            ImUtf8.DrawTableColumn(length.ToString());

            ImGui.TableNextColumn();
            if (intern.Value != -1)
            {
                ImUtf8.Selectable($"0x{characterUtility.DefaultResource(intern).Address:X}");
                if (ImGui.IsItemClicked())
                    ImUtf8.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)characterUtility.DefaultResource(intern).Address,
                            characterUtility.DefaultResource(intern).Size).ToArray().Select(b => b.ToString("X2"))));

                ImUtf8.HoverTooltip("Click to copy bytes to clipboard."u8);

                ImUtf8.DrawTableColumn($"{characterUtility.DefaultResource(intern).Size}");
            }
            else
            {
                ImGui.TableNextColumn();
            }
        }
    }

    /// <summary> Draw information about the resident resource files. </summary>
    private void DrawResidentResources()
    {
        using var tree = ImUtf8.TreeNode("Resident Resources"u8);
        if (!tree)
            return;

        if (residentResources.Address == null || residentResources.Address->NumResources == 0)
            return;

        using var table = ImUtf8.Table("##ResidentResources"u8, 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var idx = 0; idx < residentResources.Address->NumResources; ++idx)
        {
            var resource = residentResources.Address->ResourceList[idx];
            ImUtf8.DrawTableColumn($"[{idx}]");
            ImGui.TableNextColumn();
            ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource:X}");
            if (resource == null)
            {
                ImGui.TableNextRow();
                continue;
            }

            ImUtf8.DrawTableColumn(resource->CsHandle.FileName.AsSpan());
            ImGui.TableNextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            if (ImUtf8.Selectable($"0x{data:X}"))
                if (data != nint.Zero && length > 0)
                    ImUtf8.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)data, (int)length).ToArray().Select(b => b.ToString("X2"))));

            ImUtf8.HoverTooltip("Click to copy bytes to clipboard."u8);
            ImUtf8.DrawTableColumn(length.ToString());
        }
    }
}
