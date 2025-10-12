using Dalamud.Plugin;
using ImSharp;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Interop;

namespace Penumbra.Api.IpcTester;

public class RedrawingIpcTester : Luna.IUiService, IDisposable
{
    private readonly IDalamudPluginInterface    _pi;
    private readonly ObjectManager              _objects;
    public readonly  EventSubscriber<nint, int> Redrawn;

    private int      _redrawIndex;
    private StringU8 _lastRedrawnString = new("None"u8);

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
        using var _ = Im.Tree.Node("Redrawing"u8);
        if (!_)
            return;

        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(RedrawObject.Label, "Redraw by Index"u8);
        var tmp = _redrawIndex;
        Im.Item.SetNextWidthScaled(100);
        if (Im.Drag("##redrawIndex"u8, ref tmp, 0, _objects.TotalCount, 0.1f))
            _redrawIndex = Math.Clamp(tmp, 0, _objects.TotalCount);
        Im.Line.Same();
        if (Im.Button("Redraw##Index"u8))
            new RedrawObject(_pi).Invoke(_redrawIndex);

        IpcTester.DrawIntro(RedrawAll.Label, "Redraw All"u8);
        if (Im.Button("Redraw##All"u8))
            new RedrawAll(_pi).Invoke();

        IpcTester.DrawIntro(GameObjectRedrawn.Label, "Last Redrawn Object:"u8);
        Im.Text(_lastRedrawnString);
    }

    private void SetLastRedrawn(nint address, int index)
    {
        if (index < 0
         || index > _objects.TotalCount
         || address == nint.Zero
         || _objects[index].Address != address)
            _lastRedrawnString = new StringU8("Invalid"u8);

        _lastRedrawnString = new StringU8($"{_objects[index].Utf8Name} (0x{address:X}, {index})");
    }
}
