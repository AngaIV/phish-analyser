# Rule: identity-mismatch

**Rule id:** `identity-mismatch`
**Version:** 1.1
**Status:** implemented, FP fix applied and re-measured on the development split

## What it detects

Differences between the **registrable domains** of the `From`, `Return-Path` and
`Reply-To` addresses.

## Why it matters

`From` is what the user sees. `Return-Path` is where bounces go, and `Reply-To` is
where replies go. Attackers routinely set a trusted `From` while pointing replies
at a domain they control, so the victim's answer — often containing credentials or
payment details — never reaches the impersonated brand.

## Severity and weight

| Comparison | Status | Severity | Weight | Rationale |
|---|---|---|---|---|
| Reply-To domain differs from From | Flagged | Medium | 2 | Replies are redirected away from the apparent sender |
| Return-Path domain differs from From | Flagged | Low | 1 | Common for bulk mail via an ESP; weak on its own |
| From missing or unreadable | Unknown | Info | 0 | Unknown, never a pass |
| Return-Path missing | Unknown | Info | 0 | The receiving server adds it; absence means incomplete headers |
| Reply-To missing | Pass | Info | 0 | Reply-To is genuinely optional and absent on most real mail |
| Mismatch on list/bulk mail | Pass | Info | 0 | See v1.1 fix below |

Registrable domain is used, not the full host: `mail.example.com` and `example.com`
are the same domain. Two-letter TLDs with a second-level label (`co.za`, `co.uk`,
`com.au`) are handled by a minimal second-level-label set, not a full public suffix
list — a deliberate simplification, revisited if FP data demands it.

**Missing `Reply-To` is a Pass, not Unknown.** It is truly optional; treating it as
Unknown would flood every inbox with unknowns for no analytical gain.
**Missing `Return-Path` is Unknown**, because its absence signals incomplete headers
rather than a passed check. This is the fail-safe rule applied with judgement rather
than mechanically.

## Explanation text (non-technical)

- Reply-To: "Replies to this message would go to a different company than the one that appears to have sent it."
- Return-Path: "The address that would receive bounces for this message is on a different domain than the sender."
- Suppressed: "The bounce address is on a different domain, which is normal for mailing-list and bulk mail."

## Known false-positive sources

- **Mailing lists**: set `Reply-To` to the list address, which legitimately differs
  from the author's `From`.
- **Bulk senders / ESPs**: bounce domains (SendGrid, Mailchimp, Amazon SES) differ
  from the visible From domain.
- **Forwarded mail**: the forwarder's `Return-Path` replaces the original.
- **Sub-domains and brand domains**: mail sent from `mail.brand.com` legitimately differs.

## v1.1 fix: suppress mismatches on list and bulk mail

Mailing lists and bulk senders legitimately use third-party bounce and reply
domains, so a mismatch on such a message is expected rather than suspicious. The
rule now treats a mismatch as a **Pass** when the message carries any of:

- `List-Id`
- `List-Unsubscribe`
- `List-Post`
- `Precedence: bulk` or `Precedence: list`

Implemented in `Engine/Parsing/MessageContext.IsBulkOrList`. The suppression is
conditional and evidence-based — a mismatch on a message *without* list headers is
still flagged, and a test pins that behaviour so the guard cannot be lost.

## Measured precision and recall (development split, 35 phishing / 35 legitimate)

| Version | Flags | True positives | False positives | Precision | Recall |
|---|---|---|---|---|---|
| 1.0 | 30 | 8 | 22 | 0.27 | 0.23 |
| 1.1 | 5 | 4 | 1 | **0.80** | 0.11 |

**v1.0 investigation.** 21 of the 22 false positives were mailing-list or bulk mail,
18 of them Return-Path-only. That matches the predicted FP source exactly and was
the trigger for the v1.1 suppression rule.

**The cost of the fix is recall.** True positives fell from 8 to 4, because 12 of the
35 development phishing samples carry list or bulk headers — several real phishing
campaigns are sent through bulk-mail infrastructure and imitate legitimate list mail.
This trade was taken deliberately: the Phase 2 success criterion is a false-positive
rate on legitimate mail under threshold, and a rule that flags a fifth of all
legitimate mail is unusable regardless of what it catches.

**Residual false positive (documented, not fixed).** `ham_007_world_wide_words.eml` —
a genuine newsletter carrying no list headers at all, so the suppression cannot see
it. Fixing this would require content or sender-reputation signals that are out of
scope for a header-only analyser.

## Test cases

`Engine.Tests/Rules/IdentityMismatchRuleTests.cs` — 12 tests: aligned domains,
Reply-To mismatch, Return-Path mismatch, subdomain equivalence, two-letter TLD
handling (including the `nedbank.co.za` vs `nedbank.co.za.evil.com` trap), missing
Reply-To, missing Return-Path, missing From, and four suppression tests including
the guard that a mismatch without list headers is still flagged.

## Change log

- 1.0 — initial implementation.
- 1.1 — suppress Return-Path and Reply-To mismatches on list/bulk mail; precision 0.27 → 0.80.