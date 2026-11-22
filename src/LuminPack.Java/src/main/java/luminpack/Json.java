package luminpack;

import java.math.BigDecimal;
import java.math.BigInteger;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Small checked JSON codec used by manifests and the wire projection. */
final class Json {
    private static final char[] HEX = "0123456789abcdef".toCharArray();

    private Json() {}

    record Name(String token) {}

    static Name name(String text) {
        var result = new StringBuilder(text.length() + 3);
        quote(result, text);
        return new Name(result.append(':').toString());
    }

    static Object parse(String text, int maximumDepth) {
        var reader = new Reader(text, maximumDepth);
        Object value = reader.value(0);
        reader.space();
        if (reader.position != text.length()) throw failure("Trailing bytes after LuminPack JSON");
        return value;
    }

    static String write(Object value, int maximumDepth) {
        var result = new StringBuilder();
        write(result, value, 0, maximumDepth);
        return result.toString();
    }

    private static void write(StringBuilder output, Object value, int depth, int limit) {
        if (value == null) output.append("null");
        else if (value instanceof Boolean bool) output.append(bool);
        else if (value instanceof String text) quote(output, text);
        else if (value instanceof Map<?, ?> map) {
            if (depth >= limit) throw failure("LuminPack JSON depth limit exceeded");
            output.append('{');
            boolean first = true;
            for (var entry : map.entrySet()) {
                if (!first) output.append(',');
                first = false;
                if (entry.getKey() instanceof Name known) output.append(known.token());
                else if (entry.getKey() instanceof String key) {
                    quote(output, key);
                    output.append(':');
                } else throw failure("Invalid JSON property name");
                write(output, entry.getValue(), depth + 1, limit);
            }
            output.append('}');
        } else if (value instanceof List<?> list) {
            if (depth >= limit) throw failure("LuminPack JSON depth limit exceeded");
            output.append('[');
            for (int index = 0; index < list.size(); index++) {
                if (index > 0) output.append(',');
                write(output, list.get(index), depth + 1, limit);
            }
            output.append(']');
        } else if (value instanceof Float number) {
            if (!Float.isFinite(number)) throw failure("LuminPack JSON cannot encode a non-finite float");
            int bits = Float.floatToRawIntBits(number);
            int exponent = (bits >>> 23) & 0xff;
            boolean legacyRange = exponent >= 95 && exponent <= 159;
            if (!writeSmallIntegral(output, number, bits < 0, legacyRange, 16_777_216.0))
                output.append(general(Float.toString(number), 9, legacyRange));
        } else if (value instanceof Double number) {
            if (!Double.isFinite(number)) throw failure("LuminPack JSON cannot encode a non-finite double");
            long bits = Double.doubleToRawLongBits(number);
            int exponent = (int) ((bits >>> 52) & 0x7ff);
            boolean legacyRange = exponent >= 991 && exponent <= 1055;
            if (!writeSmallIntegral(output, number, bits < 0, legacyRange, 9_007_199_254_740_992.0))
                output.append(general(Double.toString(number), 17, legacyRange));
        } else if (value instanceof Number number) output.append(number);
        else throw failure("Unsupported LuminPack JSON value " + value.getClass().getName());
    }

    private static boolean writeSmallIntegral(StringBuilder output, double value, boolean negative, boolean legacyRange, double limit) {
        if (value == 0) {
            output.append(negative ? "-0" : "0");
            return true;
        }
        if (!legacyRange || Math.abs(value) >= limit || (long) value != value) return false;
        output.append((long) value);
        return true;
    }

    private static String general(String input, int precision, boolean legacyRange) {
        BigDecimal value = new BigDecimal(input).stripTrailingZeros();
        if (value.signum() == 0) return input.startsWith("-") ? "-0" : "0";
        int exponent = value.precision() - value.scale() - 1;
        if ((legacyRange && value.scale() <= precision) || (exponent >= -4 && exponent < precision))
            return value.toPlainString();
        String digits = value.unscaledValue().abs().toString();
        String sign = value.signum() < 0 ? "-" : "";
        String fraction = digits.length() == 1 ? "" : "." + digits.substring(1);
        int magnitude = Math.abs(exponent);
        return sign + digits.charAt(0) + fraction + 'E' + (exponent < 0 ? '-' : '+')
            + (magnitude < 10 ? "0" : "") + magnitude;
    }

    private static void quote(StringBuilder output, String text) {
        output.append('"');
        for (int index = 0; index < text.length(); index++) {
            char c = text.charAt(index);
            switch (c) {
                case '"' -> output.append("\\\"");
                case '\\' -> output.append("\\\\");
                case '\b' -> output.append("\\b");
                case '\t' -> output.append("\\t");
                case '\n' -> output.append("\\n");
                case '\f' -> output.append("\\f");
                case '\r' -> output.append("\\r");
                default -> {
                    if (Character.isHighSurrogate(c) && index + 1 < text.length()
                        && Character.isLowSurrogate(text.charAt(index + 1))) output.append(c).append(text.charAt(++index));
                    else if (c < 32 || Character.isSurrogate(c)) {
                        output.append("\\u").append(HEX[c >>> 12]).append(HEX[(c >>> 8) & 15])
                            .append(HEX[(c >>> 4) & 15]).append(HEX[c & 15]);
                    } else output.append(c);
                }
            }
        }
        output.append('"');
    }

    private static IllegalArgumentException failure(String message) { return new IllegalArgumentException(message + '.'); }

    private static final class Reader {
        private final String text;
        private final int maximumDepth;
        private int position;

        Reader(String text, int maximumDepth) {
            this.text = text;
            this.maximumDepth = maximumDepth;
        }

        void space() {
            while (position < text.length()) {
                char c = text.charAt(position);
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n') return;
                position++;
            }
        }

        private boolean consume(char c) {
            if (position == text.length() || text.charAt(position) != c) return false;
            position++;
            return true;
        }

        private void expect(char c) {
            space();
            if (!consume(c)) throw failure("Expected JSON token " + c);
        }

        Object value(int depth) {
            space();
            if (position == text.length()) throw failure("Truncated LuminPack JSON");
            return switch (text.charAt(position)) {
                case '[' -> array(depth);
                case '{' -> object(depth);
                case '"' -> string();
                case 't' -> literal("true", Boolean.TRUE);
                case 'f' -> literal("false", Boolean.FALSE);
                case 'n' -> literal("null", null);
                default -> number();
            };
        }

        private Object literal(String literal, Object value) {
            if (!text.startsWith(literal, position)) throw failure("Invalid LuminPack JSON literal");
            position += literal.length();
            return value;
        }

        private Map<String, Object> object(int depth) {
            if (depth >= maximumDepth) throw failure("LuminPack JSON depth limit exceeded");
            position++;
            var map = new LinkedHashMap<String, Object>();
            space();
            if (consume('}')) return map;
            do {
                space();
                String name = string();
                expect(':');
                map.put(name, value(depth + 1));
                space();
            } while (consume(','));
            expect('}');
            return map;
        }

        private List<Object> array(int depth) {
            if (depth >= maximumDepth) throw failure("LuminPack JSON depth limit exceeded");
            position++;
            var values = new ArrayList<Object>();
            space();
            if (consume(']')) return values;
            do {
                values.add(value(depth + 1));
                space();
            } while (consume(','));
            expect(']');
            return values;
        }

        private String string() {
            if (!consume('"')) throw failure("Expected a JSON string");
            int start = position;
            StringBuilder result = null;
            while (position < text.length()) {
                int index = position;
                char character = text.charAt(position++);
                if (character == '"') {
                    if (result == null) return text.substring(start, index);
                    return result.append(text, start, index).toString();
                }
                if (character < 32) throw failure("Unescaped control character in JSON string");
                if (character == '\\') {
                    if (result == null) result = new StringBuilder();
                    result.append(text, start, index).append(escaped());
                    start = position;
                }
            }
            throw failure("Unterminated JSON string");
        }

        private char escaped() {
            if (position == text.length()) throw failure("Truncated JSON escape");
            return switch (text.charAt(position++)) {
                case '"' -> '"';
                case '\\' -> '\\';
                case '/' -> '/';
                case 'b' -> '\b';
                case 'f' -> '\f';
                case 'n' -> '\n';
                case 'r' -> '\r';
                case 't' -> '\t';
                case 'u' -> unicode();
                default -> throw failure("Invalid JSON escape");
            };
        }

        private char unicode() {
            if (text.length() - position < 4) throw failure("Truncated JSON Unicode escape");
            int code = 0;
            for (int index = 0; index < 4; index++) {
                char character = text.charAt(position++);
                int digit = character <= 127 ? Character.digit(character, 16) : -1;
                if (digit < 0) throw failure("Invalid JSON Unicode escape");
                code = code * 16 + digit;
            }
            return (char) code;
        }

        private boolean digit() {
            return position < text.length() && text.charAt(position) >= '0' && text.charAt(position) <= '9';
        }

        private void digits() {
            if (!digit()) throw failure("Invalid LuminPack JSON number");
            do position++; while (digit());
        }

        private Number number() {
            int start = position;
            consume('-');
            if (!consume('0')) digits();
            boolean decimal = consume('.');
            if (decimal) digits();
            if (consume('e') || consume('E')) {
                decimal = true;
                if (!consume('+')) consume('-');
                digits();
            }
            String number = text.substring(start, position);
            if (decimal) {
                BigDecimal value = new BigDecimal(number);
                return value.signum() == 0 && number.charAt(0) == '-' ? -0.0 : value;
            }
            if (number.equals("-0")) return -0.0;
            try { return Long.valueOf(number); }
            catch (NumberFormatException ignored) { return new BigInteger(number); }
        }
    }
}
