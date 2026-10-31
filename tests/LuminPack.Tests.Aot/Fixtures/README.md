# Fixed wire fixtures

These binary records are a checked-in reference for the wire harness. Run its `verify` command against this directory after building the solution. Verification reads every record, writes it again and checks byte equality. CI must not overwrite this baseline before verification.

The fixture models and construction are defined in the parent project's `WireFixtures.cs`. Keep layout-dependent records limited to the harness's explicit stable values. A deliberate protocol change requires separate review before regenerating files; an ordinary round trip is not evidence of compatibility with persisted data.

`union-variable-utf16.bin` and `union-variable-utf8.bin` cover the mixed-size ATPD policy, including raw UTF-16 strings, raw numeric arrays and nullable fixed-object lists. The UTF-16 record uses even raw string headers on every target, including targets that retain ordinary Union dispatch. These fixtures supplement the established records; they do not replace them.
