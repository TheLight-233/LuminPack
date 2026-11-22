package luminpack

import (
	"slices"
	"strconv"
)

func (w *jsonWriter) field(m *member, value any) {
	if m.Wire != nil && m.Wire.Kind == "scalar" && m.Wire.Type == "int" {
		if n, ok := value.(int32); ok {
			// Reserve the complete head and the maximum signed Int32 width
			// together before publishing the record prefix.
			out := slices.Grow(w.data, len(m.jsonHead)+11)
			w.data = appendJSONInt32(append(out, m.jsonHead...), n)
			return
		}
	}
	w.data = append(w.data, m.jsonHead...)
	w.shape(m.Wire, value)
}

func (w *jsonWriter) nativeJSONScalar(name string, value any) bool {
	if bits, signed, width, ok := w.s.jsonInteger(name, value); ok {
		w.data = appendJSONInteger(w.data, bits, signed, width)
		return true
	}
	switch v := value.(type) {
	case bool:
		if name == "bool" {
			w.data = strconv.AppendBool(w.data, v)
			return true
		}
	case float32:
		if name == "float" {
			w.jsonFloat(float64(v), 32)
			return true
		}
	case float64:
		if name == "double" {
			w.jsonFloat(v, 64)
			return true
		}
	case uint16:
		if name == "char" {
			w.jsonChar(v)
			return true
		}
	}
	return false
}

func (s *Schema) jsonInteger(name string, value any) (bits uint64, signed bool, width int, ok bool) {
	switch v := value.(type) {
	case int8:
		return uint64(v), true, 1, name == "sbyte"
	case uint8:
		return uint64(v), false, 1, name == "byte"
	case int16:
		return uint64(v), true, 2, name == "short"
	case uint16:
		return uint64(v), false, 2, name == "ushort"
	case int32:
		return uint64(v), true, 4, name == "int" || s.nativeIntegerBytes == 4 && (name == "nint" || name == "System.IntPtr")
	case uint32:
		return uint64(v), false, 4, name == "uint" || s.nativeIntegerBytes == 4 && (name == "nuint" || name == "System.UIntPtr")
	case int64:
		return uint64(v), true, 8, name == "long" || s.nativeIntegerBytes == 8 && (name == "nint" || name == "System.IntPtr")
	case uint64:
		return v, false, 8, name == "ulong" || s.nativeIntegerBytes == 8 && (name == "nuint" || name == "System.UIntPtr")
	default:
		return 0, false, 0, false
	}
}

func (w *jsonWriter) integerPair(key, value *descriptor, entry Entry) bool {
	if key.Kind != "scalar" || value.Kind != "scalar" {
		return false
	}
	left, leftSigned, leftWidth, ok := w.s.jsonInteger(key.Type, entry.Key)
	if !ok {
		return false
	}
	right, rightSigned, rightWidth, ok := w.s.jsonInteger(value.Type, entry.Value)
	if !ok {
		return false
	}
	out := slices.Grow(w.data, 43)
	out = appendJSONInteger(append(out, '['), left, leftSigned, leftWidth)
	out = appendJSONInteger(append(out, ','), right, rightSigned, rightWidth)
	w.data = append(out, ']')
	return true
}

func (w *jsonWriter) nativeJSONItems(d *descriptor, value any) bool {
	if d.Kind != "scalar" {
		return false
	}
	switch v := value.(type) {
	case []int32:
		if d.Type == "int" {
			w.data = appendSignedJSONItems(w.data, v)
			return true
		}
	case []int64:
		if d.Type == "long" {
			w.data = appendSignedJSONItems(w.data, v)
			return true
		}
	case []uint32:
		if d.Type == "uint" {
			w.data = appendUnsignedJSONItems(w.data, v)
			return true
		}
	case []uint64:
		if d.Type == "ulong" {
			w.data = appendUnsignedJSONItems(w.data, v)
			return true
		}
	}
	return false
}

func appendSignedJSONItems[T int32 | int64](output []byte, values []T) []byte {
	if len(values) == 0 {
		return append(output, '[', ']')
	}
	separator := byte('[')
	for _, value := range values {
		output = append(output, separator)
		switch any(value).(type) {
		case int32:
			output = appendJSONInt32(output, int32(value))
		default:
			output = appendJSONInt64(output, int64(value))
		}
		separator = ','
	}
	return append(output, ']')
}

func appendUnsignedJSONItems[T uint32 | uint64](output []byte, values []T) []byte {
	if len(values) == 0 {
		return append(output, '[', ']')
	}
	separator := byte('[')
	for _, value := range values {
		output = append(output, separator)
		switch any(value).(type) {
		case uint32:
			output = appendJSONUint32(output, uint32(value))
		default:
			output = appendJSONUint64(output, uint64(value))
		}
		separator = ','
	}
	return append(output, ']')
}
