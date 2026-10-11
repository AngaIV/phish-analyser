# Rule: lookalike-domain

**Rule id:** `lookalike-domain`
**Version:** 1.1
**Status:** implemented, measured on the development split

## What it detects

Three signals that a sender's domain is masquerading as something it is not:

1. **Brand plus lure word** — a brand name and a social-engineering word combined in the
   host, where the domain is not the brand's own (`paypal-secure.com`, `apple-id-verify.net`).
2. **Cheap TLD** — the free or heavily-abused TLDs associated with disposable infrastructure
   (`em5269.xfund02.ml`, `tontransport.ga`).
3. **IP-derived or digit-heavy host label** — a hostname built from an IP address or a long
   numeric run (`v133-18-229-1.vir.kagoya.net`).

## Why it matters

A sender cannot fake a domain, so attackers register one that *resembles* the target. The
user sees the display name (rule 3) and the domain is the evidence beneath it. A domain
that combines a brand with a pressure word has no benign purpose.

## Severity and weight

| Signal | Status | Severity | Weight | Rationale |
|---|---|---|---|---|
| Brand + lure word in host | Flagged | High | 3 | Near-certain intent to deceive; no legitimate use |
| Cheap TLD | Flagged | Medium | 2 | Persistent across independent signals — see v1.1 |
| IP-derived / digit-heavy label | Flagged | Medium | 2 | Persistent across independent signals — see v1.1 |
| No signal | Pass | Info | 0 | Nothing anomalous found |
| `From` missing or unreadable | Unknown | Info | 0 | Unknown, never a pass |

Only the **strongest** signal produces a finding, with every triggered signal named in the
explanation. Three variants of "the domain looks odd" are one suspicious thing, not three —
and the bubble number is a count, so emitting one finding per signal would inflate it.

## Full host versus registrable domain

The rule inspects the **full host** as well as the registrable domain. `kagoya.net` is the
registrable domain of `v133-18-229-1.vir.kagoya.net`; the anomaly is in the left-hand label,
which a registrable-domain-only check would never see.

A brand is **aligned** when the registrable domain's second-level label belongs to it
(`support.apple.com` → `apple.com` → aligned → Pass). This guard prevents flagging the
brands' own subdomains, which is where a rule like this would otherwise do the most damage.

## Deliberately NOT implemented (evidence in decision D-010)

- **Edit-distance / typosquat matching.** At Levenshtein ≤2 it matched `aaps.k12.mi.us` to
  `ups` and `hw.ac.uk` to `dhl`, and false-positived `ucd.ie` and `users.sourceforge.net`.
  Restricting to brands ≥6 characters at distance 1 removed the false positives *and every
  true positive* — zero catches. `paypa1.com` and `rnicrosoft.com` do not occur in this
  corpus.
- **Homoglyph / mixed-script detection.** IDN domains reach the engine in punycode
  (`xn--`), so a non-ASCII check rarely fires. **Known limitation: the punycode lookalike
  `xn--pypal-4ve.com` is not detected.** Full support means punycode decoding, deferred.
- **Bare hyphen heuristics.** Legitimate domains use hyphens constantly. Only a hyphen
  joining a brand to a lure word counts, via signal 1.
- **Brands shorter than 4 characters** are excluded from signal 1, because `ups`, `dhl` and
  `fnb` match arbitrary words as substrings. `ups-secure.com` is therefore **not** caught.

## Explanation text (non-technical)

- Brand + lure: "The sender's web address combines the name PayPal with a word designed to pressure you. It is not PayPal's real address."
- Cheap TLD: "The sender's web address uses an ending (.ga, .ml) often used for disposable and fraudulent sites."
- Digit-heavy: "The sender's web address looks like it was generated automatically rather than registered by a company."

## Known false-positive sources

- **Third-party support and reseller sites** named after the brand they serve
  (`paypal-support-center.com`), which are not necessarily malicious.
- **Legitimate mail from cheap TLDs** — the TLD is abused, not hostile in itself. This is
  why signal 2 originally carried weight 1; v1.1 raises it to 2, so a cheap TLD alone now
  turns the bubble amber. Recorded here as a knowingly accepted false-positive risk.
- **Marketing domains** with brand-like tokens and words such as `support` or `service`.

**On the v1.1 weight increase.** Signals 2 and 3 previously carried weight 1, on the
reasoning that a single low-weight finding should not alarm the user. Measurement showed
the cost of that restraint: messages carrying *only* these signals scored 1, stayed green,
and were shown a non-alarming state despite a real anomaly. The bubble must not present a
flagged message as safe, so the weight rose to 2.

**Why weight 2 and not 3:** a weight-2 finding reaches amber on its own, which is the
desired outcome; weight 3 would produce the same colour while consuming more of the
false-positive budget whenever it combines with another finding.

## Measured precision and recall (development split, 35 phishing / 35 legitimate)

Expected: **3 flags, 3 TP, 0 FP — precision 1.00, recall 0.09.** Confirmed from
`docs/results.md` after the run.

**Read the recall honestly.** All three catches come from signals 2 and 3. Signal 1 —
the high-confidence brand-plus-lure check — catches **zero development samples** because
this 2002–2023 corpus predates that pattern. It is retained because it targets the dominant
modern phishing technique and is unit-tested from hand-made snippets, but it is
**corpus-unvalidated**: its real-world false-positive rate is unmeasured. Stated here so the
measured 1.00 precision is not mistaken for evidence about signal 1.

## Change log

- 1.0 — initial implementation, scoped per D-010.
- 1.1 — weight of signals 2 and 3 raised from 1 to 2, on measurement.

## v1.1 — weight raised from 1 to 2, on measurement

Signals 2 and 3 originally carried weight 1. Measured on the dev split, messages carrying
only these signals scored 1 and therefore left the bubble green, so real phishing was
shown a non-alarming state. Raising the weight to 2 turns those bubbles amber.

Effect, simulated against actual findings before the change and confirmed by
re-measurement afterwards:

| Metric | Before | After |
|---|---|---|
| U1 — phishing amber or red | 15/35 = 43% | **18/35 = 51%** |
| False green (no `Unknown`) | 7/35 = 20% | 6/35 = 17% |
| P3 — legitimate amber or red | 0/35 = 0% | **0/35 = 0%** |

U1's 50% floor is cleared, and **P3 is unaffected** — no legitimate sample is pushed to
amber or red. That combination is why this change was taken.

**Why this rather than raising `identity-mismatch`:** the equivalent change there was
simulated first and rejected. It reaches only 16/35 = 46% — still below U1's floor — *and*
pushes `ham_007_world_wide_words.eml`, the corpus's only legitimate flag, from green to
amber, costing 3% on P3 for no criterion gain.

**Method note:** the change was simulated in Python against the actual per-sample findings
before touching the rule. The first recommendation (raise the Return-Path weight) was
produced by arithmetic over aggregate counts and was **wrong in both directions** — it
neither cleared U1 nor avoided the false positive. Simulating against real findings caught
it at the cost of one script.