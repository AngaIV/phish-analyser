# Threat Model

## 1. What the app handles
Raw email headers from Outlook and webmail. Headers contain Received chains, From/Reply-To/Return-Path, Authentication-Results, Message-ID, and (in some cases) the envelope IP addresses. Later: URLs from the body for reputation checks. The app needs read access to the mail client; OAuth scopes are kept to the minimum needed to read headers (Gmail: gmail.metadata, not gmail.readonly; Outlook: read item).

## 2. Data flow table
| Data | Source | Processed | Stored | Leaves device |
|---|---|---|---|---|
| Raw headers | Outlook/API | Locally | No (memory only) | No |
| Connection IP | Parsed Received | Locally | No (memory only) | No |
| Sender domain | Parsed headers | Locally | Cache only | Only for opt-in reputation lookup |
| OAuth token | Client | Locally | OS credential store | No |

## 3. Assets worth protecting
- Mail content (the reason users trust us)
- OAuth tokens (access to mailboxes)
- Rule files (a tampered rule = a whitelist for phishers)
- The update channel (a poisoned update = malware on every install)

## 4. Threats (STRIDE)
- Spoofing: fake update server delivers a malicious build.
- Tampering: attacker edits the local rules file to whitelist their domain.
- Information disclosure: headers or tokens leak into logs/crash reports.
- DoS: an email with enormous or malformed headers freezes the parser.
- Elevation of privilege: the add-in runs with more permission than needed.
- Repudiation: less relevant — no security-relevant audit trail is claimed. Noted so the reader knows it was considered.

## 5. Mitigations
- Signed binaries and signed rule updates (Phase 6).
- Input size limits, fuzzing, safe parser wrapper (never throw to UI).
- No content logging; crash reports contain no headers.
- Tokens in Windows Credential Manager; least-privilege scopes.
- Rules loaded from a trusted, signed source; local overrides are user-visible.

## 6. Out of scope and known limitations
- Cannot reliably flag phishing sent from a compromised legitimate account (passes auth).
- Saved .eml files may lack Authentication-Results — shown as Unknown, never false-pass (confirmed by Day 5 measurement).
- First-match-wins on multiple Authentication-Results headers — authserv-id trust check deferred to Phase 2 Rule 8.