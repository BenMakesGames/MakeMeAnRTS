#!/usr/bin/env python3
"""Convert the BMPs written by MakeMeAnRTS's --screenshot mode into PNGs.

SDL can only save BMP without extra native dependencies, but BMPs are awkward to view and huge to
pass around. This uses nothing but the standard library so it works on a bare container.

    tools/bmp2png.py shot.bmp [out.png]
"""

import struct
import sys
import zlib
from pathlib import Path


def read_bmp(data: bytes) -> tuple[int, int, bytes]:
    """Return (width, height, RGB rows top-to-bottom) for the uncompressed BMPs SDL writes."""
    if data[:2] != b"BM":
        raise ValueError("not a BMP file")

    pixel_offset = struct.unpack_from("<I", data, 10)[0]
    header_size = struct.unpack_from("<I", data, 14)[0]
    width, height = struct.unpack_from("<ii", data, 18)
    bits_per_pixel = struct.unpack_from("<H", data, 28)[0]
    compression = struct.unpack_from("<I", data, 30)[0]

    if bits_per_pixel not in (24, 32):
        raise ValueError(f"only 24/32-bit BMPs are supported, got {bits_per_pixel}")
    # BI_RGB (0) and BI_BITFIELDS (3) both store raw pixels; SDL writes BGRA in either case.
    if compression not in (0, 3):
        raise ValueError(f"compressed BMPs are not supported (compression={compression})")

    bottom_up = height > 0
    height = abs(height)
    bytes_per_pixel = bits_per_pixel // 8
    stride = ((width * bits_per_pixel + 31) // 32) * 4

    rows = []
    for row_index in range(height):
        source = row_index if not bottom_up else height - 1 - row_index
        start = pixel_offset + source * stride
        row = data[start : start + width * bytes_per_pixel]
        rgb = bytearray(width * 3)
        for x in range(width):
            b, g, r = row[x * bytes_per_pixel : x * bytes_per_pixel + 3]
            rgb[x * 3 : x * 3 + 3] = bytes((r, g, b))
        rows.append(bytes(rgb))

    del header_size
    return width, height, b"".join(b"\x00" + row for row in rows)


def write_png(path: Path, width: int, height: int, filtered_rows: bytes) -> None:
    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + kind
            + payload
            + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
        )

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(filtered_rows, 9))
        + chunk(b"IEND", b"")
    )


def main(argv: list[str]) -> int:
    if not 2 <= len(argv) <= 3:
        print(__doc__, file=sys.stderr)
        return 2

    source = Path(argv[1])
    target = Path(argv[2]) if len(argv) == 3 else source.with_suffix(".png")

    width, height, rows = read_bmp(source.read_bytes())
    write_png(target, width, height, rows)
    print(f"{target} ({width}x{height})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
