using System;
using System.Collections.Generic;

namespace Penumbra.Interop;

public unsafe partial class CharacterUtility
{
    public class List : IDisposable
    {
        private readonly LinkedList< MetaReverter >     _entries = new();
        public readonly  InternalIndex                  Index;
        public readonly  Structs.CharacterUtility.Index GlobalIndex;

        public IReadOnlyCollection< MetaReverter > Entries
            => _entries;

        private IntPtr _defaultResourceData = IntPtr.Zero;
        private int    _defaultResourceSize = 0;
        public bool Ready { get; private set; } = false;

        public List( InternalIndex index )
        {
            Index       = index;
            GlobalIndex = RelevantIndices[ index.Value ];
        }

        public void SetDefaultResource( IntPtr data, int size )
        {
            if( !Ready )
            {
                _defaultResourceData = data;
                _defaultResourceSize = size;
                Ready                = _defaultResourceData != IntPtr.Zero && size != 0;
                if( _entries.Count > 0 )
                {
                    var first = _entries.First!.Value;
                    SetResource( first.Data, first.Length );
                }
            }
        }

        public (IntPtr Address, int Size) DefaultResource
            => ( _defaultResourceData, _defaultResourceSize );

        public MetaReverter TemporarilySetResource( IntPtr data, int length )
        {
            Penumbra.Log.Verbose( $"Temporarily set resource {GlobalIndex} to 0x{( ulong )data:X} ({length} bytes)." );
            var reverter = new MetaReverter( this, data, length );
            _entries.AddFirst( reverter );
            SetResourceInternal( data, length );
            return reverter;
        }

        public MetaReverter TemporarilyResetResource()
        {
            Penumbra.Log.Verbose(
                $"Temporarily reset resource {GlobalIndex} to default at 0x{_defaultResourceData:X} ({_defaultResourceSize} bytes)." );
            var reverter = new MetaReverter( this );
            _entries.AddFirst( reverter );
            ResetResourceInternal();
            return reverter;
        }

        public void SetResource( IntPtr data, int length )
        {
            Penumbra.Log.Verbose( $"Set resource {GlobalIndex} to 0x{( ulong )data:X} ({length} bytes)." );
            SetResourceInternal( data, length );
        }

        public void ResetResource()
        {
            Penumbra.Log.Verbose( $"Reset resource {GlobalIndex} to default at 0x{_defaultResourceData:X} ({_defaultResourceSize} bytes)." );
            ResetResourceInternal();
        }


        // Set the currently stored data of this resource to new values.
        private void SetResourceInternal( IntPtr data, int length )
        {
            if( Ready )
            {
                var resource = Penumbra.CharacterUtility.Address->Resource( GlobalIndex );
                resource->SetData( data, length );
            }
        }

        // Reset the currently stored data of this resource to its default values.
        private void ResetResourceInternal()
            => SetResourceInternal( _defaultResourceData, _defaultResourceSize );

        private void SetResourceToDefaultCollection()
            => Penumbra.CollectionManager.Default.SetMetaFile( GlobalIndex );

        public void Dispose()
        {
            if( _entries.Count > 0 )
            {
                foreach( var entry in _entries )
                {
                    entry.Disposed = true;
                }

                _entries.Clear();
            }

            ResetResourceInternal();
        }

        public sealed class MetaReverter : IDisposable
        {
            public readonly List   List;
            public readonly IntPtr Data;
            public readonly int    Length;
            public readonly bool   Resetter;
            public          bool   Disposed;

            public MetaReverter( List list, IntPtr data, int length )
            {
                List   = list;
                Data   = data;
                Length = length;
            }

            public MetaReverter( List list )
            {
                List     = list;
                Data     = IntPtr.Zero;
                Length   = 0;
                Resetter = true;
            }

            public void Dispose()
            {
                if( !Disposed )
                {
                    var list       = List._entries;
                    var wasCurrent = ReferenceEquals( this, list.First?.Value );
                    list.Remove( this );
                    if( !wasCurrent )
                    {
                        return;
                    }

                    if( list.Count == 0 )
                    {
                        List.SetResourceToDefaultCollection();
                    }
                    else
                    {
                        var next = list.First!.Value;
                        if( next.Resetter )
                        {
                            List.ResetResourceInternal();
                        }
                        else
                        {
                            List.SetResourceInternal( next.Data, next.Length );
                        }
                    }

                    Disposed = true;
                }
            }
        }
    }
}