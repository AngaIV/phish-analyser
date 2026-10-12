# Phish Analyser

**A Grammarly-style warning bubble for Outlook that tells you when an email looks like a phishing attempt.**

![CI](https://github.com/AngaIV/phish-analyser/actions/workflows/ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![tests](https://img.shields.io/badge/tests-74%20passing-brightgreen)
![status](https://img.shields.io/badge/status-in%20progress-orange)
![licence](https://img.shields.io/badge/licence-MIT-blue)

![Progress](https://raw.githubusercontent.com/AngaIV/phish-analyser/progress/progress.svg)

---

## What it does

Phishing emails lie about who they are. The trick is usually in the parts of an email you
never see: the **headers**, which record where a message actually came from.

Phish Analyser reads those headers and puts a small floating bubble next to the email
you are reading. The bubble shows a number, which is how many suspicious things it found.

| Bubble | What it means |
|---|---|
| **0** in green | Nothing unusual found |
| **3** in amber | Worth a closer look |
| **7** in red | Looks like phishing |
| Blank, dashed grey | Could not check it |

It runs entirely **on your own computer**. No email is ever sent anywhere. Nothing is
uploaded, logged or shared. The app makes no network calls at all.

---

## Why I built it

I work in IT support at a university. Staff and students get phishing emails constantly,
and most of them have no easy way to tell a real message from a fake one.

Big organisations solve this with expensive email security gateways. I wanted to see how
far a small, free, standalone tool could get. One that works with the mail client people
already use, tells them plainly when something looks wrong, and **never cries wolf**,
because a tool that flags safe emails gets uninstalled within a week.

That second part turned out to be the interesting problem, and the whole project is built
around measuring it honestly.

---

## How well does it actually work?

I test it against **100 real emails**, 50 phishing and 50 legitimate, and I published the
results, including the ones that do not flatter the project.

| | Result | Target | |
|---|---|---|---|
| Legitimate emails wrongly flagged | **0 out of 50** | under 5% | met |
| Phishing emails flagged | **40 to 51%** | 50% | close |
| False all clear on phishing | **17 to 33%** | 0% | not met |

**The good news:** it does not cry wolf. Across both test sets it flagged **zero**
legitimate emails. Every warning it raised was on a real phishing message.

**The honest news:** it misses about half of them today.

That is the finding so far. Phishing that leaves no trace in the headers, because the
sender used their own real, properly registered domain, is **invisible to this kind of
analysis** and no rule can catch it. But the samples it does miss are the whole point of
the next stage: I am working through them one at a time, looking for signals I have not
used yet, and adding a rule only when the measurements prove it catches phishing without
flagging safe mail.

I proved the value of that discipline already. I tested five rule changes before building
them and rejected all five when the numbers did not support them. Two of them would have
made the tool worse. I would rather ship a tool that is right when it speaks and silent
when it cannot tell, than one that guesses.

**This project is far from finished.** The detection rules are the part I expect to keep
improving for a long time, alongside the Outlook integration and the packaging.

---

## Progress

| Phase | What | Status |
|---|---|---|
| 0 | Planning and repository setup | Done |
| 1 | Email header parser and test corpus | Done |
| 2 | Detection rules, scoring, measurement | Done |
| 3 | Outlook app and floating bubble | Part done |
| 4 | Outlook add-in and web surfaces | Not started |
| 5 | Threat intelligence (optional) | Not started |
| 6 | Packaging and release | Not started |

**Phase 3 is part done.** The bubble works. It appears on screen, drags around, shows all
five states, and displays real verdicts from the detection engine. What is left is getting
email headers out of Outlook, where version differences make things awkward, and anchoring
the bubble to the reading pane.

**The detection rules will keep improving.** The current results are a starting point, not
a finished product. Every rule in the engine has unit tests, a written specification and
measured precision before it ships, so improvements are provable rather than hopeful. There
is a long list of ideas still to test.

The progress bar at the top updates itself from a GitHub Action, so it cannot drift out of
date.

---

## What it is built with

| Layer | Technology |
|---|---|
| Detection engine | C# / .NET 10, MimeKit for email parsing |
| Desktop app | WPF (Windows Presentation Foundation) |
| Email integration | Office.js (Outlook add-in) |
| Web surfaces | TypeScript |
| Testing and scripts | xUnit, Python, pytest |
| Automation | GitHub Actions (three CI jobs) |

**74 automated tests** in the C# engine and 23 in the Python tooling, run on every commit.

---

## The hard part

Three things genuinely surprised me.

**1. Good phishing passes every security check.** SPF, DKIM and DMARC are the standard
ways to prove an email is legitimate. Modern phishing is often sent from real, correctly
authenticated servers. Checking them catches only about one in six phishing emails.

**2. Being silent is a feature, not a gap.** When the tool cannot check something, it says
so, with a grey dashed circle rather than a green tick. A confident green tick on a
phishing email is worse than no tool at all, because it turns a cautious person into a
trusting one.

**3. Half the value is in what I did not build.** I tested five detection ideas before
writing them. Two would have made the tool worse. One of them would have flagged 13
legitimate emails to catch 2 phishing ones. Killing them early saved more time than any
optimisation. Those decisions are written up in [`docs/decisions.md`](docs/decisions.md).

---

## Try it

The desktop app runs today, in demonstration mode:

```bash
git clone https://github.com/AngaIV/phish-analyser.git
cd phish-analyser
dotnet run --project desktop/PhishAnalyser.Desktop
```

Press **1** to **5** to step through the bubble states. Press **Esc** to quit.

To see a real verdict on a real sample:

```bash
dotnet run --project desktop/PhishAnalyser.Desktop -- --headers testdata/phishing/phish_001_netflix_renewal_scan.eml
```

Or run the engine on its own, with no interface:

```bash
cd engine-dotnet
dotnet test
```

---

## Where to look next

| Document | What is in it |
|---|---|
| [`docs/results.md`](docs/results.md) | The actual measurements, holdout results and misses |
| [`docs/decisions.md`](docs/decisions.md) | Every design decision, including the ones I got wrong |
| [`docs/risk-register.md`](docs/risk-register.md) | What could go wrong, and what happened |
| [`docs/journal.md`](docs/journal.md) | Weekly build log |
| [`ui-design/`](ui-design/) | Interface mockups |

---

## Notes

- Windows only, for now. The engine is plain .NET and would port easily.
- Test emails are **headers only**, with no message bodies, taken from the Nazario phishing
  archive (CC-BY-4.0) and the SpamAssassin ham collections. No real student or staff email
  is in this repository, and no message body ever leaves the machine.
- Built as a personal project while working in university IT support. Not affiliated with
  or deployed by any institution.

MIT licensed.
