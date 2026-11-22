import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, rmdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const clang = process.argv[2] ?? "clang";
const source = fileURLToPath(new URL("../wasm/json-number.c", import.meta.url));
const output = fileURLToPath(new URL("../src/json-number-wasm.ts", import.meta.url));
const scratch = mkdtempSync(join(tmpdir(), "luminpack-json-number-"));
const wasm = join(scratch, "json-number.wasm");

try {
    execFileSync(clang, ["--target=wasm32", "-O3", "-ffp-contract=off", "-nostdlib", "-Wl,--no-entry",
        "-Wl,--strip-all", "-Wl,-z,stack-size=2048", "-Wl,--initial-memory=65536", "-Wl,--max-memory=65536",
        "-Wl,--export=format_float32", "-Wl,--export=format_float64", "-Wl,--export=json_number_buffer",
        "-o", wasm, source], { stdio: "inherit" });

    const bytes = readFileSync(wasm);
    const lines = ["// Generated from wasm/json-number.c by scripts/build-json-number.mjs.",
        "export const jsonNumberWasm = new Uint8Array(["];
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
