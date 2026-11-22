package luminpack

import (
	"encoding/binary"
	"unsafe"
)

type blockScalar interface {
	int8 | uint8 | int16 | uint16 | int32 | uint32 | int64 | uint64 | float32 | float64
}

const nativeLittleEndian = uint16(1)

func nativeScalarBytes[T blockScalar](values []T, width int) []byte {
	return unsafe.Slice((*byte)(unsafe.Pointer(unsafe.SliceData(values))), len(values)*width)
}

func readScalarBlock[T blockScalar](r *binaryReader, n int) []T {
	width := int(unsafe.Sizeof(*new(T)))
	if n < 0 || n > (len(r.data)-r.pos)/width {
		fail(r.pos, "truncated scalar collection")
	}
	data := r.take(n * width)
	result := make([]T, n)
	copyScalarBytes(nativeScalarBytes(result, width), data, width)
	return result
}

func writeScalarBlock[T blockScalar](w *binaryWriter, values []T) {
	width := int(unsafe.Sizeof(*new(T)))
	w.count(len(values))
	data := nativeScalarBytes(values, width)
	if binary.NativeEndian.Uint16([]byte{1, 0}) == nativeLittleEndian || width == 1 {
		w.data = append(w.data, data...)
		return
	}
	start := len(w.data)
	w.data = append(w.data, make([]byte, len(data))...)
	copyScalarBytes(w.data[start:], data, width)
}

func copyScalarBytes(dst, src []byte, width int) {
	if binary.NativeEndian.Uint16([]byte{1, 0}) == nativeLittleEndian || width == 1 {
		copy(dst, src)
		return
	}
	for start := 0; start < len(src); start += width {
		for i := range width {
			dst[start+i] = src[start+width-1-i]
		}
	}
}

func (r *binaryReader) scalarSequence(element *descriptor, n int) any {
	switch element.Type {
	case "bool":
		raw := r.take(n)
		result := make([]bool, n)
		for i, b := range raw {
			if b > 1 {
				fail(r.pos, "invalid Boolean byte")
			}
			result[i] = b == 1
		}
		return result
	case "byte":
		return readScalarBlock[uint8](r, n)
	case "sbyte":
		return readScalarBlock[int8](r, n)
	case "char", "ushort":
		return readScalarBlock[uint16](r, n)
	case "short":
		return readScalarBlock[int16](r, n)
	case "uint":
		return readScalarBlock[uint32](r, n)
	case "int":
		return readScalarBlock[int32](r, n)
	case "ulong":
		return readScalarBlock[uint64](r, n)
	case "long":
		return readScalarBlock[int64](r, n)
	case "float":
		return readScalarBlock[float32](r, n)
	case "double":
		return readScalarBlock[float64](r, n)
	case "nint", "System.IntPtr":
		if r.s.nativeIntegerBytes == 4 {
			return readScalarBlock[int32](r, n)
		}
		return readScalarBlock[int64](r, n)
	case "nuint", "System.UIntPtr":
		if r.s.nativeIntegerBytes == 4 {
			return readScalarBlock[uint32](r, n)
		}
		return readScalarBlock[uint64](r, n)
	default:
		fail(r.pos, "unsupported scalar collection "+element.Type)
		return nil
	}
}

func (w *binaryWriter) typedScalarSequence(d *descriptor, value any) bool {
	if d.Kind != "scalar" {
		return false
	}
	name := d.Type
	switch v := value.(type) {
	case []bool:
		if name == "bool" {
			w.count(len(v))
			for _, b := range v {
				if b {
					w.byte(1)
				} else {
					w.byte(0)
				}
			}
			return true
		}
	case []uint8:
		if name == "byte" {
			writeScalarBlock(w, v)
			return true
		}
	case []int8:
		if name == "sbyte" {
			writeScalarBlock(w, v)
			return true
		}
	case []uint16:
		if name == "ushort" || name == "char" {
			writeScalarBlock(w, v)
			return true
		}
	case []int16:
		if name == "short" {
			writeScalarBlock(w, v)
			return true
		}
	case []uint32:
		if name == "uint" || w.s.nativeIntegerBytes == 4 && (name == "nuint" || name == "System.UIntPtr") {
			writeScalarBlock(w, v)
			return true
		}
	case []int32:
		if name == "int" || w.s.nativeIntegerBytes == 4 && (name == "nint" || name == "System.IntPtr") {
			writeScalarBlock(w, v)
			return true
		}
	case []uint64:
		if name == "ulong" || w.s.nativeIntegerBytes == 8 && (name == "nuint" || name == "System.UIntPtr") {
			writeScalarBlock(w, v)
			return true
		}
	case []int64:
		if name == "long" || w.s.nativeIntegerBytes == 8 && (name == "nint" || name == "System.IntPtr") {
			writeScalarBlock(w, v)
			return true
		}
	case []float32:
		if name == "float" {
			writeScalarBlock(w, v)
			return true
		}
	case []float64:
		if name == "double" {
			writeScalarBlock(w, v)
			return true
		}
	}
	return false
}
