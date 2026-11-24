import datetime, ipaddress, sys, os
from cryptography import x509
from cryptography.x509.oid import NameOID
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa, ec
os.chdir(sys.argv[1])
now = datetime.datetime.now(datetime.timezone.utc)
D = datetime.timedelta
def key(bits=2048): return rsa.generate_private_key(public_exponent=65537, key_size=bits)
def name(cn): return x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, cn)])
def cert(subj, skey, iss, ikey, ca, nb, na, sans=None, h=hashes.SHA256(), pathlen=None):
    b = x509.CertificateBuilder().subject_name(name(subj)).issuer_name(name(iss)).public_key(skey.public_key()).serial_number(x509.random_serial_number()).not_valid_before(nb).not_valid_after(na)
    if ca is not None: b = b.add_extension(x509.BasicConstraints(ca=ca, path_length=pathlen), critical=True)
    if sans: b = b.add_extension(x509.SubjectAlternativeName(sans), critical=False)
    return b.sign(ikey, h)
def save(fn, *cs):
    with open(fn, 'wb') as f:
        for c in cs: f.write(c.public_bytes(serialization.Encoding.PEM))
def savekey(fn, k):
    open(fn,'wb').write(k.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.TraditionalOpenSSL, serialization.NoEncryption()))
rk = key(); root = cert("Test Root CA", rk, "Test Root CA", rk, True, now-D(days=100), now+D(days=3000))
ik = key(); inter = cert("Test Intermediate", ik, "Test Root CA", rk, True, now-D(days=50), now+D(days=1000))
lk = key(); sans=[x509.DNSName("localhost"), x509.DNSName("*.lab.test"), x509.IPAddress(ipaddress.ip_address("127.0.0.1"))]
leaf = cert("localhost", lk, "Test Intermediate", ik, False, now-D(days=1), now+D(days=90), sans)
save("root.pem", root); save("int.pem", inter); save("leaf.pem", leaf); savekey("leaf.key", lk)
save("chain-full.pem", leaf, inter, root); save("chain-noint.pem", leaf, root); save("chain-wrongorder.pem", leaf, root, inter)
el = cert("localhost", lk, "Test Intermediate", ik, False, now-D(days=100), now-D(hours=12), sans); save("leaf-expired12h.pem", el)
fl = cert("localhost", lk, "Test Intermediate", ik, False, now+D(days=2), now+D(days=90), sans); save("leaf-future.pem", fl)
xik = cert("Test Intermediate", ik, "Test Root CA", rk, True, now-D(days=500), now-D(days=3)); save("int-expired.pem", xik)
nk = key(); nonca = cert("Not A CA", nk, "Test Intermediate", ik, False, now-D(days=1), now+D(days=90))
l2 = cert("leaf2", key(), "Not A CA", nk, False, now-D(days=1), now+D(days=90)); save("nonca.pem", nonca); save("leaf2.pem", l2)
pass
wk = key(1024); w = cert("weak", wk, "Test Intermediate", ik, False, now-D(days=1), now+D(days=90)); save("leaf-1024.pem", w)
ss = cert("selfie", lk, "selfie", lk, None, now-D(days=1), now+D(days=90), sans); save("selfsigned.pem", ss)
ok = key(); oldroot = cert("Old Root", ok, "Old Root", ok, True, now-D(days=4000), now+D(days=500))
cross = cert("Test Root CA", rk, "Old Root", ok, True, now-D(days=100), now+D(days=400)); save("oldroot.pem", oldroot); save("cross.pem", cross)
save("chain-cross.pem", leaf, inter, cross)
prevk, prevn = rk, "Test Root CA"; chain=[]
for i in range(10):
    k=key(); c=cert("Mid %d"%i, k, prevn, prevk, True, now-D(days=1), now+D(days=500)); chain.append(c); prevk, prevn = k, "Mid %d"%i
ll = cert("deep", key(), prevn, prevk, False, now-D(days=1), now+D(days=90)); save("chain-long.pem", ll, *reversed(chain))
soon = cert("localhost", lk, "Test Intermediate", ik, False, now-D(days=80), now+D(days=10), sans); save("leaf-soon.pem", soon)
wl = cert("*.lab.test", lk, "Test Intermediate", ik, False, now-D(days=1), now+D(days=90), [x509.DNSName("*.lab.test")]); save("leaf-wild.pem", wl)
ek = ec.generate_private_key(ec.SECP384R1()); e = cert("eckey", ek, "Test Intermediate", ik, False, now-D(days=1), now+D(days=90)); save("leaf-ec.pem", e)
save("srv-expired-chain.pem", el, inter); save("srv-chain.pem", leaf, inter); save("srv-leafonly.pem", leaf)
print("ok")
