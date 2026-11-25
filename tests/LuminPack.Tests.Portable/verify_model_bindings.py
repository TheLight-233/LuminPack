"""Exercise Rust and Python bindings against both managed benchmark models."""
import argparse
from pathlib import Path
import subprocess

from luminpack import Codec, Format


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("fixtures", type=Path)
    parser.add_argument("simple_library", type=Path)
    parser.add_argument("serialize_library", type=Path)
    parser.add_argument("rust_transcode", type=Path)
    parser.add_argument("managed_dll", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    formats = [(Format.BINARY, "binary", ".bin"), (Format.UTF8, "utf8", ".utf8.json"),
               (Format.UTF16, "utf16", ".utf16.json")]
    for language in ("python", "rust"):
        output = args.output / language
        output.mkdir(parents=True, exist_ok=True)
        for model, library in (("simple", args.simple_library), ("serialize", args.serialize_library)):
            codec = Codec(library) if language == "python" else None
            for index, (fmt, name, suffix) in enumerate(formats):
                source = args.fixtures / (model + suffix)
                document = codec.decode(source.read_bytes(), fmt) if codec else None
                for target in (index, (index + 1) % len(formats)):
                    target_format, target_name, target_suffix = formats[target]
                    route = "" if index == target else f".from-{index}"
                    destination = output / (model + route + target_suffix)
                    if document is not None:
                        scratch = bytearray(262144)
                        size = document.encode_into(scratch, target_format)
                        destination.write_bytes(memoryview(scratch)[:size])
                    else:
                        subprocess.run([args.rust_transcode.resolve(), library.resolve(), name, source,
                                        target_name, destination, "262144"], check=True)
        subprocess.run(["dotnet", args.managed_dll, "read", output], check=True)


if __name__ == "__main__":
    main()
