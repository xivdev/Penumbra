using System;
using System.Text.RegularExpressions;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Loader;

// A logger class that contains the relevant data to log requested files via regex.
// Filters are case-insensitive.
public class ResourceLogger : IDisposable
{
    // Enable or disable the logging of resources subject to the current filter.
    public void SetState( bool value )
    {
        if( value == Penumbra.Config.EnableResourceLogging )
        {
            return;
        }

        Penumbra.Config.EnableResourceLogging = value;
        Penumbra.Config.Save();
        if( value )
        {
            _resourceLoader.ResourceRequested += OnResourceRequested;
        }
        else
        {
            _resourceLoader.ResourceRequested -= OnResourceRequested;
        }
    }

    // Set the current filter to a new string, doing all other necessary work.
    public void SetFilter( string newFilter )
    {
        if( newFilter == Filter )
        {
            return;
        }

        Penumbra.Config.ResourceLoggingFilter = newFilter;
        Penumbra.Config.Save();
        SetupRegex();
    }

    // Returns whether the current filter is a valid regular expression.
    public bool ValidRegex
        => _filterRegex != null;

    private readonly ResourceLoader _resourceLoader;
    private          Regex?         _filterRegex;

    private static string Filter
        => Penumbra.Config.ResourceLoggingFilter;

    private void SetupRegex()
    {
        try
        {
            _filterRegex = new Regex( Filter, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant );
        }
        catch
        {
            _filterRegex = null;
        }
    }

    public ResourceLogger( ResourceLoader loader )
    {
        _resourceLoader = loader;
        SetupRegex();
        if( Penumbra.Config.EnableResourceLogging )
        {
            _resourceLoader.ResourceRequested += OnResourceRequested;
        }
    }

    private void OnResourceRequested( Utf8GamePath data, bool synchronous )
    {
        var path = Match( data.Path );
        if( path != null )
        {
            Penumbra.Log.Information( $"{path} was requested {( synchronous ? "synchronously." : "asynchronously." )}" );
        }
    }

    // Returns the converted string if the filter matches, and null otherwise.
    // The filter matches if it is empty, if it is a valid and matching regex or if the given string contains it.
    private string? Match( ByteString data )
    {
        var s = data.ToString();
        return Filter.Length == 0 || ( _filterRegex?.IsMatch( s ) ?? s.Contains( Filter, StringComparison.OrdinalIgnoreCase ) )
            ? s
            : null;
    }

    public void Dispose()
        => _resourceLoader.ResourceRequested -= OnResourceRequested;
}