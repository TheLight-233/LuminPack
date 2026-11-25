package luminpack

import (
	"math"
	"reflect"
)

func (r *binaryReader) referenceID() uint32 {
	marker := int8(r.byte())
	if marker >= 0 {
		return uint32(marker)
	}
	var value uint64
	switch marker {
	case -121:
		value = uint64(r.byte())
	case -122:
		value = uint64(int64(int8(r.byte())))
	case -123:
		value = uint64(r.u16())
	case -124:
		value = uint64(int64(int16(r.u16())))
	case -125:
		value = uint64(r.u32())
	case -126:
		value = uint64(int64(int32(r.u32())))
	case -127, -128:
		value = r.u64()
	default:
		fail(r.pos, "invalid reference identifier marker")
	}
	if value > math.MaxUint32 {
		fail(r.pos, "reference identifier exceeds range")
	}
	return uint32(value)
}

func (w *binaryWriter) refID(id uint32) {
	if id <= 127 {
		w.byte(byte(id))
		return
	}
	if id <= math.MaxUint16 {
		w.byte(133)
		w.u16(uint16(id))
		return
	}
	w.byte(131)
	w.u32(id)
}

func (r *binaryReader) tracked(c *contract, p int) any {
	marker := r.byte()
	if marker == 255 {
		return nil
	}
	if marker > 250 {
		fail(r.pos, "invalid tracked record marker")
	}
	id := r.referenceID()
	if marker == 250 {
		ref, ok := r.refs[id]
		if !ok || ref.c != c {
			fail(r.pos, "unknown or mismatched reference")
		}
		return ref.value
	}
	if r.refs == nil {
		r.refs = make(map[uint32]reference)
	}
	if _, ok := r.refs[id]; ok {
		fail(r.pos, "duplicate reference identifier")
	}
	value := make(Record, len(c.Members))
	r.refs[id] = reference{id, c, value}
	r.descend()
	defer func() { r.depth-- }()
	if r.body(c, p, c.SequenceBase != "", value) == nil {
		fail(r.pos, "tracked reference contains null body")
	}
	return value
}

func recordIdentity(value Record) uintptr { return reflect.ValueOf(value).Pointer() }

func (w *binaryWriter) tracked(c *contract, data any, p int) {
	if data == nil {
		w.byte(255)
		return
	}
	value := asRecord(data)
	if value == nil {
		w.byte(255)
		return
	}
	identity := recordIdentity(value)
	if w.refs == nil {
		w.refs = make(map[uintptr]reference)
	}
	if ref, ok := w.refs[identity]; ok {
		if ref.c != c {
			fail(-1, "record has inconsistent reference types")
		}
		w.byte(250)
		w.refID(ref.id)
		return
	}
	id := uint32(len(w.refs))
	w.refs[identity] = reference{id, c, value}
	if len(c.Members) > 249 {
		fail(-1, "too many tracked record fields")
	}
	w.byte(byte(c.headerCount()))
	w.refID(id)
	w.descend()
	defer func() { w.depth-- }()
	w.body(c, p, c.SequenceBase != "", value)
}
