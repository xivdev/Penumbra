using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Api;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using Penumbra.GameData.Enums;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    public class DrawObjectState
    {
        public static event CreatingCharacterBaseDelegate? CreatingCharacterBase;
        public static event CreatedCharacterBaseDelegate? CreatedCharacterBase;

        public IEnumerable< KeyValuePair< IntPtr, (ResolveData, int) > > DrawObjects
            => _drawObjectToObject;

        public int Count
            => _drawObjectToObject.Count;

        public bool TryGetValue( IntPtr drawObject, out (ResolveData, int) value, out GameObject* gameObject )
        {
            gameObject = null;
            if( !_drawObjectToObject.TryGetValue( drawObject, out value ) )
            {
                return false;
            }

            var gameObjectIdx = value.Item2;
            return VerifyEntry( drawObject, gameObjectIdx, out gameObject );
        }


        // Set and update a parent object if it exists and a last game object is set.
        public ResolveData CheckParentDrawObject( IntPtr drawObject, IntPtr parentObject )
        {
            if( parentObject == IntPtr.Zero && LastGameObject != null )
            {
                var collection = IdentifyCollection( LastGameObject, true );
                _drawObjectToObject[ drawObject ] = ( collection, LastGameObject->ObjectIndex );
                return collection;
            }

            return ResolveData.Invalid;
        }


        public bool HandleDecalFile( ResourceType type, Utf8GamePath gamePath, out ResolveData resolveData )
        {
            if( type == ResourceType.Tex
            && LastCreatedCollection.Valid
            && gamePath.Path.Substring( "chara/common/texture/".Length ).StartsWith( "decal"u8 ) )
            {
                resolveData = LastCreatedCollection;
                return true;
            }

            resolveData = ResolveData.Invalid;
            return false;
        }


        public ResolveData LastCreatedCollection
            => _lastCreatedCollection;

        public GameObject* LastGameObject { get; private set; }

        public DrawObjectState()
        {
            SignatureHelper.Initialise( this );
        }

        public void Enable()
        {
            _characterBaseCreateHook.Enable();
            _characterBaseDestructorHook.Enable();
            _enableDrawHook.Enable();
            _weaponReloadHook.Enable();
            InitializeDrawObjects();
            Penumbra.CollectionManager.CollectionChanged += CheckCollections;
            Penumbra.TempMods.CollectionChanged          += CheckCollections;
        }

        public void Disable()
        {
            _characterBaseCreateHook.Disable();
            _characterBaseDestructorHook.Disable();
            _enableDrawHook.Disable();
            _weaponReloadHook.Disable();
            Penumbra.CollectionManager.CollectionChanged -= CheckCollections;
            Penumbra.TempMods.CollectionChanged          -= CheckCollections;
        }

        public void Dispose()
        {
            Disable();
            _characterBaseCreateHook.Dispose();
            _characterBaseDestructorHook.Dispose();
            _enableDrawHook.Dispose();
            _weaponReloadHook.Dispose();
        }

        // Check that a linked DrawObject still corresponds to the correct actor and that it still exists, otherwise remove it.
        private bool VerifyEntry( IntPtr drawObject, int gameObjectIdx, out GameObject* gameObject )
        {
            gameObject = ( GameObject* )Dalamud.Objects.GetObjectAddress( gameObjectIdx );
            var draw = ( DrawObject* )drawObject;
            if( gameObject != null
            && ( gameObject->DrawObject == draw || draw != null && gameObject->DrawObject == draw->Object.ParentObject ) )
            {
                return true;
            }

            gameObject = null;
            _drawObjectToObject.Remove( drawObject );
            return false;
        }

        // This map links DrawObjects directly to Actors (by ObjectTable index) and their collections.
        // It contains any DrawObjects that correspond to a human actor, even those without specific collections.
        private readonly Dictionary< IntPtr, (ResolveData, int) > _drawObjectToObject    = new();
        private          ResolveData                              _lastCreatedCollection = ResolveData.Invalid;

        // Keep track of created DrawObjects that are CharacterBase,
        // and use the last game object that called EnableDraw to link them.
        private delegate IntPtr CharacterBaseCreateDelegate( uint a, IntPtr b, IntPtr c, byte d );

        [Signature( "E8 ?? ?? ?? ?? 48 85 C0 74 21 C7 40", DetourName = nameof( CharacterBaseCreateDetour ) )]
        private readonly Hook< CharacterBaseCreateDelegate > _characterBaseCreateHook = null!;

        private IntPtr CharacterBaseCreateDetour( uint a, IntPtr b, IntPtr c, byte d )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.CharacterBaseCreate );

            var meta = DisposableContainer.Empty;
            if( LastGameObject != null )
            {
                _lastCreatedCollection = IdentifyCollection( LastGameObject, false );
                // Change the transparent or 1.0 Decal if necessary.
                var decal = new CharacterUtility.DecalReverter( _lastCreatedCollection.ModCollection, UsesDecal( a, c ) );
                // Change the rsp parameters.
                meta = new DisposableContainer( _lastCreatedCollection.ModCollection.TemporarilySetCmpFile(), decal );
                try
                {
                    var modelPtr = &a;
                    CreatingCharacterBase?.Invoke( ( IntPtr )LastGameObject, _lastCreatedCollection!.ModCollection.Name, ( IntPtr )modelPtr, b, c );
                }
                catch( Exception e )
                {
                    Penumbra.Log.Error( $"Unknown Error during CreatingCharacterBase:\n{e}" );
                }
            }

            var ret = _characterBaseCreateHook.Original( a, b, c, d );
            try
            {
                if( LastGameObject != null && ret != IntPtr.Zero )
                {
                    _drawObjectToObject[ ret ] = ( _lastCreatedCollection!, LastGameObject->ObjectIndex );
                    CreatedCharacterBase?.Invoke( ( IntPtr )LastGameObject, _lastCreatedCollection!.ModCollection.Name, ret );
                }
            }
            finally
            {
                meta.Dispose();
            }

            return ret;
        }

        // Check the customize array for the FaceCustomization byte and the last bit of that.
        // Also check for humans.
        public static bool UsesDecal( uint modelId, IntPtr customizeData )
            => modelId == 0 && ( ( byte* )customizeData )[ 12 ] > 0x7F;


        // Remove DrawObjects from the list when they are destroyed.
        private delegate void CharacterBaseDestructorDelegate( IntPtr drawBase );

        [Signature( "E8 ?? ?? ?? ?? 40 F6 C7 01 74 3A 40 F6 C7 04 75 27 48 85 DB 74 2F 48 8B 05 ?? ?? ?? ?? 48 8B D3 48 8B 48 30",
            DetourName = nameof( CharacterBaseDestructorDetour ) )]
        private readonly Hook< CharacterBaseDestructorDelegate > _characterBaseDestructorHook = null!;

        private void CharacterBaseDestructorDetour( IntPtr drawBase )
        {
            _drawObjectToObject.Remove( drawBase );
            _characterBaseDestructorHook!.Original.Invoke( drawBase );
        }


        // EnableDraw is what creates DrawObjects for gameObjects,
        // so we always keep track of the current GameObject to be able to link it to the DrawObject.
        private delegate void EnableDrawDelegate( IntPtr gameObject, IntPtr b, IntPtr c, IntPtr d );

        [Signature( "E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9 74 33 45 33 C0", DetourName = nameof( EnableDrawDetour ) )]
        private readonly Hook< EnableDrawDelegate > _enableDrawHook = null!;

        private void EnableDrawDetour( IntPtr gameObject, IntPtr b, IntPtr c, IntPtr d )
        {
            var oldObject = LastGameObject;
            LastGameObject = ( GameObject* )gameObject;
            _enableDrawHook!.Original.Invoke( gameObject, b, c, d );
            LastGameObject = oldObject;
        }

        // Not fully understood. The game object the weapon is loaded for is seemingly found at a1 + 8,
        // so we use that.
        private delegate void WeaponReloadFunc( IntPtr a1, uint a2, IntPtr a3, byte a4, byte a5, byte a6, byte a7 );

        [Signature( "E8 ?? ?? ?? ?? 44 8B 9F", DetourName = nameof( WeaponReloadDetour ) )]
        private readonly Hook< WeaponReloadFunc > _weaponReloadHook = null!;

        public void WeaponReloadDetour( IntPtr a1, uint a2, IntPtr a3, byte a4, byte a5, byte a6, byte a7 )
        {
            var oldGame = LastGameObject;
            LastGameObject = *( GameObject** )( a1 + 8 );
            _weaponReloadHook!.Original( a1, a2, a3, a4, a5, a6, a7 );
            LastGameObject = oldGame;
        }

        // Update collections linked to Game/DrawObjects due to a change in collection configuration.
        private void CheckCollections( CollectionType type, ModCollection? _1, ModCollection? _2, string _3 )
        {
            if( type is CollectionType.Inactive or CollectionType.Current or CollectionType.Interface )
            {
                return;
            }

            foreach( var (key, (_, idx)) in _drawObjectToObject.ToArray() )
            {
                if( !VerifyEntry( key, idx, out var obj ) )
                {
                    _drawObjectToObject.Remove( key );
                }

                var newCollection = IdentifyCollection( obj, false );
                _drawObjectToObject[ key ] = ( newCollection, idx );
            }
        }

        // Find all current DrawObjects used in the GameObject table.
        // We do not iterate the Dalamud table because it does not work when not logged in.
        private void InitializeDrawObjects()
        {
            for( var i = 0; i < Dalamud.Objects.Length; ++i )
            {
                var ptr = ( GameObject* )Dalamud.Objects.GetObjectAddress( i );
                if( ptr != null && ptr->IsCharacter() && ptr->DrawObject != null )
                {
                    _drawObjectToObject[ ( IntPtr )ptr->DrawObject ] = ( IdentifyCollection( ptr, false ), ptr->ObjectIndex );
                }
            }
        }
    }
}