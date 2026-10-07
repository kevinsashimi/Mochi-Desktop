#!/usr/bin/env python3
"""Check native Windows resources in the actual EXE, without executing it.

Usage: python3 verify-package.py path/to/Mochi.exe path/to/source/app.manifest
The cloud's former mcs compiler silently omitted -win32manifest. Checking the
source file or compiler arguments alone cannot detect that packaging failure.
"""
import struct
import sys
from pathlib import Path
import xml.etree.ElementTree as ET


def resources(data):
    def word(offset):
        return struct.unpack_from('<H', data, offset)[0]

    def dword(offset):
        return struct.unpack_from('<I', data, offset)[0]

    if data[:2] != b'MZ':
        raise ValueError('Not a Windows executable')
    pe = dword(0x3c)
    if data[pe:pe + 4] != b'PE\0\0':
        raise ValueError('Missing PE header')
    optional = pe + 24
    magic = word(optional)
    if magic not in (0x10b, 0x20b):
        raise ValueError('Unsupported PE header')
    section_start = optional + word(pe + 20)
    sections = []
    for i in range(word(pe + 6)):
        s = section_start + i * 40
        sections.append((dword(s + 12), max(dword(s + 8), dword(s + 16)), dword(s + 20)))

    def file_offset(rva):
        for start, size, raw in sections:
            if start <= rva < start + size:
                return raw + rva - start
        raise ValueError('Native resource address is outside the PE sections')

    root = file_offset(dword(optional + (96 if magic == 0x10b else 112) + 16))
    result = {}

    def walk(directory, path):
        if len(path) > 3:
            raise ValueError('Invalid native resource tree')
        count = word(directory + 12) + word(directory + 14)
        for i in range(count):
            name, target = struct.unpack_from('<II', data, directory + 16 + i * 8)
            key = path + (name,)
            if target & 0x80000000:
                walk(root + (target & 0x7fffffff), key)
            else:
                rva, size = struct.unpack_from('<II', data, root + target)
                offset = file_offset(rva)
                payload = data[offset:offset + size]
                if len(payload) != size:
                    raise ValueError('Truncated native resource')
                result[key] = payload

    walk(root, ())
    return result


def verify(executable, expected_manifest):
    native = resources(Path(executable).read_bytes())
    manifests = [v for k, v in native.items() if k[:2] == (24, 1)]
    if len(manifests) != 1:
        raise ValueError('Missing native process manifest (RT_MANIFEST, ID 1)')
    document = ET.fromstring(manifests[0])
    expected = ET.parse(expected_manifest).getroot()

    def content(element):
        return (element.tag, sorted(element.attrib.items()), (element.text or '').strip(),
                tuple(content(child) for child in element))

    if content(document) != content(expected):
        raise ValueError('Embedded manifest does not match app.manifest')
    ns = {'c': 'urn:schemas-microsoft-com:compatibility.v1',
          'v': 'urn:schemas-microsoft-com:asm.v3'}
    ids = {os.attrib.get('Id', '').lower() for os in document.findall('.//c:supportedOS', ns)}
    if '{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}' not in ids:
        raise ValueError('Windows 10/11 compatibility declaration is missing')
    level = document.find('.//v:requestedExecutionLevel', ns)
    if level is None or level.get('level') != 'asInvoker':
        raise ValueError('App must run without administrator privileges')
    for resource_type, label in ((3, 'icon image'), (14, 'icon group'), (16, 'version information')):
        if not any(k[0] == resource_type for k in native):
            raise ValueError('Missing ' + label)
    print('PASS: native process manifest matches source; Windows compatibility, DPI and asInvoker declarations; icon and version resources retained.')


if __name__ == '__main__':
    try:
        if len(sys.argv) != 3:
            raise ValueError('Usage: verify-package.py Mochi.exe app.manifest')
        verify(*sys.argv[1:])
    except (ValueError, OSError, ET.ParseError, struct.error) as error:
        sys.exit('FAIL: ' + str(error))
