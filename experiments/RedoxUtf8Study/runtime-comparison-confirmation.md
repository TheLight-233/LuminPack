Correctness: 2012 edge/random cases passed (byte equality, JSON roundtrip, canary, exact-size destination).

.NET 10.0.9; X64; CPU count 20
DOTNET_TieredCompilation=0
Preallocated buffers, reused optional state, 7 alternating rounds; median ns/op.

| Data | UTF16 units | Baseline ns | Runtime ns | Speedup | Baseline B/op | Runtime B/op |
|---|---:|---:|---:|---:|---:|---:|
| Ascii | 8 | 28.2 | 23.2 | 1.21x | 0.000 | 0.000 |
| Ascii | 16 | 22.4 | 15.8 | 1.41x | 0.000 | 0.000 |
| Ascii | 32 | 68.3 | 36.6 | 1.87x | 0.000 | 0.000 |
| Ascii | 128 | 127.6 | 20.6 | 6.18x | 0.000 | 0.000 |
| Ascii | 4096 | 3560.5 | 283.4 | 12.57x | 0.000 | 0.000 |
| Chinese | 8 | 82.6 | 23.5 | 3.51x | 0.000 | 0.000 |
| Chinese | 16 | 161.3 | 27.9 | 5.79x | 0.000 | 0.000 |
| Chinese | 32 | 301.4 | 45.5 | 6.63x | 0.000 | 0.000 |
| Chinese | 128 | 1173.8 | 127.2 | 9.23x | 0.000 | 0.000 |
| Chinese | 4096 | 36642.6 | 3315.2 | 11.05x | 0.000 | 0.000 |
| Mixed | 8 | 14.7 | 9.6 | 1.53x | 0.000 | 0.000 |
| Mixed | 16 | 80.3 | 25.4 | 3.16x | 0.000 | 0.000 |
| Mixed | 32 | 138.3 | 28.5 | 4.85x | 0.000 | 0.000 |
| Mixed | 128 | 505.5 | 57.3 | 8.82x | 0.000 | 0.000 |
| Mixed | 4096 | 15267.4 | 1469.2 | 10.39x | 0.000 | 0.000 |
| Emoji | 8 | 14.1 | 9.8 | 1.44x | 0.000 | 0.000 |
| Emoji | 16 | 94.5 | 45.1 | 2.10x | 0.000 | 0.000 |
| Emoji | 32 | 312.9 | 55.4 | 5.65x | 0.000 | 0.000 |
| Emoji | 128 | 1312.2 | 208.3 | 6.30x | 0.000 | 0.000 |
| Emoji | 4096 | 41449.6 | 2747.0 | 15.09x | 0.000 | 0.000 |
| SparseEscape | 8 | 77.5 | 21.7 | 3.57x | 0.000 | 0.000 |
| SparseEscape | 16 | 157.6 | 27.3 | 5.77x | 0.000 | 0.000 |
| SparseEscape | 32 | 301.2 | 45.8 | 6.58x | 0.000 | 0.000 |
| SparseEscape | 128 | 1179.0 | 197.7 | 5.96x | 0.000 | 0.000 |
| SparseEscape | 4096 | 36778.9 | 4462.0 | 8.24x | 0.000 | 0.000 |
| DenseEscape | 8 | 50.1 | 50.3 | 1.00x | 0.000 | 0.000 |
| DenseEscape | 16 | 92.4 | 90.8 | 1.02x | 0.000 | 0.000 |
| DenseEscape | 32 | 177.5 | 186.8 | 0.95x | 0.000 | 0.000 |
| DenseEscape | 128 | 669.5 | 686.0 | 0.98x | 0.000 | 0.000 |
| DenseEscape | 4096 | 21105.6 | 20915.9 | 1.01x | 0.000 | 0.000 |
