# Phish Analyser

**A small floating bubble for Outlook that counts the suspicious things in an email's headers.**

![CI](https://github.com/AngaIV/phish-analyser/actions/workflows/ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![tests](https://img.shields.io/badge/tests-74%20passing-brightgreen)
![status](https://img.shields.io/badge/status-in%20progress-orange)
![licence](https://img.shields.io/badge/licence-MIT-blue)

---


## What it does

Phishing emails lie about who sent them, and the evidence is in the headers you never
see. Phish Analyser reads those headers and shows a bubble next to the email you are reading with a count
of what looks off. Clicking the bubble will open a pane showing more details on each thing
it found. It runs entirely on your own machine and makes no network calls.

| Bubble | Meaning |
|---|---|
| **0**, green | Nothing unusual found |
| **3**, amber | Worth a closer look |
| **7**, red | Looks like phishing |
| Dashed grey circle | Could not check it |

I work in IT support at a university, where phishing is constant, and most people have no
easy way to check a message. I wanted to see how far a small, free tool could get.

## Status

- **Working:** header parser, detection rules, scoring, test corpus, and a desktop bubble
  with all five states showing real verdicts from the engine.
- **In progress:** getting headers out of Outlook
  and anchoring the bubble to the reading pane.
- **Planned:** the detail pane that opens when you click the bubble, Outlook add-in and web
  support, optional threat-intelligence lookups, and an installer.

## How well it works

I test against 100 real emails: 50 phishing, 50 legitimate. Full numbers, including the
misses, are in [`docs/results.md`](docs/results.md).

| | Result | Target |
|---|---|---|
| Legitimate emails flagged | 0 of 50 | under 5% |
| Phishing emails flagged | 40 to 51% | 50% |
| Phishing given a false all clear | 17 to 33% | 0% |

The ranges cover both of my test sets. Zero false flags on 50 emails is encouraging, but a small
sample, so I don't claim it never happens.

Today, it misses about half of phishing. Checking SPF, DKIM and DMARC alone catches roughly
one in six, because a lot of phishing is sent from the attacker's own properly registered
domain and leaves nothing odd in the headers. I'm working through the misses one at a time, looking for other signals.

A new rule only goes in if measurements show it catches phishing without flagging safe mail.
I tested five ideas before building them and rejected all five. One would have flagged 13
legitimate emails to catch 2 phishing ones. The reasoning is in
[`docs/decisions.md`](docs/decisions.md).

## Built with

- C# and .NET 10, with MimeKit for email parsing
- WPF for the desktop app
- xUnit and pytest, run on every commit with GitHub Actions
- Planned: an Office.js Outlook add-in and TypeScript web surfaces

## Docs

| Document | Contents |
|---|---|
| [`docs/results.md`](docs/results.md) | Measurements, holdout results and misses |
| [`docs/decisions.md`](docs/decisions.md) | Design decisions, including the ones I got wrong |
| [`docs/risk-register.md`](docs/risk-register.md) | What could go wrong, and what happened |
| [`docs/journal.md`](docs/journal.md) | Weekly build log |
| [`ui-design/`](ui-design/) | Interface mockups |

## Notes

- Windows only for now. The engine is plain .NET and would port easily.
- Test emails are headers only, taken from the Nazario phishing archive (CC-BY-4.0) and the
  SpamAssassin ham collections. No real student or staff email is in this repository.
- A personal project, built alongside my university IT support work. Not affiliated with or
  deployed by any institution.

MIT licensed.
