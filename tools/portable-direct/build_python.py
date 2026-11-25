"""Compile one projected model into an importable CPython extension."""

import argparse
import json
import keyword
import sys
from pathlib import Path
from tempfile import TemporaryDirectory

from Cython.Build import cythonize
from generate import Schema
from python_model import emit
from setuptools import Distribution, Extension
from setuptools.command.build_ext import build_ext


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("projection", type=Path)
    parser.add_argument("module")
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    if (
        not args.module.isascii()
        or not args.module.isidentifier()
        or keyword.iskeyword(args.module)
    ):
        parser.error("Expected a Python module identifier")
    if sys.implementation.name != "cpython" or sys.byteorder != "little":
        parser.error("Expected little-endian CPython")
    source = emit(Schema(json.loads(args.projection.read_bytes())))
    with TemporaryDirectory() as temporary:
        path = Path(temporary) / (args.module + ".pyx")
        path.write_text(source, encoding="utf-8")
        extensions = cythonize(
            [Extension(args.module, [str(path)], language="c++")], quiet=True
        )
        command = build_ext(Distribution({"ext_modules": extensions}))
        command.ensure_finalized()
        command.build_lib = str(args.output.resolve())
        command.build_temp = str(Path(temporary) / "objects")
        command.run()


if __name__ == "__main__":
    main()
