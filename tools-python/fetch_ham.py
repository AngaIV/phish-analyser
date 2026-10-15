#!/usr/bin/env python3
"""Extract one headers-only .eml per message from an mbox, anonymised.

Written for the D-018 follow-up: the corpus needs modern legitimate mail carrying
`Authentication-Results`. Apache's public list archives are confirmed to provide it.

Two things this does that matter:

1. **Headers only.** Everything from the first blank line onwards is discarded. The
   project never commits message bodies.
2. **Anonymised.** From, To, Cc, Return-Path and Message-ID are replaced, and any
   neighbouring address left inside a Received line is stripped. These are real people's
   names and addresses on a public list; publishing them for a benchmarking exercise would
   not be acceptable even though the list is public by design.

Envelope `From ` lines are stripped too, so the output is a header block and nothing else.

Usage:
    python tools-python/fetch_ham.py --list httpd-users --month 202401 --count 25
    python tools-python/fetch_ham.py --list tomcat-users --month 202407 --count 20 --seed 7
"""

from __future__ import annotations

import argparse
import hashlib
import random
import re
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "testdata" / "legitimate"

ARCHIVE = "https://mail-archives.apache.org/mod_mbox/{list}/{month}.mbox"

# Headers whose values identify a person and are therefore replaced wholesale. These must
# be replaced with a valid mailbox, not a placeholder, or the rules cannot read a sender.
ADDRESS_HEADERS = {"from", "to", "cc", "return-path", "message-id", "delivered-to", "reply-to"}

# Headers kept because the analyser needs them. Everything else is dropped, which also
# removes X-headers that occasionally carry personal detail.
KEEP = {
    "authentication-results", "arc-authentication-results", "arc-seal", "arc-message-signature",
    "dkim-signature", "domainkey-signature", "received", "received-spf", "date", "subject",
    "from", "to", "cc", "reply-to", "return-path", "message-id", "list-unsubscribe",
    "list-id", "list-post", "list-help", "content-type", "mime-version", "user-agent",
    "x-mailer", "x-originating-ip", "x-spam-status", "x-spam-flag", "x-spam-score",
}

# Any email address left anywhere in a retained header.
ADDRESS = re.compile(r"[\w.+-]+@[\w-]+\.[\w.-]+")
# The bare IP a Received line names, which is the sender's own address.
IPV4 = re.compile(r"\b(?:\d{1,3}\.){3}\d{1,3}\b")


def download(list_name: str, month: str) -> bytes:
    url = ARCHIVE.format(list=list_name, month=month)
    print(f"  downloading {url}")
    request = urllib.request.Request(url, headers={"User-Agent": "phish-analyser-corpus/1.0"})
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def split_messages(raw: bytes) -> list[str]:
    """Split an mbox into individual messages.

    The separator is a line starting with `From ` at the start of a line. That is the
    mbox convention and it is what the archives use.
    """
    text = raw.decode("utf-8", errors="replace").replace("\r\n", "\n")
    parts = re.split(r"(?m)^From .*\n", text)
    return [p for p in parts if p.strip()]


def headers_only(message: str) -> list[tuple[str, str]]:
    """Return the header block as (name, value) pairs, discarding the body.

    Continuation lines (those starting with whitespace) are joined onto the previous
    header, because an `Authentication-Results` verdict is frequently on a continuation
    line and losing it would silently break the very rule this corpus exists to test.
    """
    lines = message.split("\n")
    headers: list[tuple[str, str]] = []

    for line in lines:
        if line == "":
            break                                  # end of the header block
        if line[0] in " \t":
            if headers:
                name, value = headers[-1]
                headers[-1] = (name, f"{value}\n{line}")   # keep RFC 5322 folding
            continue
        if ":" not in line:
            continue
        name, _, value = line.partition(":")
        headers.append((name.strip(), value.strip()))

    return headers


def anonymise(headers: list[tuple[str, str]], index: int) -> list[tuple[str, str]]:
    out: list[tuple[str, str]] = []

    for name, value in headers:
        key = name.lower()
        if key not in KEEP:
            continue

        if key in ADDRESS_HEADERS:
            # Must remain a syntactically valid mailbox. A placeholder like
            # "<redacted-1>" is not a parseable address, so every rule that reads the
            # sender fails and the sample comes back grey for the wrong reason. The
            # domain is example.com, reserved by RFC 2606 and safe to publish.
            value = f"user{index}@example.com"
            if key in ("from", "to", "cc", "reply-to"):
                value = f"List User {index} <{value}>"
        else:
            # Received lines and DKIM signatures embed addresses, IPs and identifiers.
            value = ADDRESS.sub("user@example.com", value)
            value = IPV4.sub("0.0.0.0", value)

        out.append((name, value))

    return out


def render(headers: list[tuple[str, str]]) -> str:
    return "".join(f"{name}: {value}\n" for name, value in headers)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--list", dest="list_name", required=True)
    parser.add_argument("--month", required=True, help="YYYYMM")
    parser.add_argument("--count", type=int, default=25)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--prefix", default=None)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    raw = download(args.list_name, args.month)
    messages = split_messages(raw)
    print(f"  {len(messages)} messages in the archive")

    # Deterministic selection, so a re-run reproduces the same corpus.
    random.Random(args.seed).shuffle(messages)

    prefix = args.prefix or f"ham_apache_{args.list_name}"
    kept = 0

    for message in messages:
        if kept >= args.count:
            break

        headers = headers_only(message)

        # Only take messages that can actually exercise the rule under test. Keeping
        # messages without a verdict would rebuild the exact defect being fixed.
        # Note this must scan header NAMES as well as values: the name is
        # Authentication-Results, and the verdict tokens live in the value.
        flat = "\n".join(f"{n}\n{v}" for n, v in headers).lower()
        if "authentication-results" not in flat:
            continue
        if not re.search(r"\b(spf|dkim|dmarc)=(pass|fail|softfail|none)\b", flat):
            continue

        name = f"{prefix}_{kept + 1:03d}.eml"
        path = OUT_DIR / name
        body = render(anonymise(headers, kept + 1))

        if args.dry_run:
            print(f"  would write {name} ({len(body)} bytes)")
        else:
            path.write_text(body, encoding="utf-8")

        kept += 1

    print(f"  wrote {kept} headers-only samples as {prefix}_*.eml")
    return 0 if kept else 1


if __name__ == "__main__":
    sys.exit(main())
