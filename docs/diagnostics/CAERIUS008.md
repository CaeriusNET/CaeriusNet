# CAERIUS008 — DTO source emission failed

**Severity**: Error
**Category**: CaeriusNet.Analyzer

## Cause

CaeriusNet stopped DTO emission at its final internal containment boundary. This protects the compilation from a
generator exception (`CS8785`) and emits no partial mapper.

## How to fix

First correct any preceding DTO or mapping diagnostic. If the input is valid and `CAERIUS008` remains, report it as a
CaeriusNet bug with the smallest reproducing declaration, the SDK/Roslyn versions and the diagnostic text. Do not work
around it by accepting generated partial output: there is none by design.
