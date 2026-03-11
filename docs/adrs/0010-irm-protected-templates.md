---
agent-notes: { ctx: "ADR for handling IRM/DRM protected DOCX templates", deps: [docs/architecture.md, docs/adrs/0004-open-xml-sdk.md], state: active, last: "archie@2026-03-11" }
---

# ADR-0010: Fail-Fast with Guidance for IRM-Protected Templates

## Status

Proposed

## Context

Enterprise users may provide DOCX templates that are protected by IRM (Information Rights Management) or DRM (Digital Rights Management). IRM-protected DOCX files are encrypted at the file level -- the underlying ZIP structure is replaced with an OLE compound document containing encrypted streams. The Open XML SDK cannot open these files. There is no way to extract styles without first decrypting the file, which requires authentication against an Active Directory or Azure RMS server.

md2's `theme extract` command and `--template` flag must handle this gracefully.

**Options evaluated:**

1. **Fail fast with clear guidance.** Detect IRM protection early (file header check), print a specific error with steps to resolve, and exit with a distinct exit code.

2. **Attempt partial extraction.** Try to open the file, catch the exception, and fall back to default styles with a warning. This produces output but with no template styling applied.

3. **Integrate RMS decryption.** Use Windows-specific RMS APIs to decrypt the file with the user's credentials. This would work on domain-joined Windows machines but not on Linux, and adds significant complexity and platform-specific code.

4. **Prompt for credentials.** Ask the user for RMS server credentials and attempt decryption. Enormous security surface (credential handling, network calls) for a niche feature.

## Decision

**Fail fast with clear guidance** (option 1).

**Detection mechanism:** Before attempting to open a DOCX file with Open XML SDK, check the first 4 bytes of the file:
- ZIP signature (`50 4B 03 04`): Normal DOCX. Proceed.
- OLE compound document signature (`D0 CF 11 E0`): Likely IRM-protected (or a legacy .doc file). Report error.
- Neither: Not a valid DOCX. Report error.

**Error message (theme extract):**
```
Error: Cannot extract theme from 'corp-template.docx' -- file is IRM/DRM protected.

IRM-protected documents are encrypted and cannot be read without authentication
against your organization's rights management server.

To use this template with md2:
  1. Open the file in Microsoft Word (with appropriate permissions)
  2. File > Info > Protect Document > Restrict Access > Unrestricted Access
  3. Save the unprotected copy
  4. Run: md2 theme extract <unprotected-copy.docx> -o theme.yaml

Alternatively, use a built-in preset: md2 report.md --preset corporate -o report.docx
```

**Error message (--template flag):**
```
Error: Cannot use 'corp-template.docx' as a template -- file is IRM/DRM protected.

[same guidance as above]
```

**Exit codes:**
- 0: Success
- 1: General error
- 2: Protected/encrypted file
- 3: Invalid input file

**Additional safety for template files:**
- `.docm` files (macro-enabled): Warn and refuse by default. Macros are irrelevant to style extraction and could indicate a malicious file. A `--allow-macros` flag can override.
- Files larger than 50MB: Warn before proceeding. Style extraction should not require processing large documents.
- Malformed XML in DOCX: Catch `OpenXmlPackageException` and report which part of the document is corrupted.

## Consequences

### Positive

- Clear, actionable error messages. Users know exactly what to do.
- Distinct exit codes enable scripting (e.g., `if md2 exits with 2, prompt user to unprotect the file`).
- No silent degradation. Users never get output that looks wrong because the template was silently ignored.
- No security surface from credential handling or RMS integration.
- Cross-platform: the detection works on both Windows and Linux.

### Negative

- **No automatic handling.** Users with IRM-protected templates must manually unprotect them first. This is a friction point for enterprise users who receive protected templates from their organization.
- **OLE compound document check is imprecise.** Legacy `.doc` files (pre-OOXML) also use OLE compound document format. We may need a secondary check to distinguish IRM-protected DOCX from plain `.doc` files. Mitigation: check the file extension as well, and include `.doc` guidance in the error message ("If this is a legacy .doc file, save it as .docx in Word first").

### Neutral

- This decision does not preclude adding RMS integration in the future if enterprise demand justifies it. The fail-fast path remains the default; RMS integration would be an opt-in advanced feature behind a flag.
