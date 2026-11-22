package luminpack

import (
	"math"
	"slices"
)

func (w *jsonWriter) jsonFloat(value float64, width int) {
	if width == 32 {
		w.data = appendJSONFloat32(w.data, math.Float32bits(float32(value)))
	} else {
		w.data = appendJSONFloat64(w.data, math.Float64bits(value))
	}
}

func appendJSONFloat32(output []byte, value uint32) []byte {
	abs := value & 0x7fff_ffff
	exponent := abs >> 23
	if exponent == 0xff {
		fail(-1, "non-finite floating-point value cannot be written as JSON")
	}
	if value>>31 != 0 {
		output = append(output, '-')
	}
	if abs == 0 {
		return append(output, '0')
	}
	legacy := exponent-95 <= 64
	if legacy {
		magnitude := float64(math.Float32frombits(abs))
		if magnitude < 1<<24 && magnitude == math.Floor(magnitude) {
			integer := uint64(magnitude)
			return appendJSONDigits(output, integer, jsonDigitCount(integer), false)
		}
		if digits, fraction, ok := jsonShortDecimal(magnitude, true, 6); ok {
			return appendJSONFixed(output, digits, -fraction, jsonDigitCount(digits))
		}
	}
	digits, exponent10 := xjbBinary32(abs)
	return appendJSONDecimal(output, digits, exponent10, legacy, 9)
}

func appendJSONFloat64(output []byte, value uint64) []byte {
	abs := value & 0x7fff_ffff_ffff_ffff
	exponent := abs >> 52
	if exponent == 0x7ff {
		fail(-1, "non-finite floating-point value cannot be written as JSON")
	}
	if value>>63 != 0 {
		output = append(output, '-')
	}
	if abs == 0 {
		return append(output, '0')
	}
	legacy := exponent-991 <= 64
	if legacy {
		magnitude := math.Float64frombits(abs)
		if magnitude < 1<<53 && magnitude == math.Floor(magnitude) {
			integer := uint64(magnitude)
			return appendJSONDigits(output, integer, jsonDigitCount(integer), false)
		}
		if digits, fraction, ok := jsonShortDecimal(magnitude, false, 15); ok {
			return appendJSONFixed(output, digits, -fraction, jsonDigitCount(digits))
		}
	}
	digits, exponent10 := xjbBinary64(abs)
	return appendJSONDecimal(output, digits, exponent10, legacy, 17)
}

// The two candidate scales and digit guards mirror the managed integral and
// short-decimal fast paths. Explicit rounding prevents multiply-add fusion.
func jsonShortDecimal(magnitude float64, single bool, maxDigits int) (uint64, int, bool) {
	if magnitude > 2_147_483_648 {
		return 0, 0, false
	}
	if !single {
		scaled := magnitude * 1000
		if scaled != math.Floor(scaled) {
			return 0, 0, false
		}
	}
	for fraction := 2; fraction <= 4; fraction += 2 {
		factor, reciprocal := 100.0, 0.01
		if fraction == 4 {
			factor, reciprocal = 10_000.0, 0.0001
		}
		coefficient := uint64(float64(magnitude*factor) + 0.5)
		if coefficient == 0 {
			continue
		}
		if single {
			if float64(float32(float64(coefficient)*reciprocal)) != magnitude {
				continue
			}
		} else if float64(coefficient)/factor != magnitude {
			continue
		}
		trimmed := fraction
		for coefficient%10 == 0 {
			coefficient /= 10
			trimmed--
		}
		return coefficient, trimmed, jsonDigitCount(coefficient) <= maxDigits
	}
	return 0, 0, false
}

func appendJSONDecimal(output []byte, digits uint64, exponent int, legacy bool, maxDigits int) []byte {
	length := jsonDigitCount(digits)
	leading := exponent + length - 1
	if legacy && exponent >= -maxDigits || leading >= -4 && leading < maxDigits {
		return appendJSONFixed(output, digits, exponent, length)
	}
	return appendJSONScientific(output, digits, leading, length)
}

func appendJSONFixed(output []byte, digits uint64, exponent int, length int) []byte {
	if exponent >= 0 {
		output = appendJSONDigits(output, digits, length, false)
		return appendJSONZeros(output, exponent)
	}
	fraction := -exponent
	if length <= fraction {
		output = appendJSONZeros(append(output, '0', '.'), fraction-length)
		return appendJSONDigits(output, digits, length, false)
	}
	if fraction <= 2 {
		factor := jsonPowersOfTen[fraction]
		whole, tail := digits/factor, digits%factor
		output = appendJSONDigits(output, whole, jsonDigitCount(whole), false)
		output = append(output, '.')
		if fraction == 1 {
			return append(output, byte('0'+tail))
		}
		return append(output, jsonDigitPairs[tail*2:tail*2+2]...)
	}

	start := len(output)
	output = appendJSONDigits(slices.Grow(output, length+1), digits, length, false)
	output = output[:len(output)+1]
	dot := start + length - fraction
	copy(output[dot+1:], output[dot:dot+fraction])
	output[dot] = '.'
	return output
}

func appendJSONZeros(output []byte, n int) []byte {
	if n == 0 {
		return output
	}
	start := len(output)
	output = slices.Grow(output, n)[:start+n]
	for i := start; i < len(output); i++ {
		output[i] = '0'
	}
	return output
}

func appendJSONScientific(output []byte, digits uint64, exponent int, length int) []byte {
	start := len(output)
	output = appendJSONDigits(append(output, 0), digits, length, length >= 8)
	output[start] = output[start+1]
	if length == 1 {
		output = output[:start+1]
	} else {
		output[start+1] = '.'
	}
	if exponent < 0 {
		output = append(output, 'E', '-')
		exponent = -exponent
	} else {
		output = append(output, 'E', '+')
	}
	if exponent >= 100 {
		output = append(output, byte('0'+exponent/100))
		exponent %= 100
	}
	return append(output, jsonDigitPairs[exponent*2:exponent*2+2]...)
}
