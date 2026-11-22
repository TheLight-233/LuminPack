package luminpack

import "math/bits"

// These scalar conversions port LuminPack's C# XJB cores. The upstream
// provenance and Apache 2.0 license are recorded in docs/xjb-NOTICE.md.
func xjbBinary32(value uint32) (uint64, int) {
	const bit = 36
	sig := value & ((1 << 23) - 1)
	exp := int(value << 1 >> 24)
	coefficient := uint64(sig | 1<<23)
	q := exp - 150
	if exp == 0 {
		if sig == 0 {
			return 0, 0
		}
		q, coefficient = -149, uint64(sig)
	}

	if sig == 0 {
		// An exact power of two has an asymmetric rounding interval.
		k := (q*1233 - 512) >> 12
		h := q + ((k*-1701 - 1701) >> 9)
		power := xjbFloat32Pow10[45+k]
		high := power >> (4 - h)
		ten := (high >> bit) * 10
		fraction := high & ((1 << bit) - 1)
		halfULP := power >> (64 - bit - h)
		one := (fraction*5 + (1 << (bit - 2)) - 7 + (fraction >> (bit - 4))) >> (bit - 1)
		if halfULP>>1 > fraction {
			one = 0
		}
		if q == 31-150 || q == 214-150 || q == 217-150 {
			one++
		}
		if halfULP > (1<<bit)-1-fraction {
			one = 10
		}
		return normalizeXJB(ten+one, k)
	}

	h37 := int(xjbFloat32H37[exp])
	k := (q * 1233) >> 12
	power := xjbFloat32Pow10[45+k]
	high, _ := bits.Mul64(coefficient<<h37, power)
	halfULP := (power >> (65 - h37)) + ((uint64(sig) + 1) & 1)
	shorter := ((high + halfULP) >> bit) * 10
	longer := (high*5 + (1 << (bit - 2)) - 7 + ((high >> 32) & 15)) >> (bit - 1)
	if (high-halfULP)>>bit < (high+halfULP)>>bit {
		return normalizeXJB(shorter, k)
	}
	return normalizeXJB(longer, k)
}

func xjbBinary64(value uint64) (uint64, int) {
	const offset = 6
	const powerBias = 584
	sig := value & ((1 << 52) - 1)
	exp := int(value << 1 >> 53)
	coefficient := sig | 1<<52
	q := exp - 1075
	if exp == 0 {
		if sig == 0 {
			return 0, 0
		}
		q, coefficient = -1074, sig
	}

	if sig == 0 {
		k := (q*315653 - 131072) >> 20
		h := q + ((k*-217707 - 217707) >> 16)
		power := xjbFloat64Pow10[powerBias-k*2]
		halfULP := power >> -h
		fraction := power << (53 + h)
		ten := (power >> (11 - h)) * 10
		one := ((fraction>>(53+h))*5 + (1 << (9 - h))) >> (10 - h)
		if ((fraction>>54)*5)&((1<<9)-1) > (halfULP>>55)*5 {
			one = ((fraction>>54)*5)>>9 + 1
		}
		if fraction == 1<<62 {
			one = 2
		}
		if halfULP>>1 > fraction {
			one = 0
		}
		if halfULP > ^uint64(0)-fraction {
			one = 10
		}
		return normalizeXJB(ten+one, k)
	}

	k := ((exp - 1075) * 78913) >> 18
	h := q + (((-1 - k) * 217707) >> 16)
	cb := coefficient << (h + 1 + offset)
	index := powerBias - k*2
	powerHi, powerLo := xjbFloat64Pow10[index], xjbFloat64Pow10[index+1]
	hiA, loA := bits.Mul64(cb, powerHi)
	hiB, _ := bits.Mul64(cb, powerLo)
	low, carry := bits.Add64(loA, hiB, 0)
	high := hiA + carry
	fraction := low>>offset | high<<(64-offset)
	halfULP := (powerHi >> -h) + ((value + 1) & 1)
	ten := (high >> offset) * 10

	oneHigh, oneLow := bits.Mul64(fraction, 10)
	var addend uint64
	if fraction != 1<<62 {
		addend = (1 << 63) + 6
	}
	_, carry = bits.Add64(oneLow, addend, 0)
	one := oneHigh + carry
	if fraction < halfULP {
		one = 0
	}
	if ^uint64(0)-fraction < halfULP {
		one = 10
	}
	return normalizeXJB(ten+one, k)
}

func normalizeXJB(digits uint64, exponent int) (uint64, int) {
	for digits != 0 && digits%10 == 0 {
		digits /= 10
		exponent++
	}
	return digits, exponent
}
