using Dalamud.Plugin.Services;
using OtterGui.Services;

namespace Penumbra.Util;

public sealed class PerformanceTracker(IFramework framework) : OtterGui.Classes.PerformanceTracker<PerformanceType>(framework), IService;

public enum PerformanceType
{
    UiMainWindow,
    UiAdvancedWindow,
    CharacterResolver,
    IdentifyCollection,
    GetResourceHandler,
    ReadSqPack,
    CharacterBaseCreate,
    TimelineResources,
    LoadCharacterVfx,
    LoadAreaVfx,
    LoadSound,
    ScheduleClipUpdate,
    LoadAction,
    LoadCharacterBaseAnimation,
    LoadPap,
    LoadTextures,
    LoadShaders,
    LoadApricotResources,
    UpdateModels,
    GetEqp,
    SetupVisor,
    SetupCharacter,
    ChangeCustomize,
    DebugTimes,
}

public static class TimingExtensions
{
    public static string ToName(this PerformanceType type)
        => type switch
        {
            PerformanceType.UiMainWindow               => "Main Interface Drawing",
            PerformanceType.UiAdvancedWindow           => "Advanced Window Drawing",
            PerformanceType.GetResourceHandler         => "GetResource Hook",
            PerformanceType.ReadSqPack                 => "ReadSqPack Hook",
            PerformanceType.CharacterResolver          => "Resolving Characters",
            PerformanceType.IdentifyCollection         => "Identifying Collections",
            PerformanceType.CharacterBaseCreate        => "CharacterBaseCreate Hook",
            PerformanceType.TimelineResources          => "LoadTimelineResources Hook",
            PerformanceType.LoadCharacterVfx           => "LoadCharacterVfx Hook",
            PerformanceType.LoadAreaVfx                => "LoadAreaVfx Hook",
            PerformanceType.LoadTextures               => "LoadTextures Hook",
            PerformanceType.LoadShaders                => "LoadShaders Hook",
            PerformanceType.LoadApricotResources       => "LoadApricotFiles Hook",
            PerformanceType.UpdateModels               => "UpdateModels Hook",
            PerformanceType.GetEqp                     => "GetEqp Hook",
            PerformanceType.SetupVisor                 => "SetupVisor Hook",
            PerformanceType.SetupCharacter             => "SetupCharacter Hook",
            PerformanceType.ChangeCustomize            => "ChangeCustomize Hook",
            PerformanceType.LoadSound                  => "LoadSound Hook",
            PerformanceType.ScheduleClipUpdate         => "ScheduleClipUpdate Hook",
            PerformanceType.LoadCharacterBaseAnimation => "LoadCharacterAnimation Hook",
            PerformanceType.LoadPap                    => "LoadPap Hook",
            PerformanceType.LoadAction                 => "LoadAction Hook",
            PerformanceType.DebugTimes                 => "Debug Tracking",
            _                                          => $"Unknown {(int)type}",
        };
}
