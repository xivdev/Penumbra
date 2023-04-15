using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using OtterGui;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Collections.Cache;

public struct MetaCache : IDisposable, IEnumerable<KeyValuePair<MetaManipulation, IMod>>
{
    private readonly CollectionCacheManager             _manager;
    private readonly ModCollection                      _collection;
    private readonly Dictionary<MetaManipulation, IMod> _manipulations = new();
    private          EqpCache                           _eqpCache      = new();
    private readonly EqdpCache                          _eqdpCache     = new();
    private          EstCache                           _estCache      = new();
    private          GmpCache                           _gmpCache      = new();
    private          CmpCache                           _cmpCache      = new();
    private readonly ImcCache                           _imcCache;

    public bool TryGetValue(MetaManipulation manip, [NotNullWhen(true)] out IMod? mod)
        => _manipulations.TryGetValue(manip, out mod);

    public int Count
        => _manipulations.Count;

    public IReadOnlyCollection<MetaManipulation> Manipulations
        => _manipulations.Keys;

    public IEnumerator<KeyValuePair<MetaManipulation, IMod>> GetEnumerator()
        => _manipulations.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public MetaCache(CollectionCacheManager manager, ModCollection collection)
    {
        _manager  = manager;
        _imcCache = new ImcCache(collection);
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
        _imcCache.SetFiles(_manager, _collection);
    }

    public void Reset()
    {
        _eqpCache.Reset(_manager);
        _eqdpCache.Reset(_manager);
        _estCache.Reset(_manager);
        _gmpCache.Reset(_manager);
        _cmpCache.Reset(_manager);
        _imcCache.Reset(_manager, _collection);
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

    public bool ApplyMod(MetaManipulation manip, IMod mod)
    {
        if (_manipulations.ContainsKey(manip))
            _manipulations.Remove(manip);

        _manipulations[manip] = mod;

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

    public bool RevertMod(MetaManipulation manip)
    {
        var ret = _manipulations.Remove(manip);
        if (!Penumbra.CharacterUtility.Ready)
            return ret;

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

    // Use this when CharacterUtility becomes ready.
    private void ApplyStoredManipulations()
    {
        if (!Penumbra.CharacterUtility.Ready)
            return;

        var loaded = 0;
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

        if (_manager.IsDefault(_collection))
        {
            SetFiles();
            _manager.ResidentResources.Reload();
        }

        _manager.CharacterUtility.LoadingFinished -= ApplyStoredManipulations;
        Penumbra.Log.Debug($"{_collection.AnonymizedName}: Loaded {loaded} delayed meta manipulations.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void SetFile(MetaBaseFile? file, MetaIndex metaIndex)
    {
        if (file == null)
            _manager.CharacterUtility.ResetResource(metaIndex);
        else
            _manager.CharacterUtility.SetResource(metaIndex, (IntPtr)file.Data, file.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe CharacterUtility.MetaList.MetaReverter TemporarilySetFile(MetaBaseFile? file, MetaIndex metaIndex)
        => file == null
            ? _manager.CharacterUtility.TemporarilyResetResource(metaIndex)
            : _manager.CharacterUtility.TemporarilySetResource(metaIndex, (IntPtr)file.Data, file.Length);
}