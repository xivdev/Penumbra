using OtterGui.Filesystem;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public struct GmpCache : IDisposable
{
    private          ExpandedGmpFile?        _gmpFile          = null;
    private readonly List< GmpManipulation > _gmpManipulations = new();

    public GmpCache()
    {}

    public void SetFiles(MetaFileManager manager)
        => manager.SetFile( _gmpFile, MetaIndex.Gmp );

    public MetaList.MetaReverter TemporarilySetFiles(MetaFileManager manager)
        => manager.TemporarilySetFile( _gmpFile, MetaIndex.Gmp );

    public void Reset()
    {
        if( _gmpFile == null )
            return;

        _gmpFile.Reset( _gmpManipulations.Select( m => m.SetId ) );
        _gmpManipulations.Clear();
    }

    public bool ApplyMod( MetaFileManager manager, GmpManipulation manip )
    {
        _gmpManipulations.AddOrReplace( manip );
        _gmpFile ??= new ExpandedGmpFile(manager);
        return manip.Apply( _gmpFile );
    }

    public bool RevertMod( MetaFileManager manager, GmpManipulation manip )
    {
        if (!_gmpManipulations.Remove(manip))
            return false;

        var def = ExpandedGmpFile.GetDefault( manager, manip.SetId );
        manip = new GmpManipulation( def, manip.SetId );
        return manip.Apply( _gmpFile! );
    }

    public void Dispose()
    {
        _gmpFile?.Dispose();
        _gmpFile = null;
        _gmpManipulations.Clear();
    }
}