using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Utility;

namespace LuminPack.Option
{
    /// <summary>Provides lumin pack writer optional state functionality.</summary>
    public sealed class LuminPackWriterOptionalState : IDisposable
    {
        internal static readonly LuminPackWriterOptionalState NullOption = new LuminPackWriterOptionalState(true);

        private const int SmallReferences = 8;
        private uint _nextId;
        // The first objects of an operation are found by a reference scan, without hashing; their ids are their indices.
        private readonly ReferenceSlot[] _small = new ReferenceSlot[SmallReferences];
        // Whether the operation outgrew the scan and every identity, including the first ones, is in the identity table.
        private bool _hashed;
        private LuminIdentityMap? _objectToRef;

        /// <summary>Gets whether this operation holds any tracked objects.</summary>
        internal bool HasOperationState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _nextId != 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackWriterOptionalState</c>.</summary>
        public LuminPackWriterOptionalState()
        {
            _nextId = 0;

        }

        /// <summary>Initializes a new instance of <c>LuminPackWriterOptionalState</c>.</summary>
        /// <param name="_">The  used by this operation.</param>
        private LuminPackWriterOptionalState(bool _)
        {
            _nextId = 0;

        }

        /// <summary>Whether a generated convenience operation currently leases this state from its thread's cache.</summary>
        private bool _leased;

        /// <summary>Leases the calling thread's cached state, or installs a new cached state when it is absent or active.</summary>
        /// <param name="cache">The calling thread's idle-state slot owned by the generated serializer.</param>
        /// <returns>A reset state owned by the caller until <see cref="Release"/>.</returns>
        /// <remarks>The cached instance stays in its slot while leased, so leasing and releasing store no reference. A nested
        /// or concurrent operation that finds the lease receives an independent state, which then becomes the cached one.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LuminPackWriterOptionalState Lease(ref LuminPackWriterOptionalState? cache)
        {
            var state = cache;
            // Acquire: a release on another thread published its reset before clearing the flag.
            if (state is not null && !Volatile.Read(ref state._leased))
            {
                state._leased = true;
                return state;
            }

            return LeaseSlow(ref cache);
        }

        /// <summary>Creates a leased state and makes it the calling thread's cached state.</summary>
        /// <param name="cache">The calling thread's idle-state slot.</param>
        /// <returns>The new leased state.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static LuminPackWriterOptionalState LeaseSlow(ref LuminPackWriterOptionalState? cache)
        {
            var state = new LuminPackWriterOptionalState();
            state._leased = true;
            cache = state;
            return state;
        }

        /// <summary>Clears the operation's references and ends the lease obtained from <see cref="Lease"/>.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            ResetOperationState();
            Volatile.Write(ref _leased, false);
        }

        /// <summary>Clears state retained by the previous operation so it can be reused.</summary>
        public void Reset()
        {
            ResetOperationState();

        }

        /// <summary>Clears transient writer reference tracking for reuse by the next operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetOperationState()
        {
            // No reference can be retained before the first id is assigned.
            if (_nextId == 0)
                return;
            ResetReferences();
        }

        /// <summary>Clears a nonempty reference table without expanding its cleanup into each entry point.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ResetReferences()
        {
            if (_hashed)
            {
                _objectToRef!.Clear();
                _hashed = false;
            }
            var small = _small;
            for (int i = 0; i < small.Length && i < (int)_nextId; i++)
                small[i].Value = null;
            _nextId = 0;
        }

        /// <summary>Obtains the or add reference for the supplied input.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public (bool existsReference, uint id) GetOrAddReference(object value)
        {
            int reference = TrackReference(value);
            return reference < 0 ? (true, (uint)~reference) : (false, (uint)reference);
        }

        /// <summary>Registers an object for this operation or finds its earlier identifier.</summary>
        /// <param name="value">The object about to be written.</param>
        /// <returns>The new identifier, or the bitwise complement of the identifier assigned earlier.</returns>
        /// <remarks>One probe of the identity table answers both cases, and the single integer result stays in a register.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int TrackReference(object value)
        {
            if (!_hashed)
            {
                var small = _small;
                int count = (int)_nextId;
                for (int i = 0; i < small.Length && i < count; i++)
                    if (ReferenceEquals(small[i].Value, value))
                        return ~i;
                if ((uint)count < (uint)small.Length)
                {
                    small[count].Value = value;
                    _nextId = (uint)count + 1;
                    return count;
                }
                HashSmallReferences();
            }
            ref var id = ref _objectToRef!.GetValueRefOrAddDefault(value, out var exists);
            if (exists)
                return ~(int)id;
            if (_nextId > int.MaxValue)
                LuminPackExceptionHelper.ThrowMessage("Too many tracked references in one operation.");
            id = _nextId++;
            return (int)id;
        }

        /// <summary>Moves the scanned first identities into the identity table once an operation outgrows the scan.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void HashSmallReferences()
        {
            var map = _objectToRef ??= new LuminIdentityMap();
            var small = _small;
            for (int i = 0; i < small.Length; i++)
                map.TryAdd(small[i].Value!, (uint)i);
            _hashed = true;
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        void IDisposable.Dispose()
        {

        }
    }
}
