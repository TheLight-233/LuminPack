using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Utility;

namespace LuminPack.Option
{
    /// <summary>Provides lumin pack reader optional state pool functionality.</summary>
    public static class LuminPackReaderOptionalStatePool
    {
        private static readonly ConcurrentQueue<LuminPackReaderOptionalState> _queue = new();

        /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
        /// <returns>The result of the operation.</returns>
        public static LuminPackReaderOptionalState Rent()
        {
            if (!_queue.TryDequeue(out var state))
            {
                state = new LuminPackReaderOptionalState();
            }

            return state;
        }

        /// <summary>Returns an instance to the pool for reuse.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        internal static void Return(LuminPackReaderOptionalState state)
        {
            state.Reset();
            _queue.Enqueue(state);
        }
    }

    /// <summary>Provides lumin pack reader optional state functionality.</summary>
    public sealed class LuminPackReaderOptionalState : IDisposable
    {
        internal static readonly LuminPackReaderOptionalState NullOption = new LuminPackReaderOptionalState(true);

        private readonly LuminCircleReferenceMap<uint, object> refToObject;
        private LuminIdentityMap? reusedObjectToRef;
        private bool reuseIndexInitialized;
        // Writers assign identifiers 0, 1, 2, ... in record order, so a read stores them in an array. The map holds only
        // identifiers that arrive out of that order.
        private ReferenceSlot[] _sequential = Array.Empty<ReferenceSlot>();
        private int _sequentialCount;
        // Member tables of full version records read through the evolved path, for later short records of the same operation.
        // The tables are copied into one arena that this state keeps, so a warmed state caches them without allocating.
        private Dictionary<ulong, (int Start, int Length)>? _versionTables;
        private byte[] _versionTableBytes = Array.Empty<byte>();
        private int _versionTableUsed;

        /// <summary>Retains UTF-8 JSON escape decoding storage in the exclusively owned reader context.</summary>
        internal byte[]? JsonUtf8StringScratch;
        /// <summary>Retains UTF-16 JSON escape decoding storage in the exclusively owned reader context.</summary>
        internal char[]? JsonUtf16StringScratch;

        /// <summary>Initializes a new instance of <c>LuminPackReaderOptionalState</c>.</summary>
        public LuminPackReaderOptionalState()
        {
            refToObject = new LuminCircleReferenceMap<uint, object>();

        }

        /// <summary>Initializes a new instance of <c>LuminPackReaderOptionalState</c>.</summary>
        /// <param name="_">The  used by this operation.</param>
        private LuminPackReaderOptionalState(bool _)
        {
            refToObject = new LuminCircleReferenceMap<uint, object>();

        }

        /// <summary>Whether a generated convenience operation currently leases this state from its thread's cache.</summary>
        private bool _leased;

        /// <summary>Leases the calling thread's cached state, or installs a new cached state when it is absent or active.</summary>
        /// <param name="cache">The calling thread's idle-state slot owned by the generated serializer.</param>
        /// <returns>A reset state owned by the caller until <see cref="Release"/>.</returns>
        /// <remarks>The cached instance stays in its slot while leased, so leasing and releasing store no reference. A nested
        /// or concurrent operation that finds the lease receives an independent state, which then becomes the cached one.</remarks>
        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LuminPackReaderOptionalState Lease(ref LuminPackReaderOptionalState? cache)
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
        private static LuminPackReaderOptionalState LeaseSlow(ref LuminPackReaderOptionalState? cache)
        {
            var state = new LuminPackReaderOptionalState();
            state._leased = true;
            cache = state;
            return state;
        }

        /// <summary>Clears the operation's references and ends the lease obtained from <see cref="Lease"/>.</summary>
        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
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

        /// <summary>Clears transient reader reference tracking for reuse by the next operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetOperationState()
        {
            // No reference can be retained before the first identifier is registered.
            if (_versionTables is { Count: > 0 })
            {
                _versionTables.Clear();
                _versionTableUsed = 0;
            }
            if (_sequentialCount == 0 && refToObject.Count == 0) return;
            ResetReferences();
        }

        /// <summary>Records the member table of a full version record read in this operation.</summary>
        /// <param name="fingerprint">The table fingerprint.</param>
        /// <param name="table">The table bytes after the record header.</param>
        internal void AddVersionTable(ulong fingerprint, ReadOnlySpan<byte> table)
        {
            _versionTables ??= new Dictionary<ulong, (int Start, int Length)>();
            if (_versionTables.ContainsKey(fingerprint)) return;
            if (_versionTableBytes.Length - _versionTableUsed < table.Length)
                Array.Resize(ref _versionTableBytes, Math.Max(Math.Max(256, _versionTableBytes.Length * 2), _versionTableUsed + table.Length));
            table.CopyTo(_versionTableBytes.AsSpan(_versionTableUsed));
            _versionTables.Add(fingerprint, (_versionTableUsed, table.Length));
            _versionTableUsed += table.Length;
        }

        /// <summary>Finds the member table a short version record names.</summary>
        /// <param name="fingerprint">The table fingerprint.</param>
        /// <param name="table">Receives the table bytes.</param>
        /// <returns>Whether a full record of this operation carried the table.</returns>
        internal bool TryGetVersionTable(ulong fingerprint, out Span<byte> table)
        {
            table = default;
            if (_versionTables is null || !_versionTables.TryGetValue(fingerprint, out var entry)) return false;
            table = _versionTableBytes.AsSpan(entry.Start, entry.Length);
            return true;
        }

        /// <summary>Clears a nonempty reference table without expanding its cleanup into each entry point.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ResetReferences()
        {
            // Small graphs clear their few slots without a bulk-clear call.
            var sequential = _sequential;
            int count = _sequentialCount;
            if (count <= 16)
                for (int i = 0; i < sequential.Length && i < count; i++) sequential[i].Value = null;
            else Array.Clear(sequential, 0, count);
            _sequentialCount = 0;
            if (refToObject.Count != 0) refToObject.Clear();
            reusedObjectToRef?.Clear();
            reuseIndexInitialized = false;
        }

        /// <summary>Obtains the object reference for the supplied input.</summary>
        /// <param name="id">The id used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetObjectReference(uint id)
        {
            if (id < (uint)_sequentialCount) return _sequential[id].Value!;
            return GetMappedReference(id);
        }

        /// <summary>Finds an identifier registered out of sequence.</summary>
        /// <param name="id">The id used by this operation.</param>
        /// <returns>The registered object.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private object GetMappedReference(uint id)
        {
            if (refToObject.TryGetValue(id, out var value))
            {
                return value;
            }
            LuminPackExceptionHelper.ThrowMessage("Object is not found in this reference id:" + id);
            return null!;
        }

        /// <summary>Performs the add object reference step for the current operation.</summary>
        /// <param name="id">The id used by this operation.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObjectReference(uint id, object value)
        {
            Store(id, value);
            if (reuseIndexInitialized) reusedObjectToRef!.TryAdd(value, id);
        }

        /// <summary>Registers an existing instance as the object of a new record unless another record of this read holds it.</summary>
        /// <param name="id">The new record's identifier.</param>
        /// <param name="candidate">The instance the receiving storage already holds.</param>
        /// <returns>Whether the candidate was registered and may be reused; otherwise nothing was registered.</returns>
        /// <remarks>One identity probe both tests and registers the candidate. A few sequential references are scanned
        /// without building the identity index.</remarks>
        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        public bool ClaimReference(uint id, object candidate)
        {
            if (!reuseIndexInitialized)
            {
                if (_sequentialCount <= 8 && refToObject.Count == 0)
                {
                    var sequential = _sequential;
                    for (int i = 0; i < sequential.Length && i < _sequentialCount; i++)
                        if (ReferenceEquals(sequential[i].Value, candidate)) return false;
                    Store(id, candidate);
                    return true;
                }
                InitializeReuseIndex();
            }
            ref uint slot = ref reusedObjectToRef!.GetValueRefOrAddDefault(candidate, out bool exists);
            if (exists) return false;
            slot = id;
            Store(id, candidate);
            return true;
        }

        /// <summary>Stores a registered object under its identifier.</summary>
        /// <param name="id">The id used by this operation.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Store(uint id, object value)
        {
            if (id == (uint)_sequentialCount && id < (uint)_sequential.Length && refToObject.Count == 0)
            {
                _sequential[id].Value = value;
                _sequentialCount++;
                return;
            }
            StoreSlow(id, value);
        }

        /// <summary>Grows the sequential table or registers an identifier that arrives out of order.</summary>
        /// <param name="id">The id used by this operation.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StoreSlow(uint id, object value)
        {
            if (id == (uint)_sequentialCount && refToObject.Count == 0 && id < int.MaxValue / 2)
            {
                Array.Resize(ref _sequential, Math.Max(16, _sequential.Length * 2));
                _sequential[id].Value = value;
                _sequentialCount++;
            }
            else if (id < (uint)_sequentialCount || !refToObject.TryAdd(id, value))
            {
                LuminPackExceptionHelper.ThrowMessage("Object is already added, id:" + id);
            }
        }

        /// <summary>Indexes every registered object by identity once reuse checks outgrow the sequential scan.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeReuseIndex()
        {
            reusedObjectToRef ??= new LuminIdentityMap();
            for (int i = 0; i < _sequentialCount; i++) reusedObjectToRef.TryAdd(_sequential[i].Value!, (uint)i);
            foreach (var entry in refToObject) reusedObjectToRef.TryAdd(entry.Value, entry.Key);
            reuseIndexInitialized = true;
        }

        /// <summary>Checks whether an existing instance already represents another record in this read scope.</summary>
        /// <param name="value">The candidate instance for a new reference identifier.</param>
        /// <returns>Whether the instance has already been registered by identity.</returns>
        public bool IsObjectReferenceRegistered(object value)
        {
            if (_sequentialCount == 0 && refToObject.Count == 0) return false;
            // A few sequential references are scanned without building the identity index.
            if (!reuseIndexInitialized && _sequentialCount <= 8 && refToObject.Count == 0)
            {
                var sequential = _sequential;
                for (int i = 0; i < sequential.Length && i < _sequentialCount; i++)
                    if (ReferenceEquals(sequential[i].Value, value)) return true;
                return false;
            }
            if (!reuseIndexInitialized) InitializeReuseIndex();
            return reusedObjectToRef!.Contains(value);
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        void IDisposable.Dispose()
        {

        }
    }


}
