using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.IpcSubscribers;
using Penumbra.String.Classes;

namespace Penumbra.Api.IpcTester;

public class ResolveIpcTester(IDalamudPluginInterface pi) : IUiService
{
    private string                       _currentResolvePath = string.Empty;
    private string                       _currentReversePath = string.Empty;
    private int                          _currentReverseIdx;
    private Task<(string[], string[][])> _task = Task.FromResult<(string[], string[][])>(([], []));

    public void Draw()
    {
        using var tree = ImRaii.TreeNode("Resolving");
        if (!tree)
            return;

        ImGui.InputTextWithHint("##resolvePath", "Resolve this game path...", ref _currentResolvePath, Utf8GamePath.MaxGamePathLength);
        ImGui.InputTextWithHint("##resolveInversePath", "Reverse-resolve this path...", ref _currentReversePath,
            Utf8GamePath.MaxGamePathLength);
        ImGui.InputInt("##resolveIdx", ref _currentReverseIdx, 0, 0);
        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(ResolveDefaultPath.Label, "Default Collection Resolve");
        if (_currentResolvePath.Length != 0)
            ImGui.TextUnformatted(new ResolveDefaultPath(pi).Invoke(_currentResolvePath));

        IpcTester.DrawIntro(ResolveInterfacePath.Label, "Interface Collection Resolve");
        if (_currentResolvePath.Length != 0)
            ImGui.TextUnformatted(new ResolveInterfacePath(pi).Invoke(_currentResolvePath));

        IpcTester.DrawIntro(ResolvePlayerPath.Label, "Player Collection Resolve");
        if (_currentResolvePath.Length != 0)
            ImGui.TextUnformatted(new ResolvePlayerPath(pi).Invoke(_currentResolvePath));

        IpcTester.DrawIntro(ResolveGameObjectPath.Label, "Game Object Collection Resolve");
        if (_currentResolvePath.Length != 0)
            ImGui.TextUnformatted(new ResolveGameObjectPath(pi).Invoke(_currentResolvePath, _currentReverseIdx));

        IpcTester.DrawIntro(ReverseResolvePlayerPath.Label, "Reversed Game Paths (Player)");
        if (_currentReversePath.Length > 0)
        {
            var list = new ReverseResolvePlayerPath(pi).Invoke(_currentReversePath);
            if (list.Length > 0)
            {
                ImGui.TextUnformatted(list[0]);
                if (list.Length > 1 && ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Join("\n", list.Skip(1)));
            }
        }

        IpcTester.DrawIntro(ReverseResolveGameObjectPath.Label, "Reversed Game Paths (Game Object)");
        if (_currentReversePath.Length > 0)
        {
            var list = new ReverseResolveGameObjectPath(pi).Invoke(_currentReversePath, _currentReverseIdx);
            if (list.Length > 0)
            {
                ImGui.TextUnformatted(list[0]);
                if (list.Length > 1 && ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Join("\n", list.Skip(1)));
            }
        }

        var forwardArray = _currentResolvePath.Length > 0
            ? [_currentResolvePath]
            : Array.Empty<string>();
        var reverseArray = _currentReversePath.Length > 0
            ? [_currentReversePath]
            : Array.Empty<string>();

        IpcTester.DrawIntro(ResolvePlayerPaths.Label, "Resolved Paths (Player)");
        if (forwardArray.Length > 0 || reverseArray.Length > 0)
        {
            var ret = new ResolvePlayerPaths(pi).Invoke(forwardArray, reverseArray);
            ImGui.TextUnformatted(ConvertText(ret));
        }

        IpcTester.DrawIntro(ResolvePlayerPathsAsync.Label, "Resolved Paths Async (Player)");
        if (ImGui.Button("Start"))
            _task = new ResolvePlayerPathsAsync(pi).Invoke(forwardArray, reverseArray);
        var hovered = ImGui.IsItemHovered();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(_task.Status.ToString());
        if ((hovered || ImGui.IsItemHovered()) && _task.IsCompletedSuccessfully)
            ImGui.SetTooltip(ConvertText(_task.Result));
        return;

        static string ConvertText((string[], string[][]) data)
        {
            var text = string.Empty;
            if (data.Item1.Length > 0)
            {
                if (data.Item2.Length > 0)
                    text = $"Forward: {data.Item1[0]} | Reverse: {string.Join("; ", data.Item2[0])}.";
                else
                    text = $"Forward: {data.Item1[0]}.";
            }
            else if (data.Item2.Length > 0)
            {
                text = $"Reverse: {string.Join("; ", data.Item2[0])}.";
            }

            return text;
        }
    }
}
