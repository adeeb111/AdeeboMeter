# hostile SNMP v2c agent for testing; mode = community string
import socket, sys, time, struct
def enc_len(n):
    if n < 128: return bytes([n])
    b = n.to_bytes((n.bit_length()+7)//8, 'big'); return bytes([0x80|len(b)]) + b
def tlv(t, v): return bytes([t]) + enc_len(len(v)) + v
def enc_int(v, t=2):
    if t == 2:
        n = max(1, (v.bit_length()+8)//8); return tlv(t, v.to_bytes(n, 'big', signed=True))
    b = v.to_bytes(max(1,(v.bit_length()+7)//8), 'big')
    if b[0] & 0x80: b = b'\x00' + b
    return tlv(t, b)
def enc_oid(s):
    p = [int(x) for x in s.split('.')]; out = bytes([p[0]*40+p[1]])
    for x in p[2:]:
        st = [x & 0x7f]; x >>= 7
        while x: st.append(0x80 | (x & 0x7f)); x >>= 7
        out += bytes(reversed(st))
    return tlv(6, out)
def dec(b, i):
    t = b[i]; l = b[i+1]; i += 2
    if l & 0x80:
        n = l & 0x7f; l = int.from_bytes(b[i:i+n], 'big'); i += n
    return t, b[i:i+l], i+l
def dec_oid(v):
    p = [v[0]//40, v[0]%40]; x = 0
    for c in v[1:]:
        x = (x<<7) | (c & 0x7f)
        if not c & 0x80: p.append(x); x = 0
    return '.'.join(map(str,p))
def okey(o): return [int(x) for x in o.split('.')]
T0 = time.time(); walks = {}; gets = {}
def mib(mode):
    m = {}
    up = int((time.time()-T0)*100)
    m['1.3.6.1.2.1.1.3.0'] = (0x43, up)
    if mode == 'ifreboot':
        r = gets.get('ifreboot', 0)
        m['1.3.6.1.2.1.1.3.0'] = (0x43, 100 if r <= 1 else 360000)
        for i in range(1, 4): m['1.3.6.1.2.1.2.2.1.14.%d'%i] = (0x41, 50 if r <= 1 else 80)
    m['1.3.6.1.2.1.1.5.0'] = (4, b'hostile')
    for i in range(1, 4):
        m['1.3.6.1.2.1.2.2.1.2.%d'%i] = (4, b'port%d'%i)
        m['1.3.6.1.2.1.2.2.1.5.%d'%i] = (0x42, 1000000000)
        m['1.3.6.1.2.1.2.2.1.7.%d'%i] = (2, 1); m['1.3.6.1.2.1.2.2.1.8.%d'%i] = (2, 1)
    return m
def counters(mode, col, n):
    # n = how many times this column's walk has started (1 = first sample)
    if mode == 't32wrap':   p = 2**32 - 100_000_000; return (p + (n-1)*30_000_000) % 2**32   # +300 MB over ~5 s = 480 Mb/s, wraps
    if mode == 't32reset':  return 3_000_000_000 if n == 1 else 1_000_000                       # counter reset to ~0
    if mode == 't32reboottop': return 2**32 - 1000 if n == 1 else 5000                           # reboot while the counter was near the top
    if mode == 't32nospeed': return 3_000_000_000 if n == 1 else 1_000_000
    return 1000*n
def serve(port):
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM); s.bind(('127.0.0.1', port))
    spoof = socket.socket(socket.AF_INET, socket.SOCK_DGRAM); spoof.bind(('127.0.0.1', port+1))
    while True:
        data, addr = s.recvfrom(65535)
        try:
            t, v, _ = dec(data, 0); i = 0
            _, ver, i = dec(v, i); _, comm, i = dec(v, i); pt, pdu, i = dec(v, i)
            mode = comm.decode()
            j = 0; _, rid, j = dec(pdu, j); _, a, j = dec(pdu, j); _, b, j = dec(pdu, j); _, vbl, j = dec(pdu, j)
            rid = int.from_bytes(rid, 'big', signed=True); maxrep = int.from_bytes(b, 'big')
            oids = []; k = 0
            while k < len(vbl):
                _, vb, k = dec(vbl, k); _, o, _ = dec(vb, 0); oids.append(dec_oid(o))
        except Exception as e:
            print('bad req', e); continue
        m = mib(mode)
        def build(vbs, rid_=rid, comm_=comm):
            body = b''.join(tlv(0x30, enc_oid(o) + (tlv(ty, val) if isinstance(val, bytes) else enc_int(val, ty))) for o, ty, val in vbs)
            p = tlv(0xA2, enc_int(rid_) + enc_int(0) + enc_int(0) + tlv(0x30, body))
            return tlv(0x30, enc_int(1) + tlv(4, comm_) + p)
        out = []
        if pt == 0xA0 and oids == ['1.3.6.1.2.1.1.3.0']: gets[mode] = gets.get(mode, 0) + 1; m = mib(mode)
        if pt == 0xA0:
            for o in oids:
                if o in m: out.append((o,)+m[o])
                else: out.append((o, 0x80, b''))
        else:
            start = oids[0]
            if mode.startswith('t32') and start.startswith('1.3.6.1.2.1.31'):
                out = [('1.3.6.1.2.1.31.9.1', 4, b'x')]   # no ifXTable
            elif mode.startswith('t32') and start in ('1.3.6.1.2.1.2.2.1.10', '1.3.6.1.2.1.2.2.1.16'):
                walks[(mode, start)] = walks.get((mode, start), 0) + 1; n = walks[(mode, start)]
                out = [(start + '.%d'%i, 0x41, counters(mode, start, n)) for i in (1,)] + [('1.3.6.1.2.1.2.2.1.99.1', 0x41, 0)]
            elif mode.startswith('t32') and start == '1.3.6.1.2.1.2.2.1.5':
                out = [] if mode == 't32nospeed' else [(start+'.1', 0x42, 100_000_000)]
                out.append(('1.3.6.1.2.1.2.2.1.6.1', 4, b'\x00\x11\x22\x33\x44\x55'))
            elif mode == 'loop':
                base = start.split('.')[:10]; n = int(start.split('.')[-1]) if start.count('.') >= 10 else 0
                out = [('.'.join(base[:10]) + '.%d' % ((n % 5) + 1), 2, 1)]   # 1,2,3,4,5,1,2...
            elif mode == 'same':
                out = [(start + '.1', 2, 7)] if start.count('.') < 10 else [(start, 2, 7)]
            elif mode == 'backwards':
                out = [(start + '.5', 2, 1), (start + '.3', 2, 1)] if start.count('.') < 10 else [(start, 2, 1)]
            elif mode == 'empty':
                out = []
            elif mode == 'huge':
                n = int(start.split('.')[-1]) if start.count('.') >= 10 else 0
                out = [(start.rsplit('.',1)[0] + '.%d' % (n+1) if start.count('.') >= 10 else start + '.1', 4, b'A'*60000)] if n < 3000 else []
            elif mode == 'manyfast':
                n = int(start.split('.')[-1]) if start.count('.') >= 10 else 0
                out = [(start.rsplit('.',1)[0] + '.%d' % (n+i) if start.count('.') >= 10 else start + '.%d' % (n+i), 2, i) for i in range(1, 26)]
            elif mode == 'nextcol':   # mid-walk an OID in another column = end
                out = [(start + '.1', 2, 1), ('1.3.6.1.2.1.2.2.1.9.1', 2, 1)] if start.count('.') < 10 else []
            else:
                keys = sorted(m, key=okey); nk = [k for k in keys if okey(k) > okey(start)][:maxrep]
                out = [(k,)+m[k] for k in nk] or [(start, 0x82, b'')]
        try: reply = build(out)
        except Exception as e: print("build", e); continue
        if mode == 'garbagefirst': s.sendto(b'\x00hello', addr)
        if mode == 'wrongcommfirst': s.sendto(build(out, comm_=b'other'), addr)
        if mode == 'wrongidfirst': s.sendto(build(out, rid_=rid ^ 0x55), addr)
        if mode == 'spoofport': spoof.sendto(reply, addr); continue
        if mode == 'wrongidonly': s.sendto(build(out, rid_=rid ^ 0x55), addr); continue
        if len(reply) > 65507: reply = reply[:65507]
        s.sendto(reply, addr)
serve(int(sys.argv[1]))
