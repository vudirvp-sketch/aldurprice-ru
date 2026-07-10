#!/usr/bin/env python3
"""
Pull latest translations from Exiled Exchange 2.

Clones a shallow copy of github.com/Kvan7/Exiled-Exchange-2,
copies *.ndjson files to ocr/translations/, and commits changes
if any.

Usage:
    python scripts/update-translations.py [--verbose] [--dry-run]

Requirements:
    - git in PATH
    - write access to ocr/translations/

License:
    Exiled Exchange 2 data is used under their license — see ocr/translations/LICENSE.
"""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

REPO_URL = "https://github.com/Kvan7/Exiled-Exchange-2.git"
TEMP_DIR = Path("_exiled-exchange-tmp")
DEST_DIR = Path("ocr/translations")
LANGS = ["eng", "rus", "deu", "fra", "spa", "por", "kor", "chi_tra"]
EXPECTED_LINES = 4319


def run(cmd: list[str], verbose: bool = False) -> str:
    if verbose:
        print(f"  $ {' '.join(cmd)}")
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        print(f"ERROR: {' '.join(cmd)} failed:")
        print(result.stderr)
        sys.exit(1)
    return result.stdout


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verbose", action="store_true", help="Verbose output")
    parser.add_argument("--dry-run", action="store_true", help="Don't commit changes")
    args = parser.parse_args()

    print("Pulling translations from Exiled Exchange 2...")

    # Cleanup temp dir if exists
    if TEMP_DIR.exists():
        shutil.rmtree(TEMP_DIR)

    # Shallow clone
    print(f"Cloning {REPO_URL}...")
    run(["git", "clone", "--depth", "1", REPO_URL, str(TEMP_DIR)], args.verbose)

    # Copy NDJSON files
    DEST_DIR.mkdir(parents=True, exist_ok=True)
    total_lines = 0
    for lang in LANGS:
        # Try several possible paths in the Exiled Exchange 2 repo
        candidates = [
            TEMP_DIR / "src" / "translations" / f"{lang}.ndjson",
            TEMP_DIR / "src" / "translations" / "items" / f"{lang}.ndjson",
            TEMP_DIR / "translations" / f"{lang}.ndjson",
            TEMP_DIR / "data" / "translations" / f"{lang}.ndjson",
        ]
        src = next((c for c in candidates if c.exists()), None)
        if src is None:
            print(f"  WARNING: {lang}.ndjson not found in expected paths, skipping")
            continue

        dst = DEST_DIR / f"{lang}.ndjson"
        shutil.copy2(src, dst)
        line_count = sum(1 for _ in open(dst, encoding="utf-8"))
        total_lines += line_count

        if line_count != EXPECTED_LINES:
            print(f"  WARNING: {lang}.ndjson has {line_count} lines (expected {EXPECTED_LINES})")
        else:
            print(f"  {lang}: {line_count} lines OK")

    # Cleanup temp dir
    shutil.rmtree(TEMP_DIR)

    print(f"\nTotal: {total_lines} lines across {len(LANGS)} languages")

    if args.dry_run:
        print("\n--dry-run: not committing changes")
        return

    # Check for changes
    diff = subprocess.run(
        ["git", "status", "--porcelain", "ocr/translations/"],
        capture_output=True, text=True
    )
    if not diff.stdout.strip():
        print("\nNo changes to commit.")
        return

    # Commit
    print("\nCommitting changes...")
    run(["git", "add", "ocr/translations/"], args.verbose)
    run(["git", "commit", "-m",
         "chore: update translations from Exiled Exchange 2\n\n"
         "Automated update via scripts/update-translations.py"], args.verbose)
    print("Done. Push to remote with: git push")


if __name__ == "__main__":
    main()
