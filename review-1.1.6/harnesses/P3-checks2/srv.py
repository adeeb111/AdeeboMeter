import socket, ssl, threading, struct, time, sys
# 1) fake NTP on 127.0.0.1:12123
def ntp():
    s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); s.bind(('127.0.0.1',12123))
    while True:
        d,a=s.recvfrom(100)
        now=time.time()+2208988800
        sec=int(now); frac=int((now-sec)*2**32)
        ts=struct.pack('>II',sec,frac)
        r=bytes([0x24,2,6,0xe9])+b'\0'*8+bytes([10,0,0,1])+ts+d[40:48]+ts+ts
        s.sendto(r,a)
# 2) fake DNS on 127.0.0.1:53: MX answer for any question
def dns():
    s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); s.bind(('127.0.0.1',53))
    while True:
        d,a=s.recvfrom(1500)
        q=d[12:]; i=0
        while q[i]: i+=q[i]+1
        qt=q[i+1:i+3]; qs=q[:i+5]
        if qt==b'\x00\x0f':
            rd=b'\x00\x0a'+b'\x02mx\x07example\x03com\x00'
            ans=b'\xc0\x0c\x00\x0f\x00\x01\x00\x00\x01\x2c'+struct.pack('>H',len(rd))+rd
        elif qt==b'\x00\x02':
            rd=b'\x03ns1\x07example\x03com\x00'
            ans=b'\xc0\x0c\x00\x02\x00\x01\x00\x00\x01\x2c'+struct.pack('>H',len(rd))+rd
        else:
            ans=b'\xc0\x0c\x00\x01\x00\x01\x00\x00\x00\x3c\x00\x04'+bytes([127,0,0,1])
        r=d[:2]+b'\x81\x80'+b'\x00\x01\x00\x01\x00\x00\x00\x00'+qs+ans
        s.sendto(r,a)
# 3) TCP stall server on 127.0.0.1:12124 (accepts, never answers)
def stall():
    s=socket.socket(); s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1); s.bind(('127.0.0.1',12124)); s.listen(50); keep=[]
    while True:
        c,a=s.accept(); keep.append(c)
# 4) self-signed TLS on 127.0.0.1:443
def tls():
    ctx=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); ctx.load_cert_chain('c.pem','k.pem')
    s=socket.socket(); s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1); s.bind(('127.0.0.1',443)); s.listen(50)
    while True:
        c,a=s.accept()
        try:
            cc=ctx.wrap_socket(c,server_side=True); cc.recv(10); cc.close()
        except Exception as e: pass
for f in (ntp,dns,stall,tls): threading.Thread(target=f,daemon=True).start()
time.sleep(100000)
