// Package luminpack reads and writes the managed LuminPack wire format.
package luminpack

import (
	"encoding/json"
	"fmt"
	"math"
	"strconv"
	"strings"
)

// Record contains members under their serialized .NET names. Reusing the same map
// in a tracked graph preserves identity.
type Record map[string]any

// Entry represents one dictionary entry in wire order.
type Entry struct{ Key, Value any }

// Dictionary preserves the wire order and supports arbitrary serialized key types.
type Dictionary []Entry

// Union contains a concrete type identity and the selected payload.
type Union struct {
	Type  string
	Value any
}

// Rectangular contains row-major elements, dimensions, and lower bounds.
type Rectangular struct {
	Lengths, LowerBounds []int32
	Values               []any
}

// UTF16String preserves the unpaired code units accepted by the UTF-16 length formats.
type UTF16String []uint16

// NativeValue carries the logical value and a copy of its headerless CLR
// storage, including padding. Binary writing overlays the logical fields onto
// the supplied storage. JSON uses Value alone.
type NativeValue struct {
	Value   any
	Storage []byte
}

// FormatError reports an invalid document or a value incompatible with the schema.
type FormatError struct {
	Offset  int
	Message string
}

func (e *FormatError) Error() string {
	if e.Offset < 0 {
		return "luminpack: " + e.Message
	}
	return fmt.Sprintf("luminpack: byte %d: %s", e.Offset, e.Message)
}

func fail(offset int, message string) { panic(&FormatError{offset, message}) }
func recoverFormat(err *error) {
	if p := recover(); p != nil {
		if fault, ok := p.(*FormatError); ok {
			*err = fault
			return
		}
		panic(p)
	}
}

// Schema is an immutable compiled document contract. Independent operations can
// use the same schema concurrently.
type Schema struct {
	root                  string
	rootWire              *descriptor
	rootProfile           profile
	rootJSONDirect        bool
	binaryProfile         *int
	nativeIntegerBytes    int
	nativeScalarAlignment int
	complete              bool
	types                 map[string]*contract
	custom                map[string]CustomCodec
	registered            map[*contract]registeredUnion
}

type descriptor struct {
	Kind                  string
	Type                  string
	Element, Key, Value   *descriptor
	Elements              []*descriptor
	Rank                  int
	Profile               *int
	Memory, Required      bool
	Width                 int
	Family                string
	EnumType              string
	Layout                *valueLayout
	RawCompoundStorage    *valueLayout
	Raw, RawCollection    bool
	NullableEncoding      int
	NullableLayout        *valueLayout
	NativeNullableElement bool
}

type profile struct {
	Binary                                       int
	Declared                                     bool
	JSONEncoding                                 JSONEncoding
	JSONDepth                                    int
	StrictJSON                                   bool
	TrackReferences                              bool
	Trusted                                      bool
	MaxInput, MaxCollection, MaxString, MaxDepth *int
}

func (c *contract) headerCount() int {
	if c.BinaryMemberCount != nil {
		return *c.BinaryMemberCount
	}
	return len(c.Members)
}

func (c *contract) newJSONRecord() Record {
	result := make(Record, len(c.Members))
	if c.BinaryMemberCount != nil {
		result["$items"] = []any{}
	}
	return result
}

type contract struct {
	BinaryMemberCount                *int
	ID, Type, Kind, StorageKind      string
	Profile                          profile
	Versioned                        bool
	CustomJSON                       bool
	Storage, NativeStorage           *valueLayout
	Members                          []member
	Cases                            []unionCase
	UnionRegistration, UnionWideTags bool
	byHash                           map[uint32]*unionCase
	byType                           map[string]*unionCase
	byName                           map[string]*member
	jsonDirect                       bool
	rawFields                        *packedStorage
}

type member struct {
	Name                  string
	Profile               profile
	Wire                  *descriptor
	OldNames              []string
	NameKeys              []uint32
	Descriptor            json.RawMessage
	LegacyVersionContract json.RawMessage
	FixedNullable         bool
	Compressed            bool
	versionDescriptor     uint64
	hasVersionDescriptor  bool
	legacy                string
	jsonHead              []byte
}

type valueLayout struct {
	WireSize     int
	Tagged       bool
	ModelSizes   []int
	ModelOffsets [][]int
	Fields       []storageField
	transfers    []storageTransfer
	program      *packedStorage
	scalarSlot   bool
}

type storageField struct {
	Name    string
	Storage *valueLayout
}

type unionCase struct {
	ID, Type string
	Hash     uint32
	Tag      uint16
	OwnEntry bool
	target   *contract
}

type schemaWire struct {
	Version                    int
	Root                       string
	RootWire                   *descriptor
	RootProfile                profile
	BinaryProfile              *int
	NativeIntegerBytes         int
	NativeScalarAlignment      int
	RequireCompleteBinaryInput *bool
	Contracts                  []*contract
}

func normalize(id string) string { return strings.TrimSpace(strings.ReplaceAll(id, "global::", "")) }

// LoadSchema indexes the portable compiler projection produced by the .NET tool.
func LoadSchema(data []byte) (*Schema, error) {
	var wire schemaWire
	if err := json.Unmarshal(data, &wire); err != nil {
		return nil, err
	}
	if wire.Version != 1 {
		return nil, fmt.Errorf("luminpack: unsupported schema version %d", wire.Version)
	}
	if wire.NativeIntegerBytes != 0 && wire.NativeIntegerBytes != 4 && wire.NativeIntegerBytes != 8 {
		return nil, fmt.Errorf("luminpack: invalid native integer width")
	}
	if wire.NativeScalarAlignment != 0 && wire.NativeScalarAlignment != 4 && wire.NativeScalarAlignment != 8 {
		return nil, fmt.Errorf("luminpack: invalid native scalar alignment")
	}
	s := &Schema{root: normalize(wire.Root), rootWire: wire.RootWire, rootProfile: wire.RootProfile, binaryProfile: wire.BinaryProfile, nativeIntegerBytes: wire.NativeIntegerBytes, nativeScalarAlignment: wire.NativeScalarAlignment, complete: true, types: make(map[string]*contract, len(wire.Contracts)*2)}
	if s.nativeScalarAlignment == 0 {
		s.nativeScalarAlignment = 8
	}
	if s.nativeIntegerBytes == 0 {
		s.nativeIntegerBytes = strconv.IntSize / 8
	}
	if wire.RequireCompleteBinaryInput != nil {
		s.complete = *wire.RequireCompleteBinaryInput
	}
	for _, c := range wire.Contracts {
		if c == nil || normalize(c.ID) == "" {
			return nil, fmt.Errorf("luminpack: contract has no type identity")
		}
		for _, id := range []string{normalize(c.ID), normalize(c.Type)} {
			if id == "" {
				continue
			}
			if existing := s.types[id]; existing != nil && existing != c {
				return nil, fmt.Errorf("luminpack: duplicate contract %s", id)
			}
			s.types[id] = c
		}
		if c.BinaryMemberCount != nil && (*c.BinaryMemberCount < 0 || *c.BinaryMemberCount != len(c.Members)-1) {
			return nil, fmt.Errorf("luminpack: invalid collection base member count for %s", c.ID)
		}
		c.byName = make(map[string]*member, len(c.Members))
		for i := range c.Members {
			m := &c.Members[i]
			if c.byName[m.Name] != nil {
				return nil, fmt.Errorf("luminpack: duplicate member %s.%s", c.ID, m.Name)
			}
			c.byName[m.Name] = m
			name, _ := json.Marshal(m.Name)
			separator := byte(',')
			if i == 0 {
				separator = '{'
			}
			m.jsonHead = append(append([]byte{separator}, name...), ':')
			m.versionDescriptor, m.hasVersionDescriptor = parseDescriptor(m.Descriptor)
			_ = json.Unmarshal(m.LegacyVersionContract, &m.legacy)
		}
		for i := range c.Members {
			m := &c.Members[i]
			for _, alias := range m.OldNames {
				if c.byName[alias] == nil {
					c.byName[alias] = m
				}
			}
		}
	}
	for _, c := range wire.Contracts {
		c.byHash = make(map[uint32]*unionCase, len(c.Cases))
		c.byType = make(map[string]*unionCase, len(c.Cases)*2)
		for i := range c.Cases {
			choice := &c.Cases[i]
			if choice.Hash == 0 || choice.Hash == math.MaxUint32 || c.byHash[choice.Hash] != nil {
				return nil, fmt.Errorf("luminpack: invalid Union hash in %s", c.ID)
			}
			choice.target = s.types[normalize(choice.Type)]
			if choice.target == nil {
				choice.target = s.types[normalize(choice.ID)]
			}
			c.byHash[choice.Hash] = choice
			c.byType[normalize(choice.Type)] = choice
			c.byType[normalize(choice.ID)] = choice
		}
	}
	if s.root != "" && s.types[s.root] == nil && s.rootWire == nil {
		return nil, fmt.Errorf("luminpack: unknown root contract %s", s.root)
	}
	for _, c := range wire.Contracts {
		c.jsonDirect = s.jsonContractEligible(c, make(map[*contract]bool))
		s.compileContractStorage(c)
		for i := range c.Members {
			s.compileShapeStorage(c.Members[i].Wire)
		}
	}
	if s.rootWire != nil {
		s.rootJSONDirect = s.jsonShapeEligible(s.rootWire, make(map[*contract]bool))
		s.compileShapeStorage(s.rootWire)
	}
	return s, nil
}

func (s *Schema) operation(typeID string) (*contract, *descriptor, profile) {
	if s.rootWire != nil && (typeID == "" || normalize(typeID) == s.root) {
		return nil, s.rootWire, s.rootProfile
	}
	c := s.get(typeID)
	return c, nil, c.Profile
}

// CustomCodec supplies application behavior for a Custom contract or registered
// Union case. ReadBinary reports the consumed bytes in its input window.
// Binary-only Custom contracts do not require JSON callbacks.
type CustomCodec struct {
	ReadBinary  func([]byte) (value any, consumed int, err error)
	WriteBinary func(any) ([]byte, error)
	ReadJSON    func(json.RawMessage) (any, error)
	WriteJSON   func(any) (json.RawMessage, error)
}

// WithCustom returns a schema sharing the original contracts and using the new
// application binding for the supplied type.
func (s *Schema) WithCustom(typeID string, codec CustomCodec) (*Schema, error) {
	c := s.types[normalize(typeID)]
	if c == nil || c.Kind != "Custom" {
		return nil, fmt.Errorf("luminpack: unknown custom contract %s", typeID)
	}
	if codec.ReadBinary == nil || codec.WriteBinary == nil ||
		c.CustomJSON && (codec.ReadJSON == nil || codec.WriteJSON == nil) {
		return nil, fmt.Errorf("luminpack: incomplete custom binding for %s", typeID)
	}
	next := *s
	next.custom = make(map[string]CustomCodec, len(s.custom)+1)
	for id, binding := range s.custom {
		next.custom[id] = binding
	}
	next.custom[normalize(c.ID)] = codec
	return &next, nil
}

func (codec CustomCodec) complete() bool {
	return codec.ReadBinary != nil && codec.WriteBinary != nil && codec.ReadJSON != nil && codec.WriteJSON != nil
}

func (s *Schema) get(typeID string) *contract {
	if typeID == "" {
		typeID = s.root
	}
	if c := s.types[normalize(typeID)]; c != nil {
		return c
	}
	fail(-1, "unknown contract "+typeID)
	return nil
}

func effectiveProfile(value profile, fallback int) int {
	if value.Declared {
		return value.Binary
	}
	return fallback
}

func limit(value *int, fallback int) int {
	if value != nil && *value >= 0 {
		return *value
	}
	return fallback
}

func parseDescriptor(data json.RawMessage) (uint64, bool) {
	if len(data) == 0 || string(data) == "null" {
		return 0, false
	}
	var value string
	if json.Unmarshal(data, &value) == nil {
		n, err := strconv.ParseUint(value, 16, 64)
		return n, err == nil
	}
	var n uint64
	if json.Unmarshal(data, &n) == nil {
		return n, true
	}
	return 0, false
}
