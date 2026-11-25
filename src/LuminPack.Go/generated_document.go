package luminpack

import (
	"bytes"
	"math"
	"strconv"
	"unsafe"
)

// Text distinguishes null, Unicode text and an exact UTF-16 value containing isolated surrogates.
type Text struct {
	Value   string
	Units   *UTF16String
	Present bool
}

// TypedEntry retains dictionary wire order in a generated model.
type TypedEntry[K, V any] struct {
	Key   K
	Value V
}

// GeneratedLimits contains the constraints compiled from the root manifest.
type GeneratedLimits struct {
	Input, Collection, String, Depth, JSONDepth int
	Complete                                    bool
}

type DocumentReader struct{ binaryReader }
type DocumentWriter struct{ binaryWriter }
type DocumentJSONReader struct {
	jsonScanner
	limit GeneratedLimits
}
type DocumentJSONWriter struct{ jsonWriter }

// ReadDocument decodes into the final Go model with the generated root body.
func ReadDocument[T any](data []byte, limits GeneratedLimits, read func(*DocumentReader, int) T) (v T, err error) {
	defer recoverFormat(&err)
	if len(data) > limits.Input {
		fail(0, "input exceeds configured limit")
	}
	r := DocumentReader{binaryReader{data: data, maxCollection: limits.Collection, maxString: limits.String}}
	v = read(&r, limits.Depth)
	if limits.Complete && r.pos != len(data) {
		fail(r.pos, "trailing binary data")
	}
	return
}

func WriteDocument[T any](v T, depth int, write func(*DocumentWriter, T, int)) (data []byte, err error) {
	defer recoverFormat(&err)
	slot := encodeScratch.Get().(*[]byte)
	w := DocumentWriter{binaryWriter{data: (*slot)[:0]}}
	defer func() { releaseEncodeScratch(slot, w.data) }()
	write(&w, v, depth)
	return bytes.Clone(w.data), nil
}

func ReadJSONDocument[T any](data []byte, limits GeneratedLimits, read func(*DocumentJSONReader, int) T) (v T, err error) {
	defer recoverFormat(&err)
	if len(data) > limits.Input {
		fail(0, "input exceeds configured limit")
	}
	r := DocumentJSONReader{jsonScanner{data: data}, limits}
	v = read(&r, limits.JSONDepth)
	r.end()
	return
}

func WriteJSONDocument[T any](v T, depth int, write func(*DocumentJSONWriter, T, int)) (data []byte, err error) {
	defer recoverFormat(&err)
	slot := encodeScratch.Get().(*[]byte)
	w := DocumentJSONWriter{jsonWriter{data: (*slot)[:0]}}
	defer func() { releaseEncodeScratch(slot, w.data) }()
	write(&w, v, depth)
	return bytes.Clone(w.data), nil
}

func DocumentDepth(depth int) {
	if depth <= 0 {
		fail(-1, "document exceeds nesting limit")
	}
}
func DocumentError(message string)    { fail(-1, message) }
func (r *DocumentReader) Tag() uint32 { return r.u32() }
func (r *DocumentReader) Header(n byte) bool {
	b := r.byte()
	if b == 255 {
		return false
	}
	if b != n {
		fail(r.pos, "invalid record header")
	}
	return true
}
func (r *DocumentReader) Count(minimum int) int { return r.count(minimum) }
func (r *DocumentReader) Text(profile int) Text {
	switch v := r.text(profile).(type) {
	case string:
		return Text{Value: v, Present: true}
	case UTF16String:
		return Text{Units: &v, Present: true}
	default:
		return Text{}
	}
}
func (w *DocumentWriter) Tag(tag uint32)    { w.u32(tag) }
func (w *DocumentWriter) Header(count byte) { w.byte(count) }
func (w *DocumentWriter) Count(n int)       { w.count(n) }
func (w *DocumentWriter) Text(v Text, profile int) {
	if !v.Present {
		w.text(nil, profile)
	} else if v.Units != nil {
		w.text(*v.Units, profile)
	} else {
		w.text(v.Value, profile)
	}
}
func (r *DocumentJSONReader) Match(token string) bool {
	if !bytes.HasPrefix(r.data[r.pos:], []byte(token)) {
		return false
	}
	r.pos += len(token)
	return true
}
func (r *DocumentJSONReader) Expect(c byte)      { r.expect(c) }
func (r *DocumentJSONReader) Accept(c byte) bool { return r.accept(c) }
func (r *DocumentJSONReader) Null() bool         { return r.null() }
func (r *DocumentJSONReader) RequireKey() {
	if r.null() {
		fail(r.pos, "null dictionary key")
	}
}
func (r *DocumentJSONReader) Name() string { r.expect('"'); return r.text() }
func (r *DocumentJSONReader) Text() Text {
	if r.null() {
		return Text{}
	}
	v := r.Name()
	if len(v) > r.limit.String {
		if units, _ := measureUTF16(v); units > r.limit.String {
			fail(r.pos, "string limit")
		}
	}
	return Text{Value: v, Present: true}
}
func (r *DocumentJSONReader) numberBytes() []byte {
	r.space()
	if r.pos == len(r.data) {
		fail(r.pos, "expected number")
	}
	return r.numberToken()
}
func (r *DocumentJSONReader) Int() int32 {
	if r.null() {
		return 0
	}
	if r.pos < len(r.data) {
		if n, ok := r.int32(); ok {
			return n
		}
	}
	fail(r.pos, "invalid Int32")
	return 0
}
func (r *DocumentJSONReader) Long() int64 {
	if r.null() {
		return 0
	}
	v, e := strconv.ParseInt(jsonNumberView(r.numberBytes()), 10, 64)
	if e != nil {
		fail(r.pos, "invalid Int64")
	}
	return v
}
func (r *DocumentJSONReader) Byte() byte {
	n := r.Int()
	if n < 0 || n > 255 {
		fail(r.pos, "invalid byte")
	}
	return byte(n)
}
func (r *DocumentJSONReader) Bool() bool {
	if r.null() {
		return false
	}
	if r.Match("true") {
		return true
	}
	r.literal("false")
	return false
}
func (r *DocumentJSONReader) Float() float32 {
	r.space()
	start := r.pos
	if start == len(r.data) {
		fail(start, "expected float")
	}
	if r.data[start] == 'n' {
		r.literal("null")
		return 0
	}
	cursor := start
	negative := r.data[cursor] == '-'
	if negative {
		cursor++
	}
	first := cursor
	var magnitude uint32
	for cursor < len(r.data) && cursor-first < 8 {
		digit := r.data[cursor] - '0'
		if digit > 9 {
			break
		}
		magnitude = magnitude*10 + uint32(digit)
		cursor++
	}
	if cursor > first && (r.data[first] != '0' || cursor == first+1) {
		places := 0
		if cursor < len(r.data) && r.data[cursor] == '.' {
			cursor++
			for cursor < len(r.data) && cursor-first <= 8 {
				digit := r.data[cursor] - '0'
				if digit > 9 {
					break
				}
				magnitude = magnitude*10 + uint32(digit)
				places++
				cursor++
			}
			if places == 0 && (cursor == len(r.data) || r.data[cursor] < '0' || r.data[cursor] > '9') {
				fail(cursor, "expected fractional digits")
			}
		}
		// Divide two exactly representable operands directly in binary32.
		if magnitude <= 1<<24 && places < 8 && (cursor == len(r.data) || isDocumentDelimiter(r.data[cursor])) {
			r.pos = cursor
			f := float32(magnitude)
			if places != 0 {
				f /= [...]float32{1, 10, 100, 1000, 10000, 100000, 1000000, 10000000}[places]
			}
			if negative {
				return -f
			}
			return f
		}
	}
	v, e := strconv.ParseFloat(jsonNumberView(r.numberToken()), 32)
	if e != nil {
		fail(start, "invalid float")
	}
	return float32(v)
}

func isDocumentDelimiter(c byte) bool {
	switch c {
	case ',', ']', '}', ' ', '\t', '\r', '\n':
		return true
	}
	return false
}
func (r *DocumentJSONReader) CheckCount(n int) {
	if n >= r.limit.Collection {
		fail(r.pos, "collection limit")
	}
}
func (r *DocumentJSONReader) Skip(depth int) { r.skip(depth) }
func (r *DocumentJSONReader) Capture(depth int) *DocumentJSONReader {
	r.space()
	start := r.pos
	r.Skip(depth)
	return &DocumentJSONReader{jsonScanner{data: r.data[start:r.pos]}, r.limit}
}
func (r *DocumentJSONReader) FinishUnion(depth int) {
	for !r.Accept('}') {
		r.Expect(',')
		r.Name()
		r.Expect(':')
		r.Skip(depth)
	}
}
func (r *DocumentJSONReader) Tag() uint32 {
	v, e := strconv.ParseUint(jsonNumberView(r.numberBytes()), 10, 32)
	if e != nil {
		fail(r.pos, "invalid union tag")
	}
	return uint32(v)
}
func (w *DocumentJSONWriter) Raw(s string) { w.data = append(w.data, s...) }
func (w *DocumentJSONWriter) Int(v int32)  { w.data = appendJSONInt32(w.data, v) }
func (w *DocumentJSONWriter) Long(v int64) { w.data = appendJSONInt64(w.data, v) }
func (w *DocumentJSONWriter) Byte(v byte)  { w.data = appendJSONUint32(w.data, uint32(v)) }
func (w *DocumentJSONWriter) Float(v float32) {
	w.data = appendJSONFloat32(w.data, math.Float32bits(v))
}
func (w *DocumentJSONWriter) Bool(v bool) { w.data = strconv.AppendBool(w.data, v) }
func (w *DocumentJSONWriter) Text(v Text) {
	if !v.Present {
		w.Raw("null")
	} else if v.Units != nil {
		w.jsonUnits(*v.Units)
	} else {
		w.text(v.Value)
	}
}

func (v Text) Identity() string {
	if v.Units == nil {
		return v.Value
	}
	if text, ok := fromUnits(*v.Units, false).(string); ok {
		return text
	}
	return "\xff" + string(utf16Bytes(*v.Units))
}
func (r *DocumentReader) Int() int32     { return int32(r.u32()) }
func (r *DocumentReader) Long() int64    { return int64(r.u64()) }
func (r *DocumentReader) Float() float32 { return math.Float32frombits(r.u32()) }
func (r *DocumentReader) Bool() bool {
	b := r.byte()
	if b > 1 {
		fail(r.pos, "invalid Boolean")
	}
	return b != 0
}
func (r *DocumentReader) Byte() byte      { return r.byte() }
func (w *DocumentWriter) Int(v int32)     { w.u32(uint32(v)) }
func (w *DocumentWriter) Long(v int64)    { w.u64(uint64(v)) }
func (w *DocumentWriter) Float(v float32) { w.u32(math.Float32bits(v)) }
func (w *DocumentWriter) Bool(v bool) {
	if v {
		w.byte(1)
	} else {
		w.byte(0)
	}
}
func (w *DocumentWriter) Byte(v byte) { w.byte(v) }

// ReadDocumentScalars copies a validated little-endian payload into the result slice.
func ReadDocumentScalars[T blockScalar](r *DocumentReader) []T {
	n := r.Count(int(unsafe.Sizeof(*new(T))))
	if n < 0 {
		return nil
	}
	return readScalarBlock[T](&r.binaryReader, n)
}

// WriteDocumentScalars emits the count and payload as one contiguous scalar block.
func WriteDocumentScalars[T blockScalar](w *DocumentWriter, v []T) {
	if v == nil {
		w.Count(-1)
		return
	}
	writeScalarBlock(&w.binaryWriter, v)
}
