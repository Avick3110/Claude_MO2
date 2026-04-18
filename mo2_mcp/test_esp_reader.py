"""
test_esp_reader.py - Verify the ESP binary reader against real plugin files.

Usage:
    python test_esp_reader.py <path-to-esp-or-esm>
    python test_esp_reader.py Skyrim.esm              # if in the Data folder
    python test_esp_reader.py --quick Skyrim.esm      # header + groups only, skip full scan
"""

from __future__ import annotations

import sys
import time
from pathlib import Path

# Ensure the module can be found when run standalone
sys.path.insert(0, str(Path(__file__).resolve().parent))

from esp_reader import ESPReader, RecordEntry, GrupEntry, SubRecord


def fmt_size(n: int) -> str:
    if n >= 1_048_576:
        return f"{n / 1_048_576:.1f} MB"
    if n >= 1024:
        return f"{n / 1024:.1f} KB"
    return f"{n} B"


def print_header(title: str) -> None:
    print(f"\n{'=' * 60}")
    print(f"  {title}")
    print(f"{'=' * 60}")


def test_tes4(reader: ESPReader) -> None:
    """Print TES4 header information."""
    print_header("TES4 Header")
    t = reader.tes4

    print(f"  HEDR version:   {t.hedr_version:.2f}")
    print(f"  Record count:   {t.record_count:,}")
    print(f"  Next object ID: 0x{t.next_object_id:08X}")
    print(f"  Flags:          0x{t.flags:08X}")
    print(f"  Form version:   {t.version}")
    print(f"  Is ESM:         {t.is_master_flagged}")
    print(f"  Is ESL:         {t.is_light_flagged}")
    print(f"  Localized:      {t.is_localized}")

    if t.author:
        print(f"  Author:         {t.author}")
    if t.description:
        desc = t.description[:100] + "..." if len(t.description) > 100 else t.description
        print(f"  Description:    {desc}")

    print(f"\n  Masters ({t.master_count}):")
    for m in t.masters:
        print(f"    - {m}")

    print(f"\n  Raw subrecords ({len(t.raw_subrecords)}):")
    for sr in t.raw_subrecords:
        print(f"    {sr.type:6s} {sr.size:>6} bytes")


def test_top_groups(reader: ESPReader) -> list[GrupEntry]:
    """List all top-level GRUPs and return them."""
    print_header("Top-Level GRUPs")
    groups = list(reader.iter_top_groups())
    print(f"  Total: {len(groups)}")
    print()
    print(f"  {'Type':<8} {'Size':>14} {'Data Offset':>14}")
    print(f"  {'-'*8} {'-'*14} {'-'*14}")
    for g in groups:
        print(f"  {g.label_as_type:<8} {fmt_size(g.size):>14} 0x{g.data_offset:>10X}")
    return groups


def test_record_scan(reader: ESPReader, groups: list[GrupEntry]) -> tuple[
    RecordEntry | None, RecordEntry | None
]:
    """Scan all records, count by type. Return a sample and a compressed record."""
    print_header("Full Record Scan")

    total = 0
    compressed = 0
    deleted = 0
    type_counts: dict[str, int] = {}
    sample: RecordEntry | None = None
    compressed_sample: RecordEntry | None = None

    t0 = time.perf_counter()
    for grup in groups:
        for rec in reader.iter_records_in(grup):
            total += 1
            type_counts[rec.type] = type_counts.get(rec.type, 0) + 1

            if rec.is_compressed:
                compressed += 1
                if compressed_sample is None:
                    compressed_sample = rec

            if rec.is_deleted:
                deleted += 1

            # Pick a non-trivial record as sample (ARMO, WEAP, NPC_ preferred)
            if sample is None and rec.type in ('ARMO', 'WEAP', 'NPC_', 'MISC'):
                sample = rec
    scan_time = time.perf_counter() - t0

    print(f"  Records:        {total:,}")
    print(f"  Compressed:     {compressed:,}")
    print(f"  Deleted:        {deleted:,}")
    print(f"  Record types:   {len(type_counts)}")
    print(f"  Scan time:      {scan_time:.2f}s")
    print()

    # Top 25 record types by count
    sorted_types = sorted(type_counts.items(), key=lambda x: -x[1])
    print(f"  {'Type':<8} {'Count':>10}")
    print(f"  {'-'*8} {'-'*10}")
    for rtype, count in sorted_types[:25]:
        print(f"  {rtype:<8} {count:>10,}")
    if len(sorted_types) > 25:
        print(f"  ... and {len(sorted_types) - 25} more types")

    return sample, compressed_sample


def test_subrecords(reader: ESPReader, record: RecordEntry, label: str) -> None:
    """Read and display subrecords for a specific record."""
    print_header(f"{label}: {record}")

    t0 = time.perf_counter()
    subs = reader.read_subrecords(record)
    read_time = time.perf_counter() - t0

    edid = reader.read_edid(record)
    print(f"  Editor ID:  {edid}")
    print(f"  FormID:     0x{record.formid:08X}")
    print(f"  Flags:      0x{record.flags:08X}", end="")
    flag_labels = []
    if record.is_compressed:
        flag_labels.append("COMPRESSED")
    if record.is_deleted:
        flag_labels.append("DELETED")
    if record.is_master_flagged:
        flag_labels.append("ESM")
    if record.is_light_flagged:
        flag_labels.append("ESL")
    if flag_labels:
        print(f"  ({', '.join(flag_labels)})")
    else:
        print()
    print(f"  Version:    {record.version}")
    print(f"  Read time:  {read_time * 1000:.1f}ms")
    print(f"\n  Subrecords ({len(subs)}):")
    print(f"  {'Type':<8} {'Size':>8}  Preview")
    print(f"  {'-'*8} {'-'*8}  {'-'*30}")

    for sr in subs:
        preview = ""
        if sr.type == 'EDID':
            preview = sr.data.decode('ascii', errors='replace').rstrip('\x00')
        elif sr.type in ('FULL', 'CNAM', 'SNAM', 'DNAM') and sr.size < 100:
            try:
                text = sr.data.decode('utf-8', errors='replace').rstrip('\x00')
                if text.isprintable():
                    preview = text
            except Exception:
                pass
        if not preview and sr.size <= 8:
            preview = sr.data.hex(' ')

        if len(preview) > 50:
            preview = preview[:47] + "..."
        print(f"  {sr.type:<8} {sr.size:>8}  {preview}")


def test_edid_scan(reader: ESPReader, groups: list[GrupEntry]) -> None:
    """Test EDID extraction speed on first 1000 records."""
    print_header("EDID Extraction Speed Test (first 1000 records)")

    count = 0
    found = 0
    t0 = time.perf_counter()

    for grup in groups:
        for rec in reader.iter_records_in(grup):
            edid = reader.read_edid(rec)
            count += 1
            if edid:
                found += 1
            if count >= 1000:
                break
        if count >= 1000:
            break

    elapsed = time.perf_counter() - t0
    rate = count / elapsed if elapsed > 0 else 0

    print(f"  Records checked: {count:,}")
    print(f"  EDIDs found:     {found:,}")
    print(f"  Time:            {elapsed:.3f}s")
    print(f"  Rate:            {rate:,.0f} records/sec")


def test_grup_nesting(reader: ESPReader, groups: list[GrupEntry]) -> None:
    """Show nesting structure of a group that has sub-GRUPs (CELL or WRLD)."""
    print_header("GRUP Nesting (first nested group)")

    target = None
    for g in groups:
        if g.label_as_type in ('CELL', 'WRLD', 'DIAL'):
            target = g
            break

    if target is None:
        print("  No nested groups found (CELL/WRLD/DIAL)")
        return

    print(f"  Inspecting: {target}")
    count = 0
    for child in reader.iter_grup_contents(target):
        if isinstance(child, GrupEntry):
            print(f"    Sub-GRUP type={child.group_type}, "
                  f"label=0x{int.from_bytes(child.label, 'little'):08X}, "
                  f"size={fmt_size(child.size)}")
        else:
            print(f"    Record {child.type} 0x{child.formid:08X}")
        count += 1
        if count >= 15:
            print(f"    ... (showing first 15 children)")
            break


def main() -> None:
    args = sys.argv[1:]
    quick = False
    if '--quick' in args:
        quick = True
        args.remove('--quick')

    if not args:
        print("Usage: python test_esp_reader.py [--quick] <path-to-esp-or-esm>")
        print()
        print("  --quick  Only read header and groups, skip full record scan")
        sys.exit(1)

    filepath = Path(args[0])
    if not filepath.exists():
        print(f"File not found: {filepath}")
        sys.exit(1)

    file_size = filepath.stat().st_size
    print(f"File:  {filepath.name}")
    print(f"Path:  {filepath}")
    print(f"Size:  {fmt_size(file_size)}")

    t_start = time.perf_counter()

    with ESPReader(filepath) as reader:
        # Always run these
        test_tes4(reader)
        groups = test_top_groups(reader)

        if not quick:
            sample, compressed = test_record_scan(reader, groups)

            if sample:
                test_subrecords(reader, sample, "Sample Record")

            if compressed:
                test_subrecords(reader, compressed, "Compressed Record")

            test_edid_scan(reader, groups)
            test_grup_nesting(reader, groups)

    total_time = time.perf_counter() - t_start
    print(f"\n{'=' * 60}")
    print(f"  Total time: {total_time:.2f}s")
    print(f"{'=' * 60}")


if __name__ == "__main__":
    main()
