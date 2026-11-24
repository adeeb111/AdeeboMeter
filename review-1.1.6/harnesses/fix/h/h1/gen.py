import base64,random,sys
sys.path.insert(0,'/home/user/AdeeboMeter-dev/.github/scripts')
random.seed(1)
names=["Ada Lovelace","  José  Ünal ","देवनागरी नाम","\U0001D400\U0001D41D bold","Ada\x1fLovelace","Ada Love","Ada  B","ábc","x"*40,"日本語の名前です","가","O'Brien-Smith_Jr.","emoji 😀 name","Ⅻ roman","٣٤٥ digits","ﬁx ligature","᠎Mongol","tab\there","\U00016FE3x","zw​j","กำ","ＡＢ wide"]
for _ in range(400):
    n="".join(chr(random.choice([random.randint(0x20,0x7e),random.randint(0xa0,0x2fff),random.randint(0x3000,0xffef),random.randint(0x10000,0x1faff)])) for _ in range(random.randint(1,40)))
    names.append(n)
names=[n for n in names if not any(0xd800<=ord(c)<=0xdfff for c in n)]
open('names.txt','w').write("\n".join(base64.b64encode(n.encode('utf-8','surrogatepass')).decode() for n in names))
open('names.json','w').write(repr(names))
