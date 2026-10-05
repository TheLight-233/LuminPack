Correctness: 2012 edge/random cases passed (byte equality, JSON roundtrip, canary, exact-size destination).

.NET 10.0.9; X64; CPU count 20
DOTNET_TieredCompilation=0
Preallocated buffers, reused optional state, 7 alternating rounds; median ns/op.

| Data | UTF16 units | Baseline ns | Runtime ns | Speedup | Baseline B/op | Runtime B/op |
|---|---:|---:|---:|---:|---:|---:|
| Ascii | 8 | 22.4 | 34.0 | 0.66x | 0.000 | 0.000 |
| Ascii | 16 | 23.9 | 16.6 | 1.44x | 0.000 | 0.000 |
| Ascii | 32 | 39.1 | 17.4 | 2.24x | 0.000 | 0.000 |
| Ascii | 128 | 130.6 | 21.3 | 6.13x | 0.000 | 0.000 |
| Ascii | 4096 | 3418.3 | 275.1 | 12.43x | 0.000 | 0.000 |
| Chinese | 8 | 83.4 | 22.4 | 3.73x | 0.000 | 0.000 |
| Chinese | 16 | 161.2 | 28.1 | 5.74x | 0.000 | 0.000 |
| Chinese | 32 | 307.0 | 47.9 | 6.41x | 0.000 | 0.000 |
| Chinese | 128 | 1185.5 | 128.9 | 9.20x | 0.000 | 0.000 |
| Chinese | 4096 | 37052.7 | 3345.8 | 11.07x | 0.000 | 0.000 |
| Mixed | 8 | 14.8 | 9.9 | 1.49x | 0.000 | 0.000 |
| Mixed | 16 | 81.0 | 24.8 | 3.26x | 0.000 | 0.000 |
| Mixed | 32 | 139.5 | 27.0 | 5.17x | 0.000 | 0.000 |
| Mixed | 128 | 512.5 | 60.9 | 8.42x | 0.000 | 0.000 |
| Mixed | 4096 | 43770.8 | 4668.3 | 9.38x | 0.000 | 0.000 |
| Emoji | 8 | 31.7 | 9.7 | 3.26x | 0.000 | 0.000 |
| Emoji | 16 | 120.6 | 60.1 | 2.01x | 0.000 | 0.000 |
| Emoji | 32 | 306.6 | 55.2 | 5.55x | 0.000 | 0.000 |
| Emoji | 128 | 694.2 | 94.7 | 7.33x | 0.000 | 0.000 |
| Emoji | 4096 | 21687.3 | 2736.4 | 7.93x | 0.000 | 0.000 |
| SparseEscape | 8 | 85.6 | 22.6 | 3.80x | 0.000 | 0.000 |
| SparseEscape | 16 | 155.1 | 28.6 | 5.43x | 0.000 | 0.000 |
| SparseEscape | 32 | 293.8 | 46.2 | 6.36x | 0.000 | 0.000 |
| SparseEscape | 128 | 1176.4 | 155.8 | 7.55x | 0.000 | 0.000 |
| SparseEscape | 4096 | 36040.6 | 4449.5 | 8.10x | 0.000 | 0.000 |
| DenseEscape | 8 | 49.1 | 49.4 | 0.99x | 0.000 | 0.000 |
| DenseEscape | 16 | 86.2 | 86.9 | 0.99x | 0.000 | 0.000 |
| DenseEscape | 32 | 171.7 | 181.2 | 0.95x | 0.000 | 0.000 |
| DenseEscape | 128 | 649.5 | 663.3 | 0.98x | 0.000 | 0.000 |
| DenseEscape | 4096 | 36299.0 | 45396.8 | 0.80x | 0.000 | 0.000 |
