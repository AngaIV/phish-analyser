# Risk register

Live risks, reviewed weekly as part of the journal. Likelihood and impact are rated
low / medium / high. Each risk names the mitigation and, where possible, the **early
warning signal** that tells us it is materialising.

Last reviewed: 2026-10-11 (Phase 2 close — holdout run complete).

---

## Active risks

### RSK-01 — Header-only detection has a ceiling; the tool may be honest but useless

| | |
|---|---|
| **Likelihood** | High |
| **Impact** | High |
| **Status** | **Materialised — quantified on holdout** |

**What:** Phishing that passes authentication and arrives with intact headers is
invisible to every planned rule. Measured on the dev split, `auth-results` reaches
0.17 recall and `identity-mismatch` 0.11. Both are high-precision, low-coverage
signals.

**Early warning:** Combined detection rate stays below the 50% floor in
`docs/success.md` (U1) after rules 3 and 5 are implemented.
**Signal fired.** Dev set reached 51% only after the v1.1 weight change; the **holdout
returned 40%**, below the floor.

**Mitigation:** Rules 3 (display-name) and 5 (lookalike domains) are the intended
detection load.
**Mitigation exhausted.** Rules 6 and 7 were probed and rejected — rule 6 would have
produced 13 false positives to catch 2 phishing samples (D-013). Header-only coverage is
now measured at **40–51%** and there is no remaining rule idea that survives a probe.

**Contingency (now active):** Restate the value proposition honestly as *"warns on roughly
half of header-anomalous phishing, with essentially no false positives"* rather than a
general-purpose detector. The tool is a *warning aid*, not a filter, and it must never
claim safety — see RSK-02 and D-011. **Discuss with the I&TS stakeholder whether a
precision-first, half-coverage aid is still useful.**

---

### RSK-02 — A false green is worse than no tool

| | |
|---|---|
| **Likelihood** | **Certain** |
| **Impact** | Critical |
| **Status** | **Materialised — 20% dev / 33% holdout; fail-safe itself holds at 0%** |

**What:** If the bubble shows green on a phishing message, the user is actively
misled. This is the one failure mode that causes harm rather than inconvenience.

**Measured, after the scorer fix (2026-10-11):** of 35 development phishing samples,
**7 produce no flag and no `Unknown` from any rule** — every rule ran and returned
`Pass`. The bubble would show green on real phishing. The affected samples are
`phish_043_swiftactivation`, `phish_027_t_users`, `phish_038_gmail`, `phish_045_caresly`,
`phish_036_tontransport`, `phish_013_wellsfargo_alert`, `phish_020_gmail`.

**Why it is not 0%, and cannot be with header-only analysis:** Several of these senders
use ordinary-looking real domains — `caresly.com`, `t_users.com`, `swiftactivation.net`,
`tontransport.ga`, and a genuine university domain `hw.ac.uk` hosting a Wells Fargo
phish. They make no brand claim, show no domain anomaly, and either pass authentication
or carry no authentication headers. **There is no header evidence to find.**

**Mitigation (applied):**
- D-004 holds — missing evidence yields `Unknown`, never `Pass`.
- Scorer fix: a message with no flags **but any `Unknown`** now shows grey, not green.
  This closed 4 genuine fail-safe breaches found at first measurement (green *despite* an
  explicit `Unknown`), where the weight-1 branch bypassed the unknown check. Pinned by
  regression test `OneWeightOneFindingPlusUnknown_IsNotGreen`.
- **UI requirement:** the green state must be labelled a neutral "no issues found", never
  a tick or a shield. A tick asserts a safety guarantee the engine cannot make. Carried
  into the Phase 3 design and reflected in the `ui-design/` mockups.

**Revised criterion:** I1's target becomes **0% false green on messages where no rule
returned `Unknown`** (met), with the residual **7/35 = 20%** recorded as a documented,
unfixable limitation. The original "0% false green" wording is restated in
`docs/success.md`; it was not achievable and should not have been asserted.

**Holdout confirms the split.** On 15 unseen phishing samples: **0/15 showed green while a
rule returned `Unknown`** — the fail-safe itself holds on data it was never tuned on. The
residual all-`Pass` rate was **5/15 = 33%**, higher than dev's 20%, consistent with the
holdout simply containing more header-clean phishing. The criterion that matters
(fail-safe integrity) is **met**; the criterion that cannot be met (catching header-clean
phishing) remains a documented limit.

**Early warning:** Any rule emitting `Pass` on missing evidence. The one approved
exception (missing `Reply-To`) is documented in the rule spec with its reasoning.

**Audit:** `evaluate.py` prints the false-green count on every run, so it cannot drift
unnoticed. The corpus test asserts no sample throws.

---

### RSK-03 — False positives from legitimate bulk mail

| | |
|---|---|
| **Likelihood** | High |
| **Impact** | High |
| **Status** | Materialised once, fixed, recurring risk |

**What:** Newsletters, mailing lists, forwards and ESP-sent mail legitimately carry
third-party bounce and reply domains. Rule 2 flagged **22 of 35** legitimate dev
samples (precision 0.27) before the list/bulk suppression fix took it to 0.80.

**Early warning:** Any new rule touching sender identity, reply paths or routing shows
elevated flag-level FP on the mailing-list canaries (`ham_003`, `ham_010`, `ham_035`).

**Mitigation:** Every rule must be measured on the dev split before it is considered
done (criterion R3), with FPs investigated rather than averaged away (R4). The
`MessageContext.IsBulkOrList` helper exists to be reused by later rules.

**Contingency:** The plan's risk table specifies informational-only demotion of noisy
rules — a rule can be kept for transparency while being excluded from the score.

---

### RSK-04 — Corpus is too small to measure the false-positive target

| | |
|---|---|
| **Likelihood** | Certain |
| **Impact** | Medium |
| **Status** | Materialised — statistical, not a defect |

**What:** The dev split holds 35 legitimate samples, so one false positive is 2.9%.
The corpus cannot resolve the 5% target: distinguishing "under 5%" from "under 8%"
needs well over 100 legitimate samples.

**Mitigation:** Report every rate as a point estimate with its sample size. Grow the
legitimate set **before the holdout run**, deliberately enlarging the holdout rather
than the dev split, so the honest final measurement is the stronger one. The
SpamAssassin archives on disk hold far more material than the 100 samples currently
committed.

**Early warning:** None needed — this is a present limitation, documented in
`docs/success.md` §4.

---

### RSK-05 — Corpus composition does not match real university mail

| | |
|---|---|
| **Likelihood** | Medium |
| **Impact** | Medium |
| **Status** | Open |

**What:** The legitimate set is SpamAssassin 2002-era mail, skewed to mailing lists and
bulk senders. Real I&TS traffic — Microsoft 365 notifications, SharePoint shares, Teams
invites, banking alerts, conference mail — is structurally different and may trip rules
that today look clean. The plan explicitly asks for the author's own genuine mail for
this reason.

**Early warning:** Low measured FP on the corpus but complaints during Phase 5 beta.

**Mitigation:** Add genuine, consenting-usage samples (own mail, with personal data
removed) covering Microsoft/SharePoint notifications, banking, and university
bulletins. Note that **SpamAssassin mail also contains zero modern authentication
headers**, which is why 65% of mechanism checks return `Unknown` — a realistic corpus
would show `pass` far more often and is the harder test.

**Related:** the plan's own instruction to include a forwarded message, a mailing-list
message and a third-party newsletter as false-positive canaries is already satisfied.

---

### RSK-06 — Authserv-id is not verified, so a forged result could be trusted

| | |
|---|---|
| **Likelihood** | Low (requires targeted attack) |
| **Impact** | Medium |
| **Status** | Known gap, scheduled |

**What:** Rule 1 takes the first `Authentication-Results` header. A sender can inject a
forged `Authentication-Results: ... spf=pass`. Without checking which server wrote the
header, the forged value could win and produce a false `Pass`.

**Mitigation:** Phase 2 rule 8 (authserv-id trust check) closes this. The current
first-match-wins behaviour is pinned by the `TwoAuthResultsHeaders_FirstWins` test, so
the change is deliberate and visible rather than silent.

**Early warning:** Not externally observable — treated as a scheduled fix, not a
monitored risk.

---

### RSK-07 — POPIA / ethics compliance gap

| | |
|---|---|
| **Likelihood** | Medium |
| **Impact** | High (if deployed with real mail) |
| **Status** | Open — needs stakeholder input |

**What:** The plan's privacy section does not address POPIA (South Africa) or research
ethics. If the tool is evaluated on colleagues' real mail, or if the author's own mail is
used as corpus material, both consent and data-handling obligations apply. University
context (Rhodes University, I&TS) raises this from a theoretical concern.

**Mitigation:** Raise with the I&TS stakeholder and supervisor before any Phase 5 beta.
The design helps: the engine is local-only, makes no network calls (criterion P6), and
committed corpus samples are headers-only with bodies stripped.

**Early warning:** Any request to test on real mailboxes without a documented consent
process.

---

### RSK-08 — Scope exceeds the time-box

| | |
|---|---|
| **Likelihood** | Medium |
| **Impact** | Medium |
| **Status** | Open |
| **Owner decision required** | Rule-count target (see `docs/success.md` §6) |

**What:** Phase 2's build order lists 8 rules; project criterion P2 says at least 10.
Phase 2's exit gate requires each rule to have a spec, tests and a measured precision
figure — real work per rule. Later phases add three UI surfaces.

**Mitigation:** The plan's rung structure (CLI first, then desktop, then web) means every
phase ends with something demonstrable. **If time runs short, the cut is rule count, not
rigour** — ship 6 well-measured rules rather than 10 unmeasured ones.

**Early warning:** Any rule shipped without a measured precision figure. That is a
breach of the exit gate, not a shortcut.

---

## Closed risks

| Risk | Closed | How |
|---|---|---|
| Parser crashes on malformed input | 2026-10-11 | `SafeMessageLoader` never lets an exception escape; 8 malformed fixtures, a 10 MB header-block test and an oversize-rejection test |
| Accidentally committing unsanitised mail | 2026-10-11 | Headers-only sanitisation at the first blank line, zero body leaks verified, `testdata/raw/` git-ignored |
| Corpus too small to be meaningful | 2026-10-11 | Grown 30 → 100 samples with a reproducible 70/30 split (see RSK-04 for the residual statistical limit) |
| Measurement taken against a stale build | 2026-10-11 | Diagnosed and documented as D-007; rebuild-before-measuring recorded in `docs/results.md` |
| CI workflow invalid (`hashFiles` at job level) | 2026-10-11 | Replaced with a shell guard; all three jobs green on `main` |