package luminpack

import (
	"fmt"
	"maps"
)

type registeredUnion struct {
	byTag  map[uint16]registeredCase
	byType map[string]registeredCase
}

type registeredCase struct {
	id    string
	tag   uint16
	codec CustomCodec
}

// WithRegisteredUnion binds an application-owned case to its managed numeric
// tag. Its callbacks handle the payload after the runtime reads the Union header.
func (s *Schema) WithRegisteredUnion(unionType string, tag uint16, caseType string, codec CustomCodec) (*Schema, error) {
	if unionType == "" {
		unionType = s.root
	}
	c := s.types[normalize(unionType)]
	if c == nil || c.Kind != "Union" || !c.UnionRegistration {
		return nil, fmt.Errorf("luminpack: unknown registrable Union %s", unionType)
	}
	if !c.UnionWideTags && tag > 255 {
		return nil, fmt.Errorf("luminpack: Union %s uses byte tags", unionType)
	}
	id := normalize(caseType)
	slots := s.registered[c]
	if id == "" || c.byType[id] != nil || slots.byType[id].codec.complete() || slots.byTag[tag].codec.complete() {
		return nil, fmt.Errorf("luminpack: duplicate or invalid case registration for %s", unionType)
	}
	for _, existing := range c.Cases {
		if existing.Tag == tag || existing.Hash == uint32(tag) {
			return nil, fmt.Errorf("luminpack: reserved Union tag %d", tag)
		}
	}
	if !codec.complete() {
		return nil, fmt.Errorf("luminpack: incomplete Union binding for %s", caseType)
	}
	next := *s
	next.registered = make(map[*contract]registeredUnion, len(s.registered)+1)
	maps.Copy(next.registered, s.registered)
	newSlots := registeredUnion{
		byTag:  make(map[uint16]registeredCase, len(slots.byTag)+1),
		byType: make(map[string]registeredCase, len(slots.byType)+1),
	}
	maps.Copy(newSlots.byTag, slots.byTag)
	maps.Copy(newSlots.byType, slots.byType)
	value := registeredCase{id, tag, codec}
	newSlots.byTag[tag] = value
	newSlots.byType[id] = value
	next.registered[c] = newSlots
	return &next, nil
}

func (r *binaryReader) registeredTag(c *contract) uint16 {
	marker := r.byte()
	if c.UnionWideTags {
		if marker == 250 {
			return r.u16()
		}
		if marker > 250 {
			fail(r.pos-1, "invalid registered Union tag header")
		}
	}
	return uint16(marker)
}

func (w *binaryWriter) registeredTag(c *contract, tag uint16) {
	if c.UnionWideTags && tag >= 250 {
		w.byte(250)
		w.u16(tag)
	} else {
		w.byte(byte(tag))
	}
}

func (r *binaryReader) readCustom(codec CustomCodec, name string) any {
	if codec.ReadBinary == nil {
		fail(r.pos, "no custom binary reader for "+name)
	}
	value, n, err := codec.ReadBinary(r.data[r.pos:])
	if err != nil {
		fail(r.pos, err.Error())
	}
	r.take(n)
	return value
}

func (w *binaryWriter) writeCustom(codec CustomCodec, name string, value any) {
	if codec.WriteBinary == nil {
		fail(-1, "no custom binary writer for "+name)
	}
	raw, err := codec.WriteBinary(value)
	if err != nil {
		fail(-1, err.Error())
	}
	w.data = append(w.data, raw...)
}
