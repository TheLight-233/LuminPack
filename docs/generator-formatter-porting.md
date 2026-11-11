# Porting generator formatters

[ÖÐÎÄ](generator-formatter-porting.cn.md)

Read the [mechanism contract](mechanism-contract.md) before changing operation bodies.

## Ownership

The registration point is Rendering/Formatters/FormatterRegistry.cs. Each fully qualified open sequence definition has a SequenceFormatter descriptor. Standalone framework types have entries in Standalone.

SharedFormatterSet collects closed owner identities and element dependencies, then sorts and deduplicates them for incremental equality. Owner identities have no fixed bit budget.

SharedFormatterPublisher invokes registered renderers. Unsupported element forms have no renderer and remain unmapped during model projection.

BinaryEmitter owns record framing and cursor transitions. Registered member writers own collection algorithms. The render model carries the descriptor, so the binary and JSON emitters use a common sequence category.

The registry and its delegates execute inside the source generator. Generated application code retains concrete static calls, existing roots and compiled profiles.

## Adding a sequence

1. Create Rendering/Formatters/<Family>/. Implement the scalar, string, packable and forwarding templates supported by the old pipeline. Keep distinct variants in separate files.
2. Give owner renderers the signature Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations). Derive names from the closed fact and GeneratedNames.
3. Add one SequenceFormatter registration keyed by namespace and open definition name. Set its analysis Kind, scalar renderer and supported optional renderers.
4. When records inline the family, put its member writer in the same directory and register writeMember. FormatterRegistry.Adapt accepts writers that only need exit-label counters.
5. Set profileElements, alwaysLoops, payloadLoops and usesCandidateCache to reproduce the existing profile signatures, inlining decisions and dependencies.
6. Share complex traversal generation between roots and inline members in a family helper. ImmutableQueueTraversal is an example.

Existing raw array/list and paired/nested dictionary record shapes retain specialized storage projections. A new storage representation needs its own analysis facts. Registering an owner alone does not establish layout, raw-transfer or wire-format eligibility.

## Element forms

- scalar handles a proven built-in raw scalar.
- text handles strings and their compiled profiles.
- payload handles an expandable packable element whose normalized render model is complete.
- forwarded calls the element's concrete generated owner. Recursive dependency collection publishes child owners for nested collections.

Each registration explicitly lists its supported forms. A missing variant must not fall through to another family's renderer.

## Operations and support

Use FormatterOwnerFile and its four operation callbacks. Root bodies, serializer entries, owner forwarders and extension entries belong to the same operation selection. The default selects all four operations.

Keep support declarations tied to the operation that uses them. The immutable candidate cache, for example, is needed by binary reads.

## Preserving behavior

Carry over null and empty framing, element order, comparer behavior, construction, reuse, partial publication on failure, cursor ownership, checks and pool cleanup. Optional runtime helpers require analysis facts derived from the referenced runtime and their original fallbacks.

Use the old emitter and its completed generated output as references. Whitespace may differ. Preserve operation order when moving bodies.

Verification follows the task instructions and contributing guidance. Explicitly report skipped builds, generated-source compilation and runtime checks.
