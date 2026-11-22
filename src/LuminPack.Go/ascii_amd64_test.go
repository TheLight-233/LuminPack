//go:build !purego

package luminpack

import (
	"os"
	"testing"
)

func TestSupportedAMD64VectorTiers(t *testing.T) {
	detected := vectorFeatures
	if os.Getenv("LUMINPACK_REQUIRE_AVX512") == "1" && detected&2 == 0 {
		t.Fatal("AVX-512BW is required for this test run")
	}
	t.Cleanup(func() { vectorFeatures = detected })
	for _, tc := range []struct {
		name string
		bit  uint8
	}{{"SSE2", 0}, {"AVX2", 1}, {"AVX512BW", 2}} {
		t.Run(tc.name, func(t *testing.T) {
			if tc.bit != 0 && detected&tc.bit == 0 {
				t.Skip("processor or operating system does not provide the feature")
			}
			vectorFeatures = tc.bit
			testBoundedASCIITranscoding(t)
			testBoundedMatchPrefix(t)
		})
	}
}
