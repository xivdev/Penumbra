using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Interop;

namespace Penumbra.Api.IpcTester;

public class GameStateIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface                       _pi;
    public readonly  EventSubscriber<nint, Guid, nint, nint, nint> CharacterBaseCreating;
    public readonly  EventSubscriber<nint, Guid, nint>             CharacterBaseCreated;
    public readonly  EventSubscriber<nint, string, string>         GameObjectResourcePathResolved;

    private StringU8       _lastCreatedGameObjectName = StringU8.Empty;
    private nint           _lastCreatedDrawObject     = nint.Zero;
    private DateTimeOffset _lastCreatedGameObjectTime = DateTimeOffset.MaxValue;
    private string         _lastResolvedGamePath      = string.Empty;
    private string         _lastResolvedFullPath      = string.Empty;
    private StringU8       _lastResolvedObject        = StringU8.Empty;
    private DateTimeOffset _lastResolvedGamePathTime  = DateTimeOffset.MaxValue;
    private StringU8       _currentDrawObjectString   = StringU8.Empty;
    private nint           _currentDrawObject         = nint.Zero;
    private int            _currentCutsceneActor;
    private int            _currentCutsceneParent;
    private PenumbraApiEc  _cutsceneError = PenumbraApiEc.Success;

    public GameStateIpcTester(IDalamudPluginInterface pi)
    {
        _pi                            = pi;
        CharacterBaseCreating          = CreatingCharacterBase.Subscriber(pi, UpdateLastCreated);
        CharacterBaseCreated           = CreatedCharacterBase.Subscriber(pi, UpdateLastCreated2);
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
        using var _ = Im.Tree.Node("Game State"u8);
        if (!_)
            return;

        if (Im.Input.Text("##drawObject"u8, ref _currentDrawObjectString, "Draw Object Address.."u8, InputTextFlags.CharsHexadecimal))
            _currentDrawObject = nint.TryParse(_currentDrawObjectString, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var tmp)
                ? tmp
                : nint.Zero;

        Im.Input.Scalar("Cutscene Actor"u8,  ref _currentCutsceneActor);
        Im.Input.Scalar("Cutscene Parent"u8, ref _currentCutsceneParent);
        if (_cutsceneError is not PenumbraApiEc.Success)
        {
            Im.Line.Same();
            Im.Text("Invalid Argument on last Call"u8);
        }

        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        using (IpcTester.DrawIntro(GetDrawObjectInfo.Label, "Draw Object Info"u8))
        {
            table.NextColumn();
            if (_currentDrawObject == nint.Zero)
            {
                Im.Text("Invalid"u8);
            }
            else
            {
                var (ptr, (collectionId, collectionName)) = new GetDrawObjectInfo(_pi).Invoke(_currentDrawObject);
                Im.Text(ptr == nint.Zero ? $"No Actor Associated, {collectionName}" : $"{ptr:X}, {collectionName}");
                Im.Line.Same();
                LunaStyle.DrawGuid(collectionId);
            }
        }

        using (IpcTester.DrawIntro(GetCutsceneParentIndex.Label, "Cutscene Parent"u8))
        {
            table.DrawColumn($"{new GetCutsceneParentIndex(_pi).Invoke(_currentCutsceneActor)}");
        }

        using (IpcTester.DrawIntro(SetCutsceneParentIndex.Label, "Cutscene Parent"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Set Parent"u8))
                _cutsceneError = new SetCutsceneParentIndex(_pi)
                    .Invoke(_currentCutsceneActor, _currentCutsceneParent);
        }

        using (IpcTester.DrawIntro(CreatingCharacterBase.Label, "Last Drawobject created"u8))
        {
            if (_lastCreatedGameObjectTime < DateTimeOffset.Now)
                table.DrawColumn(_lastCreatedDrawObject != nint.Zero
                    ? $"0x{_lastCreatedDrawObject:X} for <{_lastCreatedGameObjectName}> at {_lastCreatedGameObjectTime}"
                    : $"NULL for <{_lastCreatedGameObjectName}> at {_lastCreatedGameObjectTime}");
        }

        using (IpcTester.DrawIntro(IpcSubscribers.GameObjectResourcePathResolved.Label, "Last GamePath resolved"u8))
        {
            if (_lastResolvedGamePathTime < DateTimeOffset.Now)
                table.DrawColumn(
                    $"{_lastResolvedGamePath} -> {_lastResolvedFullPath} for <{_lastResolvedObject}> at {_lastResolvedGamePathTime}");
        }
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

    private static StringU8 GetObjectName(nint gameObject)
        => new(((Actor)gameObject).StoredName());
}
