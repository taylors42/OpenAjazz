#!/usr/bin/env python3
"""Inventory AJ199 candidate HID nodes through sysfs; never opens hidraw."""
import json
import os
from pathlib import Path


def report_sizes(data):
    """Count bits per report/type from HID short items, respecting global push/pop."""
    state = {"size": 0, "count": 0, "id": 0}
    stack, bits = [], {}
    pos = 0
    while pos < len(data):
        prefix = data[pos]
        pos += 1
        if prefix == 0xFE:
            if pos + 2 > len(data):
                raise ValueError("Truncated long item")
            length = data[pos]
            pos += 2 + length
            if pos > len(data):
                raise ValueError("Truncated long item payload")
            continue
        length = (0, 1, 2, 4)[prefix & 3]
        payload = data[pos:pos + length]
        if len(payload) != length:
            raise ValueError("Truncated short item")
        pos += length
        value = int.from_bytes(payload, "little")
        kind, tag = (prefix >> 2) & 3, prefix >> 4
        if kind == 1:
            if tag in (7, 8, 9):
                state[{7: "size", 8: "id", 9: "count"}[tag]] = value
            elif tag == 10:
                stack.append(state.copy())
            elif tag == 11:
                state = stack.pop()
        elif kind == 0 and tag in (8, 9, 11):
            key = (state["id"], {8: "input", 9: "output", 11: "feature"}[tag])
            bits[key] = bits.get(key, 0) + state["size"] * state["count"]
    return [
        {"report_id": f"0x{rid:02x}", "type": kind, "payload_bits": count,
         "payload_bytes": (count + 7) // 8}
        for (rid, kind), count in sorted(bits.items())
    ]


def inventory():
    result = []
    for path in sorted(Path('/sys/bus/hid/devices').glob('*')):
        fields = dict(line.split('=', 1) for line in
                      (path / 'uevent').read_text().splitlines() if '=' in line)
        if fields.get('HID_ID') not in (
            '0003:000025A7:0000FA7B', '0003:000025A7:0000FA7C'
        ):
            continue
        record = {"sysfs_id": path.name, "properties": fields, "hidraw_nodes": []}
        for node in sorted((path / 'hidraw').glob('hidraw*')):
            device = Path('/dev') / node.name
            record['hidraw_nodes'].append({
                "path": str(device),
                "current_user_read_write": os.access(device, os.R_OK | os.W_OK),
            })
        try:
            data = (path / 'report_descriptor').read_bytes()
            record.update(descriptor_hex=data.hex(), reports=report_sizes(data))
        except (OSError, ValueError, IndexError) as exc:
            record['descriptor_error'] = str(exc)
        result.append(record)
    return {"method": "sysfs only; no HID commands sent", "devices": result}


if __name__ == '__main__':
    print(json.dumps(inventory(), indent=2, ensure_ascii=False))
