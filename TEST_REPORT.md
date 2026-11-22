# Verification checkpoint — Aethereal Voice Access Lab v0.4.0-rc1

Date: 2026-11-21
Collaborator: Aethereal CTO assistant
Environment: Angelland (authorized Windows workstation)
Source: Separate derivative of Aethereal's privately archived AJ Access Lab v0.3.1. Original source and archive were not modified.

## Verified modifications
- General product name: Aethereal Voice Access Lab.
- Replaced historical wake phrase with "Access" throughout source, parser, grammar, demos and policy tests.
- Updated the Notepad parser's prefix-length handling, not just visible text.
- Removed the developer GPT handoff button, form, embedded documentation, smoke-test path and build packaging requirement.
- Rebuilt from source, no third-party binaries or customer handoffs copied.
- No customer source code was used for this independent application.

## Checks performed
- Windows x64 C# compilation: PASS (exit code 0).
- Practice action policy: PASS 25, FAIL 0.
- Notepad action policy: PASS 28, FAIL 0.
- Practice UI smoke: PASS.
- Notepad UI smoke: PASS.
- Unified two-tab UI smoke: PASS.
- Forbidden customer/person/project-name and GPT/handoff identifier scan: No application source matches.
- No external Notepad launch, microphone start or OS input during automated tests.

Executable (local only) SHA256:
E5B765CB59B8583CBC2EA0417E45E65A4E66F86C3FDEE63A5DA64CCDA41B6BEB

## Not attempted or pending
- Human voice-recognition session for newly renamed wake phrase.
- Human Notepad navigation and emergency-stop retest on v0.4.0-rc1.
- Accessibility evaluation by end users.
- Review and selection of a public redistribution/source license.
- Public repository creation and publication.
- Human release acceptance.

## Publication boundary
The public-source candidate consists only of eight C# files, build/launcher scripts,
README, this checkpoint and .gitignore. It excludes the internal historical
handoff, prior archived ZIP, previous executables, and customer-specific material.
No published repo should be called a verified production application before human acceptance.
