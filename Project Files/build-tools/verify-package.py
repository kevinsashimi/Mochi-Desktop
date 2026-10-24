#!/usr/bin/env python3
"""Check native Windows resources in the actual EXE, without executing it.

Usage: python3 verify-package.py path/to/Mochi.exe path/to/source/app.manifest
       [--update-manifest path/to/update.xml] [--require-certificate-table]
The cloud's former mcs compiler silently omitted -win32manifest. Checking the
source file or compiler arguments alone cannot detect that packaging failure.
"""
import argparse
import base64
import binascii
import hashlib
import hmac
import re
import struct
import sys
from pathlib import Path
import xml.etree.ElementTree as ET


UPDATE_MODULUS = int.from_bytes(base64.b64decode(
    'xouWeDgE9/Lng9LSI/nnbajT+W7tWt0pt5K8rsyehyjkOzH6w8WNTiAkkkhVAjR6CL7DJdAxaAyucnMhp2ck8rsCuwydqpBE5JANY7rC/MHCgOE4yIxtEnn92yBp51ca7z1/GW/62MlhsUhjeQKtqnAf6Ote3pg2oCGPGsVHrjM9cVA96QA1/UkzmqrUmdHKwZBZAdAstm5tnwl+Sr4cZBR51vqa8uYXA/PbfPALa8ddj0KZrk1WxHrZPLwNf+abJOxU17p9QMfTx2D8raBmF5DmLOVXEea7yPuF4qDNUfi6reY+p+xrOs5dV0/pcSZOy8TcXCMnbEa+KDhm0cPHVTHzDTaob1i2+aod+QfYqziwxBL7CeqaW2Ceg2TR6L2CpQ3Z5ZrLm7HcFYhi+jd45UNgsyHU2vrSP+l1WDCVQEKhwmjgMZEdEskLW/eoJfiCONCTvQssm56pSMigb2KDBew4gSVxTh4fmwmkfhUJWS6YQck/tM50K0xLGXx7uzaO/5p3ZpA/P+u+aRbR7hg5JHoj1KZsCcWeRUyUawjfWbZMG8plrygXYLwP+1FmEtygsiGLRAt00N23Ju5kcwOVJqtu+OuwtM/3ciRi7MWw71qc9tJDA7orBl8LtLeyYXqkIjmKATof4wD3pcKp+TECRo56x92oBcejh63xJ2fo8Ns='), 'big')
UPDATE_EXPONENT = 65537
TEST_HOOK_MARKER = b'Mochi.TestHooks.Enabled'


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


def authenticode_size(data):
    """Return the PE certificate-table size (the table uses a file offset)."""
    if data[:2] != b'MZ':
        raise ValueError('Not a Windows executable')
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    if data[pe:pe + 4] != b'PE\0\0':
        raise ValueError('Missing PE header')
    optional = pe + 24
    magic = struct.unpack_from('<H', data, optional)[0]
    if magic not in (0x10b, 0x20b):
        raise ValueError('Unsupported PE header')
    directories = optional + (96 if magic == 0x10b else 112)
    certificate_offset, certificate_size = struct.unpack_from('<II', data, directories + 4 * 8)
    if certificate_size and (certificate_offset <= 0 or certificate_offset + certificate_size > len(data)):
        raise ValueError('Invalid Authenticode certificate table')
    return certificate_size


def managed_identity(data):
    """Read the Assembly table from ECMA-335 metadata without third-party modules."""
    if data[:2] != b'MZ':
        raise ValueError('Not a Windows executable')
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    optional = pe + 24
    magic = struct.unpack_from('<H', data, optional)[0]
    if data[pe:pe + 4] != b'PE\0\0' or magic not in (0x10b, 0x20b):
        raise ValueError('Missing or unsupported PE header')
    section_start = optional + struct.unpack_from('<H', data, pe + 20)[0]
    sections = []
    for index in range(struct.unpack_from('<H', data, pe + 6)[0]):
        section = section_start + index * 40
        sections.append((struct.unpack_from('<I', data, section + 12)[0],
                         max(struct.unpack_from('<I', data, section + 8)[0],
                             struct.unpack_from('<I', data, section + 16)[0]),
                         struct.unpack_from('<I', data, section + 20)[0]))

    def file_offset(rva):
        for start, size, raw in sections:
            if start <= rva < start + size:
                return raw + rva - start
        raise ValueError('Managed metadata address is outside the PE sections')

    directories = optional + (96 if magic == 0x10b else 112)
    cli_rva, cli_size = struct.unpack_from('<II', data, directories + 14 * 8)
    if not cli_rva or cli_size < 16:
        raise ValueError('Executable has no managed CLI header')
    cli = file_offset(cli_rva)
    metadata_rva, metadata_size = struct.unpack_from('<II', data, cli + 8)
    metadata = file_offset(metadata_rva)
    if data[metadata:metadata + 4] != b'BSJB' or metadata_size < 24:
        raise ValueError('Executable has invalid managed metadata')
    version_length = struct.unpack_from('<I', data, metadata + 12)[0]
    stream_header = (metadata + 16 + version_length + 3) & ~3
    stream_count = struct.unpack_from('<H', data, stream_header + 2)[0]
    cursor = stream_header + 4
    streams = {}
    for _ in range(stream_count):
        relative, size = struct.unpack_from('<II', data, cursor)
        name_start = cursor + 8
        name_end = data.find(b'\0', name_start, min(name_start + 32, len(data)))
        if name_end < 0:
            raise ValueError('Managed metadata has an invalid stream name')
        streams[data[name_start:name_end].decode('ascii')] = (metadata + relative, size)
        cursor = (name_end + 4) & ~3
    table_stream = streams.get('#~') or streams.get('#-')
    strings_stream = streams.get('#Strings')
    if table_stream is None or strings_stream is None:
        raise ValueError('Managed metadata is missing tables or strings')
    tables, table_size = table_stream
    if table_size < 24:
        raise ValueError('Managed table stream is truncated')
    heap_sizes = data[tables + 6]
    valid = struct.unpack_from('<Q', data, tables + 8)[0]
    rows = [0] * 64
    cursor = tables + 24
    for table in range(64):
        if valid & (1 << table):
            rows[table] = struct.unpack_from('<I', data, cursor)[0]
            cursor += 4

    string_index = 4 if heap_sizes & 1 else 2
    guid_index = 4 if heap_sizes & 2 else 2
    blob_index = 4 if heap_sizes & 4 else 2

    def simple(table):
        return 4 if rows[table] >= 65536 else 2

    def coded(tables_used, tag_bits):
        limit = 1 << (16 - tag_bits)
        return 4 if max(rows[table] for table in tables_used) >= limit else 2

    resolution_scope = coded((0, 26, 35, 1), 2)
    type_def_or_ref = coded((2, 1, 27), 2)
    member_ref_parent = coded((2, 1, 26, 6, 27), 3)
    has_constant = coded((4, 8, 23), 2)
    has_custom_attribute = coded((6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17,
                                  26, 27, 32, 35, 38, 39, 40, 42, 44, 43), 5)
    custom_attribute_type = coded((6, 10), 3)
    has_field_marshal = coded((4, 8), 1)
    has_decl_security = coded((2, 6, 32), 2)
    has_semantics = coded((20, 23), 1)
    method_def_or_ref = coded((6, 10), 1)
    member_forwarded = coded((4, 6), 1)
    row_sizes = {
        0: 2 + string_index + 3 * guid_index,
        1: resolution_scope + 2 * string_index,
        2: 4 + 2 * string_index + type_def_or_ref + simple(4) + simple(6),
        3: simple(4), 4: 2 + string_index + blob_index, 5: simple(6),
        6: 8 + string_index + blob_index + simple(8), 7: simple(8),
        8: 4 + string_index, 9: simple(2) + type_def_or_ref,
        10: member_ref_parent + string_index + blob_index,
        11: 2 + has_constant + blob_index,
        12: has_custom_attribute + custom_attribute_type + blob_index,
        13: has_field_marshal + blob_index,
        14: 2 + has_decl_security + blob_index,
        15: 6 + simple(2), 16: 4 + simple(4), 17: blob_index,
        18: simple(2) + simple(20), 19: simple(20),
        20: 2 + string_index + type_def_or_ref,
        21: simple(2) + simple(23), 22: simple(23),
        23: 2 + string_index + blob_index,
        24: 2 + simple(6) + has_semantics,
        25: simple(2) + 2 * method_def_or_ref, 26: string_index,
        27: blob_index, 28: 2 + member_forwarded + string_index + simple(26),
        29: 4 + simple(4), 30: 8, 31: 4,
    }
    if rows[32] != 1:
        raise ValueError('Managed executable must contain exactly one Assembly row')
    assembly = cursor + sum(rows[table] * row_sizes[table] for table in range(32))
    _, major, minor, build, revision, _ = struct.unpack_from('<IHHHHI', data, assembly)
    offset = assembly + 16 + blob_index
    name_index = int.from_bytes(data[offset:offset + string_index], 'little')
    strings, strings_size = strings_stream
    if name_index <= 0 or name_index >= strings_size:
        raise ValueError('Managed assembly has an invalid name index')
    name_start = strings + name_index
    name_end = data.find(b'\0', name_start, strings + strings_size)
    if name_end < 0:
        raise ValueError('Managed assembly name is unterminated')
    name = data[name_start:name_end].decode('utf-8')
    return name, '{}.{}.{}.{}'.format(major, minor, build, revision)


def normalized_version(text):
    if not re.fullmatch(r'[0-9]+(?:\.[0-9]+){2,3}', text or ''):
        raise ValueError('Invalid update version')
    values = [int(value) for value in text.split('.')]
    if any(value > 0x7fffffff for value in values) or values[0] < 1:
        raise ValueError('Invalid update version')
    if len(values) == 3:
        values.append(0)
    return '.'.join(str(value) for value in values)


def verify_rsa_sha256(payload, signature):
    key_bytes = (UPDATE_MODULUS.bit_length() + 7) // 8
    if len(signature) != key_bytes:
        return False
    signature_value = int.from_bytes(signature, 'big')
    if signature_value >= UPDATE_MODULUS:
        return False
    encoded = pow(signature_value, UPDATE_EXPONENT, UPDATE_MODULUS).to_bytes(key_bytes, 'big')
    digest_info = bytes.fromhex('3031300d060960864801650304020105000420') + hashlib.sha256(payload).digest()
    padding = b'\xff' * (key_bytes - len(digest_info) - 3)
    return hmac.compare_digest(encoded, b'\x00\x01' + padding + b'\x00' + digest_info)


def verify_update_manifest(executable, manifest_path, executable_data=None):
    if executable_data is None:
        executable_data = Path(executable).read_bytes()
    manifest_data = Path(manifest_path).read_bytes()
    if len(manifest_data) > 65536:
        raise ValueError('Update manifest is too large')
    root = ET.fromstring(manifest_data)
    expected = ['Version', 'File', 'Size', 'Sha256', 'Signature']
    if root.tag != 'MochiUpdate' or [child.tag for child in root] != expected:
        raise ValueError('Update manifest has an unexpected structure')
    if any(list(child) or child.attrib for child in root):
        raise ValueError('Update manifest fields must contain text only')
    fields = {child.tag: child.text or '' for child in root}
    version = normalized_version(fields['Version'])
    if fields['File'] != 'Mochi.exe':
        raise ValueError('Update manifest has an unexpected filename')
    if not re.fullmatch(r'[1-9][0-9]*', fields['Size']):
        raise ValueError('Update manifest has an invalid size')
    size = int(fields['Size'])
    if size <= 0 or size > 50 * 1024 * 1024:
        raise ValueError('Update manifest size is outside Mochi\'s accepted range')
    if size != len(executable_data):
        raise ValueError('Update manifest size does not match the executable')
    if not re.fullmatch(r'[0-9a-fA-F]{64}', fields['Sha256']):
        raise ValueError('Update manifest has an invalid SHA-256 value')
    digest = hashlib.sha256(executable_data).hexdigest()
    if not hmac.compare_digest(fields['Sha256'].lower(), digest):
        raise ValueError('Update manifest SHA-256 does not match the executable')
    canonical = ('MochiUpdate\nVersion=' + version + '\nFile=Mochi.exe\nSize=' + str(size) +
                 '\nSha256=' + fields['Sha256'].lower() + '\n').encode('utf-8')
    try:
        signature = base64.b64decode(fields['Signature'], validate=True)
    except (ValueError, binascii.Error):
        raise ValueError('Update manifest has an invalid signature encoding')
    if not verify_rsa_sha256(canonical, signature):
        raise ValueError('Update manifest signature is not valid for Mochi')
    assembly_name, assembly_version = managed_identity(executable_data)
    if assembly_name != 'Mochi' or assembly_version != version:
        raise ValueError('Executable assembly identity/version does not match the update manifest')
    print('PASS: update manifest matches the executable and has a valid pinned RSA-SHA256 signature.')


def verify(executable, expected_manifest, update_manifest=None, require_certificate_table=False):
    executable_data = Path(executable).read_bytes()
    if TEST_HOOK_MARKER in executable_data or TEST_HOOK_MARKER.decode().encode('utf-16le') in executable_data:
        raise ValueError('Release executable contains developer test hooks')
    native = resources(executable_data)
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
    if require_certificate_table and authenticode_size(executable_data) == 0:
        raise ValueError('Public release has no Authenticode certificate table')
    if update_manifest:
        verify_update_manifest(executable, update_manifest, executable_data)
    print('PASS: native process manifest matches source; Windows compatibility, DPI and asInvoker declarations; icon and version resources retained.')


if __name__ == '__main__':
    try:
        parser = argparse.ArgumentParser(description='Validate a Mochi release artifact.')
        parser.add_argument('executable')
        parser.add_argument('process_manifest')
        parser.add_argument('--update-manifest')
        parser.add_argument('--require-certificate-table', action='store_true',
                            help='require a PE certificate table; validate Windows trust separately')
        arguments = parser.parse_args()
        verify(arguments.executable, arguments.process_manifest, arguments.update_manifest,
               arguments.require_certificate_table)
    except (ValueError, OSError, ET.ParseError, struct.error) as error:
        sys.exit('FAIL: ' + str(error))
