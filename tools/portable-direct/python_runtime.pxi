# Shared runtime embedded into each generated Cython model module.
from libc.stdint cimport int32_t, int64_t, uint32_t, uint8_t
from libc.string cimport memcpy
from cpython.bytes cimport PyBytes_AS_STRING, PyBytes_FromStringAndSize
from cpython.unicode cimport PyUnicode_DecodeLatin1
import sys

cdef extern from *:
    """
    #include <algorithm>
    #include <cstring>
    #include <limits>
    #include <new>
    #include <stdexcept>
    class DirectBytes {
        PyObject* value = nullptr;
        Py_ssize_t used = 0, capacity = 0;
        void grow(Py_ssize_t extra) {
            constexpr auto maximum = PY_SSIZE_T_MAX;
            if (extra > maximum - used) throw std::length_error("Output too large");
            Py_ssize_t next = std::max(used + extra, capacity > maximum / 2 ? maximum : std::max<Py_ssize_t>(256, capacity * 2));
            if (value) {
                if (_PyBytes_Resize(&value, next) < 0) throw std::bad_alloc();
            } else {
                value = PyBytes_FromStringAndSize(nullptr, next);
                if (!value) throw std::bad_alloc();
            }
            capacity = next;
        }
    public:
        DirectBytes() = default;
        DirectBytes(const DirectBytes&) = delete;
        ~DirectBytes() { Py_XDECREF(value); }
        void reserve(Py_ssize_t n) { if (n > capacity) grow(n); }
        void append(const char* p, Py_ssize_t n) {
            if (!n) return;
            if (n > capacity - used) grow(n);
            std::memcpy(PyBytes_AS_STRING(value) + used, p, n);
            used += n;
        }
        void push_back(char c) { append(&c, 1); }
        void latin1(const char* p, Py_ssize_t length) {
            // The caller has checked that the compact length fits a signed wire header.
            const int32_t header = static_cast<int32_t>(2 * length + (length != 0));
            const Py_ssize_t span = sizeof(header) + length;
            if (span > capacity - used) grow(span);
            char* dest = PyBytes_AS_STRING(value) + used;
            std::memcpy(dest, &header, sizeof(header));
            std::memcpy(dest + sizeof(header), p, length);
            used += span;
        }
        PyObject* finish() {
            if (!value) return PyBytes_FromStringAndSize("",0);
            if (used != capacity && _PyBytes_Resize(&value, used) < 0) return nullptr;
            PyObject* result = value;
            value = nullptr;
            return result;
        }
    };
    """
    cppclass DirectBytes:
        DirectBytes() noexcept
        void reserve(Py_ssize_t) except +
        void append(const char*, Py_ssize_t) except +
        void push_back(char) except +
        void latin1(const char*, Py_ssize_t) except +
        bytes finish()

cdef Py_ssize_t previous_size = 256

if sys.byteorder != "little":
    raise ImportError("Generated binary extension requires a little-endian host")

cdef extern from "Python.h":
    int PyUnicode_KIND(object)
    const unsigned char* PyUnicode_1BYTE_DATA(object)
    Py_ssize_t PyUnicode_GET_LENGTH(object)

cdef class Reader:
    cdef const char* pos
    cdef Py_ssize_t remaining

    cdef inline const char* take(self, Py_ssize_t n) except NULL:
        if n < 0 or n > self.remaining:
            raise ValueError("Truncated binary input")
        cdef const char* p = self.pos
        self.pos += n
        self.remaining -= n
        return p

    cdef inline int32_t integer(self) except? -1:
        cdef int32_t n
        memcpy(&n, self.take(4), 4)
        return n

    cdef inline int64_t wide(self) except? -1:
        cdef int64_t n
        memcpy(&n, self.take(8), 8)
        return n

    cdef inline float single(self) except? -1:
        cdef float n
        memcpy(&n, self.take(4), 4)
        return n

    cdef inline uint8_t byte(self) except? 255:
        return <uint8_t>self.take(1)[0]

    cdef inline bint boolean(self) except -1:
        cdef unsigned int v = self.byte()
        if v > 1:
            raise ValueError("Invalid boolean")
        return v != 0

    cdef inline int32_t count(self, int minimum) except? -2:
        cdef int32_t n = self.integer()
        if n < -1 or n > COLLECTION_LIMIT or <int64_t>n * minimum > self.remaining:
            raise ValueError("Invalid collection length")
        return n

    cdef inline object text(self):
        cdef int32_t h = self.integer()
        if h == -1:
            return None
        if h < 0 or (h >> 1) > STRING_LIMIT:
            raise ValueError("Invalid string length")
        if h & 1:
            return PyUnicode_DecodeLatin1(self.take(h >> 1), h >> 1, NULL)
        return self.take(h)[:h].decode('utf-16-le','surrogatepass')

cdef inline void write_int(DirectBytes& out, int32_t v) except *:
    out.append(<const char*>&v, 4)

cdef inline void write_long(DirectBytes& out, int64_t v) except *:
    out.append(<const char*>&v, 8)

cdef inline void write_float(DirectBytes& out, float v) except *:
    out.append(<const char*>&v, 4)

cdef inline void write_text(DirectBytes& out, object text) except *:
    cdef bytes data
    cdef Py_ssize_t n
    if text is None:
        write_int(out, -1)
    elif not isinstance(text, str):
        raise TypeError("Expected a string")
    elif PyUnicode_KIND(text) == 1:
        n = PyUnicode_GET_LENGTH(text)
        if n > 1073741823:
            raise ValueError("String too large")
        out.latin1(<const char*>PyUnicode_1BYTE_DATA(text), n)
    else:
        data = str.encode(text, 'utf-16-le','surrogatepass')
        if len(data) > 2147483647:
            raise ValueError("String too large")
        write_int(out, <int32_t>len(data))
        out.append(PyBytes_AS_STRING(data), len(data))
