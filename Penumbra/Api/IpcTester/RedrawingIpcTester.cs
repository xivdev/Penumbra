using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Interop;
using Penumbra.UI;

namespace Penumbra.Api.IpcTester;

public class RedrawingIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface     _pi;
    private readonly ObjectManager              _objects;
    public readonly  EventSubscriber<nint, int> Redrawn;

    private int    _redrawIndex;
    private string _lastRedrawnString = "None";

    public RedrawingIpcTester(IDalamudPluginInterface pi, ObjectManager objects)
    {
        _pi      = pi;
        _objects = objects;
        Redrawn  = GameObjectRedrawn.Subscriber(_pi, SetLastRedrawn);
        Redrawn.Disable();
    }

    public void Dispose()
    {
        Redrawn.Dispose();
    }

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Redrawing");
        if (!_)
            return;

        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(RedrawObject.Label, "Redraw by Index");
        var tmp = _redrawIndex;
        ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
        if (ImGui.DragInt("##redrawIndex", ref tmp, 0.1f, 0, _objects.TotalCount))
            _redrawIndex = Math.Clamp(tmp, 0, _objects.TotalCount);
        ImGui.SameLine();
        if (ImGui.Button("Redraw##Index"))
            new RedrawObject(_pi).Invoke(_redrawIndex);

        IpcTester.DrawIntro(RedrawAll.Label, "Redraw All");
        if (ImGui.Button("Redraw##All"))
            new RedrawAll(_pi).Invoke();

        IpcTester.DrawIntro(GameObjectRedrawn.Label, "Last Redrawn Object:");
        ImGui.TextUnformatted(_lastRedrawnString);
    }

    private void SetLastRedrawn(nint address, int index)
    {
        if (index < 0
         || index > _objects.TotalCount
         || address == nint.Zero
         || _objects[index].Address != address)
            _lastRedrawnString = "Invalid";

        _lastRedrawnString = $"{_objects[index].Utf8Name} (0x{address:X}, {index})";
    }
}
