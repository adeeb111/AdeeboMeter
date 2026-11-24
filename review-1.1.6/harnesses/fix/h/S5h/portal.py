import http.server,sys
class H(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(302); self.send_header('Location','http://login.hotel.example/portal'); self.send_header('Content-Length','0'); self.end_headers()
    do_HEAD=do_GET
http.server.HTTPServer(('127.0.0.1',int(sys.argv[1])),H).serve_forever()
