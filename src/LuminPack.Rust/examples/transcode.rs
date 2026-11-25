use luminpack::{Codec, Format};
use std::{env, fs};

fn format(name: &str) -> Result<Format, String> {
    match name {
        "binary" => Ok(Format::Binary),
        "utf8" => Ok(Format::Utf8),
        "utf16" => Ok(Format::Utf16),
        _ => Err(format!("Unknown format: {name}")),
    }
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = env::args().skip(1).collect();
    let [model, source, input, target, output, capacity] = args.as_slice() else {
        return Err("usage: transcode MODEL FROM INPUT TO OUTPUT CAPACITY".into());
    };
    // The caller supplies the locally built model and its native dependencies.
    let codec = unsafe { Codec::load(model)? };
    let document = codec.decode(&fs::read(input)?, format(source)?)?;
    let mut destination = vec![0; capacity.parse()?];
    fs::write(
        output,
        document.encode_into(&mut destination, format(target)?)?,
    )?;
    Ok(())
}
