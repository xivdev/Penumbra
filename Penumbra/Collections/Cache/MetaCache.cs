using System.Diagnostics.CodeAnalysis;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.String.Classes;

namespace Penumbra.Collections.Cache;

public class MetaCache : IDisposable, IEnumerable<KeyValuePair<MetaManipulation, IMod>>
{
    private readonly MetaFileManager                    _manager;
    private readonly ModCollection                      _collection;
    private readonly Dictionary<MetaManipulation, IMod> _manipulations = new();
    private          EqpCache                           _eqpCache      = new();
    private readonly EqdpCache                          _eqdpCache     = new();
    private          EstCache                           _estCache      = new();
    private          GmpCache                           _gmpCache      = new();
    private          CmpCache                           _cmpCache      = new();
    private readonly ImcCache                           _imcCache      = new();

    public bool TryGetValue(MetaManipulation manip, [NotNullWhen(true)] out IMod? mod)
    {
        lock (_manipulations)
        {
            return _manipulations.TryGetValue(manip, out mod);
        }
    }

    public int Count
        => _manipulations.Count;

    public IReadOnlyCollection<MetaManipulation> Manipulations
        => _manipulations.Keys;

    public IEnumerator<KeyValuePair<MetaManipulation, IMod>> GetEnumerator()
        => _manipulations.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public MetaCache(MetaFileManager manager, ModCollection collection)
    {
        _manager    = manager;
        _collection = collection;
        if (!_manager.CharacterUtility.Ready)
            _manager.CharacterUtility.LoadingFinished += ApplyStoredManipulations;
    }

    public void SetFiles()
    {
        _eqpCache.SetFiles(_manager);
        _eqdpCache.SetFiles(_manager);
        _estCache.SetFiles(_manager);
        _gmpCache.SetFiles(_manager);
        _cmpCache.SetFiles(_manager);
        _imcCache.SetFiles(_collection, false);
    }

    public void Reset()
    {
        _eqpCache.Reset();
        _eqdpCache.Reset();
        _estCache.Reset();
        _gmpCache.Reset();
        _cmpCache.Reset();
        _imcCache.Reset(_collection);
        _manipulations.Clear();
    }

    public void Dispose()
    {
        _manager.CharacterUtility.LoadingFinished -= ApplyStoredManipulations;
        _eqpCache.Dispose();
        _eqdpCache.Dispose();
        _estCache.Dispose();
        _gmpCache.Dispose();
        _cmpCache.Dispose();
        _imcCache.Dispose();
        _manipulations.Clear();
    }

    ~MetaCache()
        => Dispose();

    public bool ApplyMod(MetaManipulation manip, IMod mod)
    {
        lock (_manipulations)
        {
            if (_manipulations.ContainsKey(manip))
                _manipulations.Remove(manip);

            _manipulations[manip] = mod;
        }

        if (!_manager.CharacterUtility.Ready)
            return true;

        // Imc manipulations do not require character utility,
        // but they do require the file space to be ready.
        return manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => _eqpCache.ApplyMod(_manager, manip.Eqp),
            MetaManipulation.Type.Eqdp    => _eqdpCache.ApplyMod(_manager, manip.Eqdp),
            MetaManipulation.Type.Est     => _estCache.ApplyMod(_manager, manip.Est),
            MetaManipulation.Type.Gmp     => _gmpCache.ApplyMod(_manager, manip.Gmp),
            MetaManipulation.Type.Rsp     => _cmpCache.ApplyMod(_manager, manip.Rsp),
            MetaManipulation.Type.Imc     => _imcCache.ApplyMod(_manager, _collection, manip.Imc),
            MetaManipulation.Type.Unknown => false,
            _                             => false,
        };
    }

    public bool RevertMod(MetaManipulation manip, [NotNullWhen(true)] out IMod? mod)
    {
        lock (_manipulations)
        {
            var ret = _manipulations.Remove(manip, out mod);
            if (!_manager.CharacterUtility.Ready)
                return ret;
        }

        // Imc manipulations do not require character utility,
        // but they do require the file space to be ready.
        return manip.ManipulationType switch
        {
            MetaManipulation.Type.Eqp     => _eqpCache.RevertMod(_manager, manip.Eqp),
            MetaManipulation.Type.Eqdp    => _eqdpCache.RevertMod(_manager, manip.Eqdp),
            MetaManipulation.Type.Est     => _estCache.RevertMod(_manager, manip.Est),
            MetaManipulation.Type.Gmp     => _gmpCache.RevertMod(_manager, manip.Gmp),
            MetaManipulation.Type.Rsp     => _cmpCache.RevertMod(_manager, manip.Rsp),
            MetaManipulation.Type.Imc     => _imcCache.RevertMod(_manager, _collection, manip.Imc),
            MetaManipulation.Type.Unknown => false,
            _                             => false,
        };
    }

    /// <summary> Set a single file. </summary>
    public void SetFile(MetaIndex metaIndex)
    {
        switch (metaIndex)
        {
            case MetaIndex.Eqp:
                _eqpCache.SetFiles(_manager);
                break;
            case MetaIndex.Gmp:
                _gmpCache.SetFiles(_manager);
                break;
            case MetaIndex.HumanCmp:
                _cmpCache.SetFiles(_manager);
                break;
            case MetaIndex.FaceEst:
            case MetaIndex.HairEst:
            case MetaIndex.HeadEst:
            case MetaIndex.BodyEst:
                _estCache.SetFile(_manager, metaIndex);
                break;
            default:
                _eqdpCache.SetFile(_manager, metaIndex);
                break;
        }
    }

    /// <summary> Set the currently relevant IMC files for the collection cache. </summary>
    public void SetImcFiles(bool fromFullCompute)
        => _imcCache.SetFiles(_collection, fromFullCompute);

    public MetaList.MetaReverter TemporarilySetEqpFile()
        => _eqpCache.TemporarilySetFiles(_manager);

    public MetaList.MetaReverter TemporarilySetEqdpFile(GenderRace genderRace, bool accessory)
        => _eqdpCache.TemporarilySetFiles(_manager, genderRace, accessory);

    public MetaList.MetaReverter TemporarilySetGmpFile()
        => _gmpCache.TemporarilySetFiles(_manager);

    public MetaList.MetaReverter TemporarilySetCmpFile()
        => _cmpCache.TemporarilySetFiles(_manager);

    public MetaList.MetaReverter TemporarilySetEstFile(EstManipulation.EstType type)
        => _estCache.TemporarilySetFiles(_manager, type);


    /// <summary> Try to obtain a manipulated IMC file. </summary>
    public bool GetImcFile(Utf8GamePath path, [NotNullWhen(true)] out Meta.Files.ImcFile? file)
        => _imcCache.GetImcFile(path, out file);

    /// <summary> Use this when CharacterUtility becomes ready. </summary>
    private void ApplyStoredManipulations()
    {
        if (!_manager.CharacterUtility.Ready)
            return;

        var loaded = 0;
        lock (_manipulations)
        {
            foreach (var manip in Manipulations)
            {
                loaded += manip.ManipulationType switch
                {
                    MetaManipulation.Type.Eqp     => _eqpCache.ApplyMod(_manager, manip.Eqp),
                    MetaManipulation.Type.Eqdp    => _eqdpCache.ApplyMod(_manager, manip.Eqdp),
                    MetaManipulation.Type.Est     => _estCache.ApplyMod(_manager, manip.Est),
                    MetaManipulation.Type.Gmp     => _gmpCache.ApplyMod(_manager, manip.Gmp),
                    MetaManipulation.Type.Rsp     => _cmpCache.ApplyMod(_manager, manip.Rsp),
                    MetaManipulation.Type.Imc     => _imcCache.ApplyMod(_manager, _collection, manip.Imc),
                    MetaManipulation.Type.Unknown => false,
                    _                             => false,
                }
                    ? 1
                    : 0;
            }
        }

        _manager.ApplyDefaultFiles(_collection);
        _manager.CharacterUtility.LoadingFinished -= ApplyStoredManipulations;
        Penumbra.Log.Debug($"{_collection.AnonymizedName}: Loaded {loaded} delayed meta manipulations.");
    }
}
