package luminpack

import "strconv"

func (c *contract) standaloneStorage() *valueLayout {
	if c.NativeStorage != nil {
		return c.NativeStorage
	}
	return nil
}

func (d *descriptor) isNullableSlot() bool {
	return d != nil && d.Kind == "nullable" && d.NullableEncoding != 0 && d.NativeNullableElement
}

func (s *Schema) nullableModel(d *descriptor, index int) int {
	if d.NullableEncoding != 2 {
		return index
	}
	if d.NullableLayout == nil || d.Element == nil || d.Element.Kind != "scalar" {
		fail(-1, "invalid native nullable storage model")
	}
	width := s.scalarWidth(d.Element.Type)
	align := min(width, s.nativeScalarAlignment)
	match := -1
	for i := range d.NullableLayout.ModelSizes {
		stride, fields := d.NullableLayout.model(i)
		if stride != align+width || len(fields) != width+1 || fields[0] != 0 {
			continue
		}
		valid := true
		for j := range width {
			valid = valid && fields[j+1] == align+j
		}
		if valid {
			if match >= 0 {
				fail(-1, "ambiguous native nullable storage model")
			}
			match = i
		}
	}
	if match < 0 {
		fail(-1, "no nullable storage model matches the configured ABI")
	}
	return match
}

func (s *Schema) sequenceStorage(d *descriptor) (plan *valueLayout, owner *contract, nullable, tagged bool) {
	element := d.Element
	segmented := d.Family == "System.Buffers.ReadOnlySequence" && !(element.RawCollection && element.Layout == nil)
	if element.Kind == "nullable" && element.NullableEncoding != 0 {
		if !segmented || element.isNullableSlot() {
			plan = element.NullableLayout
			if plan == nil {
				fail(-1, "missing nullable storage model")
			}
			return plan, nil, true, !segmented && element.NullableEncoding == 1 && len(plan.ModelSizes) > 1
		}
	}
	if segmented {
		return
	}
	switch element.Kind {
	case "contract":
		owner = s.get(element.Type)
		plan = owner.Storage
		if plan == nil || !plan.Tagged {
			return nil, nil, false, false
		}
	case "pair", "tuple":
		plan = element.Layout
		if plan == nil || !plan.Tagged {
			if element.RawCompoundStorage != nil {
				return element.RawCompoundStorage, nil, false, false
			}
			return nil, nil, false, false
		}
	default:
		return nil, nil, false, false
	}
	return plan, owner, false, len(plan.ModelSizes) > 1
}

func (p *valueLayout) model(index int) (int, []int) {
	if p == nil || index < 0 || index >= len(p.ModelSizes) || index >= len(p.ModelOffsets) {
		fail(-1, "unknown storage layout tag")
	}
	stride, offsets := p.ModelSizes[index], p.ModelOffsets[index]
	if stride <= 0 || p.WireSize <= 0 || len(offsets) != p.WireSize {
		fail(-1, "invalid storage layout")
	}
	return stride, offsets
}

func (r *binaryReader) readLayout(p *valueLayout, model int, read func(*binaryReader) any) any {
	transfer := p.transfer(model)
	source := r.take(transfer.stride)
	var local [64]byte
	var packed []byte
	if len(transfer.runs) == 1 && (p.program != nil || p.scalarSlot) {
		run := transfer.runs[0]
		packed = source[run.native : run.native+run.width]
	} else {
		if p.WireSize <= len(local) {
			packed = local[:p.WireSize]
		} else {
			packed = make([]byte, p.WireSize)
		}
		for _, run := range transfer.runs {
			copy(packed[run.packed:run.packed+run.width], source[run.native:run.native+run.width])
		}
	}
	child := *r
	child.data, child.pos = packed, 0
	value := read(&child)
	if child.pos != len(packed) {
		fail(r.pos, "storage field width mismatch")
	}
	return value
}

func (w *binaryWriter) writeLayout(p *valueLayout, model int, write func(*binaryWriter)) {
	w.writeStorageLayout(p, model, nil, write)
}

func (w *binaryWriter) writeStorageLayout(p *valueLayout, model int, storage []byte, write func(*binaryWriter)) {
	transfer := p.transfer(model)
	if storage != nil && len(storage) != transfer.stride {
		fail(-1, "native storage width mismatch")
	}
	if len(transfer.runs) == 1 && (p.program != nil || p.scalarSlot) {
		start := len(w.data)
		w.appendStorage(storage, transfer.stride)
		run := transfer.runs[0]
		child := *w
		from := start + run.native
		child.data = w.data[from : from : from+run.width]
		write(&child)
		if len(child.data) != p.WireSize {
			fail(-1, "storage field width mismatch")
		}
		return
	}
	var local [64]byte
	child := *w
	child.data = local[:0]
	write(&child)
	if len(child.data) != p.WireSize {
		fail(-1, "storage field width mismatch")
	}
	start := len(w.data)
	w.appendStorage(storage, transfer.stride)
	for _, run := range transfer.runs {
		copy(w.data[start+run.native:start+run.native+run.width], child.data[run.packed:run.packed+run.width])
	}
}

func (w *binaryWriter) appendStorage(storage []byte, stride int) {
	if storage == nil {
		w.data = append(w.data, make([]byte, stride)...)
	} else {
		w.data = append(w.data, storage...)
	}
}

func (r *binaryReader) readNativeLayout(p *valueLayout, read func(*binaryReader) any) NativeValue {
	start := r.pos
	value := r.readLayout(p, 0, read)
	return NativeValue{value, append([]byte(nil), r.data[start:r.pos]...)}
}

func (w *binaryWriter) writeNativeLayout(p *valueLayout, data any, write func(*binaryWriter, any)) {
	var storage []byte
	if native, ok := data.(NativeValue); ok {
		data, storage = native.Value, native.Storage
	}
	w.writeStorageLayout(p, 0, storage, func(child *binaryWriter) { write(child, data) })
}

func (r *binaryReader) readStorage(c *contract, d *descriptor, p *valueLayout, profile int) any {
	if p.program != nil {
		return p.program.read(r.take(p.program.width))
	}
	if c != nil {
		fields := make(Record, len(p.Fields))
		for _, field := range p.Fields {
			member := c.storageMember(field.Name)
			fields[field.Name] = r.readStorageField(member.Wire, field.Storage, effectiveProfile(member.Profile, profile))
		}
		return fields
	}
	checkStorageArity(d, p)
	items := make([]any, len(p.Fields))
	for i, field := range p.Fields {
		items[i] = r.readStorageField(d.Elements[i], field.Storage, profile)
	}
	return items
}

func (w *binaryWriter) writeStorage(c *contract, d *descriptor, p *valueLayout, value any, profile int) {
	if p.program != nil {
		p.program.write(w, value)
		return
	}
	if c != nil {
		record := asRecord(value)
		for _, field := range p.Fields {
			member := c.storageMember(field.Name)
			w.writeStorageField(member.Wire, field.Storage, record[field.Name], effectiveProfile(member.Profile, profile))
		}
		return
	}
	checkStorageArity(d, p)
	items := asSequence(value)
	if len(items) != len(p.Fields) {
		fail(-1, "wrong storage tuple arity")
	}
	for i, field := range p.Fields {
		w.writeStorageField(d.Elements[i], field.Storage, items[i], profile)
	}
}

func (c *contract) storageMember(name string) *member {
	if m := c.byName[name]; m != nil && m.Name == name {
		return m
	}
	fail(-1, "unknown storage field "+name)
	return nil
}

func checkStorageArity(d *descriptor, p *valueLayout) {
	if d == nil || len(d.Elements) != len(p.Fields) {
		fail(-1, "invalid storage tuple")
	}
}

func (r *binaryReader) readStorageField(d *descriptor, p *valueLayout, profile int) any {
	if p == nil {
		fail(r.pos, "missing field storage")
	}
	if len(p.Fields) == 0 {
		return r.shape(d, profile)
	}
	if d.Kind == "contract" {
		return r.readStorage(d.target(r.s), nil, p, profile)
	}
	return r.readStorage(nil, d, p, profile)
}

func (w *binaryWriter) writeStorageField(d *descriptor, p *valueLayout, value any, profile int) {
	if p == nil {
		fail(-1, "missing field storage")
	}
	if len(p.Fields) == 0 {
		w.shape(d, value, profile)
	} else if d.Kind == "contract" {
		w.writeStorage(d.target(w.s), nil, p, value, profile)
	} else {
		w.writeStorage(nil, d, p, value, profile)
	}
}

func (r *binaryReader) readNullableStorage(d *descriptor, index, p int) any {
	return r.readLayout(d.NullableLayout, index, func(child *binaryReader) any {
		flag := child.byte()
		if flag > 1 {
			fail(r.pos, "invalid nullable storage presence")
		}
		value := child.shape(d.Element, p)
		if flag == 0 {
			return nil
		}
		return value
	})
}

func (w *binaryWriter) writeNullableStorage(d *descriptor, index int, value any, p int) {
	w.writeLayout(d.NullableLayout, index, func(child *binaryWriter) {
		if value == nil {
			child.data = append(child.data, make([]byte, d.NullableLayout.WireSize)...)
			return
		}
		child.byte(1)
		child.shape(d.Element, value, p)
	})
}

func (w *binaryWriter) elements(d *descriptor, values []any, p int) {
	if len(values) == 0 {
		return
	}
	if plan, c, nullable, tagged := w.s.sequenceStorage(d); plan != nil {
		index := 0
		if nullable {
			index = w.s.nullableModel(d.Element, index)
		}
		if tagged {
			w.byte(1)
		}
		for _, value := range values {
			if nullable {
				w.writeNullableStorage(d.Element, index, value, p)
			} else if c == nil && d.Element.RawCompoundStorage == plan {
				w.writeNativeLayout(plan, value, func(child *binaryWriter, value any) { child.writeStorage(nil, d.Element, plan, value, p) })
			} else {
				w.writeLayout(plan, 0, func(child *binaryWriter) { child.writeStorage(c, d.Element, plan, value, p) })
			}
		}
		return
	}
	if raw := w.s.sequenceRawFields(d); raw != nil {
		w.descend()
		defer func() { w.depth-- }()
		for _, value := range values {
			raw.write(w, value)
		}
		return
	}
	for _, value := range values {
		w.shape(d.Element, value, p)
	}
}

func (s *Schema) sequenceRawFields(d *descriptor) *packedStorage {
	if d.Element.Kind == "contract" {
		c := s.get(d.Element.Type)
		if c.Storage != nil && !c.Storage.Tagged {
			return c.Storage.program
		}
		return c.rawFields
	}
	return nil
}

func (s *Schema) rawWidth(d *descriptor, depth int) int {
	if d == nil || depth > 16 {
		fail(-1, "invalid fixed-width element")
	}
	switch d.Kind {
	case "scalar":
		return s.scalarWidth(d.Type)
	case "contract":
		c := d.target(s)
		if c.StorageKind == "RawUnmanaged" && !c.Versioned && c.Storage == nil {
			if c.NativeStorage != nil {
				width, _ := c.NativeStorage.model(0)
				return width
			}
			width := 0
			for _, m := range c.Members {
				width += s.rawWidth(m.Wire, depth+1)
			}
			if width > 0 {
				return width
			}
		}
	}
	fail(-1, "unsupported fixed-width element "+strconv.Quote(d.Kind))
	return 0
}
