#!/usr/bin/env python3
"""Package the already tested, committed EXE without rebuilding it."""
import argparse
import hashlib
import importlib.util
from pathlib import Path
import re
import shutil
import struct
import zipfile


def package(tag, output):
    if not re.fullmatch(r'v[1-9][0-9]*\.[0-9]+\.[0-9]+', tag):
        raise ValueError('Expected a stable vMAJOR.MINOR.PATCH tag')
    root = Path(__file__).resolve().parents[2]
    spec = importlib.util.spec_from_file_location('verify_package', Path(__file__).with_name('verify-package.py'))
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    source = root / 'Mochi.exe'
    data = source.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if (root / 'Mochi.exe.sha256').read_text().strip() != digest + '  Mochi.exe':
        raise ValueError('Committed executable checksum does not match')
    version = tag[1:]
    if verifier.managed_identity(data) != ('Mochi', version + '.0'):
        raise ValueError('Tag does not match the executable version')
    native = [v for k, v in verifier.resources(data).items() if k[0] == 16][0]
    offset = native.index(struct.pack('<I', 0xfeef04bd))
    ms, ls = struct.unpack_from('<II', native, offset + 8)
    if (ms >> 16, ms & 65535, ls >> 16, ls & 65535) != tuple(map(int, version.split('.'))) + (0,):
        raise ValueError('Native file version does not match the tag')
    if verifier.authenticode_size(data) != 0:
        raise ValueError('Use the signed-release process for signed executables')
    verifier.verify(source, root / 'Project Files/source/app.manifest')
    report = (root / 'Project Files/MANUAL-RELEASE-TEST-RESULTS.txt').read_bytes()
    if b'FAIL:' in report or digest.encode() not in report or ('Mochi Desktop ' + version).encode() not in report:
        raise ValueError('Test report does not identify this executable')
    readme = f'''Mochi Desktop {version}

Unsigned manual-download release for Windows 10/11 with .NET Framework 4.8.
Windows may show an unknown-publisher or SmartScreen warning.

Install or update
1. Quit any older Mochi running from the folder you want to use.
2. Extract this enclosing folder, or replace only Mochi.exe in your existing
   Mochi folder to keep your settings and optional Project Files.
3. Double-click Mochi.exe. Info should display Version {version}.

Mochi checks for newer published releases at startup and through Check for
updates. When a newer version is available, he offers its official GitHub page.
Download and install the new executable manually using the steps above.
Drafts and previews are excluded. No update.xml is included.

Official releases: https://github.com/kevinsashimi/Mochi-Desktop/releases
'''
    output.mkdir(parents=True, exist_ok=True)
    files = {'Mochi.exe': data, 'Mochi.exe.sha256': (digest + '  Mochi.exe\n').encode(),
             'READ-ME-FIRST.txt': readme.encode(), 'TEST-RESULTS.txt': report,
             'CHANGELOG.md': (root / 'CHANGELOG.md').read_bytes()}
    enclosing = 'Mochi-Desktop-' + version
    archive = output / (enclosing + '.zip')
    # Fixed timestamps and stored entries make local and CI ZIPs identical,
    # independent of filesystem timestamps and zlib versions. The EXE embeds
    # compressed artwork, so deflating it saves little space.
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_STORED) as zipped:
        for name, content in sorted(files.items()):
            info = zipfile.ZipInfo(enclosing + '/' + name, (2020, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            zipped.writestr(info, content)
    with zipfile.ZipFile(archive) as zipped:
        if zipped.testzip() is not None or zipped.read(enclosing + '/Mochi.exe') != data:
            raise ValueError('ZIP integrity check failed')
    shutil.copyfile(source, output / 'Mochi.exe')
    zip_hash = hashlib.sha256(archive.read_bytes()).hexdigest()
    (output / 'SHA256SUMS.txt').write_text(digest + '  Mochi.exe\n' + zip_hash + '  ' + archive.name + '\n')
    print('PASS: packaged the tested executable in ' + str(archive))
    print('EXE SHA256: ' + digest)
    print('ZIP SHA256: ' + zip_hash)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('tag')
    parser.add_argument('output', type=Path)
    arguments = parser.parse_args()
    package(arguments.tag, arguments.output)
