Correctness: 2012 edge/random cases passed (byte equality, JSON roundtrip, canary, exact-size destination).

.NET 10.0.9; X64; CPU count 20
DOTNET_TieredCompilation=0
Preallocated buffers, reused optional state, 7 alternating rounds; median ns/op.

| Data | UTF16 units | Current ns | Segmented ns | Speedup | Current B/op | Segmented B/op |
|---|---:|---:|---:|---:|---:|---:|
| Ascii | 8 | 10.7 | 9.9 | 1.08x | 0.000 | 0.000 |
| Ascii | 32 | 29.3 | 10.2 | 2.88x | 0.000 | 0.000 |
| Ascii | 128 | 117.3 | 13.1 | 8.94x | 0.000 | 0.000 |
| Ascii | 4096 | 3045.2 | 230.0 | 13.24x | 0.000 | 0.000 |
| Chinese | 8 | 74.3 | 15.1 | 4.93x | 0.000 | 0.000 |
| Chinese | 32 | 290.3 | 38.0 | 7.64x | 0.000 | 0.000 |
| Chinese | 128 | 1115.7 | 113.3 | 9.85x | 0.000 | 0.000 |
| Chinese | 4096 | 34540.0 | 2962.8 | 11.66x | 0.000 | 0.000 |
| Mixed | 8 | 10.6 | 9.6 | 1.11x | 0.000 | 0.000 |
| Mixed | 32 | 124.6 | 19.9 | 6.27x | 0.000 | 0.000 |
| Mixed | 128 | 463.8 | 49.2 | 9.43x | 0.000 | 0.000 |
| Mixed | 4096 | 14017.0 | 1338.1 | 10.47x | 0.000 | 0.000 |
| Emoji | 8 | 10.8 | 9.5 | 1.13x | 0.000 | 0.000 |
| Emoji | 32 | 139.3 | 26.9 | 5.17x | 0.000 | 0.000 |
| Emoji | 128 | 596.7 | 79.7 | 7.48x | 0.000 | 0.000 |
| Emoji | 4096 | 19265.9 | 2439.1 | 7.90x | 0.000 | 0.000 |
| SparseEscape | 8 | 72.5 | 14.8 | 4.90x | 0.000 | 0.000 |
| SparseEscape | 32 | 285.2 | 37.7 | 7.56x | 0.000 | 0.000 |
| SparseEscape | 128 | 1049.3 | 121.5 | 8.64x | 0.000 | 0.000 |
| SparseEscape | 4096 | 33653.6 | 3726.9 | 9.03x | 0.000 | 0.000 |
| DenseEscape | 8 | 41.7 | 50.8 | 0.82x | 0.000 | 0.000 |
| DenseEscape | 32 | 154.1 | 200.7 | 0.77x | 0.000 | 0.000 |
| DenseEscape | 128 | 564.0 | 728.4 | 0.77x | 0.000 | 0.000 |
| DenseEscape | 4096 | 18118.1 | 22505.8 | 0.81x | 0.000 | 0.000 |
