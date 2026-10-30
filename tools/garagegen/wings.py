"""Reading Wings 3D files (.wings: an Erlang external term, usually compressed): the objects with their winged edges,
faces and vertices, and the materials."""
import struct
import zlib


class Atom(str):
    pass


def parse_term(d, i):
    """The Erlang term at d[i:] and the index after it."""
    t = d[i]; i += 1
    if t == 97: return d[i], i + 1                                   # small integer
    if t == 98: return struct.unpack_from('>i', d, i)[0], i + 4      # integer
    if t == 70: return struct.unpack_from('>d', d, i)[0], i + 8      # float
    if t == 99: return float(d[i:i + 31].split(b'\0')[0]), i + 31    # old float (text)
    if t in (100, 118):                                              # atom
        n = struct.unpack_from('>H', d, i)[0]; return Atom(d[i + 2:i + 2 + n].decode('utf8')), i + 2 + n
    if t in (115, 119):                                              # small atom
        n = d[i]; return Atom(d[i + 1:i + 1 + n].decode('utf8')), i + 1 + n
    if t in (104, 105):                                              # tuple
        if t == 104: n = d[i]; i += 1
        else: n = struct.unpack_from('>I', d, i)[0]; i += 4
        items = []
        for _ in range(n):
            v, i = parse_term(d, i); items.append(v)
        return tuple(items), i
    if t == 106: return [], i                                        # empty list
    if t == 107:                                                     # string (list of bytes)
        n = struct.unpack_from('>H', d, i)[0]; return list(d[i + 2:i + 2 + n]), i + 2 + n
    if t == 108:                                                     # list
        n = struct.unpack_from('>I', d, i)[0]; i += 4
        items = []
        for _ in range(n):
            v, i = parse_term(d, i); items.append(v)
        _, i = parse_term(d, i)  # tail
        return items, i
    if t == 109:                                                     # binary
        n = struct.unpack_from('>I', d, i)[0]; return bytes(d[i + 4:i + 4 + n]), i + 4 + n
    if t == 77:                                                      # bit binary
        n = struct.unpack_from('>I', d, i)[0]; return bytes(d[i + 5:i + 5 + n]), i + 5 + n
    if t in (110, 111):                                              # big integer
        if t == 110: n = d[i]; i += 1
        else: n = struct.unpack_from('>I', d, i)[0]; i += 4
        sign = d[i]; i += 1
        v = int.from_bytes(d[i:i + n], 'little'); return (-v if sign else v), i + n
    if t == 116:                                                     # map
        n = struct.unpack_from('>I', d, i)[0]; i += 4
        m = {}
        for _ in range(n):
            k, i = parse_term(d, i); v, i = parse_term(d, i); m[k] = v
        return m, i
    raise ValueError(f'tag {t} at {i - 1}')


def load(path):
    """The term of a .wings file: ('wings', version, (shapes, materials, properties))."""
    d = open(path, 'rb').read()
    if not d.startswith(b'#!WINGS-1.0'):
        raise ValueError('not a wings file')
    i = 15 + 4
    assert d[i] == 131
    i += 1
    if d[i] == 80:
        size = struct.unpack_from('>I', d, i + 1)[0]
        body = zlib.decompress(d[i + 5:])
        assert len(body) == size
        term, _ = parse_term(body, 0)
    else:
        term, _ = parse_term(d, i)
    return term


def vertex(v):
    """The position of a vertex record."""
    if isinstance(v, bytes):
        return struct.unpack('>3d', v[:24])
    for item in v:
        if isinstance(item, bytes):
            return struct.unpack('>3d', item[:24])
        if isinstance(item, tuple) and item[0] == 'position':
            p = item[1]
            return struct.unpack('>3d', p[:24]) if isinstance(p, bytes) else p
    raise ValueError('vertex without a position')


def edge(e):
    """The winged edge of an edge record: (vs, ve, lf, rf, ltpr, ltsu, rtpr, rtsu)."""
    for item in e:
        if isinstance(item, tuple) and item[0] == 'edge':
            return item[1:]
        if isinstance(item, bytes) and len(item) >= 32:
            return struct.unpack('>8i', item[:32])
    raise ValueError('edge without a record')
