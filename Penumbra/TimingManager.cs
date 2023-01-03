using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ImGuiNET;

namespace Penumbra;

public enum TimingType
{
    TotalTime,
    LaunchTime,
    DebugTimes,
    UiMainWindow,
    UiAdvancedWindow,
    GetResourceHandler,
    ReadSqPack,
    CharacterResolver,
    IdentifyCollection,
    CharacterBaseCreate,
    TimelineResources,
    LoadCharacterVfx,
    LoadAreaVfx,
    AddSubfile,
    SetResource,
    SetPathCollection,
}

public static class TimingManager
{
    public static readonly IReadOnlyList< ThreadLocal< Stopwatch > > StopWatches =
#if DEBUG
        Enum.GetValues< TimingType >().Select( e => new ThreadLocal< Stopwatch >( () => new Stopwatch(), true ) ).ToArray();
#else
        Array.Empty<ThreadLocal<Stopwatch>>();
#endif

    [Conditional( "DEBUG" )]
    public static void StartTimer( TimingType timingType )
    {
        var stopWatch = StopWatches[ ( int )timingType ].Value;
        stopWatch!.Start();
    }

    [Conditional( "DEBUG" )]
    public static void StopTimer( TimingType timingType )
    {
        var stopWatch = StopWatches[ ( int )timingType ].Value;
        stopWatch!.Stop();
    }

    [Conditional( "DEBUG" )]
    public static void StopAllTimers()
    {
        foreach( var threadWatch in StopWatches )
        {
            foreach( var stopWatch in threadWatch.Values )
            {
                stopWatch.Stop();
            }
        }
    }

    [Conditional( "DEBUG" )]
    public static void CreateTimingReport()
    {
        try
        {
            var sb = new StringBuilder( 1024 );
            sb.AppendLine( "```" );
            foreach( var type in Enum.GetValues< TimingType >() )
            {
                var watches = StopWatches[ ( int )type ];
                var timeSum = watches.Values.Sum( w => w.ElapsedMilliseconds );

                sb.AppendLine( $"{type,-20} - {timeSum,8} ms over {watches.Values.Count,2} Thread(s)" );
            }

            sb.AppendLine( "```" );

            ImGui.SetClipboardText( sb.ToString() );
        }
        catch( Exception ex )
        {
            Penumbra.Log.Error( $"Could not create timing report:\n{ex}" );
        }
    }
}