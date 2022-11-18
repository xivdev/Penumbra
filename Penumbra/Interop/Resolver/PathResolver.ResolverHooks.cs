using System;
using System.Runtime.CompilerServices;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Resolver;

public partial class PathResolver
{
    public unsafe class ResolverHooks : IDisposable
    {
        public enum Type
        {
            Human,
            Weapon,
            Other,
        }

        private delegate IntPtr GeneralResolveDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 );
        private delegate IntPtr MPapResolveDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 );
        private delegate IntPtr MaterialResolveDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 );
        private delegate IntPtr EidResolveDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3 );

        private readonly Hook< GeneralResolveDelegate >  _resolveDecalPathHook;
        private readonly Hook< EidResolveDelegate >      _resolveEidPathHook;
        private readonly Hook< GeneralResolveDelegate >  _resolveImcPathHook;
        private readonly Hook< MPapResolveDelegate >     _resolveMPapPathHook;
        private readonly Hook< GeneralResolveDelegate >  _resolveMdlPathHook;
        private readonly Hook< MaterialResolveDelegate > _resolveMtrlPathHook;
        private readonly Hook< MaterialResolveDelegate > _resolvePapPathHook;
        private readonly Hook< GeneralResolveDelegate >  _resolvePhybPathHook;
        private readonly Hook< GeneralResolveDelegate >  _resolveSklbPathHook;
        private readonly Hook< GeneralResolveDelegate >  _resolveSkpPathHook;
        private readonly Hook< EidResolveDelegate >      _resolveTmbPathHook;
        private readonly Hook< MaterialResolveDelegate > _resolveVfxPathHook;

        private readonly PathResolver _parent;

        public ResolverHooks( PathResolver parent, IntPtr* vTable, Type type )
        {
            _parent               = parent;
            _resolveDecalPathHook = Create< GeneralResolveDelegate >( vTable[ 83 ], type, ResolveDecalWeapon, ResolveDecal );
            _resolveEidPathHook   = Create< EidResolveDelegate >( vTable[ 85 ], type, ResolveEidWeapon, ResolveEid );
            _resolveImcPathHook   = Create< GeneralResolveDelegate >( vTable[ 81 ], type, ResolveImcWeapon, ResolveImc );
            _resolveMPapPathHook  = Create< MPapResolveDelegate >( vTable[ 79 ], type, ResolveMPapWeapon, ResolveMPap );
            _resolveMdlPathHook   = Create< GeneralResolveDelegate >( vTable[ 73 ], type, ResolveMdlWeapon, ResolveMdl, ResolveMdlHuman );
            _resolveMtrlPathHook  = Create< MaterialResolveDelegate >( vTable[ 82 ], type, ResolveMtrlWeapon, ResolveMtrl );
            _resolvePapPathHook   = Create< MaterialResolveDelegate >( vTable[ 76 ], type, ResolvePapWeapon, ResolvePap, ResolvePapHuman );
            _resolvePhybPathHook  = Create< GeneralResolveDelegate >( vTable[ 75 ], type, ResolvePhybWeapon, ResolvePhyb, ResolvePhybHuman );
            _resolveSklbPathHook  = Create< GeneralResolveDelegate >( vTable[ 72 ], type, ResolveSklbWeapon, ResolveSklb, ResolveSklbHuman );
            _resolveSkpPathHook   = Create< GeneralResolveDelegate >( vTable[ 74 ], type, ResolveSkpWeapon, ResolveSkp, ResolveSkpHuman );
            _resolveTmbPathHook   = Create< EidResolveDelegate >( vTable[ 77 ], type, ResolveTmbWeapon, ResolveTmb );
            _resolveVfxPathHook   = Create< MaterialResolveDelegate >( vTable[ 84 ], type, ResolveVfxWeapon, ResolveVfx );
        }

        public void Enable()
        {
            _resolveDecalPathHook.Enable();
            _resolveEidPathHook.Enable();
            _resolveImcPathHook.Enable();
            _resolveMPapPathHook.Enable();
            _resolveMdlPathHook.Enable();
            _resolveMtrlPathHook.Enable();
            _resolvePapPathHook.Enable();
            _resolvePhybPathHook.Enable();
            _resolveSklbPathHook.Enable();
            _resolveSkpPathHook.Enable();
            _resolveTmbPathHook.Enable();
            _resolveVfxPathHook.Enable();
        }

        public void Disable()
        {
            _resolveDecalPathHook.Disable();
            _resolveEidPathHook.Disable();
            _resolveImcPathHook.Disable();
            _resolveMPapPathHook.Disable();
            _resolveMdlPathHook.Disable();
            _resolveMtrlPathHook.Disable();
            _resolvePapPathHook.Disable();
            _resolvePhybPathHook.Disable();
            _resolveSklbPathHook.Disable();
            _resolveSkpPathHook.Disable();
            _resolveTmbPathHook.Disable();
            _resolveVfxPathHook.Disable();
        }

        public void Dispose()
        {
            _resolveDecalPathHook.Dispose();
            _resolveEidPathHook.Dispose();
            _resolveImcPathHook.Dispose();
            _resolveMPapPathHook.Dispose();
            _resolveMdlPathHook.Dispose();
            _resolveMtrlPathHook.Dispose();
            _resolvePapPathHook.Dispose();
            _resolvePhybPathHook.Dispose();
            _resolveSklbPathHook.Dispose();
            _resolveSkpPathHook.Dispose();
            _resolveTmbPathHook.Dispose();
            _resolveVfxPathHook.Dispose();
        }

        private IntPtr ResolveDecal( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolvePath( drawObject, _resolveDecalPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveEid( IntPtr drawObject, IntPtr path, IntPtr unk3 )
            => ResolvePath( drawObject, _resolveEidPathHook.Original( drawObject, path, unk3 ) );

        private IntPtr ResolveImc( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolvePath( drawObject, _resolveImcPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveMPap( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 )
            => ResolvePath( drawObject, _resolveMPapPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );

        private IntPtr ResolveMdl( IntPtr drawObject, IntPtr path, IntPtr unk3, uint modelType )
            => ResolvePath( drawObject, _resolveMdlPathHook.Original( drawObject, path, unk3, modelType ) );

        private IntPtr ResolveMtrl( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
            => ResolvePath( drawObject, _resolveMtrlPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );

        private IntPtr ResolvePap( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
            => ResolvePath( drawObject, _resolvePapPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );

        private IntPtr ResolvePhyb( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolvePath( drawObject, _resolvePhybPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveSklb( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolvePath( drawObject, _resolveSklbPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveSkp( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolvePath( drawObject, _resolveSkpPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveTmb( IntPtr drawObject, IntPtr path, IntPtr unk3 )
            => ResolvePath( drawObject, _resolveTmbPathHook.Original( drawObject, path, unk3 ) );

        private IntPtr ResolveVfx( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
            => ResolvePath( drawObject, _resolveVfxPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );


        private IntPtr ResolveMdlHuman( IntPtr drawObject, IntPtr path, IntPtr unk3, uint modelType )
        {
            DisposableContainer Get()
            {
                if( modelType > 9 )
                {
                    return DisposableContainer.Empty;
                }

                var data = GetResolveData( drawObject );
                return MetaState.ResolveEqdpData(data.ModCollection, MetaState.GetHumanGenderRace( drawObject ), modelType < 5, modelType > 4);
            }

            using var eqdp = Get();
            return ResolvePath( drawObject, _resolveMdlPathHook.Original( drawObject, path, unk3, modelType ) );
        }

        private IntPtr ResolvePapHuman( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        {
            using var est = GetEstChanges( drawObject );
            return ResolvePath( drawObject, _resolvePapPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );
        }

        private IntPtr ResolvePhybHuman( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        {
            using var est = GetEstChanges( drawObject );
            return ResolvePath( drawObject, _resolvePhybPathHook.Original( drawObject, path, unk3, unk4 ) );
        }

        private IntPtr ResolveSklbHuman( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        {
            using var est = GetEstChanges( drawObject );
            return ResolvePath( drawObject, _resolveSklbPathHook.Original( drawObject, path, unk3, unk4 ) );
        }

        private IntPtr ResolveSkpHuman( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        {
            using var est = GetEstChanges( drawObject );
            return ResolvePath( drawObject, _resolveSkpPathHook.Original( drawObject, path, unk3, unk4 ) );
        }

        private static DisposableContainer GetEstChanges( IntPtr drawObject )
        {
            var data = GetResolveData( drawObject );
            return new DisposableContainer( data.ModCollection.TemporarilySetEstFile( EstManipulation.EstType.Face ),
                data.ModCollection.TemporarilySetEstFile( EstManipulation.EstType.Body ),
                data.ModCollection.TemporarilySetEstFile( EstManipulation.EstType.Hair ),
                data.ModCollection.TemporarilySetEstFile( EstManipulation.EstType.Head ) );
        }

        private IntPtr ResolveDecalWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolveWeaponPath( drawObject, _resolveDecalPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveEidWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3 )
            => ResolveWeaponPath( drawObject, _resolveEidPathHook.Original( drawObject, path, unk3 ) );

        private IntPtr ResolveImcWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolveWeaponPath( drawObject, _resolveImcPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveMPapWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 )
            => ResolveWeaponPath( drawObject, _resolveMPapPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );

        private IntPtr ResolveMdlWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint modelType )
            => ResolveWeaponPath( drawObject, _resolveMdlPathHook.Original( drawObject, path, unk3, modelType ) );

        private IntPtr ResolveMtrlWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
            => ResolveWeaponPath( drawObject, _resolveMtrlPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );

        private IntPtr ResolvePapWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
            => ResolveWeaponPath( drawObject, _resolvePapPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );

        private IntPtr ResolvePhybWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolveWeaponPath( drawObject, _resolvePhybPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveSklbWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolveWeaponPath( drawObject, _resolveSklbPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveSkpWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
            => ResolveWeaponPath( drawObject, _resolveSkpPathHook.Original( drawObject, path, unk3, unk4 ) );

        private IntPtr ResolveTmbWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3 )
            => ResolveWeaponPath( drawObject, _resolveTmbPathHook.Original( drawObject, path, unk3 ) );

        private IntPtr ResolveVfxWeapon( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
            => ResolveWeaponPath( drawObject, _resolveVfxPathHook.Original( drawObject, path, unk3, unk4, unk5 ) );


        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
        private static Hook< T > Create< T >( IntPtr address, Type type, T weapon, T other, T human ) where T : Delegate
        {
            var del = type switch
            {
                Type.Human  => human,
                Type.Weapon => weapon,
                _           => other,
            };
            return Hook< T >.FromAddress( address, del );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
        private static Hook< T > Create< T >( IntPtr address, Type type, T weapon, T other ) where T : Delegate
            => Create( address, type, weapon, other, other );


        // Implementation
        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
        private IntPtr ResolvePath( IntPtr drawObject, IntPtr path )
            => _parent._paths.ResolvePath( ( IntPtr? )FindParent( drawObject, out _ ) ?? IntPtr.Zero,
                FindParent( drawObject, out var collection ) == null
                    ? Penumbra.CollectionManager.Default
                    : collection.ModCollection, path );

        // Weapons have the characters DrawObject as a parent,
        // but that may not be set yet when creating a new object, so we have to do the same detour
        // as for Human DrawObjects that are just being created.
        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
        private IntPtr ResolveWeaponPath( IntPtr drawObject, IntPtr path )
        {
            var parent = FindParent( drawObject, out var collection );
            if( parent != null )
            {
                return _parent._paths.ResolvePath( ( IntPtr )parent, collection.ModCollection, path );
            }

            var parentObject     = ( IntPtr )( ( DrawObject* )drawObject )->Object.ParentObject;
            var parentCollection = DrawObjects.CheckParentDrawObject( drawObject, parentObject );
            if( parentCollection.Valid )
            {
                return _parent._paths.ResolvePath( ( IntPtr )FindParent( parentObject, out _ ), parentCollection.ModCollection, path );
            }

            parent = FindParent( parentObject, out collection );
            return _parent._paths.ResolvePath( ( IntPtr? )parent ?? IntPtr.Zero, parent == null
                ? Penumbra.CollectionManager.Default
                : collection.ModCollection, path );
        }
    }
}