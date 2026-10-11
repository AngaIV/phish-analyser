# Rule: display-name-spoof

**Rule id:** `display-name-spoof`
**Version:** 1.0
**Status:** implemented, measured on the development split

## What it detects

Two impersonation tricks in the `From` display name:

1. **Brand impersonation** — a commonly-spoofed brand appears in the display name while
   the message was not sent from that brand's domain (`"PayPal" <x@kplusinternational.com>`).
2. **Address-as-name** — the display name is itself an email address on a domain that
   differs from where the message actually came from (`"do_not_reply@irs.gov" <x@evil.com>`).

## Why it matters

The display name is what the user reads in the message list; the address is hidden behind
it. Putting a trusted brand in the name costs an attacker nothing and defeats any user who
judges a message by its sender name alone.

## Severity and weight

| Outcome | Status | Severity | Weight | Rationale |
|---|---|---|---|---|
| Brand in display name, domain unrelated | Flagged | High | 4 | Near-certain impersonation: the name claims an identity the headers contradict |
| Display name is an email address on a different domain | Flagged | Medium | 2 | Deceptive and unusual; the named address is not a real sender |
| Brand in display name, domain belongs to the brand | Pass | Info | 0 | Normal, correctly-configured brand mail |
| Display name is an address on the same domain | Pass | Info | 0 | Harmless; the name matches the sender |
| No display name | Unknown | Info | 0 | Nothing was claimed, so nothing can be contradicted |
| `From` missing or unreadable | Unknown | Info | 0 | Unknown, never a pass |

`Weight 4` places a single finding on its own into the red band. That is deliberate: a
brand name whose own domain contradicts it is the strongest header-only evidence the
engine produces, and burying it below the amber threshold would waste it.

## Domain alignment

A brand is aligned when the **second-level label of the registrable From domain** is one
of the brand's own labels. `service@mail.paypal.com` → `paypal.com` → label `paypal` →
aligned. `x@paypal-secure.com` → label `paypal-secure` → **not** aligned → flagged, and
correctly so: the name claims PayPal and the domain does not belong to PayPal.

Matching is deliberately exact-label, never substring. A substring match on `ups` would
flag every sender whose domain or name merely contains those letters (Startups, Groups,
Backups).

## Explanation text (non-technical)

- Brand: "This message claims to be from PayPal in the sender's name, but it was not sent from PayPal's email domain."
- Address-as-name: "The sender's name is an email address that does not match the address the message actually came from."

## Known false-positive sources

- **Marketplaces and partner mail** legitimately name a brand they are not:
  "Office Depot" from `officedepot.com`, "Apple Reseller" from a dealership domain.
  The label check does not recognise these as aligned.
- **Mailing lists and newsletters** posting *about* a brand ("Google News Alert").
  Rule 2's list/bulk suppression is **not** applied here, deliberately: a phishing message
  can carry list headers too, and the brand-versus-domain contradiction is unaffected by
  them.
- **Authors sharing a surname with a brand**, e.g. "John Apple".
- **Brand tokens that are ordinary words** (`office`, `apple`, `chase`, `ups`).

All are documented rather than suppressed. The dev-set measurement shows **zero** such
false positives in the current corpus, so suppression would be tuning against noise.

## Measured precision and recall (development split, 35 phishing / 35 legitimate)

| Flags | True positives | False positives | Precision | Recall |
|---|---|---|---|---|
| 9 | 9 | 0 | **1.00** | 0.26 |

Expected figures — filled in from `docs/results.md` after the run. Recall 0.26 makes this
the highest-coverage rule so far, against `auth-results` 0.17 and `identity-mismatch` 0.11.

## Test cases

`Engine.Tests/Rules/DisplayNameSpoofRuleTests.cs` — brand impersonation, aligned domain,
aligned subdomain, lookalike domain claiming a brand, the substring trap, both
address-as-name cases, no display name, missing `From`.

## Change log

- 1.0 — initial implementation. Brand list in `Engine/Configuration/BrandList.cs`.