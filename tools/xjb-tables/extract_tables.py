# Extracts the xjb lookup tables from the locked upstream sources and emits C# table files.
#
# Source: https://github.com/xjb714/xjb @ 80cc89574a8f8457ffbf951afa2fd27c2459bd4a
#   src/xjb32_i.cpp (xjb_v2_f32_to_dec), bench/xjb/float_to_decimal/xjb64_i.cpp (xjb64_v2_f64_to_dec)
#
# Usage:
#   1. download the two sources from the commit above, e.g.
#        https://raw.githubusercontent.com/xjb714/xjb/80cc89574a8f8457ffbf951afa2fd27c2459bd4a/src/xjb32_i.cpp
#        https://raw.githubusercontent.com/xjb714/xjb/80cc89574a8f8457ffbf951afa2fd27c2459bd4a/bench/xjb/float_to_decimal/xjb64_i.cpp
#      into a local directory (default: <repo>/.tmp/xjb)
#   2. python tools/xjb-tables/extract_tables.py [source-directory]
#
# It verifies both h-shift formulas against the upstream tables, prints FNV-1a hashes (asserted by
# tests/LuminPack.Tests/JsonXjbFormatterTest.cs) and rewrites src/LuminPack.Core/Core/Json/Xjb/XjbTables.g.cs.
import io
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, ".tmp", "xjb")
OUT = os.path.join(REPO, "src", "LuminPack", "Core", "Json", "Xjb")


def read(name):
    with io.open(SRC + "\\" + name, "r", encoding="utf-8", errors="replace", newline="") as f:
        return f.read()


def block(text, start_marker, end_marker):
    start = text.index(start_marker) + len(start_marker)
    end = text.index(end_marker, start)
    return text[start:end]


def numbers(text):
    text = re.sub(r"//[^\n]*", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return [int(t, 16) for t in re.findall(r"0[xX][0-9a-fA-F]+", text)]


def decimals(text):
    text = re.sub(r"//[^\n]*", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return [int(t) for t in re.findall(r"\b\d+\b", text)]


def fnv1a64(values, byte_width):
    h = 0xCBF29CE484222325
    for v in values:
        for i in range(byte_width):
            h ^= (v >> (8 * i)) & 0xFF
            h = (h * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return h


f32 = read("xjb32_i.cpp")
f64 = read("xjb64_i.cpp")

pow10_reverse = numbers(block(f32, ".pow10_table_reverse = {", "},"))
h37 = decimals(block(f32, ".h37 = {", "};"))
g = numbers(block(f64, "static const u64 g[(323 - (-293) + 1) * 2] = {", "};"))
h7 = decimals(block(f64, "static const unsigned char h7[2048] = {", "};"))

# The C literal omits trailing zeros (h37[255] and possibly h7 tails are never read because
# exponent 255 / 2047 are non-finite), so pad back to the declared array length.
assert len(pow10_reverse) == 77, len(pow10_reverse)
assert len(g) == 1234, len(g)
assert len(h37) <= 256 and len(h7) <= 2048
h37_explicit = len(h37)
h7_explicit = len(h7)
h37 = h37 + [0] * (256 - len(h37))
h7 = h7 + [0] * (2048 - len(h7))

# --- verify the h shifts used by the x64 code paths against the upstream tables ---
def sra(v, s):  # arithmetic shift right for Python ints
    return v >> s

h37_formula = []
for exp in range(256):
    q = exp - 150
    if exp == 0:
        q = -149
    k = sra(q * 1233, 12)
    h = q + sra(k * -1701 - 1701, 9)
    h37_formula.append(h + 37)

h7_formula = []
for exp in range(2048):
    q = exp - 1075
    if exp == 0:
        q = -1074
    k = sra((exp - 1075) * 78913, 18)
    get_e10 = -1 - k
    h = q + sra(get_e10 * 217707, 16)
    h7_formula.append(h + 7)

h37_mismatch = [i for i in range(h37_explicit) if h37_formula[i] != h37[i]]
h7_mismatch = [i for i in range(h7_explicit) if h7_formula[i] != h7[i]]
print("explicit h37 entries:", h37_explicit, "h7 entries:", h7_explicit)
print("h37 formula mismatches:", len(h37_mismatch), h37_mismatch[:8])
print("h7  formula mismatches:", len(h7_mismatch), h7_mismatch[:8])
print("h37[255] =", h37[255], " h7 tail explicit:", h7[h7_explicit - 4: h7_explicit])

# Index ranges used by the ported scalar cores.
def k_float(q):
    return sra(q * 1233, 12)


def k_float_irregular(q):
    return sra(q * 1233 - 512, 12)


def k_double(q):
    return sra(q * 78913, 18)


def k_double_irregular(q):
    return sra(q * 315653 - 131072, 20)


q_min, q_max = -149, 104
q_min_irr, q_max_irr = 1 - 150, 254 - 150
f_idx = [45 + k_float(q) for q in range(q_min, q_max + 1)]
f_idx += [45 + k_float_irregular(q) for q in range(q_min_irr, q_max_irr + 1)]
d_q = [q for q in range(-1074, 972)]
d_idx = [293 - 1 - k_double(q) for q in d_q if q >= -1074]
d_idx += [293 - 1 - k_double_irregular(q) for q in range(1 - 1075, 2046 - 1075 + 1)]
print("float table index range:", min(f_idx), max(f_idx))
print("double table index range (pair start):", min(d_idx), max(d_idx))

print("FNV1A64 pow10_reverse:", hex(fnv1a64(pow10_reverse, 8)))
print("FNV1A64 h37          :", hex(fnv1a64(h37, 1)))
print("FNV1A64 g            :", hex(fnv1a64(g, 8)))
print("FNV1A64 h7           :", hex(fnv1a64(h7, 1)))
print("FNV1A64 h37_formula  :", hex(fnv1a64(h37_formula, 1)))
print("FNV1A64 h7_formula   :", hex(fnv1a64(h7_formula, 1)))


def emit_u64(values, per_line=4):
    lines = []
    for i in range(0, len(values), per_line):
        chunk = values[i:i + per_line]
        lines.append("            " + " ".join("0x%016XUL," % v for v in chunk))
    return "\n".join(lines)


def emit_bytes(values, per_line=24):
    lines = []
    for i in range(0, len(values), per_line):
        chunk = values[i:i + per_line]
        lines.append("            " + " ".join("%d," % v for v in chunk))
    return "\n".join(lines)


header = u'''// <auto-generated>
// xjb IEEE-754 to decimal lookup tables.
//
// Source: https://github.com/xjb714/xjb @ 80cc89574a8f8457ffbf951afa2fd27c2459bd4a
//   - bench/xjb/float_to_decimal/xjb32_i.cpp  (xjb_v2_f32_to_dec: .pow10_table_reverse, .h37)
//   - bench/xjb/float_to_decimal/xjb64_i.cpp  (xjb64_v2_f64_to_dec: g[1234])
//   - bench/xjb/float_to_decimal/xjb64_i.cpp  (xjb64_v2_f64_to_dec: h7[2048], verification only)
// Licensed under the terms of the upstream LICENSE file (see docs/xjb-LICENSE.txt).
// Regenerate with tools/xjb-tables/extract_tables.py.
// </auto-generated>
'''

pow10_lines = emit_u64(pow10_reverse)
h37_lines = emit_bytes(h37)
g_lines = emit_u64(g)

float32 = header + u'''
namespace LuminPack.Core
{
    internal static class XjbFloat32Tables
    {
        /// <summary>10^(-k-1) as a 64-bit fixed-point value; entry k is at index 45 + k (k in [-45, 31]).</summary>
        internal static readonly ulong[] Pow10Reverse =
        {
''' + pow10_lines + u'''
        };

        /// <summary>h + 37 for every IEEE-754 binary32 exponent byte, i.e. the left shift applied to the significand.</summary>
        internal static readonly byte[] H37 =
        {
''' + h37_lines + u'''
        };
    }
}
'''

float64 = header + u'''
namespace LuminPack.Core
{
    internal static class XjbFloat64Tables
    {
        /// <summary>Pairs of (high, low) 64-bit words of 10^(-e10) fixed-point values.
        /// The pair for e10 is at [2 * (e10 + 293)], e10 in [-293, 323]; the scalar core indexes it as 584 - 2k.</summary>
        internal static readonly ulong[] Pow10 =
        {
''' + g_lines + u'''
        };
    }
}
'''

import os
if not os.path.isdir(OUT):
    os.makedirs(OUT)
with io.open(OUT + r"\XjbTables.g.cs", "w", encoding="utf-8", newline="\r\n") as f:
    f.write(float32.replace("\n", "\r\n") + float64.replace("\n", "\r\n"))
print("tables written to", OUT + r"\XjbTables.g.cs")