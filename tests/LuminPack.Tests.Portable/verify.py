"""Exercise managed fixtures against Java, Go and TypeScript from one harness."""

import argparse
from dataclasses import dataclass
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CODEGEN = ROOT / "tools/LuminPack.Codegen/LuminPack.Codegen.csproj"
MODELS = {"simple": "LuminPackBenchmark.SimpleClass", "serialize": "LuminPackBenchmark.CharacterSaveData"}
FORMATS = (("binary", ".bin"), ("json-utf8", ".utf8.json"), ("json-utf16", ".utf16.json"))
LANGUAGES = ("java", "go", "typescript")
UTF16_FIXTURE = ("json-utf16", ".json")
BINARY_AND_UTF16 = (FORMATS[0], UTF16_FIXTURE)
LEGACY_FORMATS = (FORMATS[0], FORMATS[1], UTF16_FIXTURE)
PROFILES = ("utf8-length", "utf8-token", "utf16-length", "utf16-token", "utf8-varint", "utf16-varint")
ORDERED_JSON_ROOT = "LuminPackPortableFixtures.OrderedJsonDocument"
PACKED_LAYOUT_ROOT = "LuminPackCppLayoutFixtures.LayoutPackedPacket"
TAGGED_LAYOUT_ROOT = "LuminPackCppLayoutFixtures.LayoutTaggedPacket"
NATIVE_LAYOUT_ROOT = "LuminPackCppLayoutFixtures.LayoutNativePaddedPacket"
SUITES = {
    "profiles": "BinaryProfile",
    "version-union": "VersionUnion",
    "version-archive": "VersionCompatibility",
    "rectangular": "Rectangular",
    "recursive": "Recursive",
    "compression": "Compression",
    "packed-pairs": "Pair",
    "packed-tuples": "Tuple",
    "tagged-pairs": "Pair",
    "tagged-tuples": "Tuple",
    "references": "References",
    "custom": "Custom",
    "registered-union": "RegisteredUnion",
    "virtual-union": "VirtualUnion",
    "layout": "Layout",
    "native-layout": "Layout",
    "memory": "Memory",
    "native-integer": "NativeInteger",
    "nullable": "Nullable",
    "native-nullable": "Nullable",
    "sequence": "Sequence",
    "native-sequence": "Sequence",
    "raw-compound": "RawCompound",
    "recursive-composition": "RecursiveComposition",
    "closed-generic": "ClosedGeneric",
    "binary-input-prefix": "BinaryInput",
    "binary-input-strict": "BinaryInput",
}


@dataclass(frozen=True)
class Route:
    root: str
    input: str
    output: str
    read: str
    write: str
    profile: str | None = None
    reject: bool = False
    binding: str | None = None
    exact: str | None = None
    max_input: int | None = None
    reject_trailing: bool = False
    cpp_case: str | None = None
    expected_failure: bool = False


def suite_routes(suite):
    """Describes which fixture writer, root and reader each native operation exercises."""
    match suite:
        case "benchmarks":
            for name, root in MODELS.items():
                for index, (source, suffix) in enumerate(FORMATS):
                    yield Route(root, name + suffix, name + suffix, source, source, reject=True)
                    destination, target = FORMATS[(index + 1) % len(FORMATS)]
                    yield Route(root, name + suffix, f"{name}.from-{index}{target}", source, destination)
        case "strings":
            root = "LuminPackPortableFixtures.StringBoundaryDocument"
            for profile in PROFILES:
                filename = profile + ".bin"
                yield Route(root, filename, filename, "binary", "binary", profile, reject=True, exact=filename)
                for fmt, suffix in FORMATS[1:]:
                    encoding = fmt.removeprefix("json-")
                    yield Route(root, "strings" + suffix, f"{profile}.from-{encoding}.bin", fmt, "binary", profile,
                                exact=filename)
                    yield Route(root, filename, profile + suffix, "binary", fmt, profile)
            for fmt, suffix in FORMATS[1:]:
                filename = "strings" + suffix
                yield Route(root, filename, filename, fmt, fmt, reject=True)
        case "recursive-lists":
            for stem, model in (("list", "SelfList"), ("tracked", "SharedList"), ("wide", "SharedList"), ("tree", "UnionTree"), ("choice", "IRecursiveChoice"), ("list-case", "IRecursiveChoice")):
                root = "LuminPackPortableFixtures." + model
                yield Route(root, stem + ".bin", stem + ".json", "binary", "json-utf8", exact=stem + ".json")
                yield Route(root, stem + ".json", stem + ".bin", "json-utf8", "binary", exact=stem + ".bin")
            yield Route("LuminPackPortableFixtures.SharedList", "invalid.bin", "rejected.bin", "binary", "binary", expected_failure=True)
        case "primitive-roots":
            derived = "LuminPackPortableFixtures.PrimitiveList"
            for name, root in (
                ("scalar", "System.Int32"), ("float", "float"), ("bools", "bool[]"),
                ("nested", "System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<float>>"),
                ("array", "int?[]"), ("empty", "int?[]"), ("null", "int?[]"),
                ("list", "System.Collections.Generic.List<float?>"),
                ("dict", "System.Collections.Generic.Dictionary<int,int?>"),
                ("derived", derived),
            ):
                filename = name + ".bin"
                yield Route(root, filename, filename, "binary", "binary", exact=filename)
                for fmt, suffix in FORMATS[1:]:
                    yield Route(root, filename, name + suffix, "binary", fmt, exact=name + suffix)
                    yield Route(root, name + suffix, name + ".from-" + fmt + ".bin", fmt, "binary", exact=filename)
            for fmt, filename in (("binary", "derived-null.bin"), ("json-utf8", "derived-null.json")):
                yield Route(derived, filename, filename + ".invalid", fmt, fmt, expected_failure=True)
        case "ordered-json":
            fmt = "json-utf8"
            container = "LuminPackPortableFixtures.OrderedJsonContainer"
            limited = "LuminPackPortableFixtures.OrderedJsonLimitedDocument"
            valid = ((ORDERED_JSON_ROOT, "ordered", ("ordered", "whitespace", "reordered", "escaped", "unknown", "duplicate", "negative-zero")),
                     (container, "nested", ("nested", "alias")),
                     (limited, "limited", ("limited", "at-limit")))
            for root, canonical, names in valid:
                for name in names:
                    filename = name + ".json"
                    yield Route(root, filename, filename, fmt, fmt, exact=canonical + ".json")
            yield Route(ORDERED_JSON_ROOT, "ordered.bin", "from-binary.json", "binary", fmt, exact="ordered.json")
            invalid = ((ORDERED_JSON_ROOT, ("overflow", "underflow", "missing-comma", "missing-colon", "leading-zero", "truncated-value")),
                       (limited, ("over-limit", "depth-limit")))
            for root, names in invalid:
                for name in names:
                    filename = name + ".json"
                    yield Route(root, filename, filename + ".invalid", fmt, fmt, expected_failure=True)
        case "json-numbers":
            root = "LuminPackPortableFixtures.JsonNumberDocument"
            for source, suffix in FORMATS:
                for target, output_suffix in FORMATS[1:]:
                    stem = source.removeprefix("json-")
                    exact = "numbers" + output_suffix
                    yield Route(root, "numbers" + suffix, "numbers.from-" + stem + output_suffix, source, target, exact=exact)
            invalid = "LuminPackPortableFixtures.JsonNumberRejectDocument"
            for width in ("f32", "f64"):
                for value in ("nan", "positive-infinity", "negative-infinity"):
                    name = f"{width}-{value}"
                    binary = name + ".bin"
                    yield Route(invalid, binary, binary, "binary", "binary", exact=binary)
                    for target, suffix in FORMATS[1:]:
                        yield Route(invalid, binary, name + suffix + ".invalid", "binary", target, expected_failure=True)
        case "profiles":
            root = "LuminPackCppBinaryProfileFixtures.ProfileDocument"
            for profile in PROFILES:
                yield Route(root, profile + ".bin", profile + ".utf8.json", "binary", "json-utf8", profile)
                yield Route(root, "expected.utf16.json", profile + ".bin", "json-utf16", "binary", profile)
                yield Route(root, "expected.utf8.json", profile + ".utf16.json", "json-utf8", "json-utf16", profile)
            yield Route(root, "default.bin", "default.bin", "binary", "binary")
            yield Route("LuminPackCppBinaryProfileFixtures.DeclaredRecord", "pinned-root.bin", "pinned-root.bin",
                        "binary", "binary", "utf8-length")
        case "version-archive":
            yield Route("CompatVersion", "archive.bin", "migrated.bin", "binary", "binary")
            yield Route("EvolvedVersion", "archive.bin", "evolved.bin", "binary", "binary")
            yield Route("LegacyNullable", "nullable.bin", "nullable-modern.bin", "binary", "binary", "utf8-length")
            yield Route("ClosedVersion<int>", "closed-scalar.bin", "closed-scalar.bin", "binary", "binary")
            yield Route("ClosedVersion<global::System.Collections.Generic.List<int>>", "closed-list.bin",
                        "closed-list.bin", "binary", "binary")
        case "version-union":
            yield from documents("LuminPackCppVersionUnionFixtures", {
                "envelope": "Envelope", "graph": "GraphEnvelope", "null": "IChoice",
            })
        case "rectangular":
            yield from documents("LuminPackCppRectangularFixtures", {
                "scalar-array": "ScalarPacket", "modeled-array": "ModeledPacket",
            })
            yield from documents("LuminPackCppRectangularFixtures", {"bounds-array": "BoundsPacket"}, FORMATS[1:])
        case "recursive":
            yield from documents("LuminPackCppRecursiveFixtures", {
                "direct": "Node", "mutual": "Envelope", "version": "VersionNode",
            }, LEGACY_FORMATS)
        case "compression":
            yield from documents("LuminPackCppCompressionFixtures", {
                "compressed-record": "CompressedPacket", "compressed-version": "VersionCompressedPacket",
                "compressed-shapes": "CompressedShapePacket",
            })
        case "packed-pairs" | "tagged-pairs":
            root = "LuminPackCppPairFixtures.PairPacket"
            yield from document(root, "pair", BINARY_AND_UTF16)
            if suite == "tagged-pairs":
                yield Route(root, "pair.bin", "pair-alternate.bin", "binary", "binary", cpp_case="pair-alternate.bin")
        case "packed-tuples" | "tagged-tuples":
            root = "LuminPackCppTupleFixtures.TuplePacket"
            yield from document(root, "tuple", BINARY_AND_UTF16)
            if suite == "tagged-tuples":
                for tag in range(1, 5):
                    name = f"tuple-layout-{tag}.bin"
                    yield Route(root, "tuple.bin", name, "binary", "binary", cpp_case=name)
        case "references":
            yield from documents("LuminPackCppReferenceFixtures", {
                "node": "Node", "null-node": "Node", "version-node": "VersionNode", "envelope": "Envelope",
                "rtti-wrapper": "IWrapper", "tracked-case": "IGraphChoice", "tracked-version-case": "IGraphChoice",
                "null-tracked-union": "IGraphChoice",
            })
            yield from documents("LuminPackCppReferenceFixtures", {"tracked-union-packet": "GraphPacket"},
                                 binding="graph-external")
        case "custom":
            yield from documents("LuminPackCppCustomFixtures", {
                "custom": "Shifted", "custom-null": "Shifted", "custom-ordinary": "Ordinary", "custom-version": "Versioned",
            }, BINARY_AND_UTF16, binding="custom-shifted")
            yield from documents("LuminPackCppCustomFixtures", {"custom-binary-only": "BinaryOnly"}, FORMATS[:1],
                                 binding="custom-binary-only")
        case "registered-union":
            for name, root in (("narrow", "INarrow"), ("wide", "IWide")):
                yield from documents("LuminPackCppRegisteredUnionFixtures", {name: root}, LEGACY_FORMATS,
                                     binding="registered-" + name)
                yield from documents("LuminPackCppRegisteredUnionFixtures", {"static-" + name: root}, LEGACY_FORMATS)
        case "virtual-union":
            namespace = "LuminPackCppVirtualUnionFixtures"
            yield from documents(namespace, {
                "generic-int": "ILarge", "generic-string": "ILarge", "inherited-case": "ILarge", "null-case": "ILarge",
                "all-cases": "LargeDocument", "direct-graph": "LargeGraph",
            })
            yield from documents(namespace, {"mixed-graph": "GraphDocument"}, binding="large-graph-external")
        case "layout":
            yield from document(PACKED_LAYOUT_ROOT, "layout-packed", BINARY_AND_UTF16)
            yield from document(TAGGED_LAYOUT_ROOT, "layout-tagged", BINARY_AND_UTF16)
            yield Route(PACKED_LAYOUT_ROOT, "layout-packed.bin", "layout-packed-raw.bin", "binary", "binary", exact="layout-packed.bin")
            yield Route(TAGGED_LAYOUT_ROOT, "layout-tagged.bin", "layout-tagged-alternate.bin", "binary", "binary",
                        cpp_case="layout-tagged-alternate.bin", exact="layout-tagged.bin")
        case "native-layout":
            yield from document(NATIVE_LAYOUT_ROOT, "layout-native-padded", BINARY_AND_UTF16)
            yield Route(NATIVE_LAYOUT_ROOT, "layout-native-padded.bin", "layout-native-padded-preserved.bin", "binary", "binary",
                        exact="layout-native-padded.bin")
        case "memory":
            yield from documents("LuminPackCppMemoryFixtures", {
                "memory": "MemoryPacket", "memory-tagged": "TaggedMemory", "memory-version": "VersionedMemory",
            }, BINARY_AND_UTF16)
        case "native-integer":
            yield from documents("LuminPackCppNativeIntegerFixtures", {
                "native-integer": "NativeIntegerPacket", "version-native-integer": "VersionNativeIntegerPacket",
            })
        case "nullable" | "native-nullable":
            yield from documents("LuminPackCppFixtures", {
                "nullable": "CppNullablePacket", "nullable-version": "CppNullableVersion",
            }, BINARY_AND_UTF16)
        case "sequence" | "native-sequence":
            yield from documents("LuminPackCppSequenceFixtures", {"sequence": "SequencePacket"}, BINARY_AND_UTF16)
        case "raw-compound":
            root = "LuminPackCppRawCompoundFixtures.RawCompoundPacket"
            yield from documents("LuminPackCppRawCompoundFixtures", {"raw-compound": "RawCompoundPacket"}, BINARY_AND_UTF16)
            yield Route(root, "raw-compound.bin", "raw-compound-pad.bin", "binary", "binary", cpp_case="raw-compound-pad.bin")
        case "recursive-composition":
            yield from documents("LuminPackCppRecursiveCompositionFixtures", {
                "composition": "Document", "outgoing": "OutgoingDocument",
            }, LEGACY_FORMATS)
        case "closed-generic":
            namespace = "LuminPackCppClosedGenericFixtures"
            yield from document(f"{namespace}.Box<string>", "direct", LEGACY_FORMATS)
            yield from document(f"System.Collections.Generic.List<{namespace}.Cell<int>?>", "nested", LEGACY_FORMATS)
        case "binary-input-prefix" | "binary-input-strict":
            strict = suite == "binary-input-strict"
            namespace = "LuminPackCppStrictBinaryInput" if strict else "LuminPackCppPrefixBinaryInput"
            for name, root in (("message", "Message"), ("graph", "Node")):
                path = name + ".bin"
                identity = f"{namespace}.{root}"
                yield Route(identity, path, path, "binary", "binary", reject=True, exact=path,
                            max_input=128, reject_trailing=strict)
                if not strict:
                    yield Route(identity, name + "-joined.bin", name + "-prefix.bin", "binary", "binary", exact=path)


def documents(namespace, models, formats=FORMATS, *, binding=None):
    for name, root in models.items():
        yield from document(f"{namespace}.{root}", name, formats, binding=binding)


def document(root, name, formats, *, binding=None):
    # Cycle through encodings so a matching pair of native bugs cannot cancel out.
    for index, (source, suffix) in enumerate(formats):
        destination, target = formats[(index + 1) % len(formats)]
        yield Route(root, name + suffix, name + target, source, destination, binding=binding)


def invoke(command, cwd=ROOT, **options):
    return subprocess.run(command, cwd=cwd, check=True, **options)


def build_dotnet(project, artifacts, *, export=False, properties=()):
    # The Satori compiler host uses its own shared output and restore paths; the
    # isolated artifact tree is for the portable fixtures, not the compiler.
    settings = [f"-p:ArtifactsPath={artifacts}", "-p:LuminPackUseSatori=false", *properties]
    if export:
        settings.append("-p:LuminPackExportContract=true")
    invoke(["dotnet", "build", project, "-c", "Release", *settings])
    result = invoke(["dotnet", "msbuild", project, "-getProperty:TargetPath", "-p:Configuration=Release", *settings],
                    capture_output=True, text=True)
    assembly = Path(result.stdout.strip())
    if not assembly.is_file():
        raise FileNotFoundError(assembly)
    return assembly


def executable(name, override=None):
    command = override or shutil.which(name)
    if not command:
        raise SystemExit(f"Missing {name}. Install it or supply its executable path in the harness options.")
    return command


def java_runner(args, work):
    source = ROOT / "src/LuminPack.Java/src"
    classes = work / ("java-vector-classes" if args.java_vector else "java-classes")
    classes.mkdir(exist_ok=True)
    javac = executable("javac", args.javac)
    java = executable("java", args.java)
    invoke([javac, "-encoding", "UTF-8", "-d", classes,
            *sorted((source / "main/java").rglob("*.java")), HERE / "PortableRoundTrip.java"])
    modules = []
    if args.java_vector:
        modules = ["--add-modules", "jdk.incubator.vector"]
        fast_checks = source / "test/java/luminpack"
        invoke([javac, *modules, "-cp", classes, "-d", classes,
                *sorted((source / "vector/java").rglob("*.java")), fast_checks / "FastPathChecks.java",
                fast_checks / "RegistrationChecks.java"])
        shutil.copytree(source / "vector/resources", classes, dirs_exist_ok=True)
        invoke([java, *modules, "-cp", classes, "luminpack.FastPathChecks", "vector"])
        invoke([java, *modules, "-cp", classes, "luminpack.RegistrationChecks"])
    return [java, *modules, "-cp", classes, "PortableRoundTrip"]


def go_runner(args, work):
    root = ROOT / "src/LuminPack.Go"
    module = (root / "go.mod").read_text(encoding="utf-8").splitlines()
    version = next(line for line in module if line.startswith("go "))
    name = next(line[7:] for line in module if line.startswith("module "))
    workspace = work / "go-runner"
    workspace.mkdir(exist_ok=True)
    (workspace / "go.mod").write_text(
        f"module luminpack-portable-interop\n\n{version}\n\nrequire {name} v0.0.0\n"
        f"replace {name} => {json.dumps(root.as_posix(), ensure_ascii=False)}\n", encoding="utf-8")
    shutil.copyfile(HERE / "roundtrip.go", workspace / "main.go")
    name = "roundtrip-pure" if args.go_pure else "roundtrip"
    output = workspace / (name + ".exe" if os.name == "nt" else name)
    tags = ["-tags=purego"] if args.go_pure else []
    invoke([executable("go", args.go), "build", "-mod=mod", *tags, "-o", output, "."], cwd=workspace)
    return [output]


def typescript_runner(args):
    root = ROOT / "src/LuminPack.TypeScript"
    runtime = args.typescript_runtime
    if runtime is None:
        npm = executable("npm.cmd" if os.name == "nt" else "npm")
        if not (root / "node_modules/typescript").is_dir():
            install = ["ci"] if (root / "package-lock.json").is_file() else ["install", "--no-package-lock"]
            invoke([npm, *install], cwd=root)
        invoke([npm, "run", "build"], cwd=root)
        runtime = root / "dist/index.js"
    flags = ["--no-expose-wasm"] if args.typescript_no_wasm else []
    return [executable("node", args.node), *flags, HERE / "roundtrip.mjs", runtime.resolve()]


def prepare_inputs(args, work, codegen, routes):
    if args.suite in ("benchmarks", "strings", "ordered-json", "json-numbers", "primitive-roots", "recursive-lists"):
        project = HERE / "LuminPack.Tests.Portable.csproj"
    else:
        name = "LuminPack.Tests.Cpp." + SUITES[args.suite]
        project = ROOT / "tests" / name / (name + ".csproj")
    properties = {"packed-pairs": ("-p:LuminPackCppPairPacked=true",),
                  "packed-tuples": ("-p:LuminPackCppTuplePacked=true",),
                  "native-nullable": ("-p:LuminPackCppNullableLayout=Native",),
                  "native-sequence": ("-p:LuminPackCppSequenceLayout=Native",),
                  "binary-input-strict": ("-p:LuminPackCppStrictBinaryInput=true",)}
    managed = (args.managed_dll.resolve() if args.managed_dll else
               build_dotnet(project, work / "managed", export=True, properties=properties.get(args.suite, ())))
    inputs = work / "managed-models"
    operation = {"native-layout": "write-native", "strings": "write-strings", "ordered-json": "write-ordered-json",
                 "recursive-lists": "write-recursive-lists", "primitive-roots": "write-primitive-roots", "json-numbers": "write-json-numbers"}.get(args.suite, "write")
    write = [args.managed_dotnet, managed, operation, inputs]
    if args.suite == "version-archive":
        write.append(ROOT / "tests/LuminPack.Tests.Aot/Fixtures/version-revision-1.bin")
    manifest = inputs / "manifest.json"
    if args.fixtures:
        if args.suite == "benchmarks":
            # Earlier fixture sets lack UTF-8 JSON. Let the managed producer fill missing documents.
            invoke(write)
        else:
            inputs.mkdir(exist_ok=True)
        shutil.copyfile(args.manifest or args.fixtures / "manifest.json", manifest)
        for name in {route.input for route in routes}:
            path = args.fixtures / name
            if args.suite == "benchmarks" and name.endswith(".utf16.json") and not path.exists():
                path = args.fixtures / name.replace(".utf16.json", ".json")
            if path.is_file():
                shutil.copyfile(path, inputs / name)
            elif not (inputs / name).is_file():
                raise FileNotFoundError(path)
    else:
        invoke(write)
        assembly = managed.with_name("LuminPack.Benchmark.dll") if args.suite == "benchmarks" else managed
        invoke(["dotnet", codegen, "export", assembly, manifest])
    schemas = {}
    for root, profile in dict.fromkeys((route.root, route.profile) for route in routes):
        schema = inputs / f"schema-{len(schemas)}.json"
        flags = ["--binary-profile", profile] if profile else []
        if args.native_integer_bytes:
            flags += ["--native-integer-bytes", str(args.native_integer_bytes)]
        if args.native_scalar_alignment:
            flags += ["--native-scalar-alignment", str(args.native_scalar_alignment)]
        invoke(["dotnet", codegen, "project", *flags, manifest, root, schema])
        if args.suite in ("primitive-roots", "recursive-lists"):
            first = next(route for route in routes if route.root == root)
            header = inputs / Path(first.input).with_suffix(".hpp")
            invoke(["dotnet", codegen, "generate", "--language", "cpp", manifest, root, header])
        if args.suite == "ordered-json" and root == ORDERED_JSON_ROOT:
            assert_ordered_json_contract(schema)
        schemas[(root, profile)] = schema
    return managed, inputs, schemas


def assert_ordered_json_contract(schema):
    projected = json.loads(schema.read_text(encoding="utf-8"))
    root = next(contract for contract in projected["contracts"] if contract["id"] == ORDERED_JSON_ROOT)
    run_lengths = root.get("jsonInt32Runs") or []
    if max(run_lengths, default=0) <= 16:
        raise RuntimeError("The ordered JSON fixture requires an exported Int32 run longer than one cursor chunk.")
    generated = root.get("jsonGeneratedIntegerProperties") or []
    names = {member["name"] for member, enabled in zip(root["members"], generated) if enabled}
    if not {"FirstInt", "Unsigned32", "Signed64", "Unsigned64"}.issubset(names):
        raise RuntimeError("The ordered JSON fixture requires generated UTF-8 writers for the tested integer widths.")


def validate_rejection(command, schema, data, output, fmt, reason, *, write_format=None):
    malformed = output.with_name(output.name + "." + reason)
    malformed.write_bytes(data)
    result = subprocess.run([*command, schema, fmt, write_format or fmt, malformed, output], cwd=ROOT, capture_output=True)
    if result.returncode == 0 or output.exists():
        raise RuntimeError(f"Accepted {reason} {fmt} document: {malformed}")


def exchange(command, inputs, outputs, schemas, routes, cpp_outputs):
    for route in routes:
        schema = schemas[(route.root, route.profile)]
        original = inputs / route.input
        if route.cpp_case and cpp_outputs:
            cpp_document = cpp_outputs / route.cpp_case
            if cpp_document.is_file():
                original = cpp_document.resolve()
        binding = [route.binding] if route.binding else []
        output = outputs / route.output
        if route.expected_failure:
            validate_rejection(command, schema, original.read_bytes(), output, route.read, original.stem, write_format=route.write)
            continue
        invoke([*command, schema, route.read, route.write, original, output, *binding])
        if route.exact:
            expected = (inputs / route.exact).read_bytes()
            actual = output.read_bytes()
            if expected != actual:
                differing = next((offset for offset, (a, b) in enumerate(zip(expected, actual)) if a != b),
                                 min(len(expected), len(actual)))
                raise RuntimeError(f"Native output differs from managed {route.exact} at byte {differing}: {output}")
        if route.reject:
            invalid = outputs / (route.output + ".invalid")
            data = original.read_bytes()
            validate_rejection(command, schema, data[:-1], invalid, route.read, "truncated")
            if route.reject_trailing:
                validate_rejection(command, schema, data + data, invalid, route.read, "trailing")
            if route.max_input:
                oversized = data.ljust(route.max_input + 1, b"\0")
                validate_rejection(command, schema, oversized, invalid, route.read, "oversized")


def verify_layout_edits(suite, command, inputs, outputs, schemas):
    if suite == "layout":
        packed = inputs / "layout-packed.bin"
        tagged = inputs / "layout-tagged.bin"
        packed_schema = schemas[(PACKED_LAYOUT_ROOT, None)]
        tagged_schema = schemas[(TAGGED_LAYOUT_ROOT, None)]
        # The Packed list starts after the 12-byte scalar and four-byte count.
        # Its first entry is an Int32 followed by the 12-byte nested Packed value.
        assert_layout_edit(command, packed_schema, packed, outputs, "edit-packed", 31, 2, 127, 1234)
        # The Tagged array starts after the 13-byte scalar, count, and layout tag.
        assert_layout_edit(command, tagged_schema, tagged, outputs, "edit-tagged", 19, 4, -2, 0x01234567)
        packed_data = packed.read_bytes()
        if packed_data[30] != 0:
            raise RuntimeError("The Packed collection fixture changed its first Boolean field.")
        validate_rejection(command, packed_schema, packed_data[:30] + b"\x02" + packed_data[31:],
                           outputs / "layout-packed.invalid", "binary", "invalid-boolean")
        validate_rejection(command, packed_schema, packed_data[:47], outputs / "layout-packed.invalid", "binary", "truncated-slot")
        verify_alternate_tagged(command, tagged_schema, tagged.read_bytes(), outputs)
    elif suite == "native-layout":
        native = inputs / "layout-native-padded.bin"
        # The first 16-byte Native slot has an eight-byte amount, four-byte code, one-byte flag, and dirty padding.
        if native.read_bytes()[18:21] != bytes([0xa1]) * 3:
            raise RuntimeError("The Native layout fixture lost its distinguishable padding bytes.")
        assert_layout_edit(command, schemas[(NATIVE_LAYOUT_ROOT, None)], native, outputs, "edit-native", 13, 4, -31, 0x01234567)


def assert_layout_edit(command, schema, original, outputs, edit, offset, width, previous, value):
    data = original.read_bytes()
    if int.from_bytes(data[offset:offset + width], "little", signed=True) != previous:
        raise RuntimeError(f"The layout fixture changed the edited field in {original}.")
    expected = data[:offset] + value.to_bytes(width, "little", signed=True) + data[offset + width:]
    output = outputs / (edit + ".bin")
    invoke([*command, schema, "binary", "binary", original, output, edit])
    if output.read_bytes() != expected:
        raise RuntimeError(f"The Binary rewrite failed to update only the selected layout field: {output}")


def verify_alternate_tagged(command, schema, canonical, outputs):
    # The first two elements use the 24-byte canonical model and also fit the exported 16-byte model.
    count = int.from_bytes(canonical[14:18], "little")
    if count != 2 or canonical[18] != 1:
        raise RuntimeError("The Tagged layout fixture changed its canonical array header.")
    start = 19
    stride = 24
    slots = (canonical[index:index + stride] for index in range(start, start + count * stride, stride))
    alternate = b"".join(slot[:4] + slot[8:17] + b"\0\0\0" for slot in slots)
    source = outputs / "layout-tagged-model-2.input"
    source.write_bytes(canonical[:18] + b"\x02" + alternate + canonical[start + count * stride:])
    output = outputs / "layout-tagged-model-2.bin"
    invoke([*command, schema, "binary", "binary", source, output])
    if output.read_bytes() != canonical:
        raise RuntimeError(f"The Tagged collection did not normalize to its canonical model: {output}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--suite", choices=("benchmarks", "strings", "ordered-json", "json-numbers", "primitive-roots", "recursive-lists", *SUITES), default="benchmarks")
    parser.add_argument("--language", choices=LANGUAGES, action="append", help="Repeat to select multiple runtimes. The default runs all three.")
    parser.add_argument("--fixtures", type=Path, help="Reuse a manifest and documents emitted by the selected managed suite.")
    parser.add_argument("--manifest", type=Path, help="Use this manifest for reused fixtures stored separately from their manifest.")
    parser.add_argument("--work-dir", type=Path, help="Write all build and exchange artifacts in this directory.")
    parser.add_argument("--codegen-dll", type=Path, help="Reuse a built LuminPack.Codegen executable assembly.")
    parser.add_argument("--managed-dll", type=Path, help="Reuse a built managed verifier for the selected suite.")
    parser.add_argument("--managed-dotnet", default="dotnet", help="Managed fixture host executable override.")
    parser.add_argument("--java", help="Java executable override.")
    parser.add_argument("--javac", help="Java compiler executable override.")
    parser.add_argument("--java-vector", action="store_true", help="Compile and verify the optional Java Vector API provider.")
    parser.add_argument("--go", help="Go executable override.")
    parser.add_argument("--go-pure", action="store_true", help="Exercise Go's purego fallback.")
    parser.add_argument("--node", help="Node executable override.")
    parser.add_argument("--typescript-runtime", type=Path, help="Reuse the compiled JavaScript runtime entry point.")
    parser.add_argument("--typescript-no-wasm", action="store_true", help="Exercise the Node fallback with WebAssembly disabled.")
    parser.add_argument("--native-integer-bytes", type=int, choices=(4, 8), help="Override the producer's native integer width.")
    parser.add_argument("--native-scalar-alignment", type=int, choices=(4, 8), help="Override the producer's alignment for 64-bit scalar storage.")
    args = parser.parse_args()
    if args.manifest and not args.fixtures:
        parser.error("--manifest requires --fixtures")
    if args.work_dir is None:
        parent = ROOT / ".tmp/portable-interop"
        parent.mkdir(parents=True, exist_ok=True)
        args.work_dir = Path(tempfile.mkdtemp(dir=parent))
    work = args.work_dir.resolve()
    work.mkdir(parents=True, exist_ok=True)
    print(f"Fixture artifacts: {work}", flush=True)
    codegen = args.codegen_dll.resolve() if args.codegen_dll else build_dotnet(CODEGEN, work / "codegen")
    routes = tuple(suite_routes(args.suite))
    managed, inputs, schemas = prepare_inputs(args, work, codegen, routes)
    for language in args.language or LANGUAGES:
        if language == "java":
            command = java_runner(args, work)
            backend = "java (Vector API)" if args.java_vector else language
        elif language == "go":
            command = go_runner(args, work)
            backend = "go (purego)" if args.go_pure else language
        else:
            command = typescript_runner(args)
            backend = "typescript (WebAssembly disabled)" if args.typescript_no_wasm else language
        outputs = Path(tempfile.mkdtemp(prefix=f"{language}-", dir=work))
        exchange(command, inputs, outputs, schemas, routes, args.fixtures / "out" if args.fixtures else None)
        verify_layout_edits(args.suite, command, inputs, outputs, schemas)
        operation = {"native-layout": "read-native", "strings": "read-strings", "ordered-json": "read-ordered-json",
                     "recursive-lists": "read-recursive-lists", "primitive-roots": "read-primitive-roots", "json-numbers": "read-json-numbers"}.get(args.suite, "read")
        invoke([args.managed_dotnet, managed, operation, outputs])
        print(f"{backend}: verified {args.suite} managed round trips.", flush=True)


if __name__ == "__main__":
    main()
