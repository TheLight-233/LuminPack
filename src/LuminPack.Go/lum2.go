package luminpack

import (
	"encoding/binary"
	"errors"
	"math/bits"
	"sync"
)

const (
	lum2Magic         = 0x324d554c
	lum2Header        = 8
	lum2MaximumSource = 2_130_836_464
	lum2MinimumMatch  = 4
	lum2LastLiterals  = 5
	lum2MatchGuard    = 12
	lum2Saturated     = 15
	lum2Escape        = 255
)

var (
	errLum2    = errors.New("invalid LUM2 compression frame")
	lum2Tables = sync.Pool{New: func() any {
		table := make([]uint16, 0)
		return &table
	}}
)

func compressLum2(source []byte) ([]byte, error) {
	length := len(source)
	if length > lum2MaximumSource {
		return nil, errors.New("LUM2 source exceeds the maximum length")
	}
	capacity := lum2Header
	if length != 0 {
		capacity += length + (length >> 7) + 16
	}
	out := make([]byte, lum2Header, capacity)
	binary.LittleEndian.PutUint32(out, lum2Magic)
	binary.LittleEndian.PutUint32(out[4:], uint32(length))
	if length == 0 {
		return out, nil
	}
	anchor := 0
	if length >= lum2MinimumMatch+lum2LastLiterals+lum2MatchGuard {
		tableBits := min(15, max(8, bits.Len(uint((length-1)|1))))
		tableSize := 1 << tableBits
		shift := 32 - tableBits
		wrapper := lum2Tables.Get().(*[]uint16)
		defer lum2Tables.Put(wrapper)
		if len(*wrapper) < tableSize {
			*wrapper = make([]uint16, tableSize)
		}
		table := *wrapper
		load := func(offset int) uint32 { return binary.LittleEndian.Uint32(source[offset:]) }
		hash := func(word uint32) uint32 { return (word * 2654435761) >> shift }
		table[hash(load(0))] = 0
		matchLimit := length - lum2MatchGuard
		extensionLimit := length - lum2LastLiterals
		position := 1
		skip := 32
		for position < matchLimit {
			word := load(position)
			slot := hash(word)
			distance := int(uint16(position - int(table[slot])))
			table[slot] = uint16(position)
			if distance == 0 || distance > position || load(position-distance) != word {
				position += skip >> 5
				skip++
				continue
			}
			skip = 32
			candidate := position - distance
			for position > anchor && candidate != 0 && source[position-1] == source[candidate-1] {
				position--
				candidate--
			}
			match := lum2MatchLength(source, position, candidate, extensionLimit)
			lazyPosition := position + 1
			if lazyPosition < matchLimit {
				lazyWord := load(lazyPosition)
				lazySlot := hash(lazyWord)
				lazyDistance := int(uint16(lazyPosition - int(table[lazySlot])))
				table[lazySlot] = uint16(lazyPosition)
				lazyCandidate := lazyPosition - lazyDistance
				if lazyDistance != 0 && lazyDistance <= lazyPosition && extensionLimit-lazyPosition > match &&
					load(lazyCandidate) == lazyWord && load(lazyPosition+match-3) == load(lazyCandidate+match-3) {
					lazyLength := lum2MatchLength(source, lazyPosition, lazyCandidate, extensionLimit)
					if lazyLength > match {
						position, candidate, match = lazyPosition, lazyCandidate, lazyLength
						for position > anchor && candidate != 0 && source[position-1] == source[candidate-1] {
							position--
							candidate--
							match++
						}
					}
				}
			}
			nibble := match - lum2MinimumMatch
			out = lum2Literals(out, source[anchor:position], nibble)
			out = binary.LittleEndian.AppendUint16(out, uint16(position-candidate))
			if nibble >= lum2Saturated {
				out = lum2Extension(out, nibble-lum2Saturated)
			}
			position += match
			anchor = position
			if position < matchLimit {
				for boundary := position - 2; boundary < position; boundary++ {
					table[hash(load(boundary))] = uint16(boundary)
				}
			}
		}
	}
	return lum2Literals(out, source[anchor:], 0), nil
}

func lum2MatchLength(source []byte, position, candidate, end int) int {
	start := position + lum2MinimumMatch
	history := candidate + lum2MinimumMatch
	return lum2MinimumMatch + matchPrefix(source[start:end], source[history:history+end-start])
}

func lum2Extension(output []byte, size int) []byte {
	if size >= lum2Escape {
		output = append(output, lum2Escape)
		size -= lum2Escape
		for size >= 128 {
			output = append(output, byte(size&0x7f)|0x80)
			size >>= 7
		}
	}
	return append(output, byte(size))
}

func lum2Literals(output, literal []byte, match int) []byte {
	output = append(output, byte(min(lum2Saturated, len(literal))<<4|min(lum2Saturated, match)))
	if len(literal) >= lum2Saturated {
		output = lum2Extension(output, len(literal)-lum2Saturated)
	}
	return append(output, literal...)
}

func decompressLum2(frame []byte, expected int) ([]byte, error) {
	if expected < 0 || expected > 0x7fffffff || len(frame) < lum2Header ||
		binary.LittleEndian.Uint32(frame) != lum2Magic || binary.LittleEndian.Uint32(frame[4:]) != uint32(expected) {
		return nil, errLum2
	}
	if expected == 0 {
		if len(frame) != lum2Header {
			return nil, errLum2
		}
		return make([]byte, 0), nil
	}
	body := frame[lum2Header:]
	if (expected-1)/lum2Escape >= len(body) {
		if err := readLum2Body(body, nil, expected); err != nil {
			return nil, err
		}
	}
	result := make([]byte, expected)
	if err := readLum2Body(body, result, expected); err != nil {
		return nil, err
	}
	return result, nil
}

func readLum2Extension(source []byte, position *int, length, remaining int) (int, error) {
	if *position >= len(source) || length > remaining {
		return 0, errLum2
	}
	first := int(source[*position])
	(*position)++
	if first > remaining-length {
		return 0, errLum2
	}
	length += first
	if first != lum2Escape {
		return length, nil
	}
	extra := 0
	for shift := 0; shift <= 28; shift += 7 {
		if *position >= len(source) {
			return 0, errLum2
		}
		next := int(source[*position])
		(*position)++
		if shift == 28 && next > 7 {
			return 0, errLum2
		}
		extra |= (next & 0x7f) << shift
		if extra > remaining-length {
			return 0, errLum2
		}
		if next < 128 {
			if shift != 0 && next == 0 {
				return 0, errLum2
			}
			return length + extra, nil
		}
	}
	return 0, errLum2
}

func readLum2Body(source, output []byte, expected int) error {
	position, produced := 0, 0
	for position < len(source) {
		token := int(source[position])
		position++
		literals, nibble := token>>4, token&15
		if literals == lum2Saturated {
			var err error
			literals, err = readLum2Extension(source, &position, literals, expected-produced)
			if err != nil {
				return err
			}
		}
		if literals > len(source)-position || literals > expected-produced {
			return errLum2
		}
		if output != nil {
			copy(output[produced:], source[position:position+literals])
		}
		position += literals
		produced += literals
		if position == len(source) {
			if nibble != 0 || produced != expected {
				return errLum2
			}
			return nil
		}
		if len(source)-position < 2 {
			return errLum2
		}
		distance := int(binary.LittleEndian.Uint16(source[position:]))
		position += 2
		if distance == 0 || distance > produced {
			return errLum2
		}
		match := nibble + lum2MinimumMatch
		if nibble == lum2Saturated {
			var err error
			match, err = readLum2Extension(source, &position, match, expected-produced)
			if err != nil {
				return err
			}
		}
		if match > expected-produced {
			return errLum2
		}
		if output != nil {
			origin := produced - distance
			for copied := 0; copied < match; {
				count := min(distance+copied, match-copied)
				copy(output[produced+copied:], output[origin:origin+count])
				copied += count
			}
		}
		produced += match
	}
	return errLum2
}
