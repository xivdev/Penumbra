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
    {
        lock( _delayed )
        {
            _delayed[ tag ] = action;
        }
    }

    // Register an action that should be executed on the next frame.
    // All of those actions will be executed in the next frame.
    // If there are more than one, they will be launched in separated tasks, but waited for.
    public void RegisterImportant( string tag, Action action )
    {
        lock( _important )
        {
            _important[ tag ] = action;
        }
    }

    public void Dispose()
    {
        Dalamud.Framework.Update -= OnUpdate;
        foreach( var (_, action) in _delayed )
        {
            action();
        }

        _delayed.Clear();
    }

    private void OnUpdate( Framework _ )
    {
        try
        {
            HandleOne( _delayed );
            HandleAllTasks( _important );
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Problem saving data:\n{e}" );
        }
    }

    private static void HandleOne( IDictionary< string, Action > dict )
    {
        if( dict.Count == 0 )
        {
            return;
        }

        Action action;
        lock( dict )
        {
            ( var key, action ) = dict.First();
            dict.Remove( key );
        }

        action();
    }

    private static void HandleAllTasks( IDictionary< string, Action > dict )
    {
        if( dict.Count < 2 )
        {
            HandleOne( dict );
        }
        else
        {
            Task[] tasks;
            lock( dict )
            {
                tasks = dict.Values.Select( Task.Run ).ToArray();
                dict.Clear();
            }

            Task.WaitAll( tasks );
        }
    }
}