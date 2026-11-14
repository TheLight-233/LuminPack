# Pooling and retained memory

[Chinese](pooling.cn.md)

## Design

Core uses thread-local fast caches with bounded, unordered shared overflow. `AtomicSlotPool<T>` owns shared references: an atomic exchange removes an item before a renter can use it, and a compare-and-exchange publishes an item only into an empty slot. A dedicated first slot handles ordinary shared reuse without scanning the overflow array. Independent take/store hints advance the overflow scan after a successful operation; they never establish ownership or an exact empty/full state.

This design serves short, usually synchronous serialization operations and shallow nesting. It requires no FIFO ordering, queue segments, per-slot sequence counters, background maintenance, or allocation on a warmed successful lease. A contested operation can miss an available item and allocate normally. Every overflow attempt scans at most the configured number of slots; empty or full large caches still cost O(capacity). Keep capacities near expected simultaneous leases, not the total number of objects serialized.

Crossbeam's bounded MPMC queue maintains FIFO positions and per-slot publication stamps; its segmented queue additionally allocates blocks. Those facilities can suit a producer/consumer pipeline, but are not requirements of a reuse cache. A queue also uses atomics and does not eliminate contention. The C# reference implementation was evaluated locally; the measured results are in the untracked pool-selection experiment.

## Retention and lifetime

| Cache | Thread-local retention | Shared retention |
| --- | --- | --- |
| `ObjectPool<T>` | One item per closed `T` and thread, shared among pool instances with identity checks | `maxSize` per pool, default 16 |
| `LuminBufferWriterPool` | One writer per thread in the ordinary build | At most 32 writers |
| Immutable framework builders | One cleared builder per closed builder type and thread | At most eight builders per closed builder type |
| Frozen construction storage | None | At most four containers per closed type, subject to capacity admission limits |

The optional `LUMINPACK_BUFFERWRITER_SECOND_TLS` build symbol adds a second thread-local writer. A rented writer remains exclusively owned even when its thread-local slot still references it; its lease state prevents reuse. Nested rentals detach the previous active writer so that it can return through shared storage. Generic object and builder caches remove their local references on rent.

TLS is a strong reference while its thread is alive. An idle thread-pool thread can retain a cache for a long time; creating many closed generic types multiplies retention. Managed TLS does not by itself imply a permanent leak, but there is no idle timeout or process-wide TLS clearing here. Thread termination allows unreachable managed objects to be collected; it does **not** promise `IDisposable.Dispose`. Use the explicit clear operation on each worker when deterministic local cleanup is required. A pool becoming unreachable does not clear that type's thread-local item.

Writer return resets payload and reference state. A buffer whose logical capacity reaches `MaxPooledBufferSize` (4 MiB) is released from a thread-cached writer, or its uncached writer is disposed. These are object-count and admission bounds, not a total process-memory budget: array-pool buckets, context table capacity, active leases, and the framework's independent `ArrayPool<byte>.Shared` retention also consume memory. Generic `ObjectPool<T>` relies on the return policy to clear references and reject oversized or resource-heavy instances.

## Ownership and maintenance

- Return a lease exactly once to its originating pool, after every use has finished. Do not access an object or its borrowed spans after return. Pool thread safety does not make a rented object thread-safe.
- `ObjectPool<T>.Return` runs the policy before publication. Rejected or excess items are disposed. If the policy throws, publication has not occurred; the caller still owns the item and its cleanup.
- `ObjectPool<T>.MaxSize`, `AvailableCount` and `GetAllItems()` concern shared storage only. Setting capacity to zero leaves TLS enabled. Counts are approximate under concurrency; snapshots are diagnostic references, not leases, and must never be mutated, returned or disposed by the observer.
- `ObjectPool<T>.ClearThreadLocalCache()` clears the calling thread's slot for that closed `T`, regardless of which pool owns it. It detaches the reference before invoking disposal, including when disposal reenters or throws.
- `LuminBufferWriterPool.ClearThreadLocalCache()` clears only the calling thread's writer slots. Active writers are detached and stay valid until their eventual return. It does not clear other threads or central storage.
- `ObjectPool<T>.Resize` requires quiescence: no concurrent pool operations. It publishes replacement storage before disposing excess items, allowing disposal callbacks to reenter the pool. It drains every excess item and reports disposal failures together as `AggregateException`. TLS remains unchanged.

On .NET 8 and later, `T` implements the static `IPooledObjectPolicy<T>` members and `IDisposable`. On `netstandard2.1`, pass an `IPooledObjectPolicy<T>` instance to the constructor. The shared algorithm uses portable reference atomics and bounded array accesses; it does not depend on a vector ISA, pointer width, or native queue layout.
