package luminpack

import (
	"encoding/json"
	"math"
	"strconv"
	"unicode/utf8"
)

// JSONEncoding identifies the managed UTF-8 and little-endian UTF-16 encodings.
type JSONEncoding int

const (
	UTF8 JSONEncoding = iota
	UTF16
)

type jsonPair struct {
	key   string
	value any
}
type jsonObject []jsonPair

func (v jsonObject) prop(name string) (any, bool) {
	for _, item := range v {
		if item.key == name {
			return item.value, true
		}
	}
	return nil, false
}

func obj(value any) jsonObject {
	if result, ok := value.(jsonObject); ok {
		return result
	}
	fail(-1, "expected JSON object")
	return nil
}

func array(value any) []any {
	if result, ok := value.([]any); ok {
		return result
	}
	fail(-1, "expected JSON array")
	return nil
}

type jsonReader struct {
	s        *Schema
	encoding JSONEncoding
	refs     map[uint32]reference
}
type jsonWriter struct {
	s               *Schema
	data            []byte
	depth, maxDepth int
	encoding        JSONEncoding
	refs            map[uintptr]reference
}

// DecodeJSON decodes a JSON document using the root's declared encoding.
func (s *Schema) DecodeJSON(typeID string, data []byte) (any, error) {
	return s.decodeJSON(typeID, data, -1)
}

// DecodeJSONWithEncoding decodes a JSON document with an explicit encoding.
func (s *Schema) DecodeJSONWithEncoding(typeID string, data []byte, encoding JSONEncoding) (any, error) {
	return s.decodeJSON(typeID, data, encoding)
}

func (s *Schema) decodeJSON(typeID string, data []byte, encoding JSONEncoding) (value any, err error) {
	defer recoverFormat(&err)
	c, shape, p := s.operation(typeID)
	if !p.Trusted && len(data) > limit(p.MaxInput, math.MaxInt32) {
		fail(0, "JSON input exceeds configured limit")
	}
	if encoding < 0 {
		encoding = p.JSONEncoding
	}
	if encoding == UTF16 {
		data = []byte(decodeUTF16(data, true).(string))
	} else if encoding != UTF8 {
		fail(-1, "unsupported JSON encoding")
	}
	depth := p.JSONDepth
	if depth <= 0 {
		depth = 1024
	}
	r := jsonReader{s: s, encoding: encoding}
	scanner := jsonScanner{data: data}
	depth = min(depth, 1024)
	if !p.StrictJSON && len(s.custom) == 0 && len(s.registered) == 0 &&
		(c != nil && c.jsonDirect || c == nil && s.rootJSONDirect) {
		reader := directJSONReader{scan: scanner, fallback: &r}
		if c != nil {
			value = reader.contract(c, depth)
		} else {
			value = reader.shape(shape, depth)
		}
		reader.scan.end()
		return value, nil
	}
	node := scanner.value(depth)
	scanner.end()
	if shape != nil {
		value = r.shape(shape, node)
	} else {
		value = r.contract(c, node)
	}
	return value, nil
}

// EncodeJSON encodes a JSON document using the root's declared encoding.
func (s *Schema) EncodeJSON(typeID string, value any) ([]byte, error) {
	return s.encodeJSON(typeID, value, -1)
}

// EncodeJSONWithEncoding encodes a JSON document with an explicit encoding.
func (s *Schema) EncodeJSONWithEncoding(typeID string, value any, encoding JSONEncoding) ([]byte, error) {
	return s.encodeJSON(typeID, value, encoding)
}

func (s *Schema) encodeJSON(typeID string, value any, encoding JSONEncoding) (data []byte, err error) {
	defer recoverFormat(&err)
	c, shape, p := s.operation(typeID)
	if encoding < 0 {
		encoding = p.JSONEncoding
	}
	depth := p.JSONDepth
	if depth <= 0 {
		depth = 1024
	}
	w := jsonWriter{s: s, maxDepth: min(depth, 1024), encoding: encoding}
	if shape != nil {
		w.shape(shape, value)
	} else {
		w.contract(c, value)
	}
	if encoding == UTF16 {
		return appendUTF16Text(nil, string(w.data)), nil
	}
	if encoding != UTF8 {
		fail(-1, "unsupported JSON encoding")
	}
	return w.data, nil
}

func (r *jsonReader) contract(c *contract, value any) any {
	if c.Kind == "Custom" {
		c.requireCustomJSON(r.encoding)
		return readCustomJSON(r.s.custom[normalize(c.ID)], c.ID, value)
	}
	if value == nil {
		return nil
	}
	object := obj(value)
	if c.Kind == "Union" {
		discriminator, ok := object.prop("$type")
		if !ok {
			fail(-1, "Union JSON is missing $type")
		}
		hash := uint32(r.s.scalarValue("uint", discriminator).(uint64))
		payload, ok := object.prop("$value")
		if !ok {
			fail(-1, "Union JSON is missing $value")
		}
		selected := c.byHash[hash]
		if selected == nil || selected.target == nil {
			value, ok := r.s.registered[c].byTag[uint16(hash)]
			if !ok || hash > math.MaxUint16 {
				fail(-1, "unknown Union JSON case")
			}
			result := readCustomJSON(value.codec, value.id, payload)
			if result == nil {
				fail(-1, "registered Union JSON case has no payload")
			}
			return Union{value.id, result}
		}
		result := r.contract(selected.target, payload)
		if result == nil {
			return nil
		}
		return Union{selected.target.ID, result}
	}
	result := make(Record, len(c.Members))
	if c.Profile.TrackReferences {
		if id, ok := object.prop("$ref"); ok {
			key := uint32(r.s.scalarValue("uint", id).(uint64))
			ref, found := r.refs[key]
			if len(object) != 1 || !found || ref.c != c {
				fail(-1, "unknown or mismatched JSON reference")
			}
			return ref.value
		}
		if id, ok := object.prop("$id"); ok {
			key := uint32(r.s.scalarValue("uint", id).(uint64))
			if r.refs == nil {
				r.refs = make(map[uint32]reference)
			}
			if _, present := r.refs[key]; present {
				fail(-1, "duplicate JSON reference identifier")
			}
			r.refs[key] = reference{key, c, result}
		}
	}
	for _, item := range object {
		if field := c.byName[item.key]; field != nil {
			result[field.Name] = r.shape(field.Wire, item.value)
		}
	}
	return result
}

func (r *jsonReader) shape(d *descriptor, value any) any {
	if d == nil {
		fail(-1, "missing portable JSON descriptor")
	}
	if d.Kind == "contract" {
		return r.contract(r.s.get(d.Type), value)
	}
	if value == nil {
		if d.Kind == "sequence" && isMemory(d) {
			return []any{}
		}
		if d.Kind != "scalar" {
			return nil
		}
	}
	switch d.Kind {
	case "scalar":
		if value != nil && (d.Type == "float" || d.Type == "double") {
			if _, ok := value.(json.Number); !ok {
				fail(-1, "expected a JSON floating-point number")
			}
		}
		result := r.s.scalarValue(d.Type, value)
		switch d.Type {
		case "byte":
			return byte(result.(uint64))
		case "sbyte":
			return int8(result.(int64))
		case "char":
			if v, ok := result.(uint16); ok {
				return v
			}
			return uint16(result.(uint64))
		case "short":
			return int16(result.(int64))
		case "ushort":
			return uint16(result.(uint64))
		case "int":
			return int32(result.(int64))
		case "uint":
			return uint32(result.(uint64))
		default:
			return result
		}
	case "string":
		return parseJSONString(value)
	case "nullable":
		return r.shape(d.Element, value)
	case "sequence":
		items := array(value)
		out := make([]any, len(items))
		for i, item := range items {
			out[i] = r.shape(d.Element, item)
		}
		return out
	case "dictionary":
		result := make(Dictionary, 0)
		if object, ok := value.(jsonObject); ok && d.Key.Kind == "string" {
			for _, item := range object {
				result = append(result, Entry{item.key, r.shape(d.Value, item.value)})
			}
			return result
		}
		for _, item := range array(value) {
			pair := array(item)
			if len(pair) != 2 {
				fail(-1, "wrong JSON dictionary pair width")
			}
			key := r.shape(d.Key, pair[0])
			if key == nil {
				fail(-1, "null JSON dictionary key")
			}
			result = append(result, Entry{key, r.shape(d.Value, pair[1])})
		}
		return result
	case "pair", "tuple":
		items := array(value)
		if len(items) != len(d.Elements) {
			fail(-1, "wrong JSON tuple arity")
		}
		result := make([]any, len(items))
		for i, e := range d.Elements {
			result[i] = r.shape(e, items[i])
		}
		return result
	case "rectangular":
		object := obj(value)
		lengths, foundLengths := object.prop("Lengths")
		bounds, foundBounds := object.prop("LowerBounds")
		items, foundItems := object.prop("Values")
		if !foundLengths || !foundBounds || !foundItems {
			fail(-1, "missing JSON rectangular array shape")
		}
		shape, boundaries := array(lengths), array(bounds)
		if len(shape) != d.Rank || len(boundaries) != d.Rank {
			fail(-1, "wrong JSON rectangular rank")
		}
		result := Rectangular{Lengths: make([]int32, d.Rank), LowerBounds: make([]int32, d.Rank)}
		for i, length := range shape {
			result.Lengths[i] = int32(r.s.scalarValue("int", length).(int64))
			result.LowerBounds[i] = int32(r.s.scalarValue("int", boundaries[i]).(int64))
		}
		values := array(items)
		if dimensions(result.Lengths) != len(values) {
			fail(-1, "rectangular dimensions do not match values")
		}
		result.Values = make([]any, len(values))
		for i, item := range values {
			result.Values[i] = r.shape(d.Element, item)
		}
		return result
	default:
		fail(-1, "unsupported JSON descriptor "+d.Kind)
	}
	return nil
}

func appendJSONNode(output []byte, value any) []byte {
	switch v := value.(type) {
	case jsonObject:
		output = append(output, '{')
		for i, item := range v {
			if i > 0 {
				output = append(output, ',')
			}
			output = appendJSONNode(output, item.key)
			output = append(output, ':')
			output = appendJSONNode(output, item.value)
		}
		return append(output, '}')
	case []any:
		output = append(output, '[')
		for i, item := range v {
			if i > 0 {
				output = append(output, ',')
			}
			output = appendJSONNode(output, item)
		}
		return append(output, ']')
	default:
		if text, ok := v.(string); ok {
			return appendJSONString(output, text)
		}
		encoded, err := json.Marshal(v)
		if err != nil {
			fail(-1, err.Error())
		}
		return append(output, encoded...)
	}
}

func (w *jsonWriter) append(text string) { w.data = append(w.data, text...) }
func (w *jsonWriter) byte(b byte)        { w.data = append(w.data, b) }
func (w *jsonWriter) text(text string)   { w.data = appendJSONString(w.data, text) }

func appendJSONString(output []byte, text string) []byte {
	const hex = "0123456789abcdef"
	output = append(output, '"')
	data := asciiStringBytes(text)
	for len(data) != 0 {
		prefix := jsonLiteralPrefix(data)
		output = append(output, data[:prefix]...)
		data = data[prefix:]
		if len(data) == 0 {
			break
		}
		switch ch := data[0]; ch {
		case '"', '\\':
			output = append(output, '\\', ch)
		case '\n':
			output = append(output, '\\', 'n')
		case '\r':
			output = append(output, '\\', 'r')
		case '\t':
			output = append(output, '\\', 't')
		default:
			if ch < 0x20 {
				output = append(output, '\\', 'u', '0', '0', hex[ch>>4], hex[ch&15])
			} else {
				r, size := utf8.DecodeRune(data)
				if r == utf8.RuneError && size == 1 {
					fail(-1, "invalid Go UTF-8 string")
				}
				output = append(output, data[:size]...)
				data = data[size:]
				continue
			}
		}
		data = data[1:]
	}
	return append(output, '"')
}

func (w *jsonWriter) contract(c *contract, data any) {
	if native, ok := data.(NativeValue); ok {
		data = native.Value
	}
	if c.Kind == "Custom" {
		c.requireCustomJSON(w.encoding)
		w.writeCustomJSON(w.s.custom[normalize(c.ID)], c.ID, data)
		return
	}
	if data == nil {
		w.append("null")
		return
	}
	if w.depth >= w.maxDepth {
		fail(-1, "maximum JSON depth exceeded")
	}
	w.depth++
	defer func() { w.depth-- }()
	if c.Kind == "Union" {
		value, ok := data.(Union)
		if !ok {
			fail(-1, "expected Union value for "+c.ID)
		}
		selected := c.byType[normalize(value.Type)]
		w.append(`{"$type":`)
		if selected == nil || selected.target == nil {
			external, ok := w.s.registered[c].byType[normalize(value.Type)]
			if !ok || value.Value == nil {
				fail(-1, "unknown or empty Union JSON case")
			}
			w.data = appendJSONUint32(w.data, uint32(external.tag))
			w.append(`,"$value":`)
			w.writeCustomJSON(external.codec, external.id, value.Value)
		} else {
			w.data = appendJSONUint32(w.data, selected.Hash)
			w.append(`,"$value":`)
			w.contract(selected.target, value.Value)
		}
		w.byte('}')
		return
	}
	value := asRecord(data)
	if value == nil {
		w.append("null")
		return
	}
	if c.Profile.TrackReferences {
		w.byte('{')
		identity := recordIdentity(value)
		if w.refs == nil {
			w.refs = make(map[uintptr]reference)
		}
		if ref, ok := w.refs[identity]; ok {
			if ref.c != c {
				fail(-1, "record has inconsistent JSON reference types")
			}
			w.append(`"$ref":`)
			w.data = appendJSONUint32(w.data, ref.id)
			w.byte('}')
			return
		}
		id := uint32(len(w.refs))
		w.refs[identity] = reference{id, c, value}
		w.append(`"$id":`)
		w.data = appendJSONUint32(w.data, id)
		for i := range c.Members {
			m := &c.Members[i]
			w.byte(',')
			w.data = append(w.data, m.jsonHead[1:]...)
			w.shape(m.Wire, value[m.Name])
		}
	} else if len(c.Members) == 0 {
		w.byte('{')
	} else {
		for i := range c.Members {
			m := &c.Members[i]
			w.field(m, value[m.Name])
		}
	}
	w.byte('}')
}

func (c *contract) requireCustomJSON(encoding JSONEncoding) {
	if !c.CustomJSON || encoding != UTF16 {
		fail(-1, "no Custom JSON formatter for this encoding: "+c.ID)
	}
}

func readCustomJSON(codec CustomCodec, name string, value any) any {
	if codec.ReadJSON == nil {
		fail(-1, "no custom JSON reader for "+name)
	}
	result, err := codec.ReadJSON(appendJSONNode(nil, value))
	if err != nil {
		fail(-1, err.Error())
	}
	return result
}

func (w *jsonWriter) writeCustomJSON(codec CustomCodec, name string, data any) {
	if codec.WriteJSON == nil {
		fail(-1, "no custom JSON writer for "+name)
	}
	value, err := codec.WriteJSON(data)
	if err != nil {
		fail(-1, err.Error())
	}
	if !json.Valid(value) {
		fail(-1, "custom codec wrote invalid JSON")
	}
	w.data = append(w.data, value...)
}

func (w *jsonWriter) shape(d *descriptor, value any) {
	if native, ok := value.(NativeValue); ok {
		value = native.Value
	}
	if d == nil {
		fail(-1, "missing portable JSON descriptor")
	}
	if d.Kind == "contract" {
		w.contract(w.s.get(d.Type), value)
		return
	}
	if value == nil {
		if d.Kind == "sequence" && isMemory(d) {
			w.append("[]")
			return
		}
		if d.Kind != "scalar" {
			w.append("null")
			return
		}
	}
	switch d.Kind {
	case "scalar":
		if w.nativeJSONScalar(d.Type, value) {
			return
		}
		v := w.s.scalarValue(d.Type, value)
		switch x := v.(type) {
		case bool:
			w.data = strconv.AppendBool(w.data, x)
		case float32:
			w.jsonFloat(float64(x), 32)
		case float64:
			w.jsonFloat(x, 64)
		case int64:
			w.data = appendJSONInteger(w.data, uint64(x), true, w.s.scalarWidth(d.Type))
		case uint64:
			if d.Type == "char" {
				w.jsonChar(uint16(x))
			} else {
				w.data = appendJSONInteger(w.data, x, false, w.s.scalarWidth(d.Type))
			}
		case uint16:
			w.jsonChar(x)
		}
	case "string":
		if units, ok := value.(UTF16String); ok {
			w.jsonUnits(units)
		} else {
			w.text(parseJSONString(value))
		}
	case "nullable":
		w.shape(d.Element, value)
	case "sequence":
		if w.nativeJSONItems(d.Element, value) {
			return
		}
		w.jsonItems(d.Element, asSequence(value))
	case "pair", "tuple":
		items := asSequence(value)
		if len(items) != len(d.Elements) {
			fail(-1, "wrong tuple arity")
		}
		w.byte('[')
		for i, e := range d.Elements {
			if i > 0 {
				w.byte(',')
			}
			w.shape(e, items[i])
		}
		w.byte(']')
	case "dictionary":
		dictionary, ok := value.(Dictionary)
		if !ok {
			fail(-1, "expected Dictionary value")
		}
		strings := d.Key.Kind == "string"
		if strings {
			w.byte('{')
		} else {
			w.byte('[')
		}
		for i, item := range dictionary {
			if item.Key == nil {
				fail(-1, "null JSON dictionary key")
			}
			if i > 0 {
				w.byte(',')
			}
			if !strings && w.integerPair(d.Key, d.Value, item) {
				continue
			}
			if strings {
				w.shape(d.Key, item.Key)
				w.byte(':')
			} else {
				w.byte('[')
				w.shape(d.Key, item.Key)
				w.byte(',')
			}
			w.shape(d.Value, item.Value)
			if !strings {
				w.byte(']')
			}
		}
		if strings {
			w.byte('}')
		} else {
			w.byte(']')
		}
	case "rectangular":
		arr, ok := value.(Rectangular)
		if !ok || len(arr.Lengths) != d.Rank || len(arr.LowerBounds) != d.Rank || dimensions(arr.Lengths) != len(arr.Values) {
			fail(-1, "invalid JSON rectangular value")
		}
		w.append(`{"Lengths":`)
		w.data = appendSignedJSONItems(w.data, arr.Lengths)
		w.append(`,"LowerBounds":`)
		w.data = appendSignedJSONItems(w.data, arr.LowerBounds)
		w.append(`,"Values":`)
		w.jsonItems(d.Element, arr.Values)
		w.byte('}')
	default:
		fail(-1, "unsupported JSON descriptor "+d.Kind)
	}
}

func (w *jsonWriter) jsonItems(d *descriptor, items []any) {
	w.byte('[')
	for i, item := range items {
		if i > 0 {
			w.byte(',')
		}
		w.shape(d, item)
	}
	w.byte(']')
}

func (w *jsonWriter) jsonChar(value uint16) {
	if value >= 0xd800 && value <= 0xdfff {
		w.jsonUnits([]uint16{value})
		return
	}
	w.text(string(rune(value)))
}

func (w *jsonWriter) jsonUnits(value []uint16) {
	const hex = "0123456789abcdef"
	w.byte('"')
	for _, unit := range value {
		w.append(`\u`)
		w.data = append(w.data, hex[unit>>12], hex[(unit>>8)&15], hex[(unit>>4)&15], hex[unit&15])
	}
	w.byte('"')
}
