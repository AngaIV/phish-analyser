"""List From, Subject, and key headers for each split phishing message."""
from pathlib import Path
import email

SRC = Path("testdata/raw/phishing/split")

META = ("From", "Subject", "Reply-To", "Return-Path")
ALL = ("Received", "Authentication-Results", "DKIM-Signature", "List-Unsubscribe", "X-Mailer")

for f in sorted(SRC.glob("*.eml")):
    raw = f.read_bytes()
    try:
        msg = email.message_from_bytes(raw)
        hdr = {}
        for h in META:
            hdr[h] = msg.get(h, "").replace("\n", " ")[:70]
        received = len(msg.get_all("Received") or [])
        ar = "Y" if msg.get_all("Authentication-Results") else "N"
        dkim = "Y" if msg.get_all("DKIM-Signature") else "N"
        unsub = "Y" if msg.get_all("List-Unsubscribe") else "N"
        mailer = msg.get("X-Mailer", "")[:40]
        print(f"{f.name} | Rcv:{received} AR:{ar} DKIM:{dkim} Unsub:{unsub} | {hdr['From'][:40]} | {hdr['Subject'][:50]}")
    except Exception as e:
        print(f"{f.name} | ERROR {e}")