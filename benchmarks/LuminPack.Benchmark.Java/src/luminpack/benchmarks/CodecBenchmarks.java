package luminpack.benchmarks;

import com.alibaba.fastjson2.JSON;
import com.alibaba.fastjson2.JSONB;
import com.alibaba.fastjson2.JSONWriter;
import com.esotericsoftware.kryo.Kryo;
import com.esotericsoftware.kryo.io.Input;
import com.esotericsoftware.kryo.io.Output;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.dataformat.cbor.databind.CBORMapper;
import com.google.gson.GsonBuilder;
import com.google.gson.JsonElement;
import com.google.gson.JsonParser;
import luminpack.LuminPack;
import org.msgpack.jackson.dataformat.MessagePackMapper;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.Level;
import org.openjdk.jmh.annotations.Param;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;

import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Measures each library's byte array API with a preconstructed value and its own encoded bytes. */
public class CodecBenchmarks {
    static final String MODELS_PROPERTY = "luminpack.benchmark.models";

    public enum Serializer {
        LUMINPACK_BINARY("binary", "LuminPack"),
        KRYO("binary", "Kryo"),
        MESSAGEPACK("binary", "MessagePack"),
        JACKSON_CBOR("binary", "Jackson CBOR"),
        FASTJSON_JSONB("binary", "Fastjson2 JSONB"),
        LUMINPACK_JSON("json", "LuminPack"),
        JACKSON("json", "Jackson"),
        GSON("json", "Gson"),
        FASTJSON("json", "Fastjson2");

        final String format;
        final String displayName;

        Serializer(String format, String displayName) { this.format = format; this.displayName = displayName; }
    }

    @FunctionalInterface interface Encoder { byte[] write(Object value) throws Exception; }
    @FunctionalInterface interface Decoder { Object read(byte[] bytes) throws Exception; }
    record Adapter(Object value, Encoder encode, Decoder decode, Encoder canonicalJson) {}
    record Prepared(Adapter adapter, byte[] payload) {}

    @State(Scope.Thread)
    public static class Fixture {
        @Param({"simple", "serialize"}) public String model;
        @Param public Serializer serializer;
        private Prepared prepared;

        @Setup(Level.Trial)
        public void setup() throws Exception {
            Path directory = Path.of(System.getProperty(MODELS_PROPERTY));
            prepared = prepare(directory, model, serializer);
        }
    }

    @Benchmark public byte[] encode(Fixture state) throws Exception {
        return state.prepared.adapter.encode.write(state.prepared.adapter.value);
    }

    @Benchmark public Object decode(Fixture state) throws Exception {
        return state.prepared.adapter.decode.read(state.prepared.payload);
    }

    static Prepared prepare(Path directory, String model, Serializer name) throws Exception {
        byte[] managedBinary = Files.readAllBytes(directory.resolve(model + ".bin"));
        byte[] managedJson = Files.readAllBytes(directory.resolve(model + ".utf8.json"));
        var luminpack = LuminPack.fromManifest(Files.readString(directory.resolve(model + ".manifest.json")));
        Object nativeValue = luminpack.readBinary(managedBinary);
        if (!Arrays.equals(luminpack.writeBinary(nativeValue), managedBinary) ||
                !Arrays.equals(luminpack.writeBinary(luminpack.readJson(managedJson, LuminPack.UTF8)), managedBinary))
            throw new IllegalStateException("Managed Binary and JSON disagree for " + model);

        var treeMapper = new ObjectMapper();
        JsonNode expected = treeMapper.readTree(managedJson);
        Encoder toNativeJson = value -> luminpack.writeJson(value, LuminPack.UTF8);
        Adapter adapter = switch (name) {
            case LUMINPACK_BINARY -> new Adapter(nativeValue, luminpack::writeBinary, luminpack::readBinary, toNativeJson);
            case LUMINPACK_JSON -> new Adapter(nativeValue, toNativeJson, bytes -> luminpack.readJson(bytes, LuminPack.UTF8), toNativeJson);
            case KRYO -> kryo(plainGraph(nativeValue), toNativeJson);
            case MESSAGEPACK -> tree(new MessagePackMapper(), treeMapper, expected);
            case JACKSON_CBOR -> tree(new CBORMapper(), treeMapper, expected);
            case JACKSON -> tree(treeMapper, treeMapper, expected);
            case GSON -> gson(managedJson);
            case FASTJSON -> fastjson(managedJson, false);
            case FASTJSON_JSONB -> fastjson(managedJson, true);
        };
        byte[] payload = adapter.encode.write(adapter.value);
        Object restored = adapter.decode.read(payload);
        JsonNode actual = treeMapper.readTree(adapter.canonicalJson.write(restored));
        if (!equivalent(expected, actual)) throw new IllegalStateException(name + " changed " + model);
        if (name == Serializer.KRYO && !adapter.value.equals(restored))
            throw new IllegalStateException(name + " changed typed Java data in " + model);
        return new Prepared(adapter, payload);
    }

    private static Adapter tree(ObjectMapper codec, ObjectMapper json, JsonNode value) {
        return new Adapter(value, codec::writeValueAsBytes, codec::readTree, json::writeValueAsBytes);
    }

    private static Adapter gson(byte[] json) {
        var gson = new GsonBuilder().serializeNulls().disableHtmlEscaping().create();
        Encoder encode = value -> gson.toJson((JsonElement) value).getBytes(StandardCharsets.UTF_8);
        Decoder decode = bytes -> JsonParser.parseString(new String(bytes, StandardCharsets.UTF_8));
        return new Adapter(JsonParser.parseString(new String(json, StandardCharsets.UTF_8)), encode, decode, encode);
    }

    private static Adapter fastjson(byte[] json, boolean binary) {
        Encoder toJson = value -> JSON.toJSONBytes(value, JSONWriter.Feature.WriteNulls);
        Encoder encode = binary ? value -> JSONB.toBytes(value, JSONWriter.Feature.WriteNulls) : toJson;
        Decoder decode = binary ? JSONB::parseObject : JSON::parseObject;
        return new Adapter(JSON.parseObject(json), encode, decode, toJson);
    }

    private static Adapter kryo(Object value, Encoder toJson) {
        var codec = new Kryo();
        codec.setRegistrationRequired(true);
        codec.setReferences(false);
        codec.register(LinkedHashMap.class);
        codec.register(ArrayList.class);
        codec.register(BigInteger.class);
        codec.register(LuminPack.UnionValue.class);
        codec.register(LuminPack.RegisteredUnionValue.class);
        var output = new Output(4096, -1);
        var input = new Input();
        Encoder encode = item -> {
            output.reset();
            codec.writeClassAndObject(output, item);
            return output.toBytes();
        };
        Decoder decode = bytes -> {
            input.setBuffer(bytes);
            return codec.readClassAndObject(input);
        };
        return new Adapter(value, encode, decode, toJson);
    }

    private static Object plainGraph(Object value) {
        if (value instanceof LuminPack.UnionValue choice)
            return new LuminPack.UnionValue(choice.hash(), plainGraph(choice.value()));
        if (value instanceof LuminPack.RegisteredUnionValue choice)
            return new LuminPack.RegisteredUnionValue(choice.tag(), plainGraph(choice.value()));
        if (value instanceof Map<?, ?> map) {
            var copy = new LinkedHashMap<Object, Object>();
            map.forEach((key, item) -> copy.put(plainGraph(key), plainGraph(item)));
            return copy;
        }
        if (value instanceof List<?> list) {
            var copy = new ArrayList<Object>(list.size());
            for (Object item : list) copy.add(plainGraph(item));
            return copy;
        }
        return value;
    }

    private static boolean equivalent(JsonNode expected, JsonNode actual) {
        if (actual == null || expected.getNodeType() != actual.getNodeType()) return false;
        if (expected.isNumber()) return expected.decimalValue().compareTo(actual.decimalValue()) == 0;
        if (expected.isObject()) {
            if (expected.size() != actual.size()) return false;
            for (var field : expected.properties())
                if (!equivalent(field.getValue(), actual.get(field.getKey()))) return false;
            return true;
        }
        if (expected.isArray()) {
            if (expected.size() != actual.size()) return false;
            for (int index = 0; index < expected.size(); index++)
                if (!equivalent(expected.get(index), actual.get(index))) return false;
            return true;
        }
        return expected.equals(actual);
    }
}
