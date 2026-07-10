#!/usr/bin/env python3
"""
Parse runeshape combinations from poe2db.tw (RU and EN pages).

Downloads https://poe2db.tw/ru/Runeshape_Combinations and the EN equivalent,
extracts the RU↔EN name mapping for all runes, alloys, lineage runes, wards,
ancients, and master runes, and saves as JSON.

Usage:
    python scripts/parse-poe2db-runeshapes.py [--output PATH] [--dry-run] [--verbose]

Requirements:
    pip install requests lxml

Output format — see docs/04-RU-LOCALIZATION.md section 2.2.
"""

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

try:
    import requests
    from lxml import html
except ImportError:
    print("ERROR: install dependencies first:")
    print("  pip install requests lxml")
    sys.exit(1)


RU_URL = "https://poe2db.tw/ru/Runeshape_Combinations"
EN_URL = "https://poe2db.tw/us/Runeshape_Combinations"
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"

# Tiers based on poe2db categorization
TIER_KEYWORDS = {
    "alloy": ["сплав", "alloy"],
    "lineage": ["фаррул", "ассандр", "сеске", "гирт", "маннан", "сакаваль",
                "граннель", "фенум", "великого волка", "лельда", "гестра",
                "мирк", "краценн", "farrul", "assandra", "seske", "girte",
                "mannan", "sakaval", "grannel", "fenuma", "great wolf",
                "lelde", "hestra", "mire", "kracenne"],
    "ward": ["барьерная руна", "ward rune"],
    "ancient": ["древняя руна", "ancient rune"],
    "master": ["мастерская руна", "master rune"],
    "special": ["проводящая", "рунное отведение", "conducting", "rune diversion"],
}


def fetch_page(url: str, verbose: bool = False) -> str:
    if verbose:
        print(f"  Fetching {url}...")
    resp = requests.get(url, headers={"User-Agent": USER_AGENT}, timeout=30)
    resp.raise_for_status()
    return resp.text


def extract_combinations(html_text: str, lang: str, verbose: bool = False) -> dict[str, dict]:
    """
    Extract runeshape names from poe2db HTML.

    Returns: {canonical_key: {"name": str, "tier": str}}
    """
    tree = html.fromstring(html_text)
    combinations = {}

    # Strategy 1: Look for table rows with data-name attributes
    rows = tree.xpath("//tr[@data-name] | //tr[contains(@class, 'runeshape')]")
    for row in rows:
        name = row.get("data-name") or row.get("data-full-name")
        if not name:
            cells = row.xpath(".//td")
            if cells:
                name = cells[0].text_content().strip()
        if name and len(name) > 2:
            key = name.lower().replace(" ", "-").replace("'", "")
            combinations[key] = {"name": name, "tier": detect_tier(name)}

    # Strategy 2: Look for specific Cyrillic/Latin patterns in text
    if not combinations:
        if lang == "ru":
            pattern = re.compile(r"([А-Яа-яЁё][А-Яа-яЁё\s'\-]{4,60})")
        else:
            pattern = re.compile(r"([A-Z][a-zA-Z\s'\-]{4,60})")
        text_content = tree.text_content()
        matches = pattern.findall(text_content)
        for match in matches:
            match = match.strip()
            if 5 <= len(match) <= 60 and is_rune_like(match, lang):
                key = match.lower().replace(" ", "-").replace("'", "")
                if key not in combinations:
                    combinations[key] = {"name": match, "tier": detect_tier(match)}

    if verbose:
        print(f"    Found {len(combinations)} {lang} combinations")
    return combinations


def detect_tier(name: str) -> str:
    name_lower = name.lower()
    for tier, keywords in TIER_KEYWORDS.items():
        if any(kw in name_lower for kw in keywords):
            return tier
    return "basic"


def is_rune_like(name: str, lang: str) -> bool:
    """Heuristic: does this look like a runeshape combination name?"""
    name_lower = name.lower()
    if lang == "ru":
        return "рун" in name_lower or "сплав" in name_lower
    else:
        return "rune" in name_lower or "alloy" in name_lower


def merge_ru_en(ru_combos: dict, en_combos: dict, verbose: bool = False) -> list[dict]:
    """Merge RU and EN by canonical key."""
    merged = []
    matched_en_keys = set()

    for key, ru_data in ru_combos.items():
        en_data = en_combos.get(key)
        if en_data:
            merged.append({
                "en": en_data["name"],
                "ru": ru_data["name"],
                "tier": ru_data.get("tier", "unknown")
            })
            matched_en_keys.add(key)
        else:
            # Try fuzzy: same tier, similar first letters
            ru_first = ru_data["name"][0].lower() if ru_data["name"] else ""
            candidates = [
                v for k, v in en_combos.items()
                if k not in matched_en_keys
                and v.get("tier") == ru_data.get("tier")
                and v["name"][0].lower() == ru_first
            ]
            if candidates:
                en_data = candidates[0]
                merged.append({
                    "en": en_data["name"],
                    "ru": ru_data["name"],
                    "tier": ru_data.get("tier", "unknown")
                })
                matched_en_keys.add(en_data["name"].lower().replace(" ", "-").replace("'", ""))

    if verbose:
        print(f"    Merged: {len(merged)} combinations")

    return merged


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="ocr/runeshape-combinations-ru.json",
                        help="Output JSON file path")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print to stdout instead of writing file")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    print("Parsing runeshape combinations from poe2db.tw...")

    ru_html = fetch_page(RU_URL, args.verbose)
    ru_combos = extract_combinations(ru_html, "ru", args.verbose)

    en_html = fetch_page(EN_URL, args.verbose)
    en_combos = extract_combinations(en_html, "en", args.verbose)

    merged = merge_ru_en(ru_combos, en_combos, args.verbose)

    if not merged:
        print("\nWARNING: No combinations extracted. The poe2db.tw HTML structure may have changed.")
        print("Inspect the page manually and update the parser.")
        sys.exit(2)

    output = {
        "version": 1,
        "source": RU_URL,
        "fetched_at": datetime.now(timezone.utc).isoformat(),
        "combinations": sorted(merged, key=lambda c: (c["tier"], c["en"]))
    }

    if args.dry_run:
        print("\n--- DRY RUN OUTPUT (first 2000 chars) ---")
        print(json.dumps(output, ensure_ascii=False, indent=2)[:2000])
        return

    out_path = Path(args.output)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(
        json.dumps(output, ensure_ascii=False, indent=2),
        encoding="utf-8"
    )
    print(f"\nSaved {len(merged)} combinations to {out_path}")

    # Print tier summary
    tier_counts = {}
    for c in merged:
        tier_counts[c["tier"]] = tier_counts.get(c["tier"], 0) + 1
    print("\nBy tier:")
    for tier, count in sorted(tier_counts.items()):
        print(f"  {tier}: {count}")


if __name__ == "__main__":
    main()
