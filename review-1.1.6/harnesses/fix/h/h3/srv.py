import socket, threading, time, struct, sys
def http_echo(port):
    s=socket.socket(); s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1); s.bind(('127.0.0.1',port)); s.listen(5)
    while True:
        c,_=s.accept(); d=c.recv(65536)
        open('/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review4/h3/req.txt','ab').write(d+b'\n----\n')
        c.sendall(b"HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"); c.close()
def doh_drip(port):
    s=socket.socket(); s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1); s.bind(('127.0.0.1',port)); s.listen(5)
    while True:
        c,_=s.accept(); c.recv(65536)
        c.sendall(b"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: 60000\r\n\r\n")
        def drip(c=c):
            try:
                for i in range(60000): c.send(b'\0'); time.sleep(4)
            except Exception: pass
        threading.Thread(target=drip,daemon=True).start()
def redir(port):
    s=socket.socket(); s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1); s.bind(('127.0.0.1',port)); s.listen(5)
    while True:
        c,_=s.accept(); c.recv(65536); c.sendall(b"HTTP/1.1 302 Found\r\nLocation: file:///etc/passwd\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"); c.close()
def ntp(port):
    u=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); u.bind(('127.0.0.1',port))
    while True:
        d,a=u.recvfrom(100)
        r=bytearray(48); r[0]=0x24; r[1]=2; r[24:32]=d[40:48]  # origin = client's transmit; recv/xmit = 0
        u.sendto(bytes(r),a)
for f,p in [(http_echo,18080),(doh_drip,18081),(redir,18082),(ntp,18123)]: threading.Thread(target=f,args=(p,),daemon=True).start()
time.sleep(100000)
