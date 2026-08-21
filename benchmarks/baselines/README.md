# Measured benchmark baselines

`11.2.0/` is intentionally absent until the manual GitHub **CaeriusNet benchmarks** workflow records a complete,
successful `all` suite on `ubuntu-24.04`. Do not add hand-written JSON, a workstation measurement, or a placeholder file
under that version directory.

The reviewed workflow artifact must contain every BenchmarkDotNet `*-report-full*.json` export and its sibling
`caerius-benchmark-environment.v1.json`. The manifest binds each report hash and measurement count to the runner, CPU,
SDK/runtime, BenchmarkDotNet version, and job signature that created it. Commit the artifact contents unchanged as
`benchmarks/baselines/11.2.0/`; the performance gate rejects an incomplete or non-comparable baseline.
