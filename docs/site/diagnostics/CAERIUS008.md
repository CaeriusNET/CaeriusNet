# CAERIUS008 — DTO source emission failed

**Severity**: Error
**Applies to**: `[GenerateDto]`

This is the DTO generator's final containment boundary. It stops source emission before an unforeseen generator defect
can surface as `CS8785`, so no partial mapper is produced. Correct any preceding diagnostic first. If the input is valid
and the error persists, report a CaeriusNet bug with a minimal declaration and the SDK/Roslyn versions.
