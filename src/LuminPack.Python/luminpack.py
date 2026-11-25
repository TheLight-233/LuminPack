"""Binary and JSON bindings for a generated LuminPack model library."""
import ctypes as c
from array import array
from enum import IntEnum
import json
import struct
import sys
from pathlib import Path
from threading import Lock
import weakref


class Format(IntEnum):
    BINARY = 0
    UTF8 = 1
    UTF16 = 2


def encode_list(values):
    """Encode a nullable array.array of four-byte signed integers or floats."""
    if values is None:
        return b"\xff" * 4
    if not isinstance(values, array) or values.typecode not in ("i", "f") or values.itemsize != 4:
        raise TypeError("Expected a four-byte 'i' or 'f' array")
    if sys.byteorder != "little":
        values = values[:]
        values.byteswap()
    return b"".join((struct.pack("<i", len(values)), memoryview(values).cast("B")))


def decode_list(data, typecode):
    """Decode a complete Array/List root to array.array, preserving float bits."""
    if typecode not in ("i", "f"):
        raise TypeError("Expected 'i' or 'f'")
    view = memoryview(data).cast("B")
    if len(view) < 4:
        raise ValueError("Missing collection header")
    count, = struct.unpack_from("<i", view)
    if count == -1 and len(view) == 4:
        return None
    if count < 0 or len(view) != 4 + count * 4:
        raise ValueError("Invalid collection payload length")
    result = array(typecode)
    if result.itemsize != 4:
        raise ValueError("Expected four-byte native elements")
    result.frombytes(view[4:])
    if sys.byteorder != "little":
        result.byteswap()
    return result


class Codec:
    def __init__(self, library):
        self._lib = lib = c.CDLL(str(Path(library).resolve()))
        lib.lp_abi.argtypes = []
        lib.lp_abi.restype = c.c_uint
        if lib.lp_abi() != 1:
            raise ValueError("Unsupported LuminPack model ABI")
        lib.lp_read.argtypes = [c.c_char_p, c.c_size_t, c.c_int, c.POINTER(c.c_void_p), c.c_char_p, c.c_size_t]
        lib.lp_write.argtypes = [c.c_void_p, c.c_void_p, c.c_size_t, c.c_int, c.POINTER(c.c_size_t), c.c_char_p, c.c_size_t]
        lib.lp_free.argtypes = [c.c_void_p]
        lib.lp_free.restype = None

    def decode(self, data: bytes, fmt=Format.BINARY):
        if not isinstance(data, bytes):
            raise TypeError("Expected immutable bytes")
        value, error = c.c_void_p(), c.create_string_buffer(256)
        if self._lib.lp_read(data, len(data), fmt, c.byref(value), error, len(error)):
            raise ValueError(error.value.decode(errors="replace"))
        return Document(_Owner(self, value))

    def from_value(self, value, fmt=Format.UTF8):
        """Construct a document from its JSON representation."""
        encoding = {Format.UTF8: "utf-8", Format.UTF16: "utf-16-le"}[fmt]
        return self.decode(json.dumps(value, allow_nan=False, separators=(",", ":")).encode(encoding), fmt)


class _Owner:
    def __init__(self, codec, pointer):
        self.codec = codec
        self.pointer = pointer
        self.lock = Lock()
        weakref.finalize(self, codec._lib.lp_free, pointer)


class Document:
    def __init__(self, owner):
        self._owner = owner

    def encode_into(self, buffer, fmt=Format.BINARY):
        """Return the byte count. A failed write may modify the supplied buffer."""
        view = memoryview(buffer).cast("B")
        target = (c.c_ubyte * len(view)).from_buffer(view)
        written, error = c.c_size_t(), c.create_string_buffer(256)
        owner = self._owner
        with owner.lock:
            if owner.codec._lib.lp_write(owner.pointer, target, len(view), fmt, c.byref(written), error, len(error)):
                raise ValueError(error.value.decode(errors="replace"))
        return written.value

    def to_value(self, capacity, fmt=Format.UTF8):
        encoding = {Format.UTF8: "utf-8", Format.UTF16: "utf-16-le"}[fmt]
        buffer = bytearray(capacity)
        count = self.encode_into(buffer, fmt)
        return json.loads(buffer[:count].decode(encoding, errors="surrogatepass"))
