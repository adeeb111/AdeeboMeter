import socket, time, threading, sys
s=socket.socket(); s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1); s.bind(('127.0.0.1',15201)); s.listen(1000)
conns=[]; ctl,_=s.accept(); ctl.recv(37)
t0=time.time()
def acc():
    while True:
        c,_=s.accept(); conns.append(c)
threading.Thread(target=acc,daemon=True).start()
while time.time()-t0<20:
    ctl.send(b'\x0a'); time.sleep(0.01)
print("stream connections opened in 20 s:", len(conns)); sys.stdout.flush()
