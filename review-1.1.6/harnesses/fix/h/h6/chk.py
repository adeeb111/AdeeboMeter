import ipaddress,re
bad=0; dupe=0; n=0
for line in open("sub.txt",encoding="utf-8-sig"):
    s,q,a,opts=line.rstrip("\n").split("\t"); opts=opts.split("|"); n+=1
    if len(set(opts))!=4 or a not in opts: dupe+=1; print("OPTS",line.strip()); continue
    m=re.search(r"of ([\d.]+/\d+)\?",q)
    if m:
        net=ipaddress.IPv4Network(m.group(1),strict=False)
        exp=str(net.network_address) if "network" in q else str(net.broadcast_address)
    else:
        p=int(re.search(r"/(\d+)",q).group(1)); exp="{:,}".format((1<<(32-p))-2 if p<31 else (2 if p==31 else 1))
    if exp!=a: bad+=1; print("WRONG",line.strip(),exp)
    # another option also correct?
print("subnet",n,"wrong",bad,"bad options",dupe)
kinds={"private (home or office)":lambda a:a.is_private and not a.is_loopback and not a.is_link_local and a not in ipaddress.ip_network("100.64.0.0/10") and not any(a in ipaddress.ip_network(x) for x in ["192.0.2.0/24","198.51.100.0/24","203.0.113.0/24"]),
 "public (the internet)":lambda a:a.is_global,
 "loopback (this PC)":lambda a:a.is_loopback,"link-local (no DHCP answer)":lambda a:a.is_link_local,
 "shared by your provider (CGNAT)":lambda a:a in ipaddress.ip_network("100.64.0.0/10"),
 "multicast (a group)":lambda a:a.is_multicast,
 "documentation (examples only)":lambda a:any(a in ipaddress.ip_network(x) for x in ["192.0.2.0/24","198.51.100.0/24","203.0.113.0/24"])}
bad=0
for line in open("addr.txt",encoding="utf-8-sig"):
    ad,c=line.rstrip("\n").split("\t"); a=ipaddress.IPv4Address(ad)
    if not kinds[c](a): bad+=1; print("ADDR",ad,c)
    others=[k for k,f in kinds.items() if k!=c and f(a)]
    if others: print("ALSO",ad,c,others)
print("addr wrong",bad)
