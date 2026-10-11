"""Generate deliberately broken fixtures for parser robustness tests.

Synthetic and personal-data free. The oversized case is
built at test time instead, to keep the repository small.
"""
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / "testdata" / "malformed"
OUT.mkdir(parents=True, exist_ok=True)

SAMPLE = (
    "Return-Path: <sender@example.com>\n"
    "Received: from mx.example.com ([10.0.0.1]) by mx.local;\n"
    " Tue, 3 Jan 2023 06:11:01 +0000\n"
    "From: Example Sender <sender@example.com>\n"
    "Subject: Synthetic fixture\n"
    "Message-ID: <abc123@example.com>\n"
)

(OUT / "truncated.eml").write_text(SAMPLE[:120], encoding="utf-8", newline="\n")
(OUT / "duplicate_from.eml").write_text(
    SAMPLE + "From: Attacker <evil@example.net>\n", encoding="utf-8", newline="\n")
(OUT / "no_headers.eml").write_text("", encoding="utf-8", newline="\n")
(OUT / "missing_colon.eml").write_text(
    "From sender@example.com\nSubject has no colon\n\n", encoding="utf-8", newline="\n")
(OUT / "binary_junk.eml").write_bytes(bytes(range(256)) * 64)
(OUT / "null_bytes.eml").write_bytes(b"From: a@b.c\n\x00\x00\x00binary\n\n")
(OUT / "deeply_folded.eml").write_bytes(
    b"Received: from a\n" + b" " + b"b" * 200 + b"\n" * 5000 + b"\n")

print("wrote", len(list(OUT.glob("*.eml"))), "fixtures to", OUT)