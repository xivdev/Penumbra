using System;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Penumbra.Api.Helpers;

public sealed class FuncProvider< TRet > : IDisposable
{
    private ICallGateProvider< TRet >? _provider;

    public FuncProvider( DalamudPluginInterface pi, string label, Func< TRet > func )
    {
        try
        {
            _provider = pi.GetIpcProvider< TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterFunc( func );
    }

    public void Dispose()
    {
        _provider?.UnregisterFunc();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~FuncProvider()
        => Dispose();
}

public sealed class FuncProvider< T1, TRet > : IDisposable
{
    private ICallGateProvider< T1, TRet >? _provider;

    public FuncProvider( DalamudPluginInterface pi, string label, Func< T1, TRet > func )
    {
        try
        {
            _provider = pi.GetIpcProvider< T1, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterFunc( func );
    }

    public void Dispose()
    {
        _provider?.UnregisterFunc();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~FuncProvider()
        => Dispose();
}

public sealed class FuncProvider< T1, T2, TRet > : IDisposable
{
    private ICallGateProvider< T1, T2, TRet >? _provider;

    public FuncProvider( DalamudPluginInterface pi, string label, Func< T1, T2, TRet > func )
    {
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterFunc( func );
    }

    public void Dispose()
    {
        _provider?.UnregisterFunc();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~FuncProvider()
        => Dispose();
}

public sealed class FuncProvider< T1, T2, T3, TRet > : IDisposable
{
    private ICallGateProvider< T1, T2, T3, TRet >? _provider;

    public FuncProvider( DalamudPluginInterface pi, string label, Func< T1, T2, T3, TRet > func )
    {
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterFunc( func );
    }

    public void Dispose()
    {
        _provider?.UnregisterFunc();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~FuncProvider()
        => Dispose();
}

public sealed class FuncProvider< T1, T2, T3, T4, TRet > : IDisposable
{
    private ICallGateProvider< T1, T2, T3, T4, TRet >? _provider;

    public FuncProvider( DalamudPluginInterface pi, string label, Func< T1, T2, T3, T4, TRet > func )
    {
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, T4, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterFunc( func );
    }

    public void Dispose()
    {
        _provider?.UnregisterFunc();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~FuncProvider()
        => Dispose();
}

public sealed class FuncProvider< T1, T2, T3, T4, T5, TRet > : IDisposable
{
    private ICallGateProvider< T1, T2, T3, T4, T5, TRet >? _provider;

    public FuncProvider( DalamudPluginInterface pi, string label, Func< T1, T2, T3, T4, T5, TRet > func )
    {
        try
        {
            _provider = pi.GetIpcProvider< T1, T2, T3, T4, T5, TRet >( label );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC Provider for {label}\n{e}" );
            _provider = null;
        }

        _provider?.RegisterFunc( func );
    }

    public void Dispose()
    {
        _provider?.UnregisterFunc();
        _provider = null;
        GC.SuppressFinalize( this );
    }

    ~FuncProvider()
        => Dispose();
}