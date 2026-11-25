//go:build !purego

#include "textflag.h"

TEXT ·asciiPrefixVector(SB), NOSPLIT, $0-32
	MOVD src_base+0(FP), R0
	MOVD src_len+8(FP), R1
	MOVD $0, R2
scan:
	VLD1 (R0), [V0.B16]
	VMOV V0.D[0], R3
	VMOV V0.D[1], R4
	ORR R4, R3
	MOVD $0x8080808080808080, R4
	TST R4, R3
	BNE nonascii
	ADD $16, R0
	ADD $16, R2
	CMP R1, R2
	BLO scan
	MOVD R2, ret+24(FP)
	RET
nonascii:
	MOVBU.P 1(R0), R3
	CMP $128, R3
	BHS done
	ADD $1, R2
	B nonascii
done:
	MOVD R2, ret+24(FP)
	RET

TEXT ·widenLatin1Vector(SB), NOSPLIT, $0-48
	MOVD dst_base+0(FP), R0
	MOVD src_base+24(FP), R1
	MOVD src_len+32(FP), R2
widen:
	VLD1.P 16(R1), [V0.B16]
	VUXTL V0.B8, V1.H8
	VUXTL2 V0.B16, V2.H8
	VST1.P [V1.B16, V2.B16], 32(R0)
	SUBS $16, R2
	BNE widen
	RET

TEXT ·narrowASCIIVector(SB), NOSPLIT, $0-49
	MOVD dst_base+0(FP), R0
	MOVD src_base+24(FP), R1
	MOVD src_len+32(FP), R2
narrow:
	VLD1.P 32(R1), [V0.B16, V1.B16]
	VORR V0.B16, V1.B16, V3.B16
	VMOV V3.D[0], R3
	VMOV V3.D[1], R4
	ORR R4, R3
	MOVD $0xff80ff80ff80ff80, R4
	TST R4, R3
	BNE rejected
	VUZP1 V1.B16, V0.B16, V2.B16
	VST1.P [V2.B16], 16(R0)
	SUBS $32, R2
	BNE narrow
	MOVD $1, R3
	MOVB R3, ret+48(FP)
	RET
rejected:
	MOVB ZR, ret+48(FP)
	RET

TEXT ·jsonLiteralVector(SB), NOSPLIT, $0-32
	MOVD src_base+0(FP), R0
	MOVD src_len+8(FP), R1
	MOVD $0, R2
	VMOVI $0x1f, V4.B16
	VMOVI $0x22, V5.B16
	VMOVI $0x5c, V6.B16
jsonScan:
	VLD1 (R0), [V0.B16]
	VUMIN V0.B16, V4.B16, V1.B16
	VCMEQ V0.B16, V1.B16, V1.B16
	VCMEQ V0.B16, V5.B16, V2.B16
	VCMEQ V0.B16, V6.B16, V3.B16
	VORR V2.B16, V1.B16, V1.B16
	VORR V3.B16, V1.B16, V1.B16
	VORR V0.B16, V1.B16, V1.B16
	VMOV V1.D[0], R3
	VMOV V1.D[1], R4
	ORR R4, R3
	MOVD $0x8080808080808080, R4
	TST R4, R3
	BNE jsonSpecialByte
	ADD $16, R0
	ADD $16, R2
	CMP R1, R2
	BLO jsonScan
	MOVD R2, ret+24(FP)
	RET
jsonSpecialByte:
	MOVBU.P 1(R0), R3
	CMP $0x20, R3
	BLO jsonScanDone
	CMP $0x22, R3
	BEQ jsonScanDone
	CMP $0x5c, R3
	BEQ jsonScanDone
	CMP $128, R3
	BHS jsonScanDone
	ADD $1, R2
	B jsonSpecialByte
jsonScanDone:
	MOVD R2, ret+24(FP)
	RET

TEXT ·matchPrefixVector(SB), NOSPLIT, $0-56
	MOVD a_base+0(FP), R0
	MOVD b_base+24(FP), R1
	MOVD a_len+8(FP), R2
	MOVD $0, R3
match16:
	VLD1 (R0), [V0.B16]
	VLD1 (R1), [V1.B16]
	VEOR V0.B16, V1.B16, V0.B16
	VMOV V0.D[0], R4
	VMOV V0.D[1], R5
	ORR R5, R4
	CBNZ R4, difference16
	ADD $16, R0
	ADD $16, R1
	ADD $16, R3
	CMP R2, R3
	BLO match16
	MOVD R3, ret+48(FP)
	RET
difference16:
	MOVBU.P 1(R0), R4
	MOVBU.P 1(R1), R5
	CMP R4, R5
	BNE matchDone
	ADD $1, R3
	B difference16
matchDone:
	MOVD R3, ret+48(FP)
	RET
