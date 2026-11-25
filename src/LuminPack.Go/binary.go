package luminpack

import (
	"encoding/binary"
	"encoding/json"
	"math"
	"reflect"
	"strconv"
	"unicode/utf16"
	"unicode/utf8"
)

type binaryReader struct {
	data                               []byte
	pos                                int
	maxCollection, maxString, maxDepth int
	depth                              int
	s                                  *Schema
	refs                               map[uint32]reference
	versions                           [][]uint64
}

type binaryWriter struct {
	data            []byte
	s               *Schema
	maxDepth, depth int
	refs            map[uintptr]reference
	versions        [][]uint64
}

type reference struct {
	id    uint32
	c     *contract
	value Record
}

func (r *binaryReader) take(n int) []byte {
	if n < 0 || n > len(r.data)-r.pos {
		fail(r.pos, "truncated binary record")
	}
	start := r.pos
	r.pos += n
	return r.data[start:r.pos]
}
func (r *binaryReader) byte() byte  { return r.take(1)[0] }
func (r *binaryReader) u16() uint16 { return binary.LittleEndian.Uint16(r.take(2)) }
func (r *binaryReader) u32() uint32 { return binary.LittleEndian.Uint32(r.take(4)) }
func (r *binaryReader) u64() uint64 { return binary.LittleEndian.Uint64(r.take(8)) }
func (r *binaryReader) count(min int) int {
	n := int(int32(r.u32()))
	if n < -1 || n > r.maxCollection || (min > 0 && n > (len(r.data)-r.pos)/min) {
		fail(r.pos, "invalid collection count")
	}
	return n
}
func (r *binaryReader) descend() {
	if r.depth >= r.maxDepth {
		fail(r.pos, "maximum binary nesting exceeded")
	}
	r.depth++
}
func (w *binaryWriter) descend() {
	if w.depth >= w.maxDepth {
		fail(-1, "maximum binary nesting exceeded")
	}
	w.depth++
}
func (w *binaryWriter) byte(v byte)  { w.data = append(w.data, v) }
func (w *binaryWriter) u16(v uint16) { w.data = binary.LittleEndian.AppendUint16(w.data, v) }
func (w *binaryWriter) u32(v uint32) { w.data = binary.LittleEndian.AppendUint32(w.data, v) }
func (w *binaryWriter) u64(v uint64) { w.data = binary.LittleEndian.AppendUint64(w.data, v) }
func (w *binaryWriter) count(n int) {
	if n < -1 || n > math.MaxInt32 {
		fail(-1, "invalid collection size")
	}
	w.u32(uint32(int32(n)))
}

// DecodeBinary reads a document using the root's declared string profile.
func (s *Schema) DecodeBinary(typeID string, data []byte) (any, error) {
	return s.decodeBinary(typeID, data, -1)
}

// DecodeBinaryWithProfile overrides the root operation's built-in binary profile.
func (s *Schema) DecodeBinaryWithProfile(typeID string, data []byte, profile int) (any, error) {
	return s.decodeBinary(typeID, data, profile)
}

func (s *Schema) decodeBinary(typeID string, data []byte, selected int) (value any, err error) {
	defer recoverFormat(&err)
	c, shape, p := s.operation(typeID)
	if !p.Trusted && len(data) > limit(p.MaxInput, math.MaxInt32) {
		fail(0, "binary input exceeds configured limit")
	}
	if selected < 0 {
		selected = p.Binary
		if (c == nil || s.types[s.root] == c) && s.binaryProfile != nil {
			selected = *s.binaryProfile
		}
	}
	validateBinaryProfile(selected)
	r := binaryReader{data: data, s: s, maxCollection: limit(p.MaxCollection, math.MaxInt32), maxString: limit(p.MaxString, math.MaxInt32), maxDepth: min(512, limit(p.MaxDepth, 512))}
	if shape != nil {
		value = r.shape(shape, selected)
	} else {
		value = r.contract(c, selected, false)
	}
	if s.complete && r.pos != len(data) {
		fail(r.pos, "trailing binary bytes")
	}
	return value, nil
}

// EncodeBinary writes a document using the root's declared string profile.
func (s *Schema) EncodeBinary(typeID string, value any) ([]byte, error) {
	return s.AppendBinary(nil, typeID, value)
}

// AppendBinary appends a complete document to a reusable caller-owned buffer.
func (s *Schema) AppendBinary(output []byte, typeID string, value any) ([]byte, error) {
	return s.encodeBinary(output, typeID, value, -1)
}

// EncodeBinaryWithProfile overrides the root operation's built-in binary profile.
func (s *Schema) EncodeBinaryWithProfile(typeID string, value any, profile int) ([]byte, error) {
	return s.encodeBinary(nil, typeID, value, profile)
}

func (s *Schema) encodeBinary(out []byte, typeID string, value any, selected int) (data []byte, err error) {
	defer recoverFormat(&err)
	c, shape, p := s.operation(typeID)
	if selected < 0 {
		selected = p.Binary
		if (c == nil || s.types[s.root] == c) && s.binaryProfile != nil {
			selected = *s.binaryProfile
		}
	}
	validateBinaryProfile(selected)
	w := binaryWriter{data: out, s: s, maxDepth: 512}
	if shape != nil {
		w.shape(shape, value, selected)
	} else {
		w.contract(c, value, selected, false)
	}
	return w.data, nil
}

func validateBinaryProfile(value int) {
	if value < 0 || value > 5 {
		fail(-1, "unsupported binary string profile")
	}
}

func (r *binaryReader) contract(c *contract, inherited int, body bool) any {
	p := effectiveProfile(c.Profile, inherited)
	if c.Profile.TrackReferences {
		return r.tracked(c, p)
	}
	r.descend()
	defer func() { r.depth-- }()
	switch c.Kind {
	case "Union":
		hash := r.u32()
		if hash == 0 {
			return nil
		}
		if hash == math.MaxUint32 && c.UnionRegistration {
			tag := r.registeredTag(c)
			selected, ok := r.s.registered[c].byTag[tag]
			if !ok {
				fail(r.pos, "unknown registered Union tag")
			}
			value := r.readCustom(selected.codec, selected.id)
			if value == nil {
				fail(r.pos, "registered Union case has no payload")
			}
			return Union{selected.id, value}
		}
		choice := c.byHash[hash]
		if choice == nil || choice.target == nil {
			fail(r.pos, "unknown Union case")
		}
		value := r.contract(choice.target, p, true)
		if value == nil {
			return nil
		}
		return Union{choice.target.ID, value}
	case "Record", "Struct":
		if plan := c.standaloneStorage(); plan != nil {
			return r.readNativeLayout(plan, func(child *binaryReader) any { return child.readStorage(c, nil, plan, p) })
		}
		if c.Storage != nil {
			return r.readStorage(c, nil, c.Storage, p)
		}
		if c.rawFields != nil {
			return c.rawFields.read(r.take(c.rawFields.width))
		}
		return r.body(c, p, body, nil)
	case "Custom":
		return r.readCustom(r.s.custom[normalize(c.ID)], c.ID)
	default:
		fail(r.pos, "unsupported contract kind "+c.Kind)
	}
	return nil
}

func inlineStruct(c *contract) bool {
	return c.Kind == "Struct" && c.StorageKind != "Managed" && c.StorageKind != "Object"
}

func (r *binaryReader) body(c *contract, p int, body bool, target Record) any {
	if c.Versioned {
		return r.readVersion(c, p, target)
	}
	if !body && !inlineStruct(c) {
		count := r.byte()
		if count == 255 {
			return nil
		}
		if int(count) != c.headerCount() {
			fail(r.pos, "positional member count does not match contract")
		}
	}
	if target == nil {
		target = make(Record, len(c.Members))
	}
	for i := range c.Members {
		m := &c.Members[i]
		target[m.Name] = r.readMember(m, m.Wire, p)
	}
	return target
}

func (w *binaryWriter) contract(c *contract, data any, inherited int, body bool) {
	p := effectiveProfile(c.Profile, inherited)
	if c.Profile.TrackReferences {
		w.tracked(c, data, p)
		return
	}
	w.descend()
	defer func() { w.depth-- }()
	switch c.Kind {
	case "Union":
		if data == nil {
			w.u32(0)
			return
		}
		choice, ok := data.(Union)
		if !ok {
			fail(-1, "expected Union value for "+c.ID)
		}
		selected := c.byType[normalize(choice.Type)]
		if selected == nil || selected.target == nil {
			external, ok := w.s.registered[c].byType[normalize(choice.Type)]
			if !ok || choice.Value == nil {
				fail(-1, "unknown or empty Union case "+choice.Type)
			}
			w.u32(math.MaxUint32)
			w.registeredTag(c, external.tag)
			w.writeCustom(external.codec, external.id, choice.Value)
			return
		}
		w.u32(selected.Hash)
		w.contract(selected.target, choice.Value, p, true)
	case "Record", "Struct":
		if plan := c.standaloneStorage(); plan != nil {
			w.writeNativeLayout(plan, data, func(child *binaryWriter, value any) { child.writeStorage(c, nil, plan, value, p) })
		} else if c.Storage != nil {
			w.writeStorage(c, nil, c.Storage, data, p)
		} else if c.rawFields != nil {
			c.rawFields.write(w, data)
		} else {
			w.body(c, p, body, data)
		}
	case "Custom":
		w.writeCustom(w.s.custom[normalize(c.ID)], c.ID, data)
	default:
		fail(-1, "unsupported contract kind "+c.Kind)
	}
}

func asRecord(data any) Record {
	switch value := data.(type) {
	case Record:
		return value
	case map[string]any:
		return Record(value)
	default:
		fail(-1, "expected Record value")
		return nil
	}
}

func (w *binaryWriter) body(c *contract, p int, body bool, data any) {
	if c.Versioned {
		w.writeVersion(c, p, data)
		return
	}
	if data == nil {
		if body || inlineStruct(c) {
			fail(-1, "expected non-null body for "+c.ID)
		}
		w.byte(255)
		return
	}
	value := asRecord(data)
	if !body && !inlineStruct(c) {
		if c.headerCount() > 249 {
			fail(-1, "too many positional fields")
		}
		w.byte(byte(c.headerCount()))
	}
	for i := range c.Members {
		m := &c.Members[i]
		w.writeMember(m, m.Wire, value[m.Name], p)
	}
}

func (s *Schema) minimum(d *descriptor, p int) int {
	if d == nil {
		return 1
	}
	switch d.Kind {
	case "scalar":
		return s.scalarWidth(d.Type)
	case "string":
		switch p {
		case 0:
			return 8
		case 2:
			return 4
		case 3, 4:
			return 2
		default:
			return 1
		}
	case "contract":
		c := s.get(d.Type)
		if c.Kind == "Union" {
			return 4
		}
		return 1
	case "sequence", "dictionary":
		return 4
	case "nullable":
		if d.Width > 0 {
			return d.Width
		}
		return 1
	default:
		return 1
	}
}

func descriptorProfile(d *descriptor, p int) int {
	if d.Profile != nil {
		return *d.Profile
	}
	return p
}

func (r *binaryReader) shape(d *descriptor, p int) any {
	if d == nil {
		fail(r.pos, "missing portable field descriptor")
	}
	p = descriptorProfile(d, p)
	switch d.Kind {
	case "scalar":
		return r.scalar(d.Type)
	case "string":
		return r.text(p)
	case "contract":
		return r.contract(r.s.get(d.Type), p, false)
	case "sequence":
		return r.sequence(d, p)
	case "dictionary":
		n := r.count(r.s.minimum(d.Key, p) + r.s.minimum(d.Value, p))
		if n < 0 {
			return nil
		}
		result := make(Dictionary, 0, min(n, 128))
		seen := make(map[any]bool, min(n, 128))
		value := d.Value
		native := -1
		if value.isNullableSlot() {
			native = r.s.nullableModel(value, 0)
		}
		for range n {
			key := r.shape(d.Key, p)
			if key == nil || duplicateKey(seen, key) {
				fail(r.pos, "null or duplicate dictionary key")
			}
			if native >= 0 {
				result = append(result, Entry{key, r.readNullableStorage(value, native, p)})
			} else {
				result = append(result, Entry{key, r.shape(value, p)})
			}
		}
		return result
	case "nullable":
		switch marker := r.byte(); marker {
		case 0:
			return r.shape(d.Element, p)
		case 255:
			if d.Width > 1 {
				r.take(d.Width - 1)
			}
			return nil
		default:
			fail(r.pos, "invalid nullable marker")
		}
	case "pair", "tuple":
		if d.Raw && d.RawCompoundStorage != nil {
			return r.readNativeLayout(d.RawCompoundStorage, func(child *binaryReader) any { return child.readStorage(nil, d, d.RawCompoundStorage, p) })
		}
		values := make([]any, len(d.Elements))
		for i, e := range d.Elements {
			values[i] = r.shape(e, p)
		}
		return values
	case "rectangular":
		if r.byte() == 255 {
			return nil
		}
		if d.Rank < 2 || d.Rank > 32 {
			fail(r.pos, "invalid rectangular rank")
		}
		result := Rectangular{Lengths: make([]int32, d.Rank), LowerBounds: make([]int32, d.Rank)}
		for i := range result.Lengths {
			result.Lengths[i] = int32(r.u32())
		}
		n := r.count(r.s.minimum(d.Element, p))
		if n != dimensions(result.Lengths) {
			fail(r.pos, "rectangular dimensions do not match elements")
		}
		result.Values = r.elements(d, n, p)
		return result
	default:
		fail(r.pos, "unsupported field descriptor "+d.Kind)
	}
	return nil
}

func (r *binaryReader) sequence(d *descriptor, p int) any {
	n := r.count(r.s.minimum(d.Element, p))
	if n < 0 {
		if nullSequenceIsEmpty(d) {
			return []any{}
		}
		return nil
	}
	if d.Element.Kind == "scalar" {
		return r.scalarSequence(d.Element, n)
	}
	return r.elements(d, n, p)
}

func (r *binaryReader) elements(d *descriptor, n, p int) []any {
	if n == 0 {
		return []any{}
	}
	if plan, c, nullable, tagged := r.s.sequenceStorage(d); plan != nil {
		index := 0
		if tagged {
			index = int(r.byte()) - 1
		}
		if nullable {
			index = r.s.nullableModel(d.Element, index)
		}
		stride, _ := plan.model(index)
		if n > (len(r.data)-r.pos)/stride {
			fail(r.pos, "truncated collection storage")
		}
		values := make([]any, n)
		for i := range values {
			if nullable {
				values[i] = r.readNullableStorage(d.Element, index, p)
			} else if c == nil && d.Element.RawCompoundStorage == plan {
				values[i] = r.readNativeLayout(plan, func(child *binaryReader) any { return child.readStorage(nil, d.Element, plan, p) })
			} else {
				values[i] = r.readLayout(plan, index, func(child *binaryReader) any { return child.readStorage(c, d.Element, plan, p) })
			}
		}
		return values
	}
	if raw := r.s.sequenceRawFields(d); raw != nil {
		r.descend()
		defer func() { r.depth-- }()
		if n > (len(r.data)-r.pos)/raw.width {
			fail(r.pos, "truncated collection storage")
		}
		data := r.take(n * raw.width)
		values := make([]any, n)
		for i := range values {
			offset := i * raw.width
			values[i] = raw.read(data[offset : offset+raw.width])
		}
		return values
	}
	values := make([]any, 0, min(n, 128))
	for range n {
		values = append(values, r.shape(d.Element, p))
	}
	return values
}

func duplicateKey(seen map[any]bool, value any) bool {
	switch v := value.(type) {
	case float32:
		if math.IsNaN(float64(v)) {
			value = math.Float32bits(float32(math.NaN()))
		}
	case float64:
		if math.IsNaN(v) {
			value = math.Float64bits(math.NaN())
		}
	case UTF16String:
		value = string(utf16Bytes(v))
	}
	if t := reflect.TypeOf(value); t == nil || !t.Comparable() {
		return false
	}
	duplicate := seen[value]
	seen[value] = true
	return duplicate
}

func asSequence(value any) []any {
	if seq, ok := value.([]any); ok {
		return seq
	}
	v := reflect.ValueOf(value)
	if !v.IsValid() || (v.Kind() != reflect.Slice && v.Kind() != reflect.Array) {
		fail(-1, "expected sequence value")
	}
	result := make([]any, v.Len())
	for i := range result {
		result[i] = v.Index(i).Interface()
	}
	return result
}

func (w *binaryWriter) shape(d *descriptor, value any, p int) {
	if d == nil {
		fail(-1, "missing portable field descriptor")
	}
	p = descriptorProfile(d, p)
	switch d.Kind {
	case "scalar":
		w.scalar(d.Type, value)
	case "string":
		w.text(value, p)
	case "contract":
		w.contract(w.s.get(d.Type), value, p, false)
	case "sequence":
		if value == nil && !nullSequenceIsEmpty(d) {
			w.count(-1)
			return
		}
		if value == nil {
			w.count(0)
			return
		}
		if w.typedScalarSequence(d.Element, value) {
			return
		}
		seq := asSequence(value)
		w.count(len(seq))
		w.elements(d, seq, p)
	case "dictionary":
		if value == nil {
			w.count(-1)
			return
		}
		entries, ok := value.(Dictionary)
		if !ok {
			fail(-1, "expected Dictionary value")
		}
		seen := make(map[any]bool, len(entries))
		w.count(len(entries))
		native := -1
		if d.Value.isNullableSlot() {
			native = w.s.nullableModel(d.Value, 0)
		}
		for _, entry := range entries {
			if entry.Key == nil || duplicateKey(seen, entry.Key) {
				fail(-1, "null or duplicate dictionary key")
			}
			w.shape(d.Key, entry.Key, p)
			if native >= 0 {
				w.writeNullableStorage(d.Value, native, entry.Value, p)
			} else {
				w.shape(d.Value, entry.Value, p)
			}
		}
	case "nullable":
		if value == nil {
			w.byte(255)
			if d.Width > 1 {
				w.data = append(w.data, make([]byte, d.Width-1)...)
			}
		} else {
			w.byte(0)
			w.shape(d.Element, value, p)
		}
	case "pair", "tuple":
		if d.Raw && d.RawCompoundStorage != nil {
			w.writeNativeLayout(d.RawCompoundStorage, value, func(child *binaryWriter, value any) { child.writeStorage(nil, d, d.RawCompoundStorage, value, p) })
			return
		}
		seq := asSequence(value)
		if len(seq) != len(d.Elements) {
			fail(-1, "wrong tuple arity")
		}
		for i, element := range d.Elements {
			w.shape(element, seq[i], p)
		}
	case "rectangular":
		if value == nil {
			w.byte(255)
			return
		}
		arr, ok := value.(Rectangular)
		if !ok || len(arr.Lengths) != d.Rank || len(arr.LowerBounds) != d.Rank || dimensions(arr.Lengths) != len(arr.Values) {
			fail(-1, "invalid rectangular value")
		}
		for _, v := range arr.LowerBounds {
			if v != 0 {
				fail(-1, "binary rectangular lower bounds must be zero")
			}
		}
		w.byte(0)
		for _, dim := range arr.Lengths {
			w.u32(uint32(dim))
		}
		w.count(len(arr.Values))
		w.elements(d, arr.Values, p)
	default:
		fail(-1, "unsupported field descriptor "+d.Kind)
	}
}

// nullSequenceIsEmpty handles an explicit null at a sequence boundary.
func nullSequenceIsEmpty(d *descriptor) bool {
	if d.Required {
		fail(-1, "null collection base")
	}
	return isMemory(d)
}

func isMemory(d *descriptor) bool {
	if d.Memory {
		return true
	}
	switch d.Family {
	case "System.Memory", "System.ReadOnlyMemory", "System.Buffers.ReadOnlySequence":
		return true
	}
	return false
}

func dimensions(lengths []int32) int {
	empty := false
	for _, length := range lengths {
		if length < 0 {
			fail(-1, "negative rectangular dimension")
		}
		empty = empty || length == 0
	}
	if empty {
		return 0
	}
	result := int64(1)
	for _, length := range lengths {
		result *= int64(length)
		if result > math.MaxInt32 {
			fail(-1, "rectangular dimensions exceed element limit")
		}
	}
	return int(result)
}

func (s *Schema) scalarWidth(name string) int {
	if width := s.knownScalarWidth(name); width != 0 {
		return width
	}
	fail(-1, "unsupported scalar "+name)
	return 0
}

func (s *Schema) knownScalarWidth(name string) int {
	switch name {
	case "bool", "byte", "sbyte":
		return 1
	case "char", "short", "ushort":
		return 2
	case "int", "uint", "float":
		return 4
	case "long", "ulong", "double":
		return 8
	case "nint", "System.IntPtr", "nuint", "System.UIntPtr":
		return s.nativeIntegerBytes
	}
	return 0
}

func (r *binaryReader) scalar(name string) any {
	switch name {
	case "bool":
		b := r.byte()
		if b > 1 {
			fail(r.pos, "invalid Boolean byte")
		}
		return b != 0
	case "byte":
		return r.byte()
	case "sbyte":
		return int8(r.byte())
	case "char", "ushort":
		return r.u16()
	case "short":
		return int16(r.u16())
	case "int":
		return int32(r.u32())
	case "uint":
		return r.u32()
	case "long":
		return int64(r.u64())
	case "ulong":
		return r.u64()
	case "float":
		return math.Float32frombits(r.u32())
	case "double":
		return math.Float64frombits(r.u64())
	case "nint", "System.IntPtr":
		if r.s.nativeIntegerBytes == 4 {
			return int32(r.u32())
		}
		return int64(r.u64())
	case "nuint", "System.UIntPtr":
		if r.s.nativeIntegerBytes == 4 {
			return r.u32()
		}
		return r.u64()
	}
	fail(r.pos, "unsupported scalar "+name)
	return nil
}

func numericString(value any) string {
	if number, ok := value.(json.Number); ok {
		return string(number)
	}
	if value == nil {
		return "0"
	}
	v := reflect.ValueOf(value)
	switch v.Kind() {
	case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
		return strconv.FormatInt(v.Int(), 10)
	case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64, reflect.Uintptr:
		return strconv.FormatUint(v.Uint(), 10)
	case reflect.Float32, reflect.Float64:
		return strconv.FormatFloat(v.Float(), 'g', -1, v.Type().Bits())
	}
	fail(-1, "expected numeric scalar")
	return ""
}

func (s *Schema) scalarValue(name string, value any) any {
	if name == "bool" {
		if value == nil {
			return false
		}
		if b, ok := value.(bool); ok {
			return b
		}
		fail(-1, "expected Boolean scalar")
	}
	if name == "char" {
		if str, ok := value.(string); ok {
			r, _ := utf8.DecodeRuneInString(str)
			if str == "" {
				return uint16(0)
			}
			if r > 0xffff {
				return uint16(0xd800 + (r-0x10000)>>10)
			}
			return uint16(r)
		}
		if units, ok := value.(UTF16String); ok && len(units) > 0 {
			return units[0]
		}
	}
	if name == "float" || name == "double" {
		if v, ok := value.(float32); ok && name == "float" {
			return v
		}
		if v, ok := value.(float64); ok && name == "double" {
			return v
		}
		digits, ok := value.(string)
		if !ok {
			digits = numericString(value)
		}
		f, err := strconv.ParseFloat(digits, s.scalarWidth(name)*8)
		if err != nil {
			fail(-1, "invalid floating-point value")
		}
		if name == "float" {
			return float32(f)
		}
		return f
	}
	if value == nil {
		switch name {
		case "sbyte", "short", "int", "long", "nint", "System.IntPtr":
			return int64(0)
		default:
			return uint64(0)
		}
	}
	switch x := value.(type) {
	case uint8:
		if name == "byte" {
			return uint64(x)
		}
	case uint16:
		if name == "ushort" || name == "char" {
			return uint64(x)
		}
	case uint32:
		if name == "uint" || ((name == "nuint" || name == "System.UIntPtr") && s.nativeIntegerBytes == 4) {
			return uint64(x)
		}
	case uint64:
		if name == "ulong" || ((name == "nuint" || name == "System.UIntPtr") && s.nativeIntegerBytes == 8) {
			return x
		}
	case int8:
		if name == "sbyte" {
			return int64(x)
		}
	case int16:
		if name == "short" {
			return int64(x)
		}
	case int32:
		if name == "int" || ((name == "nint" || name == "System.IntPtr") && s.nativeIntegerBytes == 4) {
			return int64(x)
		}
	case int64:
		if name == "long" || ((name == "nint" || name == "System.IntPtr") && s.nativeIntegerBytes == 8) {
			return x
		}
	}
	width := s.scalarWidth(name) * 8
	digits := numericString(value)
	switch name {
	case "sbyte", "short", "int", "long", "nint", "System.IntPtr":
		n, err := strconv.ParseInt(digits, 10, width)
		if err != nil {
			fail(-1, "scalar out of signed range")
		}
		return n
	default:
		n, err := strconv.ParseUint(digits, 10, width)
		if err != nil {
			fail(-1, "scalar out of unsigned range")
		}
		return n
	}
}

func (w *binaryWriter) scalar(name string, value any) {
	if w.nativeScalar(name, value) {
		return
	}
	var bits uint64
	switch v := w.s.scalarValue(name, value).(type) {
	case bool:
		if v {
			bits = 1
		}
	case float32:
		bits = uint64(math.Float32bits(v))
	case float64:
		bits = math.Float64bits(v)
	case int64:
		bits = uint64(v)
	case uint64:
		bits = v
	case uint16:
		bits = uint64(v)
	}
	switch w.s.scalarWidth(name) {
	case 1:
		w.byte(byte(bits))
	case 2:
		w.u16(uint16(bits))
	case 4:
		w.u32(uint32(bits))
	case 8:
		w.u64(bits)
	}
}

func (w *binaryWriter) nativeScalar(name string, value any) bool {
	switch v := value.(type) {
	case bool:
		if name == "bool" {
			if v {
				w.byte(1)
			} else {
				w.byte(0)
			}
			return true
		}
	case int8:
		if name == "sbyte" {
			w.byte(byte(v))
			return true
		}
	case uint8:
		if name == "byte" {
			w.byte(v)
			return true
		}
	case int16:
		if name == "short" {
			w.u16(uint16(v))
			return true
		}
	case uint16:
		if name == "ushort" || name == "char" {
			w.u16(v)
			return true
		}
	case int32:
		if name == "int" || w.s.nativeIntegerBytes == 4 && (name == "nint" || name == "System.IntPtr") {
			w.u32(uint32(v))
			return true
		}
	case uint32:
		if name == "uint" || w.s.nativeIntegerBytes == 4 && (name == "nuint" || name == "System.UIntPtr") {
			w.u32(v)
			return true
		}
	case int64:
		if name == "long" || w.s.nativeIntegerBytes == 8 && (name == "nint" || name == "System.IntPtr") {
			w.u64(uint64(v))
			return true
		}
	case uint64:
		if name == "ulong" || w.s.nativeIntegerBytes == 8 && (name == "nuint" || name == "System.UIntPtr") {
			w.u64(v)
			return true
		}
	case float32:
		if name == "float" {
			w.u32(math.Float32bits(v))
			return true
		}
	case float64:
		if name == "double" {
			w.u64(math.Float64bits(v))
			return true
		}
	}
	return false
}

func utf16Bytes(value []uint16) []byte {
	out := make([]byte, len(value)*2)
	for i, unit := range value {
		binary.LittleEndian.PutUint16(out[i*2:], unit)
	}
	return out
}

func fromUnits(units []uint16, replace bool) any {
	if !replace {
		for i := 0; i < len(units); i++ {
			u := units[i]
			if u >= 0xd800 && u <= 0xdbff && i+1 < len(units) && units[i+1] >= 0xdc00 && units[i+1] <= 0xdfff {
				i++
				continue
			}
			if u >= 0xd800 && u <= 0xdfff {
				return UTF16String(units)
			}
		}
	}
	return string(utf16.Decode(units))
}
