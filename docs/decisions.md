# Decision log

One entry per decision that would be expensive to reverse or that a reader might
otherwise question. Newest last. Format: what was decided, why, what was rejected,
and what would make us revisit it.

---

## D-001 — Web-first: Office.js `getAllInternetHeadersAsync()` before COM/VSTO

**Decided:** Build the header-acquisition path on Office.js Mailbox requirement set
1.8 first, rather than starting with a classic Outlook COM/VSTO add-in.

**Why:** One implementation covers New Outlook on Windows, Outlook on the web,
classic Outlook, macOS and mobile 4.2405.0+. The COM path covers a single client.

**Evidence:** `getAllInternetHeadersAsync()` requires Mailbox 1.8, Read mode, the
narrowest `read item` permission, and is supported across all the target surfaces.
`getAsFileAsync()` was also viable (returns the full base64 EML) but needs a higher
requirement set and hands over more data than the analyser needs.

**Rejected:** `item.internetHeaders.getAsync/setAsync` — a trap worth recording,
because it *looks* like the header API. It is **Compose-mode only** and operates on
custom headers you set yourself, so it cannot read a received message at all.

**Revisit if:** Office.js header access proves unavailable or the requirement set
cannot be met on a target surface in practice.

---

## D-002 — The bubble cannot be a browser extension in New Outlook

**Decided:** The Grammarly-style overlay is a WPF window that tracks the target
application's window bounds.

**Why:** New Outlook on Windows is a **WebView2 host**, so there is no browser
extension surface to inject into. A Chrome/Edge extension could not draw on it.

**Consequence:** The browser extension remains the delivery mechanism for
Gmail and other *browser* webmail, but it is a separate surface sharing the same
engine. Three surfaces, one engine.

**Revisit if:** Microsoft exposes an extension or overlay point for New Outlook.

---

## D-003 — Corpus is headers-only, sanitised at the first blank line

**Decided:** Every committed `.eml` sample is truncated at the first blank line, so
only headers remain. Raw originals stay in the git-ignored `testdata/raw/`.

**Why:** The engine analyses headers only, so bodies add no analytical value while
adding real risk — live tracking URLs, working credential-harvesting links, personal
data, and the Nazario 2023+ archives are explicitly *not* anonymised.

**Verification:** Committed samples were checked for zero body leaks, and
`testdata/raw/` was confirmed untracked with `git check-ignore -v`.

**Rule:** Samples are only ever opened as text, never in a mail client.

---

## D-004 — Missing evidence maps to `Unknown`, never to `Pass`

**Decided:** The engine's fail-safe default is `Unknown` (`RuleStatus.Unknown`).
Absent, unparseable or unrecognised evidence never produces a green result.

**Why:** A false pass is the one failure mode that actively harms the user — it
tells them a hostile message is safe. A false `Unknown` merely withholds a verdict.

**Approved exception:** `identity-mismatch` treats a **missing `Reply-To` as a
`Pass`**, because the header is genuinely optional and absent on most legitimate
mail; marking it `Unknown` would add noise without adding information. The
distinction from `Return-Path` — which *is* Unknown when absent, since the receiving
server always adds it — is deliberate and documented in the rule spec.

**Consequence:** Across the corpus, 194 of 300 authentication mechanism-checks
return `Unknown`. That is designed behaviour, not a defect.

---

## D-005 — Rule weights are hardcoded, with configuration deferred

**Decided:** Severity and weight live as literals in each rule for now. Moving them
to configuration files is deferred.

**Why:** Tuning weights before there is a scorer and measured results is guesswork.
The plan calls for configuration-driven weights, and that remains the destination.

**Revisit:** When the scoring module lands and per-rule measurement makes tuning
meaningful.

---

## D-006 — Suppress identity mismatches on list and bulk mail (rule v1.1)

**Decided:** `identity-mismatch` treats a `From`/`Return-Path`/`Reply-To` mismatch as
a **Pass** when the message carries `List-Id`, `List-Unsubscribe`, `List-Post`, or
`Precedence: bulk|list`.

**Why:** The first dev-set measurement produced **22 false positives from 30 flags
(precision 0.27)**, and 21 of the 22 were mailing-list or bulk mail, where a
third-party bounce or reply domain is entirely normal. A rule that flags roughly a
third of legitimate mail is unusable.

**Result:** Precision 0.27 → **0.80**; flags 30 → 5; false positives 22 → 1.

**Cost, accepted knowingly:** Recall fell 0.23 → 0.11 (true positives 8 → 4), because
12 of the 35 development phishing samples carry list or bulk headers. Real phishing
campaigns do abuse bulk-mail infrastructure. The trade was taken because
false positives on legitimate mail are the primary usability failure.

**Revisit if:** Recall on this rule matters more than precision once rules 3 and 5
(display-name, lookalike) are carrying detection load — suppression could be
narrowed to `List-Id` only, or the finding downgraded to `Unknown` rather than `Pass`.

---

## D-007 — Measurement must rebuild before scoring

**Decided:** Always `dotnet build` from `engine-dotnet/` before running
`tools-python/evaluate.py`.

**Why:** `evaluate.py` invokes the CLI with `--no-build`, and `dotnet test` does not
rebuild the `Cli` project. Measuring after an engine change silently scores a stale
`Engine.dll`. **This actually happened** — the v1.1 fix initially appeared to have no
effect because the CLI was running an `Engine.dll` 13 minutes older than the engine
build, and the unchanged table was briefly taken at face value.

**Guard:** The warning is recorded in `docs/results.md` next to the method. A future
improvement is to have `evaluate.py` build the CLI itself before scoring.

---

## D-008 — Light fuzzing deferred until the parser surface freezes

**Decided:** Phase 1 task 8 (light fuzzing) is deferred rather than skipped.

**Why:** The parser surface grows through Phase 2 — `DomainHelper`, `MessageContext`
and later Received-chain parsing all consume untrusted headers. Fuzzing now would
mean fuzzing twice. It belongs immediately before the parser freezes for the
TypeScript port in Phase 4.

**Interim coverage:** The `SafeMessageLoader` malformed-input theory (8 fixtures),
the 10 MB header-block test, the oversize-rejection test, and a deliberately broad
`catch` already provide the durability guarantee; fuzzing's job is to discover new
cases to add to that theory, not to establish it.

**When it runs:** Locally and capped (~20 samples × 50 mutations), kept out of the
per-PR CI run, since fuzzing is inherently variable and a flaky red build costs more
than it catches.

---

## D-009 — Rule 4 (`message-id-alignment`) deferred behind an ESP allow-list

**Decided:** Rule 4 is **not** built next, despite the plan listing it fourth. It is
deferred until it can consult an allow-list of legitimate sending platforms.

**Why:** A probe over the 18 phishing samples that rules 1–3 miss showed rule 4 would
have caught **7 of them** — which looked like an easy win until the Message-ID domains
were inspected:

| Message-ID domain | What it actually is |
|---|---|
| `eu-west-2.amazonses.com` | Amazon SES |
| `geopod-ismtpd-4-1` | SendGrid |
| `schoolm.smtp.e.sparkpost.com` | SparkPost |
| `webmail.emailsrvr.com` | Rackspace |
| `desktop-0nldaac…` | Google desktop client |

**Every one of those 7 catches is legitimate sending infrastructure.** `Message-ID` is
stamped by the platform that sends the mail, not by the brand, so a `Message-ID`-versus-
`From` mismatch is *normal* for any organisation using an email platform — which is
almost all of them. Building rule 4 as written would have flagged a large share of
legitimate bulk mail, repeating rule 2's 0.27-precision failure and likely worse.

**Rejected alternative:** implementing it anyway and tuning the weight down. The signal
is not weak, it is *inverted* — it fires on the normal case. A lower weight just makes a
useless finding quieter.

**Revisit when:** an allow-list of ESP and client domains exists to subtract from the
comparison. Then the remaining mismatches are genuinely informative.

**Lesson:** a rule's *catch count* says nothing until you look at *what* it caught. The
probe cost one script and prevented building the wrong rule.

---

## D-010 — Edit distance and homoglyph matching rejected for rule 5

**Decided:** `lookalike-domain` v1.0 implements **three** signals — a brand plus a lure
word in a host label, cheap TLDs, and IP-derived or digit-heavy labels. It deliberately
omits **edit-distance (typosquat) matching**, **homoglyph / mixed-script detection** and
bare **hyphen heuristics**, all of which the plan's rule 5 lists.

**Why — edit distance:** probed at Levenshtein ≤2 against the brand list, it flagged 5
phishing samples but **2 legitimate ones**, and the "hits" were nonsense:

| Domain | Matched | Reality |
|---|---|---|
| `aaps.k12.mi.us` | `ups` | A US school district |
| `hw.ac.uk` | `dhl` | Heriot-Watt University |
| `ucd.ie` | `ups` | University College Dublin (false positive) |
| `users.sourceforge.net` | `usps` | SourceForge (false positive) |

The cause is brand length: `ups`, `dhl` and `fnb` are three characters, so at distance
≤2 they match almost any short domain. Restricting to brands of ≥6 characters and
distance 1 removed both false positives — **and removed all the true positives too,
yielding zero catches.** The classic typosquats (`paypa1.com`, `rnicrosoft.com`) simply
do not occur in this corpus, so the machinery would have been unvalidated code earning
nothing.

**Why — homoglyphs:** detecting them properly requires decoding punycode, because IDN
domains are normally converted to `xn--` form before the engine ever sees them. A naive
"contains non-ASCII" check would therefore rarely fire on real mail while penalising
legitimate international domains. The punycode lookalike `xn--pypal-4ve.com` is **not
detected**, and that is documented as a known limitation rather than half-implemented.

**Why — hyphen heuristics:** legitimate domains use hyphens constantly
(`users.sourceforge.net`). Only hyphens that *join a brand to a lure word*
(`paypal-secure.com`) are treated as evidence, via signal 1.

**What was kept, and why it is honest to keep it:** brand-plus-lure-word matching earns
its place despite catching **zero** development samples, because it targets the dominant
real-world phishing pattern (`paypal-secure.com`, `apple-id-verify.com`) and is unit-
tested from hand-made snippets. It is explicitly recorded as **corpus-unvalidated**.

**Consequence for the measurement:** rule 5's measured recall on this corpus (3 of 35)
comes entirely from cheap TLDs and IP-derived labels. The high-confidence signal
contributes nothing to the dev-set figure, and its real-world false-positive rate is
**unmeasured**. Stated in the rule spec and in `docs/results.md`.

---

## D-011 — False green is restated as 0%-where-checked, with a documented 20% residual

**Decided:** Success criterion I1 changes from *"0% false green on phishing"* to *"0%
false green on messages where no rule returned `Unknown`"*, with the measured residual
**7/35 (20%)** on the dev split recorded as a known, unfixable limitation.

**Why:** The original criterion **was never achievable and should not have been
asserted**. Seven development phishing samples leave no header evidence: ordinary real
domains (`caresly.com`, `t_users.com`, `swiftactivation.net`, `tontransport.ga`,
`hw.ac.uk` — a genuine university domain hosting a Wells Fargo phish), no brand claim, no
domain anomaly, and either passing authentication or carrying no authentication headers.
Every rule runs and correctly returns `Pass`. No scoring change can fix this, because
there is nothing to find.

**A real bug was found in the same measurement, and it was separate.** The first scorer
run reported 11/35 green. Four of those were **green *despite* an explicit `Unknown`
finding** — `Scorer.Colour` consulted unknowns only on the zero-flag path, so any
weight-1 finding bypassed the fail-safe. This is exactly the D-004 promise broken by an
implementation detail. Fixed, pinned by regression test
`OneWeightOneFindingPlusUnknown_IsNotGreen`, and those four samples now show grey.

**Distinguishing the two was the point.** The 11 conflated an implementation bug (4,
fixable) with an analytical limit (7, not fixable). Reporting them as one number would
have hidden a genuine fail-safe breach behind an unavoidable ceiling.

**Binding consequence for Phase 3:** the green state must be presented as a neutral
**"no issues found"** — never a tick, shield or checkmark. A tick asserts a safety
guarantee the engine cannot make. This is a UI constraint, not a wording preference.

**Lesson:** an integrity criterion asserted *before* measurement is worth writing down,
because the measurement is what reveals it was aspirational. The value was in stating it
up front and then revising it honestly, rather than quietly softening the target later.

---

## D-012 — Rule weights tuned from per-sample simulation, not aggregate arithmetic

**Decided:** Before changing a rule weight to influence a success criterion, simulate the
change in Python against the **actual per-sample findings** and confirm the effect. Do not
reason from aggregate counts.

**Why:** U1 needed 18/35 phishing samples at amber or red and stood at 15. Working from
aggregate numbers, the obvious move was to raise `identity-mismatch`'s Return-Path weight
from 1 to 2. Simulating it showed the advice was **wrong in both directions**:

| Scenario | U1 | P3 (legitimate amber/red) |
|---|---|---|
| Raise `identity-mismatch` 1→2 | 16/35 = 46% — **still short** | 1/35 = 3% — **loses the only clean FP record** |
| Raise `lookalike-domain` 1→2 | **18/35 = 51% — clears U1** | **0/35 = 0% — unaffected** |

The aggregate reasoning failed because it counted flags without knowing which rule produced
them. Four of the seven green samples were fail-safe breaches already showing grey (from
D-011), not sub-amber flags, and the only genuinely sub-amber sample was a
`lookalike-domain` one. Aggregate counts cannot distinguish those cases; per-sample data
can.

**Applied:** `lookalike-domain` signals 2 and 3, weight 1 → 2. Re-measurement matched the
simulation exactly (U1 51%, false green 17%, P3 0%).

**Cost of the method:** one throwaway Python script per weight question. **Cost of not
using it:** a rule change that would have shipped, failed to meet the criterion, and spent
the project's only legitimate-sample precision margin on nothing.

**Generalisation:** this is the same lesson as D-009 (rule 4) and D-010 (edit distance) —
a plausible-looking change is worth ten minutes of measurement before it is worth a line
of code. Three for three so far.

---

## D-013 — Rules 6 and 7 rejected on measurement; the header-only ceiling is reached

**Decided:** `received-chain` (rule 6) is **rejected outright**. `structural-oddities`
(rule 7) is **rejected** in the form the plan describes. No further detection rules are
added in Phase 2.

**Why — rule 6 (`received-chain`) would be an anti-detector.** The planned signal was
"the `From` domain does not appear in the `Received` chain or the authentication headers."
Measured on the development split:

| | From-domain absent from **both** `Received` and `Auth/DKIM` |
|---|---|
| Phishing | **2/35** |
| Legitimate | **13/35** |

A rule flagging that condition fires **six times more often on legitimate mail than on
phishing**. It would add 13 false positives to catch 2 true ones — worse than useless, and
the third instance of the same failure mode as D-009 (rule 4's ESP trap) and D-010 (edit
distance).

**Why — rule 7 (`structural-oddities`) has almost nothing to find.** All six remaining
unflagged phishing samples were probed for missing `Date`, missing `Message-ID`, missing
`To`, duplicate `From`/`Return-Path`/`Date`/`Message-ID`/`Subject`, single-hop `Received`,
suspicious `X-Mailer` values, and control characters in headers:

| Signal | Greens caught | Legitimate false positives |
|---|---|---|
| `singleHop` (only one `Received`) | 2/6 | 1/35 |
| every other signal | **0/6** | 0 |

The remaining phishing samples are **not header-poor**. They are well-formed messages.
A rule built on the one productive signal would catch 2 of 6 for 1 false positive, and
`singleHop` is weak — legitimate mail relayed once is unremarkable.

**Why this is a real conclusion and not a shortcut.** All six remaining samples were
checked separately against the two strongest structural signals available:

| Sample | From-domain in `Received` | in `Auth`/`DKIM` |
|---|---|---|
| `phish_043_swiftactivation` | ✅ | ✅ |
| `phish_027_t_users` | ✅ | ✅ |
| `phish_038_gmail` | ✅ | ✅ |
| `phish_045_caresly` | ✅ | ✅ |
| `phish_013_wellsfargo_alert` | ✅ | ✅ |
| `phish_020_gmail` | ✅ | ✅ |

Every one is internally consistent. `caresly.com`, `t_users.com` and `swiftactivation.net`
are the attackers' own registered domains, appearing legitimately in their own mail
infrastructure; two are genuine `gmail.com` accounts. **There is no header anomaly to
detect — not a hard one, none.**

**Consequences accepted:**

1. **Header-only detection is capped at ~40–51%** (18/35 = 51% dev, 6/15 = 40% holdout).
   This is the measured ceiling, not an estimate.
2. **Project criterion P2 ("at least 10 rules") is not met** — see D-014.
3. **Rule 8 (`authserv-trust`) is still worth building** — it closes a security gap (a
   forged `Authentication-Results` header) and is not a detection play. It is exempt from
   this decision.
4. **Remaining Phase 2 value is in the holdout run and the UI**, not more rules.

**Lesson, fourth instance:** probing before building has now rejected rule 4, edit
distance, a weight change, rule 6 and rule 7's core, at a cost of one script each. The
pattern is consistent enough to state as project practice: **no detection rule is
implemented until a probe shows it catches more phishing than legitimate mail.**

---

## D-014 — P2 restated: rules are measured, not counted; U1's floor is left unmet

**Decided:** Project criterion **P2** changes from *"at least 10 detection rules"* to
*"every rule built has a spec, unit tests, a probe justifying it, and measured precision
and recall on the dev split"*. The rule count is **4**, with **3 documented rejections**
(rules 4, 6, 7). U1's 50% floor is **left unmet on the holdout (40%) and not adjusted.**

**Why P2 is restated rather than met by adding rules.** The plan asked for 10 rules; six
were specified. Four are built and measured. Of the remaining three, **two were probed and
found to be actively harmful or empty** (D-013). Building them anyway to reach a count
would ship a detector that flags 13 legitimate messages to catch 2 phishing ones. **A rule
count is a proxy for thoroughness; satisfying the proxy while making the tool worse is the
definition of over-engineering.**

**Why U1 is left unmet.** The holdout returned **6/15 = 40%** against the 50% floor.
Raising the measured number is possible — lowering a threshold, or raising a weight — but
every such change would be **tuning against the holdout**, which destroys the only clean
measurement the project has. The floor stays where it was written, and the result is
reported as a miss.

**Why the floor should not now be lowered either.** Editing the target after seeing the
result is the same failure in the other direction. The honest record is: *the criterion was
set at 50%, the engine achieved 51% on dev and 40% on unseen data, and the floor is not
reliably met.*

**On the small holdout sample:** 15 samples means one sample is 6.7%. 40% and 50% are not
distinguishable at this size. The defensible claim is *"roughly 40–50%, at zero false
positives"*, not a precise figure.

**Consequence for Phase 3 and beyond:** the tool's value proposition is **precision, not
coverage**. It will flag roughly half of header-anomalous phishing and essentially no
legitimate mail. The UI must therefore never imply that an unflagged message is safe —
which is exactly the constraint D-011 already imposed on the green state.

---

## D-015 — VSTO add-in dropped; the tray app attaches to classic Outlook by COM

**Decided:** Phase 3's MVP does **not** build a VSTO add-in. The tray app attaches to a
running classic Outlook instance via **COM late binding**, reads the selected item's
transport headers, and runs the engine in-process. The named pipe and the two-process
split are dropped. VSTO becomes the documented **fallback**, not the starting point.

**Why:** The toolchain is not present on the development machine, and installing it is a
large tax for one add-in:

| Requirement | Status |
|---|---|
| Visual Studio with the Office/SharePoint workload | **Absent** — BuildTools 2019 only, no IDE |
| VSTO runtime | **Not installed** |
| Admin rights to register the add-in | Deployment complexity, not a blocker |

Classic Outlook **is** installed (16.0.20430.20146), and WPF `net10.0-windows` builds and
runs on this machine (verified with a throwaway project).

**What this buys:**

- Deletes plan tasks 1, 2 and 5 (hello-world add-in, header read, pipe protocol) and the
  entire tray-app/add-in split.
- Deletes two documented failure modes: *"add-in does not load or is disabled by Outlook
  after a slow start"* and *"header property unavailable for some items"* across a pipe.
- No new NuGet package. COM late binding needs no reference assembly, so **MimeKit stays
  the only dependency**.
- Nothing to uninstall beyond the app itself, so the exit gate item *"uninstalling cleanly
  removes the add-in and auto-start entry"* reduces to the auto-start entry.

**Accepted cost, stated plainly:** Microsoft does **not** officially support driving
Outlook from an external process. This approach works and is widely used, but it is
unsupported surface — a policy or a future update could break it. That is the reason the
plan's own fallback exists: if COM attachment proves fragile, the detail panel ships as an
Outlook task pane add-in and the bubble is kept for the web and extension surfaces.

**Consequence:** the engine and rules are unaffected. This changes only how headers reach
the engine and how the overlay is drawn.

---

## D-016 — Phase 3 starts with U1 unmet; the phase is not blocked by it

**Decided:** Phase 3 proceeds even though Phase 2's exit gate is **not fully passed** —
U1 measured **40% on the holdout** against a 50% floor (D-014).

**Why:** Phase 3's entry criterion is written as *"Phase 2 exit gate passed."* Read
strictly it is not, and pretending otherwise would be exactly the kind of quiet
target-shifting this project has avoided. But the gate exists to ensure the UI is built on
a measured engine, and that is true: the engine is implemented, tested (66 tests), and
measured on both splits. **The unmet criterion is coverage, and coverage does not affect
any Phase 3 task** — every Phase 3 deliverable works identically at 40% or 90%.

**What would genuinely block Phase 3** is an *unstable or unmeasured* engine, or an engine
whose output contract is still moving. Neither applies.

**Consequence:** the U1 shortfall is carried forward openly as a known defect of the
product, not silently dropped. It is recorded in `docs/results.md`, `RSK-01`, and here.
The repositioning in D-014 — precision-first warning aid, roughly half coverage — is what
Phase 3's UI must communicate honestly, which is why D-011's green-state wording is
binding on this phase rather than advisory.

**Revisit if:** real-traffic testing (recommended after Phase 3) shows coverage is
materially different from 40%, in either direction.