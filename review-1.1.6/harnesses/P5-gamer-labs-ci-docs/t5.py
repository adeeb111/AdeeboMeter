from playwright.sync_api import sync_playwright
P="http://adeebometer.com/"
cases=[("none",""),("dnt","Object.defineProperty(Navigator.prototype,'doNotTrack',{get:()=>'1'});"),("gpc","Object.defineProperty(Navigator.prototype,'globalPrivacyControl',{get:()=>true});")]
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1243/chrome-linux64/chrome", args=["--no-sandbox","--host-resolver-rules=MAP adeebometer.com:80 127.0.0.1:8765"])
    for name,init in cases:
        for pg in ["index.html","privacy.html","ar/index.html","learn/dns.html","app.html","games.html","remote.html","live.html","files.html","r.html","hall-of-fame.html","lab.html","learn/","fr/"]:
            ctx=br.new_context(service_workers="block")
            if init: ctx.add_init_script(init)
            page=ctx.new_page(); hits=[]
            def rt(r,q,hits=hits):
                if q.url.startswith(P): return r.continue_()
                hits.append(q.url.split('/')[2]); return r.abort()
            page.route("**/*", rt)
            page.goto(P+pg); page.wait_for_timeout(700)
            print(name, pg, sorted(set(hits)))
            ctx.close()
    br.close()
