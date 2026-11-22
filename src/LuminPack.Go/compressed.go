package luminpack

import "math"

func (r *binaryReader) readMember(m *member, d *descriptor, p int) any {
	p = effectiveProfile(m.Profile, p)
	if m.Compressed {
		return r.readCompressed(d, p)
	}
	return r.shape(d, p)
}

func (w *binaryWriter) writeMember(m *member, d *descriptor, value any, p int) {
	p = effectiveProfile(m.Profile, p)
	if m.Compressed {
		w.writeCompressed(d, value, p)
	} else {
		w.shape(d, value, p)
	}
}

func (r *binaryReader) readCompressed(d *descriptor, p int) any {
	if d.Kind != "sequence" || d.Element == nil {
		fail(r.pos, "invalid compressed collection schema")
	}
	n := r.count(0)
	if n < 0 {
		return nil
	}
	if n == 0 {
		return []any{}
	}
	if d.Element.Kind == "sequence" {
		r.descend()
		defer func() { r.depth-- }()
		if n > (len(r.data)-r.pos)/4 {
			fail(r.pos, "truncated compressed collection")
		}
		result := make([]any, n)
		for i := range result {
			result[i] = r.readCompressed(d.Element, p)
		}
		return result
	}
	width := r.s.rawWidth(d.Element, 0)
	if n > math.MaxInt32/width {
		fail(r.pos, "decompressed collection exceeds size limit")
	}
	expected := n * width
	length := r.u32()
	if length > math.MaxInt32-8 || expected/256 > int(length) {
		fail(r.pos, "invalid compressed collection frame length")
	}
	frame := r.take(int(length))
	raw, err := decompressLum2(frame, expected)
	if err != nil {
		fail(r.pos, err.Error())
	}
	child := *r
	child.data, child.pos = raw, 0
	var result any
	if d.Element.Kind == "scalar" {
		result = child.scalarSequence(d.Element, n)
	} else {
		result = child.elements(d, n, p)
	}
	if child.pos != len(raw) {
		fail(r.pos, "compressed collection width mismatch")
	}
	return result
}

func (w *binaryWriter) writeCompressed(d *descriptor, value any, p int) {
	if d.Kind != "sequence" || d.Element == nil {
		fail(-1, "invalid compressed collection schema")
	}
	if value == nil {
		w.count(-1)
		return
	}
	values := asSequence(value)
	w.count(len(values))
	if len(values) == 0 {
		return
	}
	if d.Element.Kind == "sequence" {
		w.descend()
		defer func() { w.depth-- }()
		for _, element := range values {
			w.writeCompressed(d.Element, element, p)
		}
		return
	}
	width := w.s.rawWidth(d.Element, 0)
	if len(values) > math.MaxInt32/width {
		fail(-1, "uncompressed collection exceeds size limit")
	}
	raw := *w
	raw.data = make([]byte, 0, len(values)*width)
	raw.elements(d, values, p)
	if len(raw.data) != len(values)*width {
		fail(-1, "compressed collection width mismatch")
	}
	frame, err := compressLum2(raw.data)
	if err != nil {
		fail(-1, err.Error())
	}
	if len(frame) > math.MaxInt32-8 {
		fail(-1, "compressed collection exceeds size limit")
	}
	w.u32(uint32(len(frame)))
	w.data = append(w.data, frame...)
}
