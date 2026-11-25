import { BinaryReader, BinaryWriter, WireError, decodeUtf16, decodeUtf8, defaultLimits, encodeUtf8, littleEndian,
    readString, writeString, type Limits } from "./binary.ts";
import { JsonNumber, OrderedJsonCursor, jsonArray, jsonObject, orderedJsonMiss, parseJson, put, stringifyJson,
    type JsonValue } from "./json.ts";
import { formatJsonInteger } from "./json-number.ts";
import { compressLum2, decompressLum2 } from "./lum2.ts";
import { defaultScalar, fixedScalarRead, fixedScalarWrite, readJsonNumericScalar, readJsonScalar, readScalar, readScalars,
    scalarCollectionView, scalarWidth, writeJsonScalar, writeScalar, writeScalars,
    type FixedScalarRead, type FixedScalarWrite } from "./scalar.ts";
import { VersionState, nameKey, readLegacyMetadata, readReferenceNumber, schemaHash, typeKey, widthCode,
    writeReferenceNumber } from "./version.ts";

export { BinaryReader, BinaryWriter, WireError, type Limits } from "./binary.ts";
export { JsonNumber, type JsonValue } from "./json.ts";

export type JsonEncoding = "utf8" | "utf16";

export interface UnionValue<TType extends string = string, TValue = unknown> {
    type: TType;
    value: TValue;
}

export interface RectangularArray<T> {
    lengths: number[];
    lowerBounds: number[];
    values: T[];
}

export interface CustomCodec<T = unknown> {
    readBinary(input: BinaryReader): T;
    writeBinary(output: BinaryWriter, value: T): void;
    readJson?(value: JsonValue, encoding: JsonEncoding): T;
    writeJson?(value: T, encoding: JsonEncoding): JsonValue;
    minimumSize?: number;
}

export interface RegisteredCase {
    type: string;
    tag: number;
    codec: CustomCodec;
}

export interface CodecOptions {
    custom?: Readonly<Record<string, CustomCodec>>;
    registered?: Readonly<Record<string, readonly RegisteredCase[]>>;
    limits?: Partial<Limits>;
}

export interface Codec<T, TInput = T> {
    readBinary(input: Uint8Array): T;
    writeBinary(value: TInput): Uint8Array;
    readJson(input: Uint8Array, encoding?: JsonEncoding): T;
    writeJson(value: TInput, encoding?: JsonEncoding): Uint8Array;
}

type BinaryProfile = { binary: number; declared?: boolean; jsonEncoding?: number; jsonDepth?: number; trusted?: boolean;
    maxInput?: number; maxCollection?: number; maxString?: number; maxDepth?: number };

interface Storage {
    wireSize: number;
    tagged: boolean;
    modelSizes: number[];
    modelOffsets: number[][];
    fields: { name: string; storage: Storage }[];
}

interface StorageLeaf {
    path: (string | number)[];
    type: string;
    width: number;
    read: FixedScalarRead;
    write: FixedScalarWrite;
}
interface StorageModel { stride: number; offsets: number[]; order: number[]; nativeKey?: string; }
interface StoragePlan {
    packed: StorageModel;
    models: StorageModel[];
    leaves: StorageLeaf[];
    array: boolean;
    native: boolean;
    tagged: boolean;
}
interface NullableModel { stride: number; presence: number; payload: number; read: FixedScalarRead; }

type Wire = { rawCollection?: boolean; layout?: Storage } & (
    | { kind: "scalar"; type: string }
    | { kind: "string" }
    | { kind: "contract"; type: string }
    | { kind: "sequence"; element: Wire; family?: string; required?: boolean }
    | { kind: "rectangular"; element: Wire; rank: number }
    | { kind: "dictionary"; key: Wire; value: Wire }
    | { kind: "nullable"; element: Wire; nullableEncoding?: number; nullableLayout?: Storage; nativeNullableElement?: boolean }
    | { kind: "pair" | "tuple"; elements: Wire[]; rawCompoundStorage?: Storage; raw?: boolean }
);

interface Member {
    name: string;
    profile?: BinaryProfile;
    wire: Wire;
    descriptor?: string | null;
    nameKeys?: number[] | null;
    oldNames?: string[] | null;
    fixedNullable?: boolean | null;
    legacyVersionContract?: string | null;
    compressed?: boolean;
}

interface UnionCase { id: string; type?: string; hash: number; tag?: number; }

interface Contract {
    sequenceBase?: string | null;
    sequenceWire?: Wire;
    binaryMemberCount?: number;
    id: string;
    kind: string;
    profile: BinaryProfile & { trackReferences?: boolean };
    members: Member[];
    cases?: UnionCase[] | null;
    unionWideTags?: boolean;
    unionRegistration?: boolean;
    customJson?: boolean;
    versioned?: boolean;
    storageKind?: string | null;
    storage?: Storage | null;
    nativeStorage?: Storage | null;
}

interface Manifest {
    root: string;
    rootWire?: Wire;
    rootProfile?: BinaryProfile;
    version: number;
    binaryProfile?: number;
    jsonEncoding?: number;
    jsonDepth?: number;
    nativeIntegerBytes?: number;
    nativeScalarAlignment?: number;
    requireCompleteBinaryInput?: boolean;
    contracts: Contract[];
}

type Target = Record<string, unknown>;

interface Plan {
    contract: Contract;
    byHash: Map<number, string>;
    byId: Map<string, number>;
    registeredByTag: Map<number, RegisteredCase>;
    registeredByType: Map<string, RegisteredCase>;
    jsonByName: Map<string, Member>;
    scalarJsonBody?: { member: Member; head: string; type: string }[];
    jsonHeads: readonly { member: Member; head: string; next: string }[];
    descriptors?: bigint[];
    versionHash?: bigint;
}

interface Reference { type: string; value: Target; }

class ReadState {
    keyOrder?: WeakMap<object, string[]>;
    readonly references = new Map<number, Reference>();
    readonly version = new VersionState();
    depth = 0;
}

class WriteState {
    readonly references = new Map<object, { id: number; type: string }>();
    readonly version = new VersionState();
    depth = 0;
}

const own = (value: object, key: string) => Object.prototype.hasOwnProperty.call(value, key);
const identity = (value: string) => value.replace(/global::/g, "").trim();
const useProfile = (profile: BinaryProfile | undefined, inherited: number) => profile?.declared ? profile.binary : inherited;

function object(value: unknown): Target {
    if (value === null || typeof value !== "object" || Array.isArray(value) || value instanceof Map)
        throw new WireError("Expected a LuminPack record.");
    return value as Target;
}

function sequence(value: unknown, max: number): unknown[] {
    if (!Array.isArray(value) || value.length > max) throw new WireError("Invalid LuminPack sequence.");
    return value;
}

function storageField(value: object, key: string | number): unknown {
    return Object.prototype.hasOwnProperty.call(value, key) ? (value as Target)[key] : undefined;
}

function storageSet(value: object, path: (string | number)[], leaf: unknown): void {
    let current = value;
    for (let index = 0; index < path.length - 1; index++) {
        const key = path[index];
        let nested = storageField(current, key);
        if (nested === undefined) {
            nested = typeof path[index + 1] === "number" ? [] : {};
            put(current as Target, String(key), nested);
        }
        current = nested as object;
    }
    put(current as Target, String(path[path.length - 1]), leaf);
}

function nullSequence(wire: Extract<Wire, { kind: "sequence" }>): [] | null {
    if (wire.required) throw new WireError("Null LuminPack collection base.");
    return defaultMemory(wire.family) ? [] : null;
}

function defaultMemory(family?: string): boolean {
    return family === "System.Memory" || family === "System.ReadOnlyMemory" || family === "System.Buffers.ReadOnlySequence";
}

function dimensions(value: unknown, rank: number): number[] {
    const items = sequence(value, rank);
    if (items.length !== rank || items.some(item => typeof item !== "number" || !Number.isInteger(item) ||
        item < -0x8000_0000 || item > 0x7fff_ffff)) throw new WireError("Invalid LuminPack rectangular dimensions.");
    return items as number[];
}

function rectangularSize(lengths: readonly number[], max: number): number {
    if (lengths.some(length => length < 0)) throw new WireError("Negative LuminPack rectangular dimension.");
    if (lengths.includes(0)) return 0;
    let result = 1;
    for (const length of lengths) {
        if (result > Math.floor(max / length)) throw new WireError("LuminPack rectangular array exceeds its size limit.");
        result *= length;
    }
    return result;
}

function rectangularBounds(lengths: readonly number[], lowerBounds: readonly number[]): void {
    for (let i = 0; i < lengths.length; i++) {
        const upper = lowerBounds[i] + lengths[i] - 1;
        if (upper < -0x8000_0000 || upper > 0x7fff_ffff)
            throw new WireError("Invalid LuminPack rectangular upper bound.");
    }
}

function jsonUint(value: JsonValue): number {
    if (!(value instanceof JsonNumber) || !/^\d+$/.test(value.text)) throw new WireError("Invalid LuminPack JSON identifier.");
    const number = Number(value.text);
    if (number > 0xffff_ffff) throw new WireError("Invalid LuminPack JSON identifier.");
    return number;
}

function limit(projected: number | undefined, configured: number | undefined, fallback: number): number {
    const checked = Math.min(configured ?? fallback, projected ?? fallback, fallback);
    if (!Number.isInteger(checked) || checked < 0) throw new WireError("Invalid LuminPack limit.");
    return checked;
}

class CompiledCodec<T, TInput> implements Codec<T, TInput> {
    private readonly plans = new Map<string, Plan>();
    private readonly encodedJsonNames = new Map(
        ["$id", "$ref", "$type", "$value", "Lengths", "LowerBounds", "Values"].map(name => [name, JSON.stringify(name) + ":"]));
    private readonly storagePlans = new WeakMap<Storage, StoragePlan>();
    private readonly nullableModels = new WeakMap<Storage, NullableModel[]>();
    private readonly nativeRows = new WeakMap<object, { model: StorageModel; bytes: Uint8Array }>();
    private readonly manifest: Manifest;
    private readonly options: CodecOptions;
    private readonly root: Plan | undefined;
    private readonly rootWire: Wire | undefined;
    private readonly nativeBytes: number;
    private readonly nativeScalarAlignment: number;
    private readonly binaryProfile: number;
    private readonly limits: Limits;
    private readonly jsonDepth: number;
    private readonly jsonEncoding: JsonEncoding;
    private readonly orderedJson: boolean;
    private readonly trackedJson: boolean;

    constructor(manifest: unknown, options: CodecOptions) {
        this.manifest = manifest as Manifest;
        this.options = options;
        if (!this.manifest || this.manifest.version !== 1 || typeof this.manifest.root !== "string" ||
            !Array.isArray(this.manifest.contracts)) throw new WireError("Invalid LuminPack portable manifest.");
        this.nativeBytes = this.manifest.nativeIntegerBytes ?? 8;
        if (this.nativeBytes !== 4 && this.nativeBytes !== 8) throw new WireError("Invalid LuminPack native integer width.");
        this.nativeScalarAlignment = this.manifest.nativeScalarAlignment ?? 8;
        if (this.nativeScalarAlignment !== 4 && this.nativeScalarAlignment !== 8)
            throw new WireError("Invalid LuminPack native scalar alignment.");
        let orderedJson = true;
        for (const contract of this.manifest.contracts) {
            const type = identity(contract.id);
            if (this.plans.has(type)) throw new WireError("Duplicate LuminPack contract " + type + ".");
            const members = contract.members ?? [];
            if (contract.binaryMemberCount !== undefined &&
                (contract.binaryMemberCount < 0 || contract.binaryMemberCount !== members.length - 1))
                throw new WireError("Invalid LuminPack collection base member count.");
            const jsonByName = new Map<string, Member>();
            for (const member of members) {
                if (!this.encodedJsonNames.has(member.name)) this.encodedJsonNames.set(member.name, JSON.stringify(member.name) + ":");
                jsonByName.set(member.name, member);
                for (const alias of member.oldNames ?? []) if (!jsonByName.has(alias)) jsonByName.set(alias, member);
            }
            const jsonHeads = Object.values(Object.fromEntries(members.map(member => {
                const name = this.encodedJsonNames.get(member.name)!;
                return [member.name, { member, head: "{" + name, next: "," + name }];
            })));
            const plan: Plan = { contract, byHash: new Map(), byId: new Map(), registeredByTag: new Map(),
                registeredByType: new Map(), jsonByName, jsonHeads };
            if (contract.kind === "Struct" && !contract.profile.trackReferences && jsonHeads.length > 0 &&
                jsonHeads.every(entry => entry.member.wire.kind === "scalar")) {
                plan.scalarJsonBody = jsonHeads.map(({ member, head, next }, index) => ({
                    member, head: index === 0 ? head : next, type: (member.wire as Extract<Wire, { kind: "scalar" }>).type,
                }));
            }
            const declaredCases = contract.cases ?? [];
            for (const entry of declaredCases) {
                if (!Number.isInteger(entry.hash) || entry.hash <= 0 || entry.hash >= 0xffff_ffff ||
                    plan.byHash.has(entry.hash) || plan.byId.has(identity(entry.id)))
                    throw new WireError("Invalid LuminPack Union case.");
                const caseType = identity(entry.id);
                plan.byHash.set(entry.hash, caseType);
                plan.byId.set(caseType, entry.hash);
            }
            for (const entry of options.registered?.[type] ?? []) {
                if (contract.kind !== "Union" || !contract.unionRegistration || !Number.isInteger(entry.tag) ||
                    entry.tag < 0 || entry.tag > (contract.unionWideTags ? 65535 : 255) ||
                    plan.registeredByTag.has(entry.tag) || plan.registeredByType.has(identity(entry.type)) ||
                    plan.byId.has(identity(entry.type))) throw new WireError("Invalid registered LuminPack Union case.");
                if (plan.byHash.has(entry.tag) || declaredCases.some(item => item.tag === entry.tag))
                    throw new WireError("This LuminPack Union tag belongs to a generated case.");
                plan.registeredByTag.set(entry.tag, entry);
                plan.registeredByType.set(identity(entry.type), entry);
            }
            if (contract.kind === "Custom" || contract.profile.trackReferences || plan.registeredByTag.size !== 0)
                orderedJson = false;
            this.plans.set(type, plan);
            this.plans.set(identity((contract as Contract & { type?: string }).type ?? type), plan);
        }
        this.orderedJson = orderedJson;
        this.trackedJson = this.manifest.contracts.some(contract => contract.profile.trackReferences);
        this.rootWire = this.manifest.rootWire;
        this.root = this.rootWire ? undefined : this.plan(this.manifest.root);
        const profile = this.root?.contract.profile ?? this.manifest.rootProfile ??
            { binary: 2, jsonEncoding: this.manifest.jsonEncoding, jsonDepth: this.manifest.jsonDepth };
        this.binaryProfile = this.manifest.binaryProfile ?? profile.binary;
        if (!Number.isInteger(this.binaryProfile) || this.binaryProfile < 0 || this.binaryProfile > 5)
            throw new WireError("Invalid LuminPack binary string profile.");
        this.jsonEncoding = profile.jsonEncoding === 0 ? "utf8" : "utf16";
        this.limits = {
            maxInput: limit(profile.trusted ? undefined : profile.maxInput, options.limits?.maxInput, defaultLimits.maxInput),
            maxCollection: limit(profile.maxCollection, options.limits?.maxCollection, defaultLimits.maxCollection),
            maxString: limit(profile.maxString, options.limits?.maxString, defaultLimits.maxString),
            maxDepth: limit(profile.maxDepth, options.limits?.maxDepth, defaultLimits.maxDepth),
        };
        this.jsonDepth = limit(profile.jsonDepth || undefined, options.limits?.maxDepth, 1024);
    }

    private plan(type: string): Plan {
        const plan = this.plans.get(type) ?? this.plans.get(identity(type));
        if (!plan) throw new WireError("Unknown LuminPack type " + type + ".");
        return plan;
    }

    private custom(plan: Plan): CustomCodec {
        const type = identity(plan.contract.id);
        const codec = own(this.options.custom ?? {}, type) ? this.options.custom?.[type] : undefined;
        if (!codec) throw new WireError("Missing Custom LuminPack codec for " + type + ".");
        return codec;
    }

    readBinary(bytes: Uint8Array): T {
        const input = new BinaryReader(bytes, this.limits);
        const state = new ReadState();
        const value = this.rootWire ? this.readWire(this.rootWire, input, this.binaryProfile, state) :
            this.readContract(this.root!, input, this.binaryProfile, state);
        if (this.manifest.requireCompleteBinaryInput !== false) input.end();
        return value as T;
    }

    writeBinary(value: TInput): Uint8Array {
        const output = new BinaryWriter();
        const state = new WriteState();
        if (this.rootWire) this.writeWire(this.rootWire, output, value, this.binaryProfile, state);
        else this.writeContract(this.root!, output, value, this.binaryProfile, state);
        return output.finish();
    }

    readJson(bytes: Uint8Array, encoding: JsonEncoding = this.jsonEncoding): T {
        if (bytes.length > this.limits.maxInput) throw new WireError("LuminPack JSON exceeds its input limit.");
        const text = encoding === "utf8" ? decodeUtf8(bytes, true) : decodeUtf16(bytes);
        // The ordered attempt has no callbacks or reference registrations to replay.
        if (this.orderedJson) {
            const input = new OrderedJsonCursor(text, this.jsonDepth);
            try {
                const value = this.rootWire ? this.readOrderedJsonWire(this.rootWire, input, encoding === "utf8", 1) :
                    this.readOrderedJsonContract(this.root!, input, encoding === "utf8", 1);
                input.end();
                return value as T;
            } catch (error) {
                if (error !== orderedJsonMiss && !(error instanceof WireError)) throw error;
            }
        }
        const state = new ReadState();
        if (this.trackedJson) state.keyOrder = new WeakMap();
        const value = parseJson(text, this.jsonDepth, this.limits.maxCollection, state.keyOrder);
        return (this.rootWire ? this.readJsonWire(this.rootWire, value, encoding, state) :
            this.readJsonContract(this.root!, value, encoding, state)) as T;
    }

    writeJson(value: TInput, encoding: JsonEncoding = this.jsonEncoding): Uint8Array {
        const state = new WriteState();
        const parts: string[] = [];
        if (this.rootWire) this.writeJsonWire(this.rootWire, value, encoding, state, parts);
        else this.writeJsonContract(this.root!, value, encoding, state, parts);
        const text = parts.join("");
        if (encoding === "utf8") return encodeUtf8(text);
        const output = new BinaryWriter(text.length * 2);
        output.utf16(text, false);
        return output.finish();
    }

    private minWidth(wire: Wire, profile: number): number {
        switch (wire.kind) {
            case "scalar": return scalarWidth(wire.type, this.nativeBytes);
            case "string": return profile === 0 ? 8 : profile === 2 ? 4 : profile === 3 || profile === 4 ? 2 : 1;
            case "sequence": case "dictionary": return 4;
            case "pair": case "tuple": {
                const layout = this.aggregateStorage(wire, false);
                return layout ? this.standaloneModel(layout).stride :
                    wire.elements.reduce((width, item) => width + this.minWidth(item, profile), 0);
            }
            case "nullable": case "rectangular": return 1;
            case "contract": {
                const plan = this.plan(wire.type);
                if (plan.contract.kind === "Union") return 4;
                if (plan.contract.kind === "Custom") return this.custom(plan).minimumSize ?? 0;
                if (plan.contract.kind === "Struct") {
                    if (plan.contract.versioned) return 1;
                    const layout = this.contractStorage(plan.contract);
                    return layout ? this.standaloneModel(layout).stride : plan.contract.members.reduce((width, member) =>
                        width + this.minWidth(member.wire, useProfile(member.profile, profile)), 0);
                }
                return 1;
            }
            default: throw new WireError("Unsupported LuminPack wire shape.");
        }
    }

    private zero(wire: Wire): unknown {
        switch (wire.kind) {
            case "scalar": return defaultScalar(wire.type, this.nativeBytes);
            case "sequence": return wire.required || defaultMemory(wire.family) ? [] : null;
            case "pair": case "tuple": return wire.elements.map(item => this.zero(item));
            case "contract": {
                const plan = this.plan(wire.type);
                return plan.contract.kind === "Struct" ? this.defaults(plan) : null;
            }
            default: return null;
        }
    }

    private defaults(plan: Plan): Target {
        const result: Target = {};
        for (const member of plan.contract.members) put(result, member.name, this.zero(member.wire));
        return result;
    }

    private compileStorage(layout: Storage, wire: Wire, native: boolean): StoragePlan {
        const existing = this.storagePlans.get(layout);
        if (existing) return existing;
        const leaves: StorageLeaf[] = [];
        const collect = (shape: Storage, item: Wire, path: (string | number)[]): void => {
            if (item.kind === "scalar" && shape.fields.length === 0 && scalarWidth(item.type, this.nativeBytes) === shape.wireSize) {
                leaves.push({ path, type: item.type, width: shape.wireSize, read: fixedScalarRead(item.type, this.nativeBytes),
                    write: fixedScalarWrite(item.type, this.nativeBytes) });
                return;
            }
            let children: { key: string | number; wire: Wire }[];
            if (item.kind === "pair" || item.kind === "tuple") {
                children = item.elements.map((element, key) => ({ key, wire: element }));
            } else if (item.kind === "contract") {
                const members = this.plan(item.type).contract.members;
                children = shape.fields.map(field => {
                    const member = members.find(part => part.name === field.name);
                    if (!member) throw new WireError("Unknown LuminPack storage member.");
                    return { key: member.name, wire: member.wire };
                });
            } else throw new WireError("Unsupported LuminPack storage leaf.");
            if (children.length !== shape.fields.length || children.length === 0)
                throw new WireError("Invalid LuminPack storage structure.");
            for (let index = 0; index < children.length; index++)
                collect(shape.fields[index].storage, children[index].wire, [...path, children[index].key]);
        };
        collect(layout, wire, []);
        const order = leaves.map((_, index) => index);
        const offsets: number[] = [];
        let packedWidth = 0;
        for (const leaf of leaves) {
            offsets.push(packedWidth);
            packedWidth += leaf.width;
        }
        if (packedWidth !== layout.wireSize || layout.modelSizes.length !== layout.modelOffsets.length ||
            ((layout.tagged || native) && layout.modelSizes.length === 0))
            throw new WireError("Invalid LuminPack storage width.");
        const models = layout.modelSizes.map((stride, index) => {
            const byteOffsets = layout.modelOffsets[index];
            if (byteOffsets.length !== packedWidth || !Number.isInteger(stride) ||
                byteOffsets.some(value => !Number.isInteger(value) || value < 0 || value >= stride) ||
                new Set(byteOffsets).size !== packedWidth)
                throw new WireError("Invalid LuminPack storage model.");
            const modelOffsets = offsets.map((position, field) => {
                const first = byteOffsets[position];
                for (let index = 1; index < leaves[field].width; index++)
                    if (byteOffsets[position + index] !== first + index)
                        throw new WireError("Invalid LuminPack scalar storage offsets.");
                return first;
            });
            const wireOrder = [...order].sort((a, b) => modelOffsets[a] - modelOffsets[b]);
            const nativeKey = native && stride > packedWidth
                ? `${stride}:${wireOrder.map(field => `${modelOffsets[field]}+${leaves[field].width}`).join(",")}` : undefined;
            return { stride, offsets: modelOffsets, order: wireOrder, nativeKey };
        });
        const plan = { packed: { stride: packedWidth, offsets, order }, models, leaves,
            array: wire.kind === "pair" || wire.kind === "tuple", native, tagged: layout.tagged };
        this.storagePlans.set(layout, plan);
        return plan;
    }

    private contractStorage(contract: Contract): StoragePlan | undefined {
        const proof = contract.storage ?? contract.nativeStorage;
        return proof ? this.compileStorage(proof, { kind: "contract", type: contract.id }, !!contract.nativeStorage) : undefined;
    }

    private aggregateStorage(wire: Extract<Wire, { kind: "pair" | "tuple" }>, collection: boolean): StoragePlan | undefined {
        if (wire.layout) return this.compileStorage(wire.layout, wire, false);
        if (wire.rawCompoundStorage && (wire.raw || collection)) return this.compileStorage(wire.rawCompoundStorage, wire, true);
        return undefined;
    }

    private standaloneModel(plan: StoragePlan): StorageModel {
        return plan.native ? plan.models[this.nativeBytes === 4 && plan.models.length > 1 ? 1 : 0] : plan.packed;
    }

    private readStorage(input: BinaryReader, plan: StoragePlan, model: StorageModel): object {
        return this.readStorageAt(input, input.reserve(model.stride), plan, model);
    }

    private readStorageAt(input: BinaryReader, offset: number, plan: StoragePlan, model: StorageModel,
        nativeRow?: Uint8Array): object {
        const result = plan.array ? [] : {};
        for (const index of model.order) {
            const leaf = plan.leaves[index];
            storageSet(result, leaf.path, leaf.read(input.view, offset + model.offsets[index]));
        }
        if (plan.native && model.stride > plan.packed.stride) {
            const bytes = nativeRow ?? new Uint8Array(input.bytes.subarray(offset, offset + model.stride));
            this.nativeRows.set(result, { model, bytes });
        }
        return result;
    }

    private writeStorage(output: BinaryWriter, value: unknown, plan: StoragePlan, model: StorageModel): void {
        const bytes = output.writable(model.stride);
        const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        this.writeStorageAt(bytes, view, 0, value, plan, model);
    }

    private writeStorageAt(bytes: Uint8Array, view: DataView, offset: number, value: unknown,
        plan: StoragePlan, model: StorageModel): void {
        const data = plan.array ? sequence(value, this.limits.maxCollection) : object(value);
        if (model.stride > plan.packed.stride) {
            const previous = plan.native ? this.nativeRows.get(data) : undefined;
            if (previous && previous.model.nativeKey === model.nativeKey) bytes.set(previous.bytes, offset);
            else bytes.fill(0, offset, offset + model.stride);
        }
        for (const index of model.order) {
            const leaf = plan.leaves[index];
            let current: unknown = data;
            for (const key of leaf.path) {
                if (current === undefined) break;
                if (current === null || typeof current !== "object") throw new WireError("Expected a LuminPack storage value.");
                current = storageField(current, key);
            }
            leaf.write(view, offset + model.offsets[index], current ?? defaultScalar(leaf.type, this.nativeBytes));
        }
    }

    private nullableStorage(wire: Extract<Wire, { kind: "nullable" }>): NullableModel[] {
        const { element, nullableLayout, nullableEncoding } = wire;
        if (!nullableLayout || element.kind !== "scalar") throw new WireError("Invalid LuminPack nullable storage.");
        const width = scalarWidth(element.type, this.nativeBytes);
        let models = this.nullableModels.get(nullableLayout);
        if (!models) {
            const read = fixedScalarRead(element.type, this.nativeBytes);
            models = nullableLayout.modelSizes.map((stride, index) => {
                const offsets = nullableLayout.modelOffsets[index];
                if (!offsets || offsets.length !== width + 1 || new Set(offsets).size !== offsets.length ||
                    offsets.some(offset => !Number.isInteger(offset) || offset < 0 || offset >= stride) ||
                    offsets.slice(1).some((offset, index) => offset !== offsets[1] + index))
                    throw new WireError("Invalid LuminPack nullable storage model.");
                return { stride, presence: offsets[0], payload: offsets[1], read };
            });
            if (models.length === 0) throw new WireError("Missing LuminPack nullable storage models.");
            this.nullableModels.set(nullableLayout, models);
        }
        if (nullableEncoding === 2) {
            const alignment = Math.min(width, this.nativeScalarAlignment);
            const model = models.find(model => model.presence === 0 && model.payload === alignment &&
                model.stride === Math.ceil((alignment + width) / alignment) * alignment);
            if (models.length !== 1 && !model) throw new WireError("No LuminPack nullable storage for this ABI.");
            return [model ?? models[0]];
        }
        return models;
    }

    private readNullableStorage(input: BinaryReader, model: NullableModel, wire: Extract<Wire, { kind: "nullable" }>): unknown {
        return this.readNullableStorageAt(input, input.reserve(model.stride), model, wire);
    }

    private readNullableStorageAt(input: BinaryReader, offset: number, model: NullableModel,
        wire: Extract<Wire, { kind: "nullable" }>): unknown {
        const flag = input.bytes[offset + model.presence];
        const type = (wire.element as Extract<Wire, { kind: "scalar" }>).type;
        if (flag > 1 || (type === "bool" && input.bytes[offset + model.payload] > 1))
            throw new WireError("Invalid LuminPack nullable storage flag.");
        if (flag === 0) return null;
        return model.read(input.view, offset + model.payload);
    }

    private writeNullableStorage(output: BinaryWriter, value: unknown, model: NullableModel,
        wire: Extract<Wire, { kind: "nullable" }>): void {
        if (value === null) { output.clear(model.stride); return; }
        const start = output.position;
        const type = (wire.element as Extract<Wire, { kind: "scalar" }>).type;
        if (model.presence < model.payload) {
            output.clear(model.presence);
            output.u8(1);
            output.clear(model.payload - model.presence - 1);
            writeScalar(output, type, value, this.nativeBytes);
        } else {
            output.clear(model.payload);
            writeScalar(output, type, value, this.nativeBytes);
            output.clear(model.presence - (output.position - start));
            output.u8(1);
        }
        output.clear(model.stride - (output.position - start));
    }

    private sequenceStorage(wire: Wire): StoragePlan | undefined {
        if (wire.kind === "contract") return this.contractStorage(this.plan(wire.type).contract);
        if (wire.kind === "pair" || wire.kind === "tuple") return this.aggregateStorage(wire, true);
        return undefined;
    }

    private readLayout<TModel>(input: BinaryReader, models: TModel[]): TModel {
        if (models.length === 1) return models[0];
        const tag = input.u8();
        if (tag === 0 || tag > models.length) throw new WireError("Invalid LuminPack storage tag.");
        return models[tag - 1];
    }

    private readWire(wire: Wire, input: BinaryReader, profile: number, state: ReadState, fixedWidth = 0): unknown {
        switch (wire.kind) {
            case "scalar": return readScalar(input, wire.type, this.nativeBytes);
            case "string": return readString(input, profile);
            case "contract": return this.readContract(this.plan(wire.type), input, profile, state);
            case "pair": case "tuple": {
                const plan = this.aggregateStorage(wire, false);
                return plan ? this.readStorage(input, plan, this.standaloneModel(plan)) :
                    wire.elements.map(item => this.readWire(item, input, profile, state));
            }
            case "nullable": {
                const marker = input.u8();
                if (marker === 0) return this.readWire(wire.element, input, profile, state);
                if (marker !== 255) throw new WireError("Invalid LuminPack nullable marker.");
                if (fixedWidth !== 0) input.take(fixedWidth - 1);
                return null;
            }
            case "sequence": {
                const elements = this.readSequence(wire.element, input, profile, state, undefined, this.segmentedElements(wire));
                return elements ?? nullSequence(wire);
            }
            case "dictionary": {
                const count = input.count(this.minWidth(wire.key, profile) + this.minWidth(wire.value, profile));
                if (count === null) return null;
                const dictionary = new Map<unknown, unknown>();
                const nullable = wire.value.kind === "nullable" && wire.value.nativeNullableElement ? wire.value : undefined;
                const model = nullable ? this.nullableStorage(nullable)[0] : undefined;
                for (let index = 0; index < count; index++) {
                    const key = this.readWire(wire.key, input, profile, state);
                    if (key === null || dictionary.has(key)) throw new WireError("Invalid or duplicate LuminPack dictionary key.");
                    dictionary.set(key, nullable && model ? this.readNullableStorage(input, model, nullable) :
                        this.readWire(wire.value, input, profile, state));
                }
                return dictionary;
            }
            case "rectangular": {
                if (input.u8() === 255) return null;
                const lengths = Array.from({ length: wire.rank }, () => input.i32());
                const count = rectangularSize(lengths, input.limits.maxCollection);
                const values = this.readSequence(wire.element, input, profile, state, count);
                return { lengths, lowerBounds: Array(wire.rank).fill(0), values };
            }
            default: throw new WireError("Unsupported LuminPack wire shape.");
        }
    }

    private readElements(wire: Wire, count: number, input: BinaryReader, profile: number, state: ReadState): unknown[] {
        if (wire.kind === "scalar") return readScalars(input, count, wire.type, this.nativeBytes);
        const values: unknown[] = [];
        if (wire.kind === "contract") {
            const plan = this.plan(wire.type);
            for (let index = 0; index < count; index++) values.push(this.readContract(plan, input, profile, state));
        } else {
            for (let index = 0; index < count; index++) values.push(this.readWire(wire, input, profile, state));
        }
        return values;
    }

    private segmentedElements(wire: Extract<Wire, { kind: "sequence" }>): boolean {
        return wire.family === "System.Buffers.ReadOnlySequence" && (!wire.element.rawCollection || !!wire.element.layout);
    }

    private primitiveCollectionValues(element: Wire, value: unknown, max: number): ArrayLike<unknown> & Iterable<unknown> {
        const typed = element.kind === "scalar" ? scalarCollectionView(value, element.type, this.nativeBytes) : undefined;
        if (typed && typed.length <= max) return typed;
        return sequence(value, max);
    }

    private readSequence(wire: Wire, input: BinaryReader, profile: number, state: ReadState, fixed?: number,
        segmented = false): unknown[] | null {
        const plan = segmented ? undefined : this.sequenceStorage(wire);
        const nullable = wire.kind === "nullable" && wire.nullableEncoding && (!segmented || wire.nativeNullableElement) ? wire : undefined;
        const count = input.count(plan || nullable ? 0 : this.minWidth(wire, profile));
        if (fixed !== undefined && count !== fixed)
            throw new WireError("LuminPack rectangular dimensions differ from the element count.");
        if (count === null) return null;
        if (count === 0) return [];
        const values: unknown[] = [];
        if (nullable) {
            const models = this.nullableStorage(nullable);
            const model = segmented ? models[0] : this.readLayout(input, models);
            let offset = input.reserve(count * model.stride);
            for (let index = 0; index < count; index++, offset += model.stride)
                values.push(this.readNullableStorageAt(input, offset, model, nullable));
        } else if (plan) {
            const model = plan.tagged ? this.readLayout(input, plan.models) : this.standaloneModel(plan);
            const width = count * model.stride;
            const offset = input.reserve(width);
            const owned = plan.native && model.stride > plan.packed.stride
                ? new Uint8Array(input.bytes.subarray(offset, offset + width)) : undefined;
            for (let row = 0; row < width; row += model.stride)
                values.push(this.readStorageAt(input, offset + row, plan, model, owned?.subarray(row, row + model.stride)));
        } else return this.readElements(wire, count, input, profile, state);
        return values;
    }

    private writeWire(wire: Wire, output: BinaryWriter, value: unknown, profile: number, state: WriteState, fixedWidth = 0): void {
        switch (wire.kind) {
            case "scalar": writeScalar(output, wire.type, value, this.nativeBytes); return;
            case "string": writeString(output, value as string | null, profile); return;
            case "contract": this.writeContract(this.plan(wire.type), output, value, profile, state); return;
            case "pair": case "tuple": {
                const values = sequence(value, wire.elements.length);
                if (values.length !== wire.elements.length) throw new WireError("Incorrect LuminPack tuple length.");
                const plan = this.aggregateStorage(wire, false);
                if (plan) this.writeStorage(output, values, plan, this.standaloneModel(plan));
                else wire.elements.forEach((item, index) => this.writeWire(item, output, values[index], profile, state));
                return;
            }
            case "nullable":
                output.u8(value === null ? 255 : 0);
                if (value !== null) this.writeWire(wire.element, output, value, profile, state);
                else for (let index = 1; index < fixedWidth; index++) output.u8(0);
                return;
            case "sequence": {
                if (value === null && nullSequence(wire) === null) { output.i32(-1); return; }
                const elements = this.primitiveCollectionValues(wire.element, value ?? [], this.limits.maxCollection);
                if (Array.isArray(elements)) {
                    this.writeElements(wire.element, output, elements, profile, state, this.segmentedElements(wire));
                } else if (littleEndian && ArrayBuffer.isView(elements)) {
                    output.appendCountedBlock(elements.length, new Uint8Array(elements.buffer, elements.byteOffset, elements.byteLength));
                } else this.writeElements(wire.element, output, Array.from(elements), profile, state);
                return;
            }
            case "dictionary": {
                if (value === null) { output.i32(-1); return; }
                if (!(value instanceof Map) || value.size > this.limits.maxCollection)
                    throw new WireError("Expected a LuminPack dictionary Map.");
                output.i32(value.size);
                const nullable = wire.value.kind === "nullable" && wire.value.nativeNullableElement ? wire.value : undefined;
                const model = nullable ? this.nullableStorage(nullable)[0] : undefined;
                for (const [key, item] of value) {
                    if (key === null) throw new WireError("Null LuminPack dictionary key.");
                    this.writeWire(wire.key, output, key, profile, state);
                    if (nullable && model) this.writeNullableStorage(output, item, model, nullable);
                    else this.writeWire(wire.value, output, item, profile, state);
                }
                return;
            }
            case "rectangular": {
                if (value === null) { output.u8(255); return; }
                const rect = object(value);
                const lengths = dimensions(rect.lengths, wire.rank);
                const lower = dimensions(rect.lowerBounds, wire.rank);
                const elements = sequence(rect.values, this.limits.maxCollection);
                if (lower.some(bound => bound !== 0) || rectangularSize(lengths, this.limits.maxCollection) !== elements.length)
                    throw new WireError("Invalid LuminPack binary rectangular array.");
                output.u8(0);
                for (const length of lengths) output.i32(length);
                this.writeElements(wire.element, output, elements, profile, state);
                return;
            }
            default: throw new WireError("Unsupported LuminPack wire shape.");
        }
    }

    private writeElements(wire: Wire, output: BinaryWriter, elements: unknown[], profile: number, state: WriteState,
        segmented = false): void {
        output.i32(elements.length);
        if (!elements.length) return;
        if (wire.kind === "scalar") { writeScalars(output, elements, wire.type, this.nativeBytes); return; }
        if (wire.kind === "nullable" && wire.nullableEncoding && (!segmented || wire.nativeNullableElement)) {
            const models = this.nullableStorage(wire);
            if (!segmented && models.length > 1) output.u8(1);
            for (const item of elements) this.writeNullableStorage(output, item, models[0], wire);
            return;
        }
        const plan = segmented ? undefined : this.sequenceStorage(wire);
        if (plan) {
            if (plan.tagged) output.u8(1);
            const model = plan.tagged ? plan.models[0] : this.standaloneModel(plan);
            const bytes = output.writable(elements.length * model.stride);
            const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
            let offset = 0;
            for (const item of elements) {
                this.writeStorageAt(bytes, view, offset, item, plan, model);
                offset += model.stride;
            }
            return;
        }
        for (const item of elements) this.writeWire(wire, output, item, profile, state);
    }

    private readContract(plan: Plan, input: BinaryReader, inherited: number, state: ReadState, caseBody = false): unknown {
        if (state.depth++ >= this.limits.maxDepth) throw new WireError("LuminPack record nesting exceeds its limit.");
        try {
            const { contract } = plan;
            const profile = useProfile(contract.profile, inherited);
            if (contract.sequenceWire) return this.readWire(contract.sequenceWire, input, profile, state);
            if (contract.kind === "Custom") return this.custom(plan).readBinary(input);
            if (contract.kind === "Union") return this.readUnion(plan, input, profile, state);
            if (contract.kind !== "Record" && contract.kind !== "Struct") throw new WireError("Unsupported LuminPack contract kind.");
            const storage = this.contractStorage(contract);
            if (storage) return this.readStorage(input, storage, this.standaloneModel(storage));
            const tracked = contract.profile.trackReferences === true;
            const result: Target = {};
            if (tracked) {
                const marker = input.u8();
                if (marker === 255) return null;
                const id = readReferenceNumber(input);
                if (marker === 250) return this.lookupReference(state, id, contract.id);
                if (marker > 249 || (!contract.sequenceBase && input.peek() === 255) || state.references.has(id))
                    throw new WireError("Invalid LuminPack reference definition.");
                state.references.set(id, { type: identity(contract.id), value: result });
            }
            if (contract.versioned) return this.readVersion(plan, input, profile, state, result);
            if (contract.kind === "Record" && !contract.sequenceBase && (!caseBody || tracked)) {
                const marker = input.u8();
                if (marker === 255) return null;
                if (marker !== (contract.binaryMemberCount ?? contract.members.length) || marker > 249)
                    throw new WireError("Invalid LuminPack positional member count.");
            }
            for (const member of contract.members) put(result, member.name, this.readMember(member, input, profile, state));
            return result;
        } finally { state.depth--; }
    }

    private writeContract(plan: Plan, output: BinaryWriter, value: unknown, inherited: number, state: WriteState, caseBody = false): void {
        if (state.depth++ >= this.limits.maxDepth) throw new WireError("LuminPack record nesting exceeds its limit.");
        try {
            const { contract } = plan;
            const profile = useProfile(contract.profile, inherited);
            if (contract.sequenceWire) return this.writeWire(contract.sequenceWire, output, value, profile, state);
            if (contract.kind === "Custom") { this.custom(plan).writeBinary(output, value); return; }
            if (contract.kind === "Union") { this.writeUnion(plan, output, value, profile, state); return; }
            if (contract.kind !== "Record" && contract.kind !== "Struct") throw new WireError("Unsupported LuminPack contract kind.");
            const storage = this.contractStorage(contract);
            if (storage) { this.writeStorage(output, value, storage, this.standaloneModel(storage)); return; }
            if (value === null && contract.kind !== "Struct" && (!caseBody || contract.versioned || contract.profile.trackReferences)) {
                output.u8(255);
                return;
            }
            const record = object(value);
            if ((contract.binaryMemberCount ?? contract.members.length) > 249) throw new WireError("Too many LuminPack record members.");
            const tracked = contract.profile.trackReferences === true;
            if (tracked) {
                const reference = this.referenceIndex(state, record, contract.id);
                output.u8(reference.existing ? 250 : (contract.binaryMemberCount ?? contract.members.length));
                writeReferenceNumber(output, reference.id);
                if (reference.existing) return;
            }
            if (contract.versioned) { this.writeVersion(plan, output, record, profile, state); return; }
            if (contract.kind === "Record" && !contract.sequenceBase && (!caseBody || tracked)) output.u8(contract.binaryMemberCount ?? contract.members.length);
            for (const member of contract.members) this.writeMember(member, output, this.field(record, member), profile, state);
        } finally { state.depth--; }
    }

    private field(record: Target, member: Member): unknown {
        return own(record, member.name) ? record[member.name] : this.zero(member.wire);
    }

    private readMember(member: Member, input: BinaryReader, inherited: number, state: ReadState, width = 0): unknown {
        const profile = useProfile(member.profile, inherited);
        if (member.compressed) return this.readCompressed(member.wire, input, profile, state);
        return this.readWire(member.wire, input, profile, state, member.fixedNullable ? width : 0);
    }

    private writeMember(member: Member, output: BinaryWriter, value: unknown, inherited: number, state: WriteState, width = 0): void {
        const profile = useProfile(member.profile, inherited);
        if (member.compressed) this.writeCompressed(member.wire, output, value, profile, state);
        else this.writeWire(member.wire, output, value, profile, state, member.fixedNullable ? width : 0);
    }

    private compressedWidth(wire: Wire, profile: number): number {
        if (wire.kind === "scalar") return scalarWidth(wire.type, this.nativeBytes);
        if (wire.kind === "contract") {
            const { contract } = this.plan(wire.type);
            if (contract.kind === "Struct" && contract.storageKind === "RawUnmanaged" && !contract.versioned)
                return this.minWidth(wire, profile);
        }
        throw new WireError("Unsupported LuminPack compressed element.");
    }

    private readCompressed(wire: Wire, input: BinaryReader, profile: number, state: ReadState): unknown {
        if (wire.kind !== "sequence") throw new WireError("Expected a LuminPack compressed sequence.");
        const nested = wire.element.kind === "sequence";
        const count = input.count(nested ? 4 : 0);
        if (count === null) return null;
        if (count === 0) return [];
        if (nested) {
            const values = [];
            for (let index = 0; index < count; index++) values.push(this.readCompressed(wire.element, input, profile, state));
            return values;
        }
        const width = this.compressedWidth(wire.element, profile);
        if (width === 0 || count > Math.floor(this.limits.maxInput / width))
            throw new WireError("LuminPack compressed field exceeds its decompressed size limit.");
        const frame = input.take(input.i32());
        let bytes: Uint8Array;
        try { bytes = decompressLum2(frame, count * width); }
        catch { throw new WireError("Invalid LuminPack compressed field."); }
        const payload = new BinaryReader(bytes, this.limits);
        const values = this.readElements(wire.element, count, payload, profile, state);
        payload.end();
        return values;
    }

    private writeCompressed(wire: Wire, output: BinaryWriter, value: unknown, profile: number, state: WriteState): void {
        if (wire.kind !== "sequence") throw new WireError("Expected a LuminPack compressed sequence.");
        if (value === null) { output.i32(-1); return; }
        const elements = this.primitiveCollectionValues(wire.element, value, this.limits.maxCollection);
        output.i32(elements.length);
        if (!elements.length) return;
        if (wire.element.kind === "sequence") {
            for (const row of elements) this.writeCompressed(wire.element, output, row, profile, state);
            return;
        }
        const width = this.compressedWidth(wire.element, profile);
        if (width === 0 || elements.length > Math.floor(this.limits.maxInput / width))
            throw new WireError("LuminPack compressed field exceeds its decompressed size limit.");
        let storage: Uint8Array;
        if (littleEndian && ArrayBuffer.isView(elements)) {
            storage = new Uint8Array(elements.buffer, elements.byteOffset, elements.byteLength);
        } else {
            const raw = new BinaryWriter(width * elements.length);
            if (wire.element.kind === "scalar")
                writeScalars(raw, Array.isArray(elements) ? elements : Array.from(elements), wire.element.type, this.nativeBytes);
            else for (const item of elements) this.writeWire(wire.element, raw, item, profile, state);
            storage = raw.finish();
        }
        if (storage.length !== width * elements.length) throw new WireError("Invalid LuminPack compressed storage width.");
        const frame = compressLum2(storage);
        output.i32(frame.length);
        output.append(frame);
    }

    private lookupReference(state: ReadState, id: number, type: string): Target {
        const reference = state.references.get(id);
        if (!reference || reference.type !== identity(type)) throw new WireError("Unknown or incompatible LuminPack reference.");
        return reference.value;
    }

    private referenceIndex(state: WriteState, value: Target, type: string): { id: number; existing: boolean } {
        const previous = state.references.get(value);
        if (previous) {
            if (previous.type !== identity(type)) throw new WireError("Incompatible LuminPack reference type.");
            return { id: previous.id, existing: true };
        }
        const id = state.references.size;
        if (id > 0x7fff_ffff) throw new WireError("Too many LuminPack references.");
        state.references.set(value, { id, type: identity(type) });
        return { id, existing: false };
    }

    private readUnion(plan: Plan, input: BinaryReader, profile: number, state: ReadState): UnionValue | null {
        const hash = input.u32();
        if (hash === 0) return null;
        if (hash === 0xffff_ffff && plan.contract.unionRegistration) {
            const header = input.u8();
            const tag = !plan.contract.unionWideTags ? header : header === 250 ? input.u16() : header > 250 ? 0 : header;
            const entry = plan.registeredByTag.get(tag);
            if (!entry) throw new WireError("Unknown registered LuminPack Union tag.");
            return { type: identity(entry.type), value: entry.codec.readBinary(input) };
        }
        const type = plan.byHash.get(hash);
        if (!type) throw new WireError("Unknown LuminPack Union case hash.");
        const value = this.readContract(this.plan(type), input, profile, state, true);
        return value === null ? null : { type, value };
    }

    private writeUnion(plan: Plan, output: BinaryWriter, value: unknown, profile: number, state: WriteState): void {
        if (value === null) { output.i32(0); return; }
        const entry = object(value);
        if (typeof entry.type !== "string" || entry.value == null) throw new WireError("Invalid LuminPack Union value.");
        const type = identity(entry.type);
        const hash = plan.byId.get(type);
        if (hash !== undefined) {
            output.i32(hash);
            this.writeContract(this.plan(type), output, entry.value, profile, state, true);
            return;
        }
        const registered = plan.registeredByType.get(type);
        if (!registered) throw new WireError("Unknown LuminPack Union case.");
        output.i32(-1);
        if (plan.contract.unionWideTags && registered.tag >= 250) {
            output.u8(250);
            output.u16(registered.tag);
        } else output.u8(registered.tag);
        registered.codec.writeBinary(output, entry.value);
    }

    private descriptors(plan: Plan): bigint[] {
        if (!plan.descriptors) {
            plan.descriptors = plan.contract.members.map(member => {
                if (typeof member.descriptor !== "string" || !/^[0-9a-f]{16}$/i.test(member.descriptor))
                    throw new WireError("Missing concrete LuminPack Version descriptor.");
                return BigInt("0x" + member.descriptor);
            });
            plan.versionHash = schemaHash(plan.descriptors);
        }
        return plan.descriptors;
    }

    private readVersion(plan: Plan, input: BinaryReader, profile: number, state: ReadState, target: Target): Target | null {
        const marker = input.u8();
        if (marker === 255) return null;
        if (marker !== 251) throw new WireError("Invalid LuminPack Version marker.");
        const form = input.u8();
        const members = plan.contract.members;
        if (form === 1) return this.readLegacy(members, input, profile, state, target);
        const incoming = state.version.read(input, profile, form);
        const expected = this.descriptors(plan);
        const slots = incoming.map(descriptor => members.findIndex((member, index) =>
            nameKey(expected[index]) === nameKey(descriptor) || member.nameKeys?.includes(nameKey(descriptor))));
        const seen = new Set<number>();
        for (let index = 0; index < incoming.length; index++) {
            const slot = slots[index];
            if (slot < 0) continue;
            if (seen.has(slot) || typeKey(incoming[index]) !== typeKey(expected[slot]) || widthCode(incoming[index]) !== widthCode(expected[slot]))
                throw new WireError("Invalid LuminPack Version member contract.");
            seen.add(slot);
        }
        for (let index = 0; index < incoming.length; index++) {
            const slot = slots[index];
            const width = widthCode(incoming[index]);
            if (slot < 0) {
                if (width === 254) readString(input, profile, true);
                else input.take(width === 255 ? input.u32() : width);
                continue;
            }
            const member = members[slot];
            const versionSize = state.version.size;
            const bounded = width === 254 ? input : input.subrecord(width === 255 ? input.u32() : width);
            try {
                const value = this.readMember(member, bounded, profile, state, width);
                if (width !== 254) bounded.end();
                put(target, member.name, value);
            } finally { if (width === 255) state.version.truncate(versionSize); }
        }
        for (let index = 0; index < members.length; index++) {
            if (!seen.has(index)) put(target, members[index].name, this.zero(members[index].wire));
        }
        return target;
    }

    private readLegacy(members: Member[], input: BinaryReader, profile: number, state: ReadState, target: Target): Target {
        if (input.u8() !== profile) throw new WireError("Invalid LuminPack legacy Version profile.");
        const count = input.u8();
        if (count > 249) throw new WireError("Invalid LuminPack legacy Version member count.");
        const slots: number[] = [];
        const seen = new Set<number>();
        for (let index = 0; index < count; index++) {
            const name = readLegacyMetadata(input);
            const contract = readLegacyMetadata(input);
            const slot = members.findIndex(member => member.name === name || member.oldNames?.includes(name));
            if (slot >= 0) {
                if (seen.has(slot) || members[slot].legacyVersionContract !== contract)
                    throw new WireError("Invalid LuminPack legacy Version member contract.");
                seen.add(slot);
            }
            slots.push(slot);
        }
        const lengths = Array.from({ length: count }, () => readReferenceNumber(input));
        if (lengths.reduce((sum, length) => sum + length, 0) > input.remaining)
            throw new WireError("Truncated LuminPack legacy Version members.");
        for (let index = 0; index < count; index++) {
            const bounded = input.subrecord(lengths[index]);
            const member = members[slots[index]];
            if (member) {
                const size = state.version.size;
                try {
                    put(target, member.name, this.readMember(member, bounded, profile, state));
                    bounded.end();
                } finally { state.version.truncate(size); }
            }
        }
        for (let index = 0; index < members.length; index++) {
            if (!seen.has(index)) put(target, members[index].name, this.zero(members[index].wire));
        }
        return target;
    }

    private writeVersion(plan: Plan, output: BinaryWriter, record: Target, profile: number, state: WriteState): void {
        const descriptors = this.descriptors(plan);
        state.version.write(output, profile, descriptors, plan.versionHash!);
        for (let index = 0; index < descriptors.length; index++) {
            const member = plan.contract.members[index];
            const width = widthCode(descriptors[index]);
            const start = output.position;
            const versionSize = state.version.size;
            if (width === 255) output.i32(0);
            try { this.writeMember(member, output, this.field(record, member), profile, state, width); }
            finally { if (width === 255) state.version.truncate(versionSize); }
            if (width === 255) output.patchI32(start, output.position - start - 4);
            else if (width <= 250 && output.position - start !== width)
                throw new WireError("Invalid fixed LuminPack Version member width.");
        }
    }

    private jsonPlan(type: string): Plan {
        return this.plans.get(type) ?? this.plan(type);
    }

    private readOrderedJsonContract(plan: Plan, input: OrderedJsonCursor, utf8: boolean, depth: number): unknown {
        const { contract, jsonHeads } = plan;
        if (contract.sequenceWire) return this.readOrderedJsonWire(contract.sequenceWire, input, utf8, depth);
        if (contract.kind !== "Struct" && input.consume("null")) return null;
        input.depth(depth);
        if (contract.kind === "Union") {
            input.expect('{"$type":');
            const discriminator = input.number();
            if (!/^\d+$/.test(discriminator)) throw orderedJsonMiss;
            const type = plan.byHash.get(Number(discriminator));
            if (!type) throw orderedJsonMiss;
            input.expect(',"$value":');
            const value = this.readOrderedJsonContract(this.jsonPlan(type), input, utf8, depth + 1);
            input.expect("}");
            return value === null ? null : { type, value };
        }
        const value: Target = {};
        if (jsonHeads.length === 0) input.expect("{");
        for (let index = 0; index < jsonHeads.length; index++) {
            const { member, head, next } = jsonHeads[index];
            input.expect(index === 0 ? head : next);
            put(value, member.name, this.readOrderedJsonWire(member.wire, input, utf8, depth + 1));
        }
        input.expect("}");
        return value;
    }

    private readOrderedJsonWire(wire: Wire, input: OrderedJsonCursor, utf8: boolean, depth: number): unknown {
        const maximum = this.limits.maxCollection;
        switch (wire.kind) {
            case "scalar":
                if (wire.type === "int") return input.int32();
                if (wire.type === "char") return readJsonScalar(input.string(), "char", this.nativeBytes, utf8);
                if (wire.type === "bool") {
                    if (input.consume("true")) return true;
                    input.expect("false");
                    return false;
                }
                return readJsonNumericScalar(input.number(), wire.type, this.nativeBytes);
            case "string": {
                if (input.consume("null")) return null;
                const value = input.string();
                if (value.length > this.limits.maxString) throw orderedJsonMiss;
                return value;
            }
            case "contract": return this.readOrderedJsonContract(this.jsonPlan(wire.type), input, utf8, depth);
            case "nullable": return input.consume("null") ? null : this.readOrderedJsonWire(wire.element, input, utf8, depth);
            case "sequence": {
                if (input.consume("null")) return nullSequence(wire);
                input.depth(depth);
                input.expect("[");
                const values: unknown[] = [];
                if (input.consume("]")) return values;
                do {
                    if (values.length === maximum) throw orderedJsonMiss;
                    values.push(this.readOrderedJsonWire(wire.element, input, utf8, depth + 1));
                } while (input.consume(","));
                input.expect("]");
                return values;
            }
            case "pair": case "tuple": {
                input.depth(depth);
                if (wire.elements.length > maximum) throw orderedJsonMiss;
                input.expect("[");
                const values = [];
                for (const element of wire.elements) {
                    if (values.length !== 0) input.expect(",");
                    values.push(this.readOrderedJsonWire(element, input, utf8, depth + 1));
                }
                input.expect("]");
                return values;
            }
            case "dictionary": {
                if (input.consume("null")) return null;
                input.depth(depth);
                if (wire.key.kind === "string" && input.consume("{")) {
                    const values: Target = {};
                    let count = 0;
                    if (input.consume("}")) return new Map();
                    do {
                        const key = input.string();
                        if (key.length > this.limits.maxString || (!own(values, key) && ++count > maximum))
                            throw orderedJsonMiss;
                        input.expect(":");
                        put(values, key, this.readOrderedJsonWire(wire.value, input, utf8, depth + 1));
                    } while (input.consume(","));
                    input.expect("}");
                    return new Map(Object.entries(values));
                }
                const values = new Map();
                input.expect("[");
                if (input.consume("]")) return values;
                let count = 0;
                do {
                    if (++count > maximum || maximum < 2) throw orderedJsonMiss;
                    input.depth(depth + 1);
                    input.expect("[");
                    const key = this.readOrderedJsonWire(wire.key, input, utf8, depth + 2);
                    if (key === null) throw orderedJsonMiss;
                    input.expect(",");
                    values.set(key, this.readOrderedJsonWire(wire.value, input, utf8, depth + 2));
                    input.expect("]");
                } while (input.consume(","));
                input.expect("]");
                return values;
            }
            default: throw orderedJsonMiss;
        }
    }

    private readJsonWire(wire: Wire, value: JsonValue, encoding: JsonEncoding, state: ReadState): unknown {
        switch (wire.kind) {
            case "scalar": return readJsonScalar(value, wire.type, this.nativeBytes, encoding === "utf8");
            case "string":
                if (value !== null && (typeof value !== "string" || value.length > this.limits.maxString))
                    throw new WireError("Invalid LuminPack JSON string.");
                return value;
            case "contract": return this.readJsonContract(this.plan(wire.type), value, encoding, state);
            case "nullable": return value === null ? null : this.readJsonWire(wire.element, value, encoding, state);
            case "sequence": return value === null ? nullSequence(wire) :
                jsonArray(value, this.limits.maxCollection).map(item => this.readJsonWire(wire.element, item, encoding, state));
            case "pair": case "tuple": {
                const values = jsonArray(value, wire.elements.length);
                if (values.length !== wire.elements.length) throw new WireError("Incorrect LuminPack JSON tuple length.");
                return wire.elements.map((item, index) => this.readJsonWire(item, values[index], encoding, state));
            }
            case "dictionary": {
                if (value === null) return null;
                const dictionary = new Map<unknown, unknown>();
                if (wire.key.kind === "string" && !Array.isArray(value)) {
                    const object = jsonObject(value);
                    const keys = state.keyOrder?.get(object) ?? Object.keys(object);
                    if (keys.length > this.limits.maxCollection) throw new WireError("LuminPack JSON dictionary exceeds its limit.");
                    for (const key of keys) dictionary.set(this.readJsonWire(wire.key, key, encoding, state),
                        this.readJsonWire(wire.value, object[key], encoding, state));
                } else {
                    for (const item of jsonArray(value, this.limits.maxCollection)) {
                        const pair = jsonArray(item, 2);
                        if (pair.length !== 2) throw new WireError("Invalid LuminPack JSON dictionary entry.");
                        const key = this.readJsonWire(wire.key, pair[0], encoding, state);
                        if (key === null) throw new WireError("Null LuminPack JSON dictionary key.");
                        dictionary.set(key, this.readJsonWire(wire.value, pair[1], encoding, state));
                    }
                }
                return dictionary;
            }
            case "rectangular": {
                if (value === null) return null;
                const rect = jsonObject(value);
                const lengths = dimensions(jsonArray(rect.Lengths, wire.rank).map(item => readJsonScalar(item, "int", 4, true)), wire.rank);
                const lowerBounds = dimensions(jsonArray(rect.LowerBounds, wire.rank).map(item => readJsonScalar(item, "int", 4, true)), wire.rank);
                const items = jsonArray(rect.Values, this.limits.maxCollection);
                if (rectangularSize(lengths, this.limits.maxCollection) !== items.length)
                    throw new WireError("LuminPack JSON rectangular dimensions differ from its element count.");
                rectangularBounds(lengths, lowerBounds);
                return { lengths, lowerBounds, values: items.map(item => this.readJsonWire(wire.element, item, encoding, state)) };
            }
            default: throw new WireError("Unsupported LuminPack JSON shape.");
        }
    }

    private writeJsonElements(wire: Wire, values: ArrayLike<unknown>, encoding: JsonEncoding, state: WriteState, parts: string[]): void {
        parts.push("[");
        for (let index = 0; index < values.length; index++) {
            if (index !== 0) parts.push(",");
            this.writeJsonWire(wire, values[index], encoding, state, parts);
        }
        parts.push("]");
    }

    private writeJsonDimensions(values: readonly number[], parts: string[]): void {
        parts.push("[");
        for (let index = 0; index < values.length; index++) {
            if (index !== 0) parts.push(",");
            parts.push(formatJsonInteger(values[index]));
        }
        parts.push("]");
    }

    private writeJsonWire(wire: Wire, value: unknown, encoding: JsonEncoding, state: WriteState, parts: string[]): void {
        switch (wire.kind) {
            case "scalar": parts.push(writeJsonScalar(wire.type, value, this.nativeBytes)); return;
            case "string":
                if (value !== null && typeof value !== "string") throw new WireError("Invalid LuminPack JSON string.");
                parts.push(value === null ? "null" : JSON.stringify(value));
                return;
            case "contract": this.writeJsonContract(this.jsonPlan(wire.type), value, encoding, state, parts); return;
            case "nullable":
                if (value === null) parts.push("null");
                else this.writeJsonWire(wire.element, value, encoding, state, parts);
                return;
            case "sequence":
                if (value === null && nullSequence(wire) === null) parts.push("null");
                else this.writeJsonElements(wire.element, this.primitiveCollectionValues(wire.element,
                    value ?? [], this.limits.maxCollection), encoding, state, parts);
                return;
            case "pair": case "tuple": {
                const values = sequence(value, wire.elements.length);
                if (values.length !== wire.elements.length) throw new WireError("Incorrect LuminPack JSON tuple length.");
                parts.push("[");
                for (let index = 0; index < values.length; index++) {
                    if (index !== 0) parts.push(",");
                    this.writeJsonWire(wire.elements[index], values[index], encoding, state, parts);
                }
                parts.push("]");
                return;
            }
            case "dictionary": {
                if (value === null) { parts.push("null"); return; }
                if (!(value instanceof Map) || value.size > this.limits.maxCollection)
                    throw new WireError("Expected a LuminPack JSON dictionary Map.");
                if (wire.key.kind === "string") {
                    if (this.trackedJson && wire.value.kind !== "scalar") {
                        parts.push("{");
                        let first = true;
                        for (const [key, item] of value) {
                            if (typeof key !== "string") throw new WireError("Invalid LuminPack JSON dictionary key.");
                            if (!first) parts.push(",");
                            first = false;
                            parts.push(JSON.stringify(key), ":");
                            this.writeJsonWire(wire.value, item, encoding, state, parts);
                        }
                        parts.push("}");
                        return;
                    }
                    const properties: Record<string, string> = {};
                    for (const [key, item] of value) {
                        if (typeof key !== "string") throw new WireError("Invalid LuminPack JSON dictionary key.");
                        const head = JSON.stringify(key) + ":";
                        if (wire.value.kind === "scalar") {
                            put(properties, key, head + writeJsonScalar(wire.value.type, item, this.nativeBytes));
                        } else {
                            const member = [head];
                            this.writeJsonWire(wire.value, item, encoding, state, member);
                            put(properties, key, member.join(""));
                        }
                    }
                    parts.push("{", Object.values(properties).join(","), "}");
                } else {
                    parts.push("[");
                    let first = true;
                    for (const [key, item] of value) {
                        if (key === null) throw new WireError("Null LuminPack JSON dictionary key.");
                        if (wire.key.kind === "scalar") {
                            parts.push(first ? "[" : ",[", writeJsonScalar(wire.key.type, key, this.nativeBytes), ",");
                            this.writeJsonWire(wire.value, item, encoding, state, parts);
                            parts.push("]");
                        } else {
                            // Preserve the reference-registration order for keyed records.
                            const keyParts: string[] = [];
                            const itemParts: string[] = [];
                            this.writeJsonWire(wire.value, item, encoding, state, itemParts);
                            this.writeJsonWire(wire.key, key, encoding, state, keyParts);
                            parts.push(first ? "[" : ",[", keyParts.join(""), ",", itemParts.join(""), "]");
                        }
                        first = false;
                    }
                    parts.push("]");
                }
                return;
            }
            case "rectangular": {
                if (value === null) { parts.push("null"); return; }
                const rect = object(value);
                const lengths = dimensions(rect.lengths, wire.rank);
                const lowerBounds = dimensions(rect.lowerBounds, wire.rank);
                const items = sequence(rect.values, this.limits.maxCollection);
                if (rectangularSize(lengths, this.limits.maxCollection) !== items.length)
                    throw new WireError("LuminPack JSON rectangular dimensions differ from its element count.");
                rectangularBounds(lengths, lowerBounds);
                parts.push('{"Lengths":');
                this.writeJsonDimensions(lengths, parts);
                parts.push(',"LowerBounds":');
                this.writeJsonDimensions(lowerBounds, parts);
                parts.push(',"Values":');
                this.writeJsonElements(wire.element, items, encoding, state, parts);
                parts.push("}");
                return;
            }
            default: throw new WireError("Unsupported LuminPack JSON shape.");
        }
    }

    private readJsonContract(plan: Plan, value: JsonValue, encoding: JsonEncoding, state: ReadState): unknown {
        const { contract } = plan;
        if (contract.sequenceWire) return this.readJsonWire(contract.sequenceWire, value, encoding, state);
        if (contract.kind === "Custom") {
            const codec = this.custom(plan);
            if (!contract.customJson || encoding !== "utf16" || !codec.readJson)
                throw new WireError("No Custom JSON formatter is registered for this encoding.");
            return codec.readJson(value, encoding);
        }
        if (value === null && contract.kind !== "Struct") return null;
        const encoded = jsonObject(value);
        if (contract.kind === "Union") {
            const discriminator = jsonUint(encoded.$type);
            const type = plan.byHash.get(discriminator);
            if (type) {
                const payload = this.readJsonContract(this.plan(type), encoded.$value, encoding, state);
                return payload === null ? null : { type, value: payload };
            }
            const registered = plan.registeredByTag.get(discriminator);
            if (registered?.codec.readJson)
                return { type: identity(registered.type), value: registered.codec.readJson(encoded.$value, encoding) };
            if (registered) throw new WireError("No registered LuminPack Union JSON reader.");
            throw new WireError("Unknown LuminPack JSON Union discriminator.");
        }
        const result: Target = {};
        const tracked = contract.profile.trackReferences === true;
        for (const key of state.keyOrder?.get(encoded) ?? Object.keys(encoded)) {
            if (tracked && key === "$ref") return this.lookupReference(state, jsonUint(encoded[key]), contract.id);
            if (tracked && key === "$id") {
                const id = jsonUint(encoded[key]);
                if (state.references.has(id)) throw new WireError("Duplicate LuminPack JSON reference ID.");
                state.references.set(id, { type: identity(contract.id), value: result });
                continue;
            }
            const member = plan.jsonByName.get(key);
            if (!member) continue;
            const item = encoded[key];
            put(result, member.name, tracked && item === null && member.wire.kind === "scalar" ? this.zero(member.wire) :
                this.readJsonWire(member.wire, item, encoding, state));
        }
        for (const member of contract.members) {
            if (!own(result, member.name)) put(result, member.name, this.zero(member.wire));
        }
        return result;
    }

    private writeJsonContract(plan: Plan, value: unknown, encoding: JsonEncoding, state: WriteState, parts: string[]): void {
        if (state.depth++ >= this.jsonDepth) throw new WireError("LuminPack JSON nesting exceeds its limit.");
        try {
            const { contract } = plan;
            if (contract.sequenceWire) return this.writeJsonWire(contract.sequenceWire, value, encoding, state, parts);
            if (contract.kind === "Custom") {
                const codec = this.custom(plan);
                if (!contract.customJson || encoding !== "utf16" || !codec.writeJson)
                    throw new WireError("No Custom JSON formatter is registered for this encoding.");
                parts.push(stringifyJson(codec.writeJson(value, encoding), this.encodedJsonNames));
                return;
            }
            if (value === null && contract.kind !== "Struct") { parts.push("null"); return; }
            const record = object(value);
            if (contract.kind === "Union") {
                if (typeof record.type !== "string" || record.value == null) throw new WireError("Invalid LuminPack JSON Union value.");
                const type = plan.byId.has(record.type) || plan.registeredByType.has(record.type) ? record.type : identity(record.type);
                const hash = plan.byId.get(type);
                if (hash !== undefined) {
                    parts.push('{"$type":', formatJsonInteger(hash), ',"$value":');
                    this.writeJsonContract(this.jsonPlan(type), record.value, encoding, state, parts);
                } else {
                    const registered = plan.registeredByType.get(type);
                    if (!registered) throw new WireError("Unknown LuminPack JSON Union type.");
                    if (!registered.codec.writeJson) throw new WireError("No registered LuminPack Union JSON writer.");
                    parts.push('{"$type":', formatJsonInteger(registered.tag), ',"$value":',
                        stringifyJson(registered.codec.writeJson(record.value, encoding), this.encodedJsonNames));
                }
                parts.push("}");
                return;
            }
            if (plan.scalarJsonBody) {
                let body = "";
                for (const { member, head, type } of plan.scalarJsonBody)
                    body += head + writeJsonScalar(type, this.field(record, member), this.nativeBytes);
                parts.push(body + "}");
                return;
            }
            let first = true;
            if (contract.profile.trackReferences === true) {
                const reference = this.referenceIndex(state, record, contract.id);
                if (reference.existing) { parts.push('{"$ref":', formatJsonInteger(reference.id), "}"); return; }
                parts.push('{"$id":', formatJsonInteger(reference.id));
                first = false;
            }
            for (const { member, head, next } of plan.jsonHeads) {
                parts.push(first ? head : next);
                this.writeJsonWire(member.wire, this.field(record, member), encoding, state, parts);
                first = false;
            }
            if (first) parts.push("{");
            parts.push("}");
        } finally { state.depth--; }
    }
}

/** Compiles the shared portable contract to a checked Binary and JSON codec. */
export function createCodec<T = unknown, TInput = T>(manifest: unknown, options: CodecOptions = {}): Codec<T, TInput> {
    return new CompiledCodec<T, TInput>(manifest, options);
}
