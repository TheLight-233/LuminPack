Correctness: 2012 edge/random cases passed (byte equality, JSON roundtrip, canary, exact-size destination).

.NET 10.0.9; X64; CPU count 20
DOTNET_TieredCompilation=0
Preallocated buffers, reused optional state, 7 alternating rounds; median ns/op.

| Data | UTF16 units | Current ns | Segmented ns | Speedup | Current B/op | Segmented B/op |
|---|---:|---:|---:|---:|---:|---:|
| Ascii | 8 | 10.8 | 9.9 | 1.09x | 0.000 | 0.000 |
| Ascii | 32 | 30.6 | 10.8 | 2.82x | 0.000 | 0.000 |
| Ascii | 128 | 124.1 | 15.3 | 8.14x | 0.000 | 0.000 |
| Ascii | 4096 | 3149.5 | 238.1 | 13.23x | 0.000 | 0.000 |
| Chinese | 8 | 77.8 | 16.3 | 4.76x | 0.000 | 0.000 |
| Chinese | 32 | 300.9 | 39.8 | 7.56x | 0.000 | 0.000 |
| Chinese | 128 | 1130.2 | 109.9 | 10.29x | 0.000 | 0.000 |
| Chinese | 4096 | 34346.1 | 2988.8 | 11.49x | 0.000 | 0.000 |
| Mixed | 8 | 10.4 | 9.8 | 1.06x | 0.000 | 0.000 |
| Mixed | 32 | 123.5 | 21.3 | 5.79x | 0.000 | 0.000 |
| Mixed | 128 | 459.0 | 50.7 | 9.06x | 0.000 | 0.000 |
| Mixed | 4096 | 14054.9 | 1448.6 | 9.70x | 0.000 | 0.000 |
| Emoji | 8 | 10.2 | 9.7 | 1.05x | 0.000 | 0.000 |
| Emoji | 32 | 139.0 | 26.7 | 5.20x | 0.000 | 0.000 |
| Emoji | 128 | 603.2 | 80.1 | 7.53x | 0.000 | 0.000 |
| Emoji | 4096 | 19444.5 | 2423.6 | 8.02x | 0.000 | 0.000 |
| SparseEscape | 8 | 72.9 | 15.7 | 4.63x | 0.000 | 0.000 |
| SparseEscape | 32 | 281.3 | 37.9 | 7.43x | 0.000 | 0.000 |
| SparseEscape | 128 | 1057.2 | 123.1 | 8.59x | 0.000 | 0.000 |
| SparseEscape | 4096 | 33991.1 | 3727.2 | 9.12x | 0.000 | 0.000 |
| DenseEscape | 8 | 40.4 | 53.7 | 0.75x | 0.000 | 0.000 |
| DenseEscape | 32 | 153.4 | 205.7 | 0.75x | 0.000 | 0.000 |
| DenseEscape | 128 | 568.2 | 731.4 | 0.78x | 0.000 | 0.000 |
| DenseEscape | 4096 | 17437.8 | 22301.4 | 0.78x | 0.000 | 0.000 |
