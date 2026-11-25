using System;
using System.Collections.Generic;
using System.Linq;

namespace AdeeboMeter
{
    // The hover card on a tool (Adeeb, 22 Nov: "so many tools, but you don't know how to find them"): what it does, and three
    // plain facts - what you type, about how long it takes, what it sends. The times are from a real run of every tool (22 Nov).
    sealed partial class FullView
    {
        const string ToolTimes = "mtu=2,udpmtu=2,mtr=long,ret=3,owners=2,route=2,gateway=2,v6routers=1,rlisten=long,discover=long,ping6=7,overlap=2,ip46=2,vpn=2,proxy=2,hosts=1,dnsrules=1,port=2,teams=2,clock=2,iperf=9,bloat=19,ping=long,dscp=19,tcpping=18,twamp=long,cloud=3,dig=2,http=1,httptime=2,doh=2,ntp=2,mail=2,ssh=4,snmp=3,snmpif=6,snmptraffic=14,snmpcpu=6,snmplldp=3,snmpsens=3,snmpsnap=3,snmpmac=6,snmpvlan=8,snmppoe=11,snmplag=3,snmpprn=8,snmpups=3,snmpwalk=3,mibs=2,syslog=2,icmpb=9,udpsend=2,tcpsend=3,arpwho=3,dhcpopt=2,trap=2,netflow=2,httpreq=2,stun=1,nbname=3,phonevs=1,phonedown=2,phoneweek=2,phconn=1,phspeed=2,phregions=1,phwifisafe=2,phvpn=2,phstun=2,phport=2,phsite=2,phspam=2,phdns=2,phfastdns=2,phemail=2,phstatus=2,phwho=2,phrdap=2,phmac=2,manyports=2,allips=2,rdpcheck=1,ldapcheck=2,dnscmp=2,beforeafter=2,twopc=2,srvchange=2,board=long,uptime=2,certcal=2,mysites=2,shares=20,svcfail=2,speedtest=14,gamecompare=2,join=2,dot1x=2,dhcpreplay=1,rareplay=1,eapreplay=2,icmpexplain=2,certcmp=2,smtppath=2,samplelab=2,tradetape=2,marketopen=2,ispreport=long,tidy=6,certmatch=2,certlive=2,certfile=2,chain=2,https=1,pfx=2,csr=2,certconv=2,hashf=2,b64=2,certcmds=2,radius=2,tacacs=8,arp=2,hairpin=2,sweep=7,roams=2,dhcp=2,subnet=1,stack=calc,vlsm=calc,bandwidth=calc,binhex=calc,wildcard=calc,v6split=calc,bdp=calc,units=calc,cellular=calc,wifirate=calc,linkbudget=calc,raid=calc,backup=calc,ups=calc,sla=calc,growth=calc,stream=calc,datause=calc,plan=calc,tdm=calc,jitterbuf=calc,fxconv=calc,pnl=calc,possize=calc,avgcost=calc,compound=calc,pctback=calc,tradelat=calc,comunits=calc,futures=calc,perunit=calc,tickrate=calc,timeline=1,hotel=1,dnsrace=3,inventory=4,drivers=3,usb=2,lag=2,winupdate=1,gpn=2,afterstream=1,obs=2,golive=2,prematch=2,besttime=2,tonight=1,console=1,month=2,sixmonths=1,rushhour=2,wakeup=2,capfc=2,dltime=2,netsnap=2,ipv6score=1,vpnleak=2,qr=2,roast=2,therapy=1,letter=2,bluetooth=1,dupip=2,winev=2,auth=2,dc=2,gpo=9,certs=2,fw=1,listen=2,events=1,timesrc=1,pressure=2,disk=2,space=10,bg=2,crash=2,leaks=2,gpu=2,changed=1,battery=7,pcwhy=2,hwmap=1,feltslow=2,diskspeed=18,ramcheck=2,gpucheck=2,cpuheld=4,netcap=1,hidden=2,laptop=long";
        static Dictionary<string, string> toolTimes;
        static string ToolTakes(Tool t)
        {
            if (toolTimes == null) toolTimes = ToolTimes.Split(',').Select(p => p.Split('=')).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
            if ((t.Kind ?? t.Group) == "CALC") return "nothing to wait for - it answers as you type";
            switch (t.Id)
            {
                case "mtr": return "as many minutes as you choose";
                case "rlisten": case "discover": return "the seconds you choose (10-120)";
                case "ping": return "how many pings × how often - 200 pings every 200 ms is 40 s";
                case "twamp": return "about a second per 10 packets";
                case "board": return "a few seconds - or keeps watching, if you give it minutes";
                case "ispreport": return "about a minute";
                case "laptop": return "a few minutes (it tests the disk and memory)";
                case "speedtest": return "about 20 seconds";
                // R2-161: the tools a real run didn't time
                case "radiuslab": return "up to 4 seconds - one Access-Request";
                case "remote": return "as long as the command you run on the server";
                case "encrypt": case "decrypt": return "a second or two, longer for a big file";
                case "keys": case "newkey": case "csrmake": case "selfsigned": case "recipients": return "a second or two";
                case "wol": return "an instant - the PC itself takes a minute to wake";
                case "soundtest": case "screentest": case "keytest": return "as long as you test";
                case "friend": return "nothing to wait for";
            }
            string v; int s;
            if (toolTimes.TryGetValue(t.Id, out v) && int.TryParse(v, out s)) return s <= 2 ? "a few seconds" : "about " + s + " s";
            return "a few seconds";
        }
        static string ToolSends(Tool t)
        {
            switch (t.Id)
            {
                case "speedtest": return "up to 1 GB to the speed server you picked";
                case "https": case "certlive": case "certcal": case "mysites": return "one TLS handshake to each site";
                case "chain": case "certfile": case "pfx": case "csr": case "certconv": case "hashf": case "b64": case "certcmds": case "certmatch": return "nothing - it reads the file or text you give it";
                case "qr": return "nothing - the result rides in the code";
                // bug-hunt R2-008: the group switch below missed most groups - these are what each one really sends
                case "subnet": case "roams": case "dhcp": case "dot1x": case "join": case "hosts": case "arp": case "icmpexplain": case "dhcpreplay": case "rareplay": case "eapreplay": case "twopc": case "samplelab": case "friend": case "overlap": case "route": case "shares": case "certcmp":
                    return "nothing - it reads this PC, Windows or what you paste";
                case "beforeafter": return "the same small checks as Home (router, internet, names), to compare";
                case "sweep": return "one ping to every address in your network (you confirm it's yours)";
                case "inventory": return "broadcast and multicast name questions on your own network";
                case "wol": return "one wake-up packet, broadcast on your own network";
                case "dupip": return "ARP probes for this PC's own address, on your own network";
                case "discover": case "rlisten": return "nothing - it only listens";
                case "mtr": case "ret": case "ping6": case "udpmtu": case "mtu": return "pings with rising hop limits to the address you type";
                case "radiuslab": return "one sign-in request (Access-Request) with the test account to the RADIUS server you type";
                case "icmpb": case "udpsend": return "the packets you set up (up to 1,000) to the address you type";
                case "iperf": return "an iperf3 test stream to your iperf3 server";
                case "phonevs": return "a request to your linked phone";
                case "timeline": return "nothing - it reads this PC's History";
                case "smtppath": return "a connection to each mail server of the domain you type";
                case "hairpin": return "a STUN question to a public server, then a connection to your own public address";
            }
            switch (t.Kind ?? t.Group)   /* review 1.1.6 U3 #6: the tool's own group, not the question it is listed under */
            {
                case "CALC": return "nothing - it's maths on this PC";
                case "THE PC": case "HARDWARE NERDS": case "WINDOWS": case "OVER TIME": case "JUST FOR FUN": case "WATCH": case "KEYS": return "nothing - it reads this PC and Windows";
                case "PATH": return "pings and small probes to the address you type";
                case "YOUR NETWORK": return "small questions to the device you type, on your own network";
                case "REMOTE": return t.Id == "remote" ? "an SSH session to the server you type" : "a request to your linked phone";
                case "SNMP": return "SNMP questions to the device you type (read only)";
                case "LAB": return "the test packets you set up - nothing else";
                case "SPEED": return "pings and test traffic to the server you type";
            }
            return "a few small requests to the address you type";
        }
        // the tip for a tool: the term from knowledge, plus the facts
        static Explain.Entry ToolTip(string id, Explain.Entry e)
        {
            var t = toolsStatic != null ? toolsStatic.FirstOrDefault(z => z.Id == id) : null; if (t == null) return e;
            var c = new Explain.Entry { Friendly = e.Friendly, Cat = e.Cat, Tag = e.Tag, What = e.What, How = e.How == e.What ? "" : e.How, Why = e.Why, Without = e.Without, Close = e.Close };
            string ins = string.Join(" · ", t.Inputs.Where(h => h != "").Select(h => BoxLabel(h)));
            c.Facts = new List<string[]> {
                new[] { "YOU TYPE", ins == "" ? "nothing - just press Run" : ins },
                new[] { "TAKES", ToolTakes(t) },
                new[] { "SENDS", ToolSends(t) } };
            return c;
        }
        static List<Tool> toolsStatic;   // set when the Toolbox's list is built, for the tip
    }
}
