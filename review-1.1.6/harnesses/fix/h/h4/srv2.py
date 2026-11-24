import ssl, socket, sys, threading
port=int(sys.argv[1]); ctx=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); ctx.load_cert_chain(sys.argv[2],sys.argv[3])
ctx.maximum_version=ssl.TLSVersion.TLSv1_2; ctx.set_ciphers(sys.argv[4]); ctx.load_dh_params if False else None
s=socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR,1); s.bind(("127.0.0.1",port)); s.listen(5)
def h(c):
    try: cc=ctx.wrap_socket(c, server_side=True); print("ok", cc.cipher(), flush=True); cc.recv(10); cc.close()
    except Exception as e: print("srv:",type(e).__name__, e, flush=True)
    finally: c.close()
while True:
    c,_=s.accept(); threading.Thread(target=h,args=(c,),daemon=True).start()
