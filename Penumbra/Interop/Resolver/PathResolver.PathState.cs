using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.String;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    public class PathState : IDisposable
    {
        [Signature( Sigs.HumanVTable, ScanType = ScanType.StaticAddress )]
        public readonly IntPtr* HumanVTable = null!;

        [Signature( Sigs.WeaponVTable, ScanType = ScanType.StaticAddress )]
        private readonly IntPtr* _weaponVTable = null!;

        [Signature( Sigs.DemiHumanVTable, ScanType = ScanType.StaticAddress )]
        private readonly IntPtr* _demiHumanVTable = null!;

        [Signature( Sigs.MonsterVTable, ScanType = ScanType.StaticAddress )]
        private readonly IntPtr* _monsterVTable = null!;

        private readonly ResolverHooks _human;
        private readonly ResolverHooks _weapon;
        private readonly ResolverHooks _demiHuman;
        private readonly ResolverHooks _monster;

        // This map links files to their corresponding collection, if it is non-default.
        private readonly ConcurrentDictionary< ByteString, ResolveData > _pathCollections   = new();

        public PathState( PathResolver parent )
        {
            SignatureHelper.Initialise( this );
            _human     = new ResolverHooks( parent, HumanVTable, ResolverHooks.Type.Human );
            _weapon    = new ResolverHooks( parent, _weaponVTable, ResolverHooks.Type.Weapon );
            _demiHuman = new ResolverHooks( parent, _demiHumanVTable, ResolverHooks.Type.Other );
            _monster   = new ResolverHooks( parent, _monsterVTable, ResolverHooks.Type.Other );
        }

        public void Enable()
        {
            _human.Enable();
            _weapon.Enable();
            _demiHuman.Enable();
            _monster.Enable();
        }

        public void Disable()
        {
            _human.Disable();
            _weapon.Disable();
            _demiHuman.Disable();
            _monster.Disable();
        }

        public void Dispose()
        {
            _human.Dispose();
            _weapon.Dispose();
            _demiHuman.Dispose();
            _monster.Dispose();
        }

        public int Count
            => _pathCollections.Count;

        public IEnumerable< KeyValuePair< ByteString, ResolveData > > Paths
            => _pathCollections;

        public bool TryGetValue( ByteString path, out ResolveData collection )
            => _pathCollections.TryGetValue( path, out collection );

        public bool Consume( ByteString path, out ResolveData collection )
            => _pathCollections.TryRemove( path, out collection );

        // Just add or remove the resolved path.
        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
        public IntPtr ResolvePath( IntPtr gameObject, ModCollection collection, IntPtr path )
        {
            if( path == IntPtr.Zero )
            {
                return path;
            }

            var gamePath = new ByteString( ( byte* )path );
            SetCollection( gameObject, gamePath, collection );
            return path;
        }

        // Special handling for paths so that we do not store non-owned temporary strings in the dictionary.
        public void SetCollection( IntPtr gameObject, ByteString path, ModCollection collection )
        {
            if( _pathCollections.ContainsKey( path ) || path.IsOwned )
            {
                _pathCollections[ path ] = collection.ToResolveData( gameObject );
            }
            else
            {
                _pathCollections[ path.Clone() ] = collection.ToResolveData( gameObject );
            }
        }
    }
}