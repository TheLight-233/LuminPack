package luminpack

import (
	"encoding/binary"
	"math/bits"
	"slices"
)

const jsonDigitPairs = "00010203040506070809101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899"

var jsonPowersOfTen = [...]uint64{
	1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000, 1_000_000_000,
	10_000_000_000, 100_000_000_000, 1_000_000_000_000, 10_000_000_000_000, 100_000_000_000_000,
	1_000_000_000_000_000, 10_000_000_000_000_000, 100_000_000_000_000_000,
	1_000_000_000_000_000_000, 10_000_000_000_000_000_000,
}

// The packed heads and padded quads encode digits in explicit little-endian
// byte order. The same head table covers the smaller type working sets.
var jsonDecimalTables = func() struct {
	heads   [10_000]uint32
	lengths [10_000]uint8
	quads   [10_000]uint32
} {
	var tables struct {
		heads   [10_000]uint32
		lengths [10_000]uint8
		quads   [10_000]uint32
	}
	for i := range tables.heads {
		high := (i / 100) * 2
		low := (i % 100) * 2
		quad := uint32(jsonDigitPairs[high]) | uint32(jsonDigitPairs[high+1])<<8 |
			uint32(jsonDigitPairs[low])<<16 | uint32(jsonDigitPairs[low+1])<<24
		length := jsonDigitCount(uint64(i))
		tables.quads[i] = quad
		tables.heads[i] = quad >> (8 * (4 - length))
		tables.lengths[i] = uint8(length)
	}
	return tables
}()

func jsonDigitCount(value uint64) int {
	if value == 0 {
		return 1
	}
	digits := ((bits.Len64(value) - 1) * 1233 >> 12) + 1
	if digits < len(jsonPowersOfTen) && value >= jsonPowersOfTen[digits] {
		return digits + 1
	}
	return digits
}

func appendPackedJSON(output []byte, value uint32) []byte {
	start := len(output)
	output = binary.LittleEndian.AppendUint32(output, jsonDecimalTables.heads[value])
	return output[:start+int(jsonDecimalTables.lengths[value])]
}

func appendJSONInt32(output []byte, value int32) []byte {
	if value < 0 {
		return appendJSONUint32(append(output, '-'), 0-uint32(value))
	}
	return appendJSONUint32(output, uint32(value))
}

func appendJSONUint32(output []byte, value uint32) []byte {
	if value < 10_000 {
		return appendPackedJSON(output, value)
	}
	high := value / 10_000
	low := value - high*10_000
	if high < 10_000 {
		output = appendPackedJSON(output, high)
	} else {
		top := high / 10_000
		output = appendPackedJSON(output, top)
		output = binary.LittleEndian.AppendUint32(output, jsonDecimalTables.quads[high-top*10_000])
	}
	return binary.LittleEndian.AppendUint32(output, jsonDecimalTables.quads[low])
}

func appendJSONInt64(output []byte, value int64) []byte {
	if value < 0 {
		return appendJSONUint64(append(output, '-'), 0-uint64(value))
	}
	return appendJSONUint64(output, uint64(value))
}

func appendJSONUint64(output []byte, value uint64) []byte {
	if value < 10_000 {
		return appendPackedJSON(output, uint32(value))
	}
	return appendJSONDigits(output, value, jsonDigitCount(value), true)
}

func appendJSONInteger(output []byte, value uint64, signed bool, width int) []byte {
	switch width {
	case 1:
		if signed && int64(value) < 0 {
			return appendPackedJSON(append(output, '-'), uint32(0-value))
		}
		return appendPackedJSON(output, uint32(value))
	case 2:
		if signed && int64(value) < 0 {
			output = append(output, '-')
			value = 0 - value
		}
		lutLimit := uint64(10_000)
		if signed {
			lutLimit = 1024
		}
		if value < lutLimit {
			return appendPackedJSON(output, uint32(value))
		}
		return appendJSONDigits(output, value, jsonDigitCount(value), false)
	case 4:
		if signed {
			return appendJSONInt32(output, int32(value))
		}
		return appendJSONUint32(output, uint32(value))
	default:
		if signed {
			return appendJSONInt64(output, int64(value))
		}
		return appendJSONUint64(output, value)
	}
}

func appendJSONDigits(output []byte, value uint64, digits int, quads bool) []byte {
	start := len(output)
	output = slices.Grow(output, digits)[:start+digits]
	dest := output[start:]
	pos := digits
	if quads {
		for value >= 10_000 {
			next := value / 10_000
			pos -= 4
			binary.LittleEndian.PutUint32(dest[pos:], jsonDecimalTables.quads[value-next*10_000])
			value = next
		}
		if value >= 1000 {
			binary.LittleEndian.PutUint32(dest, jsonDecimalTables.quads[value])
			return output
		}
	}
	for value >= 100 {
		next := value / 100
		index := (value - next*100) * 2
		pos -= 2
		copy(dest[pos:pos+2], jsonDigitPairs[index:index+2])
		value = next
	}
	if value < 10 {
		dest[0] = byte('0' + value)
	} else {
		index := value * 2
		copy(dest[:2], jsonDigitPairs[index:index+2])
	}
	return output
}
