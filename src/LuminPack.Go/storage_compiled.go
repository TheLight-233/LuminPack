package luminpack

import (
	"encoding/binary"
	"math"
)

type storageTransfer struct {
	stride int
	runs   []storageRun
}

type storageRun struct {
	packed, native, width int
}

// Compile the compiler's byte projection once. Each run describes two
// bounded contiguous windows, and padding is outside their union.
func (p *valueLayout) compileTransfers() {
	if p == nil || p.transfers != nil {
		return
	}
	p.transfers = make([]storageTransfer, len(p.ModelSizes))
	for index, stride := range p.ModelSizes {
		if stride <= 0 || p.WireSize <= 0 || index >= len(p.ModelOffsets) {
			continue
		}
		offsets := p.ModelOffsets[index]
		if len(offsets) != p.WireSize {
			continue
		}
		seen := make(map[int]bool, len(offsets))
		runs := make([]storageRun, 0, 1)
		for i, offset := range offsets {
			if offset < 0 || offset >= stride || seen[offset] {
				runs = nil
				break
			}
			seen[offset] = true
			last := len(runs) - 1
			if last >= 0 && runs[last].native+runs[last].width == offset {
				runs[last].width++
			} else {
				runs = append(runs, storageRun{i, offset, 1})
			}
		}
		if len(runs) != 0 {
			p.transfers[index] = storageTransfer{stride, runs}
		}
	}
}

func (p *valueLayout) transfer(index int) storageTransfer {
	if p == nil || index < 0 || index >= len(p.transfers) {
		fail(-1, "unknown storage layout tag")
	}
	plan := p.transfers[index]
	if len(plan.runs) == 0 {
		fail(-1, "invalid storage layout")
	}
	return plan
}

type packedStorage struct {
	fields []packedField
	width  int
	tuple  bool
}

type packedField struct {
	name, scalar  string
	child         *packedStorage
	offset, width int
}

func (s *Schema) compileContractStorage(c *contract) {
	if c.Storage != nil {
		c.Storage.compileTransfers()
		c.Storage.program = s.compileStorageFields(c, nil, c.Storage)
	}
	if c.NativeStorage != nil {
		c.NativeStorage.compileTransfers()
		c.NativeStorage.program = s.compileStorageFields(c, nil, c.NativeStorage)
	}
	if c.Kind != "Struct" || c.StorageKind != "RawUnmanaged" || c.Versioned || c.Profile.TrackReferences ||
		c.Storage != nil || c.NativeStorage != nil {
		return
	}
	program := &packedStorage{fields: make([]packedField, 0, len(c.Members))}
	for i := range c.Members {
		m := &c.Members[i]
		if m.Wire == nil || m.Wire.Kind != "scalar" || m.Compressed {
			return
		}
		width := s.knownScalarWidth(m.Wire.Type)
		if width == 0 {
			return
		}
		program.fields = append(program.fields, packedField{name: m.Name, scalar: m.Wire.Type, offset: program.width, width: width})
		program.width += width
	}
	if program.width > 0 {
		c.rawFields = program
	}
}

func (s *Schema) compileShapeStorage(d *descriptor) {
	if d == nil {
		return
	}
	for _, layout := range []*valueLayout{d.Layout, d.RawCompoundStorage} {
		if layout != nil {
			layout.compileTransfers()
			if d.Kind == "pair" || d.Kind == "tuple" {
				layout.program = s.compileStorageFields(nil, d, layout)
			}
		}
	}
	if d.NullableLayout != nil {
		d.NullableLayout.compileTransfers()
		if d.Element != nil && d.Element.Kind == "scalar" {
			width := s.knownScalarWidth(d.Element.Type)
			d.NullableLayout.scalarSlot = width != 0 && d.NullableLayout.WireSize == 1+width
		}
	}
	s.compileShapeStorage(d.Element)
	s.compileShapeStorage(d.Key)
	s.compileShapeStorage(d.Value)
	for _, element := range d.Elements {
		s.compileShapeStorage(element)
	}
}

func (s *Schema) compileStorageFields(c *contract, d *descriptor, p *valueLayout) *packedStorage {
	if len(p.Fields) == 0 || c != nil && (c.Versioned || c.Profile.TrackReferences) {
		return nil
	}
	if c == nil && (d == nil || len(d.Elements) != len(p.Fields)) {
		return nil
	}
	program := &packedStorage{fields: make([]packedField, len(p.Fields)), tuple: c == nil}
	for i, field := range p.Fields {
		if field.Storage == nil {
			return nil
		}
		var wire *descriptor
		if c == nil {
			wire = d.Elements[i]
		} else {
			member := c.byName[field.Name]
			if member == nil || member.Name != field.Name || member.Compressed {
				return nil
			}
			wire = member.Wire
		}
		if wire == nil {
			return nil
		}
		part := packedField{name: field.Name, offset: program.width, width: field.Storage.WireSize}
		if part.width <= 0 || part.width > p.WireSize-program.width {
			return nil
		}
		if len(field.Storage.Fields) == 0 {
			if wire.Kind != "scalar" || s.knownScalarWidth(wire.Type) != part.width {
				return nil
			}
			part.scalar = wire.Type
		} else {
			switch wire.Kind {
			case "contract":
				owner := s.types[normalize(wire.Type)]
				if owner == nil {
					return nil
				}
				part.child = s.compileStorageFields(owner, nil, field.Storage)
			case "pair", "tuple":
				part.child = s.compileStorageFields(nil, wire, field.Storage)
			}
			if part.child == nil {
				return nil
			}
		}
		program.width += part.width
		program.fields[i] = part
	}
	if program.width != p.WireSize {
		return nil
	}
	return program
}

func (p *packedStorage) read(data []byte) any {
	if p.tuple {
		values := make([]any, len(p.fields))
		for i, field := range p.fields {
			values[i] = field.read(data)
		}
		return values
	}
	values := make(Record, len(p.fields))
	for _, field := range p.fields {
		values[field.name] = field.read(data)
	}
	return values
}

func (p packedField) read(data []byte) any {
	data = data[p.offset : p.offset+p.width]
	if p.child != nil {
		return p.child.read(data)
	}
	// The packed field plan proves the exact scalar width before accessing data.
	switch p.scalar {
	case "bool":
		if data[0] > 1 {
			fail(-1, "invalid Boolean byte")
		}
		return data[0] == 1
	case "byte":
		return data[0]
	case "sbyte":
		return int8(data[0])
	case "char", "ushort":
		return binary.LittleEndian.Uint16(data)
	case "short":
		return int16(binary.LittleEndian.Uint16(data))
	case "int":
		return int32(binary.LittleEndian.Uint32(data))
	case "uint":
		return binary.LittleEndian.Uint32(data)
	case "long":
		return int64(binary.LittleEndian.Uint64(data))
	case "ulong":
		return binary.LittleEndian.Uint64(data)
	case "float":
		return math.Float32frombits(binary.LittleEndian.Uint32(data))
	case "double":
		return math.Float64frombits(binary.LittleEndian.Uint64(data))
	case "nint", "System.IntPtr":
		if p.width == 4 {
			return int32(binary.LittleEndian.Uint32(data))
		}
		return int64(binary.LittleEndian.Uint64(data))
	case "nuint", "System.UIntPtr":
		if p.width == 4 {
			return binary.LittleEndian.Uint32(data)
		}
		return binary.LittleEndian.Uint64(data)
	default:
		fail(-1, "unsupported scalar storage")
		return nil
	}
}

func (p *packedStorage) write(w *binaryWriter, data any) {
	if p.tuple {
		values := asSequence(data)
		if len(values) != len(p.fields) {
			fail(-1, "wrong storage tuple arity")
		}
		for i, field := range p.fields {
			field.write(w, values[i])
		}
		return
	}
	value := asRecord(data)
	for _, field := range p.fields {
		field.write(w, value[field.name])
	}
}

func (p packedField) write(w *binaryWriter, value any) {
	if p.child == nil {
		w.scalar(p.scalar, value)
	} else {
		p.child.write(w, value)
	}
}
