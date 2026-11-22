package luminpack

import (
	"bytes"
	"math"
	"strconv"
	"unsafe"
)

func (s *Schema) jsonContractEligible(c *contract, visited map[*contract]bool) bool {
	if c == nil || c.Profile.TrackReferences || c.Profile.StrictJSON {
		return false
	}
	if visited[c] {
		return true
	}
	visited[c] = true
	switch c.Kind {
	case "Record", "Struct":
		for i := range c.Members {
			if !s.jsonShapeEligible(c.Members[i].Wire, visited) {
				return false
			}
		}
		return true
	case "Union":
		for i := range c.Cases {
			if !s.jsonContractEligible(c.Cases[i].target, visited) {
				return false
			}
		}
		return true
	default:
		return false
	}
}

func (s *Schema) jsonShapeEligible(d *descriptor, visited map[*contract]bool) bool {
	if d == nil {
		return false
	}
	switch d.Kind {
	case "scalar", "string":
		return true
	case "contract":
		return s.jsonContractEligible(s.types[normalize(d.Type)], visited)
	case "nullable", "sequence":
		return s.jsonShapeEligible(d.Element, visited)
	case "dictionary":
		return s.jsonShapeEligible(d.Key, visited) && s.jsonShapeEligible(d.Value, visited)
	default:
		return false
	}
}

// The direct reader binds checked property heads to their compiled ordinal
// before visiting the value. The name lookup remains available at any mismatch.
type directJSONReader struct {
	scan     jsonScanner
	fallback *jsonReader
}

func (s *jsonScanner) end() {
	s.space()
	if s.pos != len(s.data) {
		fail(s.pos, "trailing JSON content")
	}
}

func (s *jsonScanner) match(head []byte) bool {
	if !bytes.HasPrefix(s.data[s.pos:], head) {
		return false
	}
	s.pos += len(head)
	return true
}

func (s *jsonScanner) null() bool {
	s.space()
	if s.pos < len(s.data) && s.data[s.pos] == 'n' {
		s.literal("null")
		return true
	}
	return false
}

func (s *jsonScanner) begin(depth int) int {
	if depth <= 0 {
		fail(s.pos, "configured JSON depth exceeded")
	}
	return depth - 1
}

func (r *directJSONReader) contract(c *contract, depth int) any {
	if r.scan.null() {
		return nil
	}
	if c.Kind == "Union" {
		return r.union(c, depth)
	}
	return r.record(c, r.scan.begin(depth))
}

func (r *directJSONReader) record(c *contract, depth int) Record {
	result := make(Record, len(c.Members))
	ordinal := 0
	for first := true; ; first = false {
		if ordinal < len(c.Members) {
			m := &c.Members[ordinal]
			if r.scan.match(m.jsonHead) {
				result[m.Name] = r.shape(m.Wire, depth)
				ordinal++
				continue
			}
		}
		if first {
			r.scan.expect('{')
		}
		if r.scan.accept('}') {
			return result
		}
		if !first {
			r.scan.expect(',')
		}
		r.scan.expect('"')
		name := r.scan.text()
		r.scan.expect(':')
		m := c.byName[name]
		if m == nil {
			r.scan.value(depth)
		} else {
			result[m.Name] = r.shape(m.Wire, depth)
		}
		if ordinal < len(c.Members) && m == &c.Members[ordinal] {
			ordinal++
		} else {
			ordinal = len(c.Members)
		}
	}
}

func (r *directJSONReader) union(c *contract, depth int) any {
	start := r.scan.pos
	if r.scan.match([]byte("{\"$type\":")) {
		childDepth := r.scan.begin(depth)
		r.scan.space()
		if r.scan.pos < len(r.scan.data) && isNumberStart(r.scan.data[r.scan.pos]) {
			discriminator := r.scan.numberToken()
			hash, err := strconv.ParseUint(jsonNumberView(discriminator), 10, 32)
			if err == nil && r.scan.match([]byte(",\"$value\":")) {
				if choice := c.byHash[uint32(hash)]; choice != nil {
					payload := r.contract(choice.target, childDepth)
					for !r.scan.accept('}') {
						r.scan.expect(',')
						r.scan.expect('"')
						r.scan.text()
						r.scan.expect(':')
						r.scan.value(childDepth)
					}
					if payload == nil {
						return nil
					}
					return Union{choice.target.ID, payload}
				}
			}
		}
	}
	// The object reader preserves first-occurrence discriminator semantics and
	// supports arbitrary property order, escapes, and unknown envelope fields.
	r.scan.pos = start
	return r.fallback.contract(c, r.scan.value(depth))
}

func (r *directJSONReader) shape(d *descriptor, depth int) any {
	switch d.Kind {
	case "contract":
		return r.contract(r.fallback.s.get(d.Type), depth)
	case "scalar":
		return r.scalar(d, depth)
	}
	if r.scan.null() {
		if d.Kind == "sequence" && isMemory(d) {
			return []any{}
		}
		return nil
	}
	switch d.Kind {
	case "string":
		r.scan.expect('"')
		return r.scan.text()
	case "nullable":
		return r.shape(d.Element, depth)
	case "sequence":
		return r.sequence(d.Element, r.scan.begin(depth))
	case "dictionary":
		return r.dictionary(d, r.scan.begin(depth))
	default:
		return r.fallback.shape(d, r.scan.value(depth))
	}
}

func (r *directJSONReader) sequence(element *descriptor, depth int) []any {
	r.scan.expect('[')
	if r.scan.accept(']') {
		return []any{}
	}
	values := make([]any, 0, 8)
	for {
		values = append(values, r.shape(element, depth))
		if !r.scan.accept(',') {
			break
		}
	}
	r.scan.expect(']')
	return values
}

func (r *directJSONReader) dictionary(d *descriptor, depth int) Dictionary {
	result := make(Dictionary, 0)
	if d.Key.Kind == "string" && r.scan.accept('{') {
		if r.scan.accept('}') {
			return result
		}
		for {
			r.scan.expect('"')
			key := r.scan.text()
			r.scan.expect(':')
			result = append(result, Entry{key, r.shape(d.Value, depth)})
			if !r.scan.accept(',') {
				break
			}
		}
		r.scan.expect('}')
		return result
	}
	r.scan.expect('[')
	if r.scan.accept(']') {
		return result
	}
	for {
		r.scan.expect('[')
		pairDepth := r.scan.begin(depth)
		key := r.shape(d.Key, pairDepth)
		if key == nil {
			fail(-1, "null JSON dictionary key")
		}
		r.scan.expect(',')
		value := r.shape(d.Value, pairDepth)
		r.scan.expect(']')
		result = append(result, Entry{key, value})
		if !r.scan.accept(',') {
			break
		}
	}
	r.scan.expect(']')
	return result
}

func (r *directJSONReader) scalar(d *descriptor, depth int) any {
	r.scan.space()
	if r.scan.pos < len(r.scan.data) && isNumberStart(r.scan.data[r.scan.pos]) {
		switch d.Type {
		case "int":
			if n, ok := r.scan.int32(); ok {
				return n
			}
		case "uint":
			n, err := strconv.ParseUint(jsonNumberView(r.scan.numberToken()), 10, 32)
			if err != nil {
				fail(-1, "scalar out of unsigned range")
			}
			return uint32(n)
		case "long":
			n, err := strconv.ParseInt(jsonNumberView(r.scan.numberToken()), 10, 64)
			if err != nil {
				fail(-1, "scalar out of signed range")
			}
			return n
		case "ulong":
			n, err := strconv.ParseUint(jsonNumberView(r.scan.numberToken()), 10, 64)
			if err != nil {
				fail(-1, "scalar out of unsigned range")
			}
			return n
		case "float", "double":
			width := r.fallback.s.scalarWidth(d.Type) * 8
			n, err := strconv.ParseFloat(jsonNumberView(r.scan.numberToken()), width)
			if err != nil {
				fail(-1, "invalid floating-point value")
			}
			if width == 32 {
				return float32(n)
			}
			return n
		}
	}
	return r.fallback.shape(d, r.scan.value(depth))
}

func isNumberStart(ch byte) bool { return ch == '-' || ch >= '0' && ch <= '9' }

// The borrowed token is used only during conversion. Successful values do
// not retain the input, and errors are replaced by the normal format error.
func jsonNumberView(token []byte) string {
	return unsafe.String(unsafe.SliceData(token), len(token))
}

// Match a plain Int32 within the current window. Syntax and range misses leave
// the scanner untouched so the regular number and format checks can run.
func (s *jsonScanner) int32() (int32, bool) {
	data, i := s.data, s.pos
	negative := data[i] == '-'
	if negative {
		i++
	}
	if i == len(data) || data[i] < '0' || data[i] > '9' {
		return 0, false
	}
	zero := data[i] == '0'
	first := i
	var value uint64
	for i < len(data) && data[i] >= '0' && data[i] <= '9' {
		if i-first == 10 || i > first && zero {
			return 0, false
		}
		value = value*10 + uint64(data[i]-'0')
		i++
	}
	if i < len(data) {
		switch data[i] {
		case ',', '}', ']', ' ', '\r', '\n', '\t':
		default:
			return 0, false
		}
	}
	if negative {
		if value > 1<<31 {
			return 0, false
		}
		s.pos = i
		return int32(-int64(value)), true
	}
	if value > math.MaxInt32 {
		return 0, false
	}
	s.pos = i
	return int32(value), true
}
