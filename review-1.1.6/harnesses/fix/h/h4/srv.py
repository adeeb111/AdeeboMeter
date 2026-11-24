import ssl, socket, sys, threading
port=int(sys.argv[1]); chain=sys.argv[2]; key=sys.argv[3]; mode=sys.argv[4] if len(sys.argv)>4 else ""
ctx=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); ctx.load_cert_chain(chain,key)
ctx.maximum_version=ssl.TLSVersion.TLSv1_2 if "tls12" in mode else ssl.TLSVersion.TLSv1_3
if "mtls" in mode: ctx.verify_mode=ssl.CERT_REQUIRED; ctx.load_verify_locations(sys.argv[5])
s=socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR,1); s.bind(("127.0.0.1",port)); s.listen(5)
def h(c):
    try:
        cc=ctx.wrap_socket(c, server_side=True); cc.recv(10); cc.close()
    except Exception as e: print("srv:",type(e).__name__, e, flush=True)
    finally: c.close()
while True:
    c,_=s.accept(); threading.Thread(target=h,args=(c,),daemon=True).start()
