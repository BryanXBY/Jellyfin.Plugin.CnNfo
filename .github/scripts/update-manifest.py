#!/usr/bin/env python3
"""Insert a new version entry into manifest.json on release."""

import argparse
import datetime as dt
import json
import sys
from pathlib import Path

MANIFEST = Path(__file__).resolve().parents[2] / "manifest.json"
TARGET_ABI = "10.10.0.0"


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True)
    p.add_argument("--checksum", required=True)
    p.add_argument("--url", required=True)
    args = p.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    plugin = manifest[0]
    plugin.setdefault("versions", [])

    full_version = f"{args.version}.0"
    new_entry = {
        "version": full_version,
        "changelog": f"release {args.version}",
        "targetAbi": TARGET_ABI,
        "sourceUrl": args.url,
        "checksum": args.checksum,
        "timestamp": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }

    # 去重：先剔除所有已存在的同 version 条目（含 changelog 已被手工编辑的情况），
    # 然后把新条目插到最前。CI 多次跑（比如 tag 重打）也只会有一条。
    plugin["versions"] = [v for v in plugin["versions"] if v.get("version") != full_version]
    plugin["versions"].insert(0, new_entry)

    MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"manifest.json updated: {full_version} ({len(plugin['versions'])} versions total)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
