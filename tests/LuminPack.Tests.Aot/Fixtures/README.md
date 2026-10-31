# Fixed wire fixtures

These binary records are a checked-in reference for the wire harness. Run its `verify` command against this directory after building the solution. Verification reads every record, writes it again and checks byte equality. CI must not overwrite this baseline before verification.

The fixture models and construction are defined in the parent project's `Program.cs`. Keep layout-dependent records limited to the harness's explicit stable values. A deliberate protocol change requires separate review before regenerating files; an ordinary round trip is not evidence of compatibility with persisted data.
