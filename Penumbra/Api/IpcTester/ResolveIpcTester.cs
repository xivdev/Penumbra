using Dalamud.Plugin;
using ImSharp;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class ResolveIpcTester(IDalamudPluginInterface pi) : Luna.IUiService
{
    private string                       _currentResolvePath = string.Empty;
    private string                       _currentReversePath = string.Empty;
    private int                          _currentReverseIdx;
    private Task<(string[], string[][])> _task = Task.FromResult<(string[], string[][])>(([], []));

    public void Draw()
    {
        using var tree = Im.Tree.Node("Resolving"u8);
        if (!tree)
            return;

        Im.Input.Text("##resolvePath"u8,        ref _currentResolvePath, "Resolve this game path..."u8);
        Im.Input.Text("##resolveInversePath"u8, ref _currentReversePath, "Reverse-resolve this path..."u8);
        Im.Input.Scalar("##resolveIdx"u8, ref _currentReverseIdx);
        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        using (IpcTester.DrawIntro(ResolveDefaultPath.LabelU8, "Default Collection Resolve"u8))
        {
            if (_currentResolvePath.Length is not 0)
                table.DrawColumn(new ResolveDefaultPath(pi).Invoke(_currentResolvePath));
        }

        using (IpcTester.DrawIntro(ResolveInterfacePath.LabelU8, "Interface Collection Resolve"u8))
        {
            if (_currentResolvePath.Length is not 0)
                table.DrawColumn(new ResolveInterfacePath(pi).Invoke(_currentResolvePath));
        }

        using (IpcTester.DrawIntro(ResolvePlayerPath.LabelU8, "Player Collection Resolve"u8))
        {
            if (_currentResolvePath.Length is not 0)
                table.DrawColumn(new ResolvePlayerPath(pi).Invoke(_currentResolvePath));
        }

        using (IpcTester.DrawIntro(ResolveGameObjectPath.LabelU8, "Game Object Collection Resolve"u8))
        {
            if (_currentResolvePath.Length is not 0)
                table.DrawColumn(new ResolveGameObjectPath(pi).Invoke(_currentResolvePath, _currentReverseIdx));
        }

        using (IpcTester.DrawIntro(ReverseResolvePlayerPath.LabelU8, "Reversed Game Paths (Player)"u8))
        {
            if (_currentReversePath.Length is not 0)
            {
                var list = new ReverseResolvePlayerPath(pi).Invoke(_currentReversePath);
                if (list.Length > 0)
                {
                    table.DrawColumn(list[0]);
                    if (list.Length > 1 && Im.Item.Hovered())
                        Im.Tooltip.Set(StringU8.Join((byte)'\n', list.Skip(1)));
                }
            }
        }

        using (IpcTester.DrawIntro(ReverseResolveGameObjectPath.LabelU8, "Reversed Game Paths (Game Object)"u8))
        {
            if (_currentReversePath.Length is not 0)
            {
                var list = new ReverseResolveGameObjectPath(pi).Invoke(_currentReversePath, _currentReverseIdx);
                if (list.Length > 0)
                {
                    table.DrawColumn(list[0]);
                    if (list.Length > 1 && Im.Item.Hovered())
                        Im.Tooltip.Set(StringU8.Join((byte)'\n', list.Skip(1)));
                }
            }
        }

        string[] forwardArray = _currentResolvePath.Length > 0 ? [_currentResolvePath] : [];
        string[] reverseArray = _currentReversePath.Length > 0 ? [_currentReversePath] : [];

        using (IpcTester.DrawIntro(ResolvePlayerPaths.LabelU8, "Resolved Paths (Player)"u8))
        {
            if (forwardArray.Length > 0 || reverseArray.Length > 0)
            {
                var ret = new ResolvePlayerPaths(pi).Invoke(forwardArray, reverseArray);
                table.DrawColumn(ConvertText(ret));
            }
        }

        using (IpcTester.DrawIntro(ResolvePlayerPathsAsync.LabelU8, "Resolved Paths Async (Player)"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Start"u8))
                _task = new ResolvePlayerPathsAsync(pi).Invoke(forwardArray, reverseArray);
            var hovered = Im.Item.Hovered();
            Im.Line.Same();
            ImEx.TextFrameAligned($"{_task.Status}");
            if ((hovered || Im.Item.Hovered()) && _task.IsCompletedSuccessfully && _task.Result.Item1.Length > 0)
                Im.Tooltip.Set(ConvertText(_task.Result));
        }

        return;

        static StringU8 ConvertText((string[], string[][]) data)
        {
            var text = StringU8.Empty;
            if (data.Item1.Length > 0)
                text = data.Item2.Length > 0
                    ? new StringU8($"Forward: {data.Item1[0]} | Reverse: {StringU8.Join("; "u8, data.Item2[0])}.")
                    : new StringU8($"Forward: {data.Item1[0]}.");
            else if (data.Item2.Length > 0)
                text = new StringU8($"Reverse: {StringU8.Join("; "u8, data.Item2[0])}.");

            return text;
        }
    }
}
