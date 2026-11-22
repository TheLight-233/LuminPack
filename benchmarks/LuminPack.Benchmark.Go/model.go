package main

import (
	"encoding/json"
	"fmt"
	"reflect"
	"strconv"
	"strings"

	luminpack "github.com/TheLight-233/LuminPack/src/LuminPack.Go"
)

type manifest struct {
	Root      string
	Contracts []contract
}

type contract struct {
	ID, Type, Kind string
	Members        []struct {
		Name string
		Wire *wire
	}
	Cases []struct {
		ID, Type string
		Hash     uint32
	}
}

type wire struct {
	Kind, Type          string
	Element, Key, Value *wire
}

type projection struct {
	kind                string
	typ                 reflect.Type
	fields              []*projection
	names               []string
	cases               map[string]unionCase
	element, key, value *projection
}

type unionCase struct {
	payload *projection
	index   int
	hash    uint32
}

type projector struct {
	declared map[string]contract
	built    map[string]*projection
	building map[string]bool
}

var scalarTypes = map[string]reflect.Type{
	"bool": reflect.TypeFor[bool](), "byte": reflect.TypeFor[uint8](), "sbyte": reflect.TypeFor[int8](),
	"short": reflect.TypeFor[int16](), "ushort": reflect.TypeFor[uint16](), "char": reflect.TypeFor[uint16](),
	"int": reflect.TypeFor[int32](), "uint": reflect.TypeFor[uint32](),
	"long": reflect.TypeFor[int64](), "ulong": reflect.TypeFor[uint64](),
	"float": reflect.TypeFor[float32](), "double": reflect.TypeFor[float64](),
}

func normalize(name string) string { return strings.ReplaceAll(name, "global::", "") }

// A shared typed model keeps the library baselines on their ordinary struct
// APIs. The reflection work and projection from LuminPack happen before timing.
func referenceProjection(data []byte) (*projection, error) {
	var source manifest
	if err := json.Unmarshal(data, &source); err != nil {
		return nil, err
	}
	p := projector{make(map[string]contract), make(map[string]*projection), make(map[string]bool)}
	for _, item := range source.Contracts {
		p.declared[normalize(item.Type)] = item
		p.declared[normalize(item.ID)] = item
	}
	return p.contract(source.Root)
}

func (p *projector) contract(name string) (*projection, error) {
	t, ok := p.declared[normalize(name)]
	if !ok {
		return nil, fmt.Errorf("missing benchmark contract %q", name)
	}
	if done := p.built[t.ID]; done != nil {
		return done, nil
	}
	if p.building[t.ID] {
		return nil, fmt.Errorf("recursive benchmark contract %q", name)
	}
	p.building[t.ID] = true
	defer delete(p.building, t.ID)
	model := &projection{kind: t.Kind}
	var fields []reflect.StructField
	switch t.Kind {
	case "Record", "Struct":
		for i, member := range t.Members {
			field, err := p.shape(member.Wire, false)
			if err != nil {
				return nil, fmt.Errorf("%s.%s: %w", name, member.Name, err)
			}
			model.names = append(model.names, member.Name)
			model.fields = append(model.fields, field)
			fields = append(fields, namedField("Field"+strconv.Itoa(i), member.Name, field.typ))
		}
	case "Union":
		// Each case has a typed optional field. The discriminator and selected
		// payload survive each library's native round trip.
		fields = append(fields, namedField("Type", "$type", reflect.TypeFor[uint32]()))
		model.cases = make(map[string]unionCase, len(t.Cases)*2)
		for _, alternative := range t.Cases {
			payload, err := p.contract(alternative.Type)
			if err != nil {
				return nil, err
			}
			field := "Case" + strconv.Itoa(len(fields)-1)
			slot := unionCase{payload, len(fields), alternative.Hash}
			model.cases[normalize(alternative.Type)] = slot
			model.cases[normalize(alternative.ID)] = slot
			fields = append(fields, namedField(field, field+",omitempty", payload.typ))
		}
	default:
		return nil, fmt.Errorf("unsupported benchmark contract %s of kind %s", name, t.Kind)
	}
	model.typ = reflect.StructOf(fields)
	if t.Kind != "Struct" {
		model.typ = reflect.PointerTo(model.typ)
	}
	p.built[t.ID] = model
	return model, nil
}

func namedField(name, wireName string, typ reflect.Type) reflect.StructField {
	tag := strconv.Quote(wireName)
	return reflect.StructField{Name: name, Type: typ, Tag: reflect.StructTag("json:" + tag + " msgpack:" + tag + " cbor:" + tag)}
}

func (p *projector) shape(w *wire, key bool) (*projection, error) {
	if w == nil {
		return nil, fmt.Errorf("missing benchmark wire shape")
	}
	shape := &projection{kind: w.Kind}
	switch w.Kind {
	case "scalar":
		shape.typ = scalarTypes[w.Type]
		if shape.typ == nil {
			return nil, fmt.Errorf("unsupported benchmark scalar %q", w.Type)
		}
	case "string":
		shape.typ = reflect.TypeFor[string]()
		if !key {
			shape.typ = reflect.TypeFor[*string]()
		}
	case "contract":
		return p.contract(w.Type)
	case "sequence", "nullable":
		element, err := p.shape(w.Element, false)
		if err != nil {
			return nil, err
		}
		shape.element = element
		if w.Kind == "sequence" {
			shape.typ = reflect.SliceOf(element.typ)
		} else {
			shape.typ = reflect.PointerTo(element.typ)
		}
	case "dictionary":
		var err error
		shape.key, err = p.shape(w.Key, true)
		if err != nil {
			return nil, err
		}
		shape.value, err = p.shape(w.Value, false)
		if err != nil {
			return nil, err
		}
		shape.typ = reflect.MapOf(shape.key.typ, shape.value.typ)
	default:
		return nil, fmt.Errorf("unsupported benchmark wire kind %s", w.Kind)
	}
	return shape, nil
}

func (p *projection) convert(source any) reflect.Value {
	if source == nil {
		return reflect.Zero(p.typ)
	}
	switch p.kind {
	case "scalar":
		return reflect.ValueOf(source).Convert(p.typ)
	case "string":
		if p.typ.Kind() == reflect.Pointer {
			value := source.(string)
			return reflect.ValueOf(&value)
		}
		return reflect.ValueOf(source.(string))
	case "Record", "Struct":
		fields := source.(luminpack.Record)
		typ := p.typ
		pointer := typ.Kind() == reflect.Pointer
		if pointer {
			typ = typ.Elem()
		}
		record := reflect.New(typ)
		target := record.Elem()
		for i, field := range p.fields {
			target.Field(i).Set(field.convert(fields[p.names[i]]))
		}
		if pointer {
			return record
		}
		return target
	case "Union":
		value := source.(luminpack.Union)
		slot, ok := p.cases[normalize(value.Type)]
		if !ok {
			panic(fmt.Sprintf("benchmark projection cannot resolve Union case %q", value.Type))
		}
		result := reflect.New(p.typ.Elem())
		result.Elem().Field(0).SetUint(uint64(slot.hash))
		result.Elem().Field(slot.index).Set(slot.payload.convert(value.Value))
		return result
	case "sequence":
		input := reflect.ValueOf(source)
		result := reflect.MakeSlice(p.typ, input.Len(), input.Len())
		for i := 0; i < input.Len(); i++ {
			result.Index(i).Set(p.element.convert(input.Index(i).Interface()))
		}
		return result
	case "dictionary":
		input := source.(luminpack.Dictionary)
		result := reflect.MakeMapWithSize(p.typ, len(input))
		for _, item := range input {
			result.SetMapIndex(p.key.convert(item.Key), p.value.convert(item.Value))
		}
		return result
	case "nullable":
		result := reflect.New(p.typ.Elem())
		result.Elem().Set(p.element.convert(source))
		return result
	default:
		panic("unsupported benchmark projection " + p.kind)
	}
}
