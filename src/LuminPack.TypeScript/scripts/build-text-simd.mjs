import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, rmdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const clang = process.argv[2] ?? "clang";
const source = fileURLToPath(new URL("../wasm/text.c", import.meta.url));
const output = fileURLToPath(new URL("../src/text-wasm.ts", import.meta.url));
const scratch = mkdtempSync(join(tmpdir(), "luminpack-text-simd-"));
const wasm = join(scratch, "text.wasm");

try {
    execFileSync(clang, ["--target=wasm32", "-msimd128", "-Oz", "-nostdlib", "-Wl,--no-entry",
        "-Wl,--strip-all", "-Wl,-z,stack-size=0", "-Wl,--initial-memory=65536", "-Wl,--max-memory=65536",
        "-Wl,--export=find_utf16_zero", "-Wl,--export=has_utf16_surrogate", "-Wl,--export=is_ascii",
        "-o", wasm, source], { stdio: "inherit" });

    const bytes = readFileSync(wasm);
    const lines = ["// Generated from wasm/text.c by scripts/build-text-simd.mjs.",
        "export const textWasm = new Uint8Array(["];
    for (let offset = 0; offset < bytes.length; offset += 16) {
        const line = Array.from(bytes.subarray(offset, offset + 16));
        lines.push("    " + line.join(", ") + ",");
    }
    lines.push("]);", "");
    writeFileSync(output, lines.join("\n"));
} finally {
    rmSync(wasm, { force: true });
    rmdirSync(scratch);
}
