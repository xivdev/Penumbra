using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Penumbra.String;

namespace Penumbra.Api.IpcTester;

public class GameStateIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface                        _pi;
    public readonly  EventSubscriber<nint, Guid, nint, nint, nint> CharacterBaseCreating;
    public readonly  EventSubscriber<nint, Guid, nint>             CharacterBaseCreated;
    public readonly  EventSubscriber<nint, string, string>         GameObjectResourcePathResolved;

    private string         _lastCreatedGameObjectName = string.Empty;
    private nint           _lastCreatedDrawObject     = nint.Zero;
    private DateTimeOffset _lastCreatedGameObjectTime = DateTimeOffset.MaxValue;
    private string         _lastResolvedGamePath      = string.Empty;
    private string         _lastResolvedFullPath      = string.Empty;
    private string         _lastResolvedObject        = string.Empty;
    private DateTimeOffset _lastResolvedGamePathTime  = DateTimeOffset.MaxValue;
    private string         _currentDrawObjectString   = string.Empty;
    private nint           _currentDrawObject         = nint.Zero;
    private int            _currentCutsceneActor;
    private int            _currentCutsceneParent;
    private PenumbraApiEc  _cutsceneError = PenumbraApiEc.Success;

    public GameStateIpcTester(IDalamudPluginInterface pi)
    {
        _pi                            = pi;
        CharacterBaseCreating          = IpcSubscribers.CreatingCharacterBase.Subscriber(pi, UpdateLastCreated);
        CharacterBaseCreated           = IpcSubscribers.CreatedCharacterBase.Subscriber(pi, UpdateLastCreated2);
        GameObjectResourcePathResolved = IpcSubscribers.GameObjectResourcePathResolved.Subscriber(pi, UpdateGameObjectResourcePath);
        CharacterBaseCreating.Disable();
        CharacterBaseCreated.Disable();
        GameObjectResourcePathResolved.Disable();
    }

    public void Dispose()
    {
        CharacterBaseCreating.Dispose();
        CharacterBaseCreated.Dispose();
        GameObjectResourcePathResolved.Dispose();
    }

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Game State");
        if (!_)
            return;

        if (ImGui.InputTextWithHint("##drawObject", "Draw Object Address..", ref _currentDrawObjectString, 16,
                ImGuiInputTextFlags.CharsHexadecimal))
            _currentDrawObject = nint.TryParse(_currentDrawObjectString, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var tmp)
                ? tmp
                : nint.Zero;

        ImGui.InputInt("Cutscene Actor",  ref _currentCutsceneActor,  0);
        ImGui.InputInt("Cutscene Parent", ref _currentCutsceneParent, 0);
        if (_cutsceneError is not PenumbraApiEc.Success)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted("Invalid Argument on last Call");
        }

        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(GetDrawObjectInfo.Label, "Draw Object Info");
        if (_currentDrawObject == nint.Zero)
        {
            ImGui.TextUnformatted("Invalid");
        }
        else
        {
            var (ptr, (collectionId, collectionName)) = new GetDrawObjectInfo(_pi).Invoke(_currentDrawObject);
            ImGui.TextUnformatted(ptr == nint.Zero ? $"No Actor Associated, {collectionName}" : $"{ptr:X}, {collectionName}");
            ImGui.SameLine();
            using (ImRaii.PushFont(UiBuilder.MonoFont))
            {
                ImGui.TextUnformatted(collectionId.ToString());
            }
        }

        IpcTester.DrawIntro(GetCutsceneParentIndex.Label, "Cutscene Parent");
        ImGui.TextUnformatted(new GetCutsceneParentIndex(_pi).Invoke(_currentCutsceneActor).ToString());

        IpcTester.DrawIntro(SetCutsceneParentIndex.Label, "Cutscene Parent");
        if (ImGui.Button("Set Parent"))
            _cutsceneError = new SetCutsceneParentIndex(_pi)
                .Invoke(_currentCutsceneActor, _currentCutsceneParent);

        IpcTester.DrawIntro(CreatingCharacterBase.Label, "Last Drawobject created");
        if (_lastCreatedGameObjectTime < DateTimeOffset.Now)
            ImGui.TextUnformatted(_lastCreatedDrawObject != nint.Zero
                ? $"0x{_lastCreatedDrawObject:X} for <{_lastCreatedGameObjectName}> at {_lastCreatedGameObjectTime}"
                : $"NULL for <{_lastCreatedGameObjectName}> at {_lastCreatedGameObjectTime}");

        IpcTester.DrawIntro(IpcSubscribers.GameObjectResourcePathResolved.Label, "Last GamePath resolved");
        if (_lastResolvedGamePathTime < DateTimeOffset.Now)
            ImGui.TextUnformatted(
                $"{_lastResolvedGamePath} -> {_lastResolvedFullPath} for <{_lastResolvedObject}> at {_lastResolvedGamePathTime}");
    }

    private void UpdateLastCreated(nint gameObject, Guid _, nint _2, nint _3, nint _4)
    {
        _lastCreatedGameObjectName = GetObjectName(gameObject);
        _lastCreatedGameObjectTime = DateTimeOffset.Now;
        _lastCreatedDrawObject     = nint.Zero;
    }

    private void UpdateLastCreated2(nint gameObject, Guid _, nint drawObject)
    {
        _lastCreatedGameObjectName = GetObjectName(gameObject);
        _lastCreatedGameObjectTime = DateTimeOffset.Now;
        _lastCreatedDrawObject     = drawObject;
    }

    private void UpdateGameObjectResourcePath(nint gameObject, string gamePath, string fullPath)
    {
        _lastResolvedObject       = GetObjectName(gameObject);
        _lastResolvedGamePath     = gamePath;
        _lastResolvedFullPath     = fullPath;
        _lastResolvedGamePathTime = DateTimeOffset.Now;
    }

    private static unsafe string GetObjectName(nint gameObject)
    {
        var obj  = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject;
        return obj != null && obj->Name[0] != 0 ? new ByteString(obj->Name).ToString() : "Unknown";
    }
}
