#!/usr/bin/env python3
"""Insert a new version entry into manifest.json on release."""

import argparse
import datetime as dt
import json
import sys
from pathlib import Path

MANIFEST = Path(__file__).resolve().parents[2] / "manifest.json"
TARGET_ABI = "10.11.0.0"


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True)
    p.add_argument("--checksum", required=True)
    p.add_argument("--url", required=True)
    args = p.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    plugin = manifest[0]
    plugin.setdefault("versions", [])

    entry = {
        "version": f"{args.version}.0",
        "changelog": f"release {args.version}",
        "targetAbi": TARGET_ABI,
        "sourceUrl": args.url,
        "checksum": args.checksum,
        "timestamp": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    plugin["versions"].insert(0, entry)
    MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
