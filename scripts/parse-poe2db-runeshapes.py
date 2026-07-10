#!/usr/bin/env python3
"""
Parse runeshape combinations from poe2db.tw (RU and EN pages).

Downloads https://poe2db.tw/ru/Runeshape_Combinations and the EN equivalent,
extracts the RU↔EN name mapping for all runes, alloys, lineage runes, wards,
ancients, and master runes, and saves as JSON.

HTML structure (verified 2025-07):

  Pattern A — base runes (top of page, level-range grid):
    <a href="Fire_Rune">
      <img data-bs-title=" Руна огня" ... />
    </a>

  Pattern B — combinations / alloys / lineage / ancient / ward runes (tables):
    <a class="whiteitem SoulCore" href="Farruls_Rune_of_the_Chase">
      <img ... />Руна погони Фаррул
    </a>

Pattern B also lists some general currency (Divine Orb, etc.) on the same page;
we filter by href keyword ("Rune" or "Alloy") to keep only runeshape items.

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

# Href must contain one of these (case-insensitive) to be considered a runeshape
# item. Excludes general currency (Mirror_of_Kalandra, Divine_Orb, etc.).
HREF_KEYWORDS = ("rune", "alloy")

# Tier detection by English name keywords (lowercased).
# Order matters: lineage must be checked before basic, since lineage runes
# also contain "Rune" in their name.
TIER_RULES = [
    ("alloy",    ["alloy"]),
    ("lineage",  ["farrul", "assandra", "seske", "girte", "mannan", "sakaval",
                  "grannel", "fenuma", "fenumus", "saqawal", "craiceann",
                  "great wolf", "greatwolf", "lelde", "hestra",
                  "mire", "myrk", "kracenne", "thane", "countess", "courtesan",
                  "hedgewitch", "lady"]),
    ("ward",     ["warding rune", "ward rune"]),
    ("ancient",  ["ancient rune"]),
    ("master",   ["masterwork rune", "master rune"]),
    ("special",  ["charging rune", "rune diversion", "conducting rune"]),
]


def detect_tier(en_name: str) -> str:
    n = en_name.lower()
    for tier, kws in TIER_RULES:
        if any(kw in n for kw in kws):
            return tier
    return "basic"


def fetch_page(url: str, verbose: bool = False) -> str:
    if verbose:
        print(f"  Fetching {url}...")
    resp = requests.get(url, headers={"User-Agent": USER_AGENT}, timeout=30)
    resp.raise_for_status()
    return resp.text


def clean_title(title: str) -> str:
    """Strip leading 'Level X - Y ' prefix that poe2db embeds in Pattern A titles.

    'Level 24 - 100 Руна барьера' → 'Руна барьера'
    'Level 15 - 40 Fire Rune'      → 'Fire Rune'
    """
    return re.sub(r"^Level\s+\d+\s*-\s*\d+\s+", "", title).strip()


def href_is_rune_like(href: str) -> bool:
    h = href.lower()
    return any(kw in h for kw in HREF_KEYWORDS)


def extract(html_text: str, verbose: bool = False) -> dict[str, str]:
    """
    Extract {href: localised_name} pairs from a poe2db Runeshape_Combinations page.

    Combines:
      Pattern A — <a href><img data-bs-title="..."/></a>  (top-of-page grid)
      Pattern B — <a href><img/>Text</a>                   (combination tables)

    Pattern B is filtered to hrefs containing 'rune' or 'alloy' to exclude
    general currency (Mirror of Kalandra, Divine Orb, etc.) which also appears
    on this page in the same HTML structure.
    """
    tree = html.fromstring(html_text)
    items: dict[str, str] = {}

    # Pattern A: data-bs-title on inner <img>
    for a in tree.xpath('//a[.//img[@data-bs-title]]'):
        href = (a.get("href") or "").strip()
        if not href:
            continue
        img = a.xpath('.//img[@data-bs-title]')[0]
        title = clean_title((img.get("data-bs-title") or "").strip())
        if title and href_is_rune_like(href):
            items[href] = title

    # Pattern B: items in combination tables. Two sub-variants seen in the wild:
    #   B1: <a class="..."><img/>Text</a>  — text in img.tail (alloys, etc.)
    #   B2: <a class="whiteitem SoulCore">Text</a>  — text in a.text, no <img>
    #       (lineage runes, ancient runes, ward runes)
    # Filter hrefs to rune/alloy-like to exclude general currency that also
    # appears in tables on this page.
    for a in tree.xpath('//a[@href]'):
        href = (a.get("href") or "").strip()
        if not href or not href_is_rune_like(href):
            continue
        # Skip navigation/relative links like "/ru/Runeshape_Combinations"
        # and anchor links like "#Alloys", "#Runes"
        if href.startswith("/") or href.startswith("#"):
            continue
        # Don't overwrite Pattern A entries (they're more precise for base runes)
        if href in items:
            continue

        # Try a.text first (B2), then img.tail (B1)
        title = clean_title((a.text or "").strip())
        if not title:
            for img in a.xpath('./img'):
                tail = clean_title((img.tail or "").strip())
                if tail:
                    title = tail
                    break
        if not title:
            continue
        # Skip if title is identical to href (navigation links, not items)
        if title == href:
            continue
        # Skip obvious non-item text (e.g., "x1", "Lv70+", currency amounts)
        if re.fullmatch(r"x\d+|Lv[\d\-+]+|\d+(?:\.\d+)?", title):
            continue
        items[href] = title

    if verbose:
        print(f"    Extracted {len(items)} unique items (by href)")
    return items


def href_to_canonical(href: str) -> str:
    """Convert href slug to a canonical English name (best-effort).

    'Fire_Rune' → 'Fire Rune'
    'Farruls_Rune_of_the_Chase' → "Farrul's Rune of the Chase"
    'The_Runefathers_Alloy' → "The Runefather's Alloy"
    """
    return (href.replace("_", " ")
                .replace(" s ", "'s ")
                .strip())


def merge_ru_en(ru: dict[str, str], en: dict[str, str], verbose: bool = False) -> list[dict]:
    """
    Merge RU and EN by canonical key (href).

    Both dicts are {href: localised_title}. Join on href.
    """
    merged: list[dict] = []
    for href in sorted(ru.keys() | en.keys()):
        ru_name = ru.get(href)
        en_name = en.get(href)
        if ru_name and en_name:
            merged.append({
                "en": en_name,
                "ru": ru_name,
                "href": href,
                "tier": detect_tier(en_name),
            })
        elif ru_name and verbose:
            print(f"    WARN: ru-only entry href={href!r} ru={ru_name!r}")
        elif en_name and verbose:
            print(f"    WARN: en-only entry href={href!r} en={en_name!r}")
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
    ru_items = extract(ru_html, args.verbose)

    en_html = fetch_page(EN_URL, args.verbose)
    en_items = extract(en_html, args.verbose)

    merged = merge_ru_en(ru_items, en_items, args.verbose)

    if not merged:
        print("\nWARNING: No combinations extracted. The poe2db.tw HTML structure may have changed.")
        print("Inspect the page manually and update the parser.")
        sys.exit(2)

    output = {
        "version": 1,
        "source": RU_URL,
        "fetched_at": datetime.now(timezone.utc).isoformat(),
        "count": len(merged),
        "combinations": sorted(merged, key=lambda c: (c["tier"], c["en"].lower())),
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
    tier_counts: dict[str, int] = {}
    for c in merged:
        tier_counts[c["tier"]] = tier_counts.get(c["tier"], 0) + 1
    print("\nBy tier:")
    for tier, count in sorted(tier_counts.items()):
        print(f"  {tier}: {count}")


if __name__ == "__main__":
    main()
