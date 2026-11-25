use std::{env, process::Command};
fn main() {
    let models = env::var("LP_MODELS").expect("Set LP_MODELS to the projected fixture directory");
    println!("cargo:rerun-if-env-changed=LP_MODELS");
    println!("cargo:rerun-if-changed={models}/simple.manifest.json");
    println!("cargo:rerun-if-changed={models}/serialize.manifest.json");
    println!("cargo:rerun-if-changed=generate_models.py");
    println!("cargo:rerun-if-changed=../../tools/portable-direct");
    assert!(Command::new("python")
        .current_dir("../..")
        .arg("benchmarks/LuminPack.Benchmark.Rust/generate_models.py")
        .arg(models)
        .arg(env::var_os("OUT_DIR").unwrap())
        .status()
        .unwrap()
        .success());
}
