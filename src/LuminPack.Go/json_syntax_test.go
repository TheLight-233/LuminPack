package luminpack

import (
	"encoding/json"
	"strings"
	"testing"
)

func TestJSONSyntax(t *testing.T) {
	for _, input := range []string{
		`[]`, `{}`, `true`, `false`, `null`, `0`, `-0`, `0.01`, `1.1e+2`, `-1E-3`, `"a"`,
		`[null,true,false,1,"☺",{"a":"\uD83D\uDE00","b":"\u003f","c":"\b\t\f\r\n\/\\\""}]`,
		`[` + strings.Repeat(`"with non-ascii é中 🎁 and a \\ slash",`, 20) + `"end"]`,
		``, `[`, `{`, `{"a":}`, `{"a":1,}`, `[true,]`, `[1 2]`, `0 1`, `00`, `-01`, `+`, `-`,
		`1.`, `1e`, `1e+`, `1e--1`, `"`, `"\u12"`, `"\u12x4"`, `"\z"`,
	} {
		err := syntaxError([]byte(input))
		if (err == nil) != json.Valid([]byte(input)) {
			t.Errorf("unexpected parsing result for %q: %v", input, err)
		}
	}
	for _, invalid := range []string{`"\ud800"`, `"\udfff"`, "\"\xff\""} {
		if err := syntaxError([]byte(invalid)); err == nil {
			t.Errorf("accepted malformed text %q", invalid)
		}
	}
}

func syntaxError(data []byte) (err error) {
	defer recoverFormat(&err)
	r := jsonScanner{data: data}
	r.value(32)
	r.space()
	if r.pos != len(r.data) {
		fail(r.pos, "trailing JSON")
	}
	return
}
