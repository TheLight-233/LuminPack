package luminpack

import "math"

func versionHash(descriptors []uint64) uint64 {
	const prime uint64 = 1099511628211
	hash := (uint64(14695981039346656037) ^ uint64(len(descriptors))) * prime
	for _, d := range descriptors {
		for range 8 {
			hash = (hash ^ (d & 255)) * prime
			d >>= 8
		}
	}
	return hash
}

func readVersionTable(r *binaryReader, p int) []uint64 {
	form := r.byte()
	if form == 0x50|byte(p) {
		index := int(r.varuint((1<<30)-1, false))
		if index >= len(r.versions) {
			fail(r.pos, "unknown Version schema ordinal")
		}
		return r.versions[index]
	}
	if form != 0x40|byte(p) {
		fail(r.pos, "Version profile or revision mismatch")
	}
	count := int(r.byte())
	if count > 249 {
		fail(r.pos, "too many Version fields")
	}
	hash := r.u64()
	descriptors := make([]uint64, count)
	seen := make(map[uint32]bool, count)
	for i := range descriptors {
		d := r.u64()
		key := uint32(d)
		width := d >> 56
		if seen[key] || width > 250 && width < 254 {
			fail(r.pos, "invalid Version descriptor")
		}
		seen[key] = true
		descriptors[i] = d
	}
	if versionHash(descriptors) != hash {
		fail(r.pos, "invalid Version hash")
	}
	r.versions = append(r.versions, descriptors)
	return descriptors
}

func writeVersionTable(w *binaryWriter, p int, descriptors []uint64) {
	if len(descriptors) > 249 {
		fail(-1, "too many Version fields")
	}
	w.byte(251)
	for index, known := range w.versions {
		if equalDescriptors(known, descriptors) {
			w.byte(0x50 | byte(p))
			w.varuint(uint64(index))
			return
		}
	}
	w.byte(0x40 | byte(p))
	w.byte(byte(len(descriptors)))
	w.u64(versionHash(descriptors))
	for _, d := range descriptors {
		w.u64(d)
	}
	w.versions = append(w.versions, descriptors)
}

func equalDescriptors(left, right []uint64) bool {
	if len(left) != len(right) {
		return false
	}
	for i, x := range left {
		if x != right[i] {
			return false
		}
	}
	return true
}

func versionMember(c *contract, key uint32) *member {
	for i := range c.Members {
		m := &c.Members[i]
		if m.hasVersionDescriptor && uint32(m.versionDescriptor) == key {
			return m
		}
		for _, alias := range m.NameKeys {
			if alias == key {
				return m
			}
		}
	}
	return nil
}

func versionShape(m *member, width int) *descriptor {
	if !m.FixedNullable || m.Wire == nil || m.Wire.Kind != "nullable" || width > 250 {
		return m.Wire
	}
	shape := *m.Wire
	shape.Width = width
	return &shape
}

func (r *binaryReader) readVersion(c *contract, p int, target Record) any {
	marker := r.byte()
	if marker == 255 {
		return nil
	}
	if marker != 251 {
		fail(r.pos, "invalid Version marker")
	}
	if target == nil {
		target = make(Record, len(c.Members))
	}
	if r.pos < len(r.data) && r.data[r.pos] == 1 {
		r.pos++
		r.readLegacy(c, p, target)
		return target
	}
	table := readVersionTable(r, p)
	seen := make(map[string]bool, len(table))
	for _, d := range table {
		width := int(d >> 56)
		m := versionMember(c, uint32(d))
		if m != nil {
			if seen[m.Name] {
				fail(r.pos, "duplicate mapped Version member")
			}
			seen[m.Name] = true
			if !m.hasVersionDescriptor || d>>32 != m.versionDescriptor>>32 {
				fail(r.pos, "incompatible Version member "+m.Name)
			}
		}
		if width == 254 {
			if m == nil {
				r.readText(p, true)
			} else {
				target[m.Name] = r.readVersionMember(m, versionShape(m, width), p)
			}
			continue
		}
		length := width
		if width == 255 {
			raw := r.u32()
			if raw > math.MaxInt32 {
				fail(r.pos, "Version field exceeds size limit")
			}
			length = int(raw)
		}
		child := *r
		child.data = r.take(length)
		child.pos = 0
		if m != nil {
			target[m.Name] = child.readVersionMember(m, versionShape(m, width), p)
			if child.pos != len(child.data) {
				fail(r.pos, "Version field has unconsumed bytes")
			}
			r.refs = child.refs
			if width != 255 {
				r.versions = child.versions
			}
		}
	}
	return target
}

func (r *binaryReader) readVersionMember(m *member, d *descriptor, p int) any {
	return r.readMember(m, d, p)
}

func (w *binaryWriter) writeVersion(c *contract, p int, value any) {
	if value == nil {
		w.byte(255)
		return
	}
	fields := asRecord(value)
	descriptors := make([]uint64, len(c.Members))
	for i, m := range c.Members {
		if !m.hasVersionDescriptor {
			fail(-1, "missing Version descriptor for "+m.Name)
		}
		descriptors[i] = m.versionDescriptor
	}
	writeVersionTable(w, p, descriptors)
	for i := range c.Members {
		m := &c.Members[i]
		width := int(descriptors[i] >> 56)
		start := len(w.data)
		if width == 255 {
			w.u32(0)
		}
		scope := len(w.versions)
		w.writeMember(m, versionShape(m, width), fields[m.Name], p)
		if width == 255 {
			count := len(w.data) - start - 4
			if count > math.MaxInt32 {
				fail(-1, "Version field exceeds size limit")
			}
			w.data[start] = byte(count)
			w.data[start+1] = byte(count >> 8)
			w.data[start+2] = byte(count >> 16)
			w.data[start+3] = byte(count >> 24)
			w.versions = w.versions[:scope]
		} else if width <= 250 && len(w.data)-start != width {
			fail(-1, "Version field does not match fixed width")
		}
	}
}

func (r *binaryReader) readLegacy(c *contract, p int, target Record) {
	if r.byte() != byte(p) {
		fail(r.pos, "legacy Version profile mismatch")
	}
	count := int(r.byte())
	if count > 249 {
		fail(r.pos, "too many legacy Version fields")
	}
	items := make([]*member, count)
	seen := make(map[string]bool, count)
	for i := range items {
		name := string(r.take(int(r.varuint(math.MaxInt32, true))))
		legacy := string(r.take(int(r.varuint(math.MaxInt32, true))))
		m := c.byName[name]
		items[i] = m
		if m != nil {
			if seen[m.Name] || len(m.LegacyVersionContract) == 0 || string(m.LegacyVersionContract) == "null" || legacy != m.legacy {
				fail(r.pos, "incompatible legacy Version member "+name)
			}
			seen[m.Name] = true
		}
	}
	lengths := make([]uint32, count)
	for i := range lengths {
		lengths[i] = r.referenceID()
	}
	for i, m := range items {
		if lengths[i] > math.MaxInt32 {
			fail(r.pos, "legacy Version field exceeds size limit")
		}
		child := *r
		child.data = r.take(int(lengths[i]))
		child.pos = 0
		if m != nil {
			target[m.Name] = child.readVersionMember(m, m.Wire, p)
			if child.pos != len(child.data) {
				fail(r.pos, "legacy Version field has unconsumed bytes")
			}
			r.refs = child.refs
		}
	}
}
