"""
esp_reader.py - Binary reader for ESP/ESM/ESL files (Skyrim SE).

Reads the container format: TES4 header, GRUPs, records, and subrecords.
Used only for record-index building (header + EDID scan) as of v2.0.0;
field interpretation moved to spooky-bridge.exe (Mutagen-backed).

Reference: https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format
"""

from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO, Iterator


# ── Constants ───────────────────────────────────────────────────────────

HEADER_SIZE = 24            # Record headers and GRUP headers are both 24 bytes
SUBRECORD_HEADER_SIZE = 6   # Type (4) + size (2)

# Pre-compiled struct formats (little-endian)
_FMT_RECORD = struct.Struct('<4sIIIIHH')    # type dataSize flags formID revision version unk
_FMT_GRUP   = struct.Struct('<4sI4siHHHH')  # "GRUP" size label groupType stamp unk1 ver unk2
_FMT_SUBREC = struct.Struct('<4sH')         # type size
_FMT_HEDR   = struct.Struct('<fII')         # version numRecords nextObjectId
_FMT_U16    = struct.Struct('<H')
_FMT_U32    = struct.Struct('<I')

# Record flags
FLAG_MASTER     = 0x00000001
FLAG_DELETED    = 0x00000020
FLAG_LOCALIZED  = 0x00000080   # TES4 only: plugin uses string tables
FLAG_LIGHT      = 0x00000200   # ESL-flagged
FLAG_COMPRESSED = 0x00040000


# ── Data Classes ────────────────────────────────────────────────────────

@dataclass(slots=True)
class SubRecord:
    """A single subrecord: 4-char type + raw data bytes."""
    type: str
    data: bytes

    @property
    def size(self) -> int:
        return len(self.data)

    def __repr__(self) -> str:
        return f"SubRecord({self.type!r}, {self.size} bytes)"


@dataclass(slots=True)
class RecordEntry:
    """Header metadata for one record. Payload is read on demand."""
    type: str
    data_size: int
    flags: int
    formid: int
    version: int
    file_offset: int    # start of the 24-byte header in file
    data_offset: int    # start of payload = file_offset + 24

    @property
    def is_compressed(self) -> bool:
        return bool(self.flags & FLAG_COMPRESSED)

    @property
    def is_deleted(self) -> bool:
        return bool(self.flags & FLAG_DELETED)

    @property
    def is_master_flagged(self) -> bool:
        return bool(self.flags & FLAG_MASTER)

    @property
    def is_light_flagged(self) -> bool:
        return bool(self.flags & FLAG_LIGHT)

    def __repr__(self) -> str:
        return (f"Record({self.type}, 0x{self.formid:08X}, "
                f"{self.data_size}B, off=0x{self.file_offset:X})")


@dataclass(slots=True)
class GrupEntry:
    """Metadata for one GRUP container."""
    label: bytes         # raw 4-byte label (meaning varies by group_type)
    group_type: int      # 0-10
    size: int            # total bytes including this 24-byte header
    file_offset: int     # start of "GRUP" in file
    data_offset: int     # start of children = file_offset + 24

    @property
    def data_end(self) -> int:
        """Byte offset where this GRUP ends."""
        return self.file_offset + self.size

    @property
    def label_as_type(self) -> str:
        """Type-0 groups: label is a 4-char record-type signature."""
        if self.group_type == 0:
            return self.label.decode('ascii', errors='replace').rstrip('\x00')
        return ''

    @property
    def label_as_formid(self) -> int:
        """Types 1, 6-10: label is a FormID."""
        return _FMT_U32.unpack(self.label)[0]

    @property
    def label_as_block(self) -> tuple[int, int]:
        """Types 2-3: label is a block/sub-block number (int32).
        Types 4-5: label is grid coords (y<<16 | x, signed int16 each)."""
        if self.group_type in (2, 3):
            return (struct.unpack('<i', self.label)[0], 0)
        raw = _FMT_U32.unpack(self.label)[0]
        y = (raw >> 16) & 0xFFFF
        x = raw & 0xFFFF
        if y >= 0x8000:
            y -= 0x10000
        if x >= 0x8000:
            x -= 0x10000
        return (x, y)

    def __repr__(self) -> str:
        if self.group_type == 0:
            return f"GRUP({self.label_as_type}, {self.size:,}B)"
        return f"GRUP(type={self.group_type}, {self.size:,}B)"


@dataclass
class TES4Header:
    """Parsed TES4 file header with extracted fields."""
    flags: int
    formid: int
    version: int
    masters: list[str]
    author: str | None
    description: str | None
    record_count: int
    next_object_id: int
    hedr_version: float
    raw_subrecords: list[SubRecord]

    @property
    def is_localized(self) -> bool:
        return bool(self.flags & FLAG_LOCALIZED)

    @property
    def is_master_flagged(self) -> bool:
        return bool(self.flags & FLAG_MASTER)

    @property
    def is_light_flagged(self) -> bool:
        return bool(self.flags & FLAG_LIGHT)

    @property
    def master_count(self) -> int:
        return len(self.masters)


# ── Subrecord Parsing ───────────────────────────────────────────────────

def parse_subrecords(data: bytes) -> list[SubRecord]:
    """Parse a byte buffer into SubRecords, handling XXXX oversize markers."""
    result: list[SubRecord] = []
    pos = 0
    length = len(data)
    xxxx_size: int | None = None

    while pos + SUBRECORD_HEADER_SIZE <= length:
        type_bytes, declared_size = _FMT_SUBREC.unpack_from(data, pos)
        type_str = type_bytes.decode('ascii', errors='replace')
        pos += SUBRECORD_HEADER_SIZE

        # XXXX marker: the *next* subrecord's real size is in this 4-byte payload
        if type_str == 'XXXX' and declared_size == 4:
            xxxx_size = _FMT_U32.unpack_from(data, pos)[0]
            pos += declared_size
            continue

        actual_size = xxxx_size if xxxx_size is not None else declared_size
        xxxx_size = None

        end = pos + actual_size
        if end > length:
            # Truncated -- take what's available
            result.append(SubRecord(type=type_str, data=data[pos:length]))
            break

        result.append(SubRecord(type=type_str, data=data[pos:end]))
        pos = end

    return result


def _decompress(raw: bytes) -> bytes:
    """Decompress a compressed record's payload.
    First 4 bytes = decompressed size, rest = zlib stream."""
    if len(raw) < 4:
        raise ValueError("Compressed record payload too short")
    decompressed_size = _FMT_U32.unpack_from(raw)[0]
    return zlib.decompress(raw[4:], bufsize=decompressed_size)


def _extract_edid(data: bytes) -> str | None:
    """Scan raw subrecord bytes for the first EDID and return the string."""
    pos = 0
    length = len(data)
    xxxx_size: int | None = None

    while pos + SUBRECORD_HEADER_SIZE <= length:
        type_bytes = data[pos:pos + 4]
        declared_size = _FMT_U16.unpack_from(data, pos + 4)[0]
        pos += SUBRECORD_HEADER_SIZE

        if type_bytes == b'XXXX' and declared_size == 4:
            xxxx_size = _FMT_U32.unpack_from(data, pos)[0]
            pos += declared_size
            continue

        actual_size = xxxx_size if xxxx_size is not None else declared_size
        xxxx_size = None

        if type_bytes == b'EDID':
            end = min(pos + actual_size, length)
            return data[pos:end].decode('ascii', errors='replace').rstrip('\x00')

        pos += actual_size

    return None


# ── Main Reader ─────────────────────────────────────────────────────────

class ESPReader:
    """Streaming reader for Skyrim SE ESP/ESM/ESL binary files.

    Uses seek-based I/O so large files are never loaded entirely into RAM.
    Record payloads are read on demand via read_subrecords() / read_edid().

    Usage::

        with ESPReader("Skyrim.esm") as reader:
            print(reader.tes4.masters)
            for grup in reader.iter_top_groups():
                print(grup)
                for rec in reader.iter_records_in(grup):
                    edid = reader.read_edid(rec)
    """

    def __init__(self, filepath: str | Path):
        self.filepath = Path(filepath)
        self._fh: BinaryIO | None = None
        self._file_size: int = 0
        self.tes4: TES4Header | None = None
        self._data_start: int = 0   # byte offset where GRUPs begin

    # ── Lifecycle ────────────────────────────────────────────────────

    def open(self) -> ESPReader:
        """Open the file and parse the TES4 header."""
        self._fh = open(self.filepath, 'rb')
        self._file_size = self.filepath.stat().st_size
        self.tes4 = self._read_tes4()
        return self

    def close(self) -> None:
        if self._fh:
            self._fh.close()
            self._fh = None

    def __enter__(self) -> ESPReader:
        return self.open()

    def __exit__(self, *exc) -> None:
        self.close()

    # ── Low-level I/O ────────────────────────────────────────────────

    def _read_at(self, offset: int, n: int) -> bytes:
        """Read exactly *n* bytes starting at *offset*."""
        self._fh.seek(offset)
        data = self._fh.read(n)
        if len(data) < n:
            raise EOFError(
                f"Wanted {n} bytes at 0x{offset:X}, got {len(data)} "
                f"(file size 0x{self._file_size:X})"
            )
        return data

    # ── TES4 Header ──────────────────────────────────────────────────

    def _read_tes4(self) -> TES4Header:
        buf = self._read_at(0, HEADER_SIZE)
        type_sig, data_size, flags, formid, _rev, version, _unk = \
            _FMT_RECORD.unpack(buf)

        if type_sig != b'TES4':
            raise ValueError(
                f"Not a valid plugin file: expected TES4, got {type_sig!r}"
            )

        payload = self._read_at(HEADER_SIZE, data_size)
        self._data_start = HEADER_SIZE + data_size

        subs = parse_subrecords(payload)

        masters: list[str] = []
        author: str | None = None
        description: str | None = None
        record_count = 0
        next_object_id = 0
        hedr_version = 0.0

        for sr in subs:
            if sr.type == 'HEDR' and sr.size >= 12:
                hedr_version, record_count, next_object_id = \
                    _FMT_HEDR.unpack_from(sr.data)
            elif sr.type == 'MAST':
                masters.append(
                    sr.data.decode('ascii', errors='replace').rstrip('\x00'))
            elif sr.type == 'CNAM':
                author = sr.data.decode('utf-8', errors='replace').rstrip('\x00')
            elif sr.type == 'SNAM':
                description = sr.data.decode('utf-8', errors='replace').rstrip('\x00')

        return TES4Header(
            flags=flags, formid=formid, version=version,
            masters=masters, author=author, description=description,
            record_count=record_count, next_object_id=next_object_id,
            hedr_version=hedr_version, raw_subrecords=subs,
        )

    # ── Header Readers ───────────────────────────────────────────────

    def _record_at(self, offset: int) -> RecordEntry:
        """Read a record header at the given file offset."""
        buf = self._read_at(offset, HEADER_SIZE)
        type_sig, data_size, flags, formid, _rev, version, _unk = \
            _FMT_RECORD.unpack(buf)
        return RecordEntry(
            type=type_sig.decode('ascii', errors='replace'),
            data_size=data_size,
            flags=flags,
            formid=formid,
            version=version,
            file_offset=offset,
            data_offset=offset + HEADER_SIZE,
        )

    def _grup_at(self, offset: int) -> GrupEntry:
        """Read a GRUP header at the given file offset."""
        buf = self._read_at(offset, HEADER_SIZE)
        _sig, size, label, group_type, _stamp, _unk1, _ver, _unk2 = \
            _FMT_GRUP.unpack(buf)
        return GrupEntry(
            label=label,
            group_type=group_type,
            size=size,
            file_offset=offset,
            data_offset=offset + HEADER_SIZE,
        )

    # ── Iteration ────────────────────────────────────────────────────

    def iter_top_groups(self) -> Iterator[GrupEntry]:
        """Yield every top-level GRUP in the file."""
        pos = self._data_start
        while pos + HEADER_SIZE <= self._file_size:
            sig = self._read_at(pos, 4)
            if sig != b'GRUP':
                break
            grup = self._grup_at(pos)
            if grup.size < HEADER_SIZE:
                break  # malformed
            yield grup
            pos = grup.data_end

    def iter_grup_contents(
        self, grup: GrupEntry,
    ) -> Iterator[RecordEntry | GrupEntry]:
        """Yield the immediate children of a GRUP (records and sub-GRUPs).
        Does NOT recurse into sub-GRUPs."""
        pos = grup.data_offset
        end = grup.data_end
        while pos + HEADER_SIZE <= end:
            sig = self._read_at(pos, 4)
            if sig == b'GRUP':
                child = self._grup_at(pos)
                if child.size < HEADER_SIZE:
                    break
                yield child
                pos = child.data_end
            else:
                rec = self._record_at(pos)
                yield rec
                pos = rec.data_offset + rec.data_size

    def iter_records_in(self, grup: GrupEntry) -> Iterator[RecordEntry]:
        """Yield all records inside a GRUP, recursing into sub-GRUPs."""
        pos = grup.data_offset
        end = grup.data_end
        while pos + HEADER_SIZE <= end:
            try:
                sig = self._read_at(pos, 4)
            except (EOFError, OSError):
                break
            if sig == b'GRUP':
                try:
                    child = self._grup_at(pos)
                except (EOFError, OSError, struct.error):
                    break
                if child.size < HEADER_SIZE:
                    break
                yield from self.iter_records_in(child)
                pos = child.data_end
            else:
                try:
                    rec = self._record_at(pos)
                except (EOFError, OSError, struct.error):
                    break
                yield rec
                pos = rec.data_offset + rec.data_size

    def iter_all_records(self) -> Iterator[RecordEntry]:
        """Yield every record in the file (flat), excluding TES4."""
        for grup in self.iter_top_groups():
            yield from self.iter_records_in(grup)

    # ── Record Data Access ───────────────────────────────────────────

    def read_record_data(self, record: RecordEntry) -> bytes:
        """Read the raw payload of a record, decompressing if needed."""
        raw = self._read_at(record.data_offset, record.data_size)
        if record.is_compressed:
            return _decompress(raw)
        return raw

    def read_subrecords(self, record: RecordEntry) -> list[SubRecord]:
        """Read and parse all subrecords from a record."""
        data = self.read_record_data(record)
        return parse_subrecords(data)

    def read_edid(self, record: RecordEntry) -> str | None:
        """Read just the Editor ID from a record.

        Optimised for scanning: for uncompressed records, peeks at the
        first subrecord without reading the full payload.
        """
        if record.data_size == 0:
            return None

        if record.is_compressed:
            try:
                data = self.read_record_data(record)
            except (zlib.error, ValueError):
                return None
            return _extract_edid(data)

        # Uncompressed: EDID is almost always the first subrecord.
        # Peek at up to 262 bytes (6-byte header + up to 256 chars).
        peek_len = min(record.data_size, 262)
        if peek_len < SUBRECORD_HEADER_SIZE:
            return None

        peek = self._read_at(record.data_offset, peek_len)
        first_type = peek[0:4]
        first_size = _FMT_U16.unpack_from(peek, 4)[0]

        if first_type == b'EDID' and SUBRECORD_HEADER_SIZE + first_size <= len(peek):
            return peek[6:6 + first_size].decode(
                'ascii', errors='replace'
            ).rstrip('\x00')

        # EDID wasn't first -- fall back to full scan
        if peek_len < record.data_size:
            full = self._read_at(record.data_offset, record.data_size)
            return _extract_edid(full)
        return _extract_edid(peek)
