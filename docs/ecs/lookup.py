#!/usr/bin/env python3
"""Look up Friflo.Engine.ECS API docs that ship inside the NuGet package.

Usage:
    python3 docs/ecs/lookup.py CommandBuffer
    python3 docs/ecs/lookup.py CommandBuffer Playback      # all terms must match
    python3 docs/ecs/lookup.py --path                      # print the file used
    python3 docs/ecs/lookup.py Entity --full --limit 20

Set FRIFLO_ECS_XML to override which reference file is read.
"""

import argparse
import glob
import html
import os
import re
import sys
import xml.etree.ElementTree as ET

PACKAGE = "friflo.engine.ecs"


def reference_path() -> str:
    override = os.environ.get("FRIFLO_ECS_XML")
    if override:
        return override

    root = os.path.expanduser("~/.nuget/packages/" + PACKAGE)
    matches = sorted(glob.glob(os.path.join(root, "*", "lib", "net10.0", "*.xml")))
    if not matches:
        matches = sorted(glob.glob(os.path.join(root, "*", "lib", "*", "*.xml")))
    if not matches:
        sys.exit(
            f"No {PACKAGE} XML docs found under {root}.\n"
            "Restore the project (dotnet build) or set FRIFLO_ECS_XML."
        )

    return matches[-1]


def short_cref(cref: str) -> str:
    """M:Friflo.Engine.ECS.CommandBuffer.Playback -> CommandBuffer.Playback"""
    name = cref.split(":", 1)[-1]
    return ".".join(name.split(".")[-2:])


def render(node) -> str:
    if node is None:
        return ""

    xml = ET.tostring(node, encoding="unicode")
    xml = re.sub(r'<see cref="([^"]+)"\s*/>', lambda m: short_cref(m.group(1)), xml)
    xml = re.sub(r'<see langword="([^"]+)"\s*/>', lambda m: m.group(1), xml)
    xml = re.sub(r"<br\s*/?>", " ", xml)
    xml = re.sub(r"<[^>]+>", "", xml)
    return html.unescape(re.sub(r"\s+", " ", xml)).strip()


def member_name(name: str) -> str:
    kind, _, full = name.partition(":")
    full = full.split("(")[0].rstrip("`0123456789")
    if kind == "T":
        return full.split(".")[-1]

    owner, _, member = full.rpartition(".")
    return f"{owner.split('.')[-1]}.{member}"


def main() -> None:
    parser = argparse.ArgumentParser(add_help=True)
    parser.add_argument("terms", nargs="*", help="case-insensitive substrings to match")
    parser.add_argument("--path", action="store_true", help="print the XML file used")
    parser.add_argument("--full", action="store_true", help="also print params and remarks")
    parser.add_argument("--limit", type=int, default=60)
    args = parser.parse_args()

    path = reference_path()
    if args.path:
        print(path)
        return

    if not args.terms:
        parser.error("give at least one search term, or --path")

    root = ET.parse(path).getroot()
    terms = [term.lower() for term in args.terms]
    shown = 0
    total = 0

    for member in root.iter("member"):
        name = member_name(member.get("name", ""))
        if not all(term in name.lower() for term in terms):
            continue

        total += 1
        if shown >= args.limit:
            continue

        shown += 1
        print(name)
        summary = render(member.find("summary"))
        if summary:
            print(f"  {summary}")

        if args.full:
            for tag in ("remarks", "returns"):
                body = render(member.find(tag))
                if body:
                    print(f"  {tag}: {body}")
            for param in member.iter("param"):
                body = render(param)
                if body:
                    print(f"  param {param.get('name')}: {body}")
        print()

    if total == 0:
        print(f"No members match {terms} in {path}", file=sys.stderr)
        sys.exit(1)

    if total > shown:
        print(f"... {total - shown} more; narrow the terms or raise --limit.")


if __name__ == "__main__":
    main()
