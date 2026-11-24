import socket
s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); s.bind(('127.0.0.53',53))
while True:
    d,a=s.recvfrom(512)
    q=d[12:]; i=0
    while q[i]: i+=q[i]+1
    qs=q[:i+5]
    r=d[:2]+b'\x81\x80'+b'\x00\x01\x00\x01\x00\x00\x00\x00'+qs+b'\xc0\x0c\x00\x01\x00\x01\x00\x00\x00\x3c\x00\x04'+bytes([93,184,216,34])
    s.sendto(r,a)
