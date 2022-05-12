using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game;

namespace Penumbra.Util;

// Manage certain actions to only occur on framework updates.
public class FrameworkManager : IDisposable
{
    private readonly Dictionary< string, Action > _important = new();
    private readonly Dictionary< string, Action > _delayed   = new();

    public FrameworkManager()
        => Dalamud.Framework.Update += OnUpdate;

    // Register an action that is not time critical.
    // One action per frame will be executed.
    // On dispose, any remaining actions will be executed.
    public void RegisterDelayed( string tag, Action action )
        => _delayed[ tag ] = action;

    // Register an action that should be executed on the next frame.
    // All of those actions will be executed in the next frame.
    // If there are more than one, they will be launched in separated tasks, but waited for.
    public void RegisterImportant( string tag, Action action )
        => _important[ tag ] = action;

    public void Dispose()
    {
        Dalamud.Framework.Update -= OnUpdate;
        HandleAll( _delayed );
    }

    private void OnUpdate( Framework _ )
    {
        HandleOne();
        HandleAllTasks( _important );
    }

    private void HandleOne()
    {
        if( _delayed.Count > 0 )
        {
            var (key, action) = _delayed.First();
            action();
            _delayed.Remove( key );
        }
    }

    private static void HandleAll( IDictionary< string, Action > dict )
    {
        foreach( var (_, action) in dict )
        {
            action();
        }

        dict.Clear();
    }

    private static void HandleAllTasks( IDictionary< string, Action > dict )
    {
        if( dict.Count < 2 )
        {
            HandleAll( dict );
        }
        else
        {
            var tasks = dict.Values.Select( Task.Run ).ToArray();
            Task.WaitAll( tasks );
            dict.Clear();
        }
    }
}