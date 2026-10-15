# Journal

Weekly review, per the build plan's §5.5 template. One entry per week.
Hours are recorded by the author.

---

## Week of 2026-10-11 — Phase 1 closed, Phase 2 started

### 1. Hours spent this week

_To be filled in by the author._

### 2. What I finished

**Phase 1 (corpus and parser) — exit gate met.**

- Corpus grown from 30 to **100 labelled samples** (50 phishing / 50 legitimate), all
  headers-only with zero body leaks. Sources: Nazario phishing-2023 (CC-BY-4.0) and
  three SpamAssassin archives (`easy_ham`, `easy_ham_2`, `hard_ham`), which is what
  supplies the hard legitimate cases — mailing lists, `Precedence: bulk`, newsletters,
  thread replies and forwards. Those are the false-positive canaries.
- Reproducible **70/30 development/holdout split** (deterministic seed `20261011`),
  with the holdout enforced as untouched by a test.
- Deterministic tooling: `grow_corpus.py`, `rename_corpus.py`, `split_corpus.py`,
  `make_malformed.py`, `evaluate.py`. All 70 auto-named files renamed to
  domain-derived names.
- `SafeMessageLoader` — parse wrapper guaranteeing no exception escapes, a size
  ceiling enforced *before* parsing, and anomalies surfaced as warnings rather than
  thrown. Plus `ParseResult` so failure is a value.
- Parser robustness: 8 malformed fixtures (truncated, duplicate `From`, empty, missing
  colons, binary junk, null bytes, deeply folded, 10 MB header block), a 10 MB
  within-ceiling test, and an oversize-rejection test.
- Python corpus invariants: 6 passing. C#: 33 passing.

**Phase 2 (rules and measurement) — started.**

- Rule 1 `auth-results` (SPF/DKIM/DMARC) implemented, spec'd, 7 unit tests, corpus test.
- Rule 2 `identity-mismatch` implemented, spec'd, 12 tests, **measured and then fixed**
  after its first measurement exposed a serious false-positive problem.
- `evaluate.py` harness producing per-rule precision and recall on the dev split.
- `docs/results.md` results table established.

### 3. What broke or surprised me

- **A stale build produced a wrong measurement.** After applying the rule 2 fix,
  `evaluate.py` reported an identical table — the fix appeared to do nothing. The CLI's
  `Engine.dll` was 13 minutes older than the engine's: `evaluate.py` uses `--no-build`,
  and `dotnet test` does not rebuild the `Cli` project, so the harness was scoring the
  old engine. Rebuilding revealed the real figures (precision 0.27 → 0.80). **Lesson:
  a surprising "no change" after a targeted fix is more likely a stale artefact than a
  genuinely ineffective fix.** Logged as D-007.
- **The predicted rule 2 outcome was worse than the spec anticipated.** The spec listed
  mailing lists as "the single largest FP risk"; the measurement showed 21 of 22 false
  positives were list or bulk mail. Worth noting that writing the FP sources down *in
  advance* made the diagnosis immediate — the measurement matched the prediction, so
  no time was lost deciding whether the corpus was at fault.
- **Fixing precision cost half the recall** (TP 8 → 4). This was not obvious until
  measured, and it is the first hard evidence that header-only identity checks have a
  ceiling.
- **`auth-results` is nearly useless alone**: precision 1.00 but recall 0.17, with 65%
  of mechanism-checks across the corpus returning `Unknown`. Modern phishing passes
  authentication. This reframes the whole project — the lookalike and display-name
  rules are the core signal, not supporting cast.
- **A typo in `labels.csv`** (`phish_015_ups_delivery.phish.eml`) was caught by the
  corpus invariant test on its first run. The invariant test earned its place
  immediately.
- Template leftovers (`Class1.cs`, `UnitTest1.cs`) survived until Phase 1 close.

### 4. Decisions made (and logged)

D-001 web-first header access; D-002 bubble overlay rather than browser extension in
New Outlook; D-003 headers-only sanitisation; D-004 `Unknown` never `Pass`; D-005
weights hardcoded for now; D-006 list/bulk suppression in rule 2; D-007 rebuild before
measuring; D-008 fuzzing deferred to the parser freeze. Full reasoning in
`docs/decisions.md`.

### 5. Risks that changed

- **Increased:** the ceiling on header-only detection. Relying on authentication
  results was the plan's first rule and it covers 17% of phishing. If rules 3–6 do not
  recover coverage, the success criteria may need renegotiating with the stakeholder.
- **Increased:** false-positive pressure from bulk mail. Any future rule touching
  sender identity or reply paths must handle list and bulk mail explicitly, or it will
  repeat rule 2's 0.27 precision.
- **Unchanged:** the authserv-id trust gap (rule 8) — an attacker can inject a forged
  `Authentication-Results: ... pass` header that the current rule would trust.
- **Unchanged:** POPIA/ethics compliance remains a gap in the plan's privacy section;
  flagged for the I&TS stakeholder conversation.
- **Reduced:** parser robustness risk. 33 tests including a 10 MB header block and
  eight malformed fixtures, with no exception escaping the loader.

### 6. Plan for next week (three items maximum)

1. Rule 3 — display-name spoofing (address or brand name in the display name).
2. Rule 4 — `Message-ID` domain and DKIM signing-domain alignment with `From`.
3. Rule 5 — lookalike domains (homoglyphs, mixed scripts, edit distance, suspicious
   TLDs, hyphen tricks), since the measurements say this is where detection must come
   from.

### 7. Am I inside the phase time-box?

Phase 1 completed with its exit gate met on all five criteria; task 8 (light fuzzing)
deferred by decision (D-008), not by overrun. Phase 2 is on schedule but its exit gate
is heavier — 8–10 rules each with a measured precision figure, and an overall
false-positive rate under threshold — so the cut, if needed, is **rule count**: ship
6 well-measured rules rather than 10 unmeasured ones.

---

## Phase 2 close — rules, scoring and the holdout run

### 2. What I finished

- **Four detection rules**, each spec'd, unit-tested and measured: `auth-results`,
  `identity-mismatch` (v1.1 after the list/bulk fix), `display-name-spoof`,
  `lookalike-domain` (v1.1 after the weight change).
- **Scoring module** — bubble number (count of flagged findings) and colour from the
  weighted score (green / amber / red / grey). Fail-safe behaviour pinned by regression
  test. 66 tests passing.
- **`evaluate.py`** reporting per-rule precision and recall plus the combined exit gate.
- **Holdout run — completed once**, 15 phishing / 15 legitimate, after all tuning.

### 3. What broke or surprised me

- **The fail-safe had a hole, and the first measurement hid it inside a bigger number.**
  False green measured 11/35. That figure conflated two unrelated problems: **4 samples
  showed green *despite* an explicit `Unknown` finding** (a real bug — the scorer's
  weight-1 branch bypassed the unknown check), and **7 were honest misses** with no
  header evidence at all. Separating them mattered: documenting 11/35 as one number would
  have buried a genuine integrity breach inside an unavoidable ceiling. After the fix:
  7/35 = 20% residual, fail-safe breaches **0**.
- **My own recommendation was wrong, twice, until it was simulated.** U1 stood at 43%.
  From aggregate counts I advised raising `identity-mismatch`'s Return-Path weight. A
  Python simulation against actual per-sample findings showed that reaches only 46% —
  still short — *and* costs the corpus's only legitimate false positive. Raising
  `lookalike-domain` instead reached **51% with P3 unchanged at 0%**. Aggregate arithmetic
  produced a plausible answer but not a correct one. Logged as D-012.
- **Two more rules died on the probe, which is the real story of Phase 2.** Rule 7
  (`structural-oddities`): all six remaining unflagged phishing samples are *well-formed* —
  only `singleHop` fires, catching 2 of 6 for 1 false positive. Rule 6 (`received-chain`):
  "From-domain absent from `Received` and auth headers" fires on **13/35 legitimate**
  samples and **2/35 phishing** — an anti-detector. Both rejected before a line of code
  was written (D-013). That is now the fourth and fifth time probing killed a
  plausible-looking idea cheaply.
- **The holdout came in below the dev set.** Dev measured 51%; holdout measured **40%**
  against a 50% floor. My earlier estimate of a "55–65% ceiling" was **too optimistic**
  and has been corrected in `docs/success.md`. The gap is the ordinary cost of measuring
  on untouched data — and with 15 samples, one sample is 6.7%, so 40% and 50% are not
  reliably distinguishable. The number is reported as a miss, not tuned away.

### 5. Risks that changed

- **RSK-01 (detection ceiling) — materialised and closed.** Coverage is measured at
  **40–51%**; the contingency is now active: the tool is repositioned as a precision-first
  warning aid, not a general detector. **This needs an I&TS conversation.**
- **RSK-02 (false green) — materialised.** 20% dev / 33% holdout residual, but the
  fail-safe itself holds at **0% on both splits**. The binding outcome is a UI constraint:
  green must read "no issues found", never a tick.
- **Reduced, but on vacuous evidence — corrected by D-018.** Zero legitimate samples were
  flagged on both the dev split and the holdout (0/35 and 0/15), but that figure was
  produced by rules that never ran: no legitimate sample carries `Authentication-Results`,
  so `auth-results` returned `Unknown` and the fail-safe greyed every clean email. The
  claim that the precision promise "fully holds" is withdrawn. It may yet hold, but this
  corpus cannot show it.

### 6. Plan for next

1. **Rule 8 — `authserv-trust`** (security fix for the forged `Authentication-Results`
   gap; not a detection play, so unaffected by D-013).
2. **Phase 3 — the UI.** Green-state wording per D-011; the bubble overlay decision (D-002).
3. **Restate P2** (10-rule target) with the stakeholder — see D-014.

### 7. Am I inside the phase time-box?

Phase 2 is complete and inside the box, but **not on the original terms.** The exit gate
called for 8–10 rules; 4 are built and 3 were probed and rejected on evidence. The journal
predicted "the cut, if needed, is **rule count**: ship 6 well-measured rules rather than 10
unmeasured ones." **The cut was deeper than predicted and for a better reason** — the
rejected rules were not merely unmeasured, they were measured and found harmful or empty.
Two of the three criteria are met (P3, I1's fail-safe); U1 is missed at 40% and left
unadjusted.