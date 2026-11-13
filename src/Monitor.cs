// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== Monitor: all reading happens here, off the UI thread
    sealed class Monitor : IDisposable
    {
        public void Dispose() { Stop(); }   // the sampler/probe threads release their own counters and event as they exit
        public readonly object L = new object();
        public Ring Cpu = new Ring(120), Mem = new Ring(120), Commit = new Ring(120), Disk = new Ring(120);
        public Ring Down = new Ring(120), Up = new Ring(120);
        public Ring GwPing = new Ring(30), NetPing = new Ring(30), Loss = new Ring(20), Dns = new Ring(12);
        public long NetTries, NetLostN; int icmpMiss;   // internet probes and lost ones since the network last changed (the Loss lab needs more than 20)
        public Ring Retx = new Ring(120), Rst = new Ring(120), Fail = new Ring(120), Unreach = new Ring(120), UdpDrop = new Ring(120);

        public int Cores = Environment.ProcessorCount;
        public double TotalGB, UsedGB, CommitLimitGB, CommitUsedGB;
        public double DiskReadMBs = double.NaN, DiskWriteMBs = double.NaN;
        public string CFree = "";
        public NetworkInterface Primary;
        public string Adapter = "", AdapterKind = "LAN", LinkSpeed = "", Gateway = "", DnsServer = "", IfaceId = "", Gateway6 = "";   // Gateway6: an IPv6-only network's router (no IPv4 gateway at all)
        public bool VpnUp; public string VpnName = ""; public int VpnMtu, PrimaryMtu;
        public bool HasWifi; public int WifiSignal = -1; public string Ssid = "", WifiChannel = "";
        public bool GwEverReplied, NetEverReplied, DnsFailedLast;
        // networks that block ping (many company and hotel networks): the internet check falls back to timing a TCP connection
        // to port 443 of the same address - opened and closed at once, nothing is sent over it
        public bool NetIcmpEver, NetViaTcp;
        // cable plugged in -> first answer from the gateway: 30-50 s is classic spanning tree on a port without "edge / port fast"
        readonly Dictionary<string, DateTime> ethUp = new Dictionary<string, DateTime>(); bool ethSeen; public string LinkWait = "";
        public string NetTarget = "1.1.1.1";
        public double DownBytesTotal;
        public List<AppStat> Apps = new List<AppStat>();
        public Dictionary<int, string> PidNames = new Dictionary<int, string>();
        public Dictionary<string, string> IpNames = new Dictionary<string, string>();
        public bool CallLikely;
        public string LocalIp = "", Bssid = "", WifiState = ""; public int RxRate = -1;
        public readonly Timeline Events = new Timeline();

        // change tracking for the timeline (probe / sampler threads only)
        string pAdapter, pGw, pDns, pIp, pState, pBssid = "", pCh = ""; bool pVpn; int pRate = -1, pSig = -1;
        DateTime lastRoam = DateTime.MinValue, lastSigDrop = DateTime.MinValue, netDownSince, lastConnT = Clock.Mono;
        public bool NetQuiet { get { return netDown >= 3; } }
        int netDown, prevLevel = -1; bool gwFlag, prevCall; double gwBase = double.NaN; string hint = "";
        HashSet<string> prevKeys; readonly Dictionary<string, DateTime> synFirst = new Dictionary<string, DateTime>();
        readonly Dictionary<string, DateTime> suppress = new Dictionary<string, DateTime>();
        volatile bool netChanged;
        NetworkAddressChangedEventHandler addrHandler; NetworkAvailabilityChangedEventHandler availHandler;

        public volatile bool ViewOpen, ProcsTab;   // ProcsTab: the What's running tab is showing - keep the process list at 2 s       // engineer view open: probe faster, read processes more often
        public volatile Snap Latest = new Snap();
        public int Version;
        int countdown = 3; int level; string headline = "Waking up... give me ten seconds, boss.";
        int[] path = { 3, 3, 3, 3, 3 };

        long pIdle, pKern, pUser; bool cpuInit;
        long pRx = -1, pTx = -1; DateTime pNetT;
        long pRetx = -1, pRst, pFail, pUnr, pUdp;
        volatile PerformanceCounter pcIdle, pcRead, pcWrite;
        volatile bool stop;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly ManualResetEvent stopping = new ManualResetEvent(false); Thread samplerT, probeT;

        public void Start()
        {
            if (samplerT != null) return;   // already running: never subscribe twice
            stop = false; stopping.Reset();
            Events.Add(3, "app", "AdeeboMeter started");
            addrHandler = delegate { netChanged = true; wake.Set(); };
            availHandler = delegate(object o, NetworkAvailabilityEventArgs e) { Events.Add(e.IsAvailable ? 0 : 2, "net", e.IsAvailable ? "Network is back" : Voice.P("Every network connection just dropped.", "All network connections dropped.")); netChanged = true; wake.Set(); };
            try { NetworkChange.NetworkAddressChanged += addrHandler; NetworkChange.NetworkAvailabilityChanged += availHandler; } catch { }
            RefreshInterfaces();
            var s = new Thread(SamplerLoop); s.IsBackground = true; s.Name = "sampler"; s.Priority = ThreadPriority.BelowNormal; samplerT = s; s.Start();
            var p = new Thread(ProbeLoop); p.IsBackground = true; p.Name = "probe"; p.Priority = ThreadPriority.BelowNormal; probeT = p; p.Start();
        }
        public void Stop()
        {
            stop = true; wake.Set(); stopping.Set();
            try { NetworkChange.NetworkAddressChanged -= addrHandler; NetworkChange.NetworkAvailabilityChanged -= availHandler; } catch { }
            foreach (var t in new[] { samplerT, probeT }) if (t != null && t != Thread.CurrentThread) t.Join(1500);   // both finish their tick (the stop event wakes them; quitting never waits long) and release their counters
            samplerT = null; probeT = null;   // so Start() can run again
        }
        void Once(string key, int seconds, int lv, string kind, string text)
        {
            DateTime t;
            lock (suppress)
            {
                if (suppress.TryGetValue(key, out t) && (DateTime.Now - t).TotalSeconds < seconds) return;
                if (suppress.Count > 500) foreach (var old in suppress.Where(kv => (DateTime.Now - kv.Value).TotalHours >= 1).Select(kv => kv.Key).ToList()) suppress.Remove(old);   // forget the old ones, never every window at once
                if (suppress.Count > 2000) suppress.Clear();
                suppress[key] = DateTime.Now;
            }
            Events.Add(lv, kind, text);
        }
        public void Nudge() { wake.Set(); }

        // ---------- sampler: every second
        void SamplerLoop()
        {
            // the disk counters can take seconds to open (a damaged counter registry): opened beside the sampler, which runs
            // from the first second and shows the disk once they're ready
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                PerformanceCounter r = null, w = null, i = null;
                try
                {
                    r = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                    w = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                    i = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total");
                    i.NextValue(); r.NextValue(); w.NextValue();
                    if (stopping.WaitOne(0)) throw new OperationCanceledException("stopped");   // the sampler already ended: nobody would dispose them
                    pcRead = r; pcWrite = w; pcIdle = i;   // pcIdle last: the sampler starts using them when it is set
                }
                catch (Exception e) { pcIdle = null; foreach (var pc in new[] { r, w, i }) if (pc != null) try { pc.Dispose(); } catch { } if (!(e is OperationCanceledException)) Program.Log("Disk counters unavailable: " + e.Message); }
            });
            while (!stop)
            {
                var t0 = Clock.Mono;
                try
                {
                    Tick();
                    if (--countdown <= 0)
                    {
                        Judge(); countdown = 10;
                    }
                    Latest = BuildSnap();
                    Interlocked.Increment(ref Version);
                    Brain.Second(this);     // history, usage, baselines, alerts, calls - arithmetic on what was just measured
                }
                catch (Exception e) { Program.Log("sampler: " + e.Message); }
                int wait = 1000 - (int)(Clock.Mono - t0).TotalMilliseconds;
                if (wait > 0) stopping.WaitOne(wait);   // Stop wakes it at once
            }
            foreach (var pc in new[] { pcIdle, pcRead, pcWrite }) if (pc != null) try { pc.Dispose(); } catch { }
        }

        public void Tick()
        {
            long idle, kern, user;
            if (Native.GetSystemTimes(out idle, out kern, out user))
            {
                if (cpuInit) { long di = idle - pIdle, tot = (kern - pKern) + (user - pUser); Cpu.Add(tot > 0 ? Clamp((tot - di) * 100.0 / tot) : 0); }
                pIdle = idle; pKern = kern; pUser = user; cpuInit = true;
            }
            var ms = new Native.MEMORYSTATUSEX();
            if (Native.GlobalMemoryStatusEx(ms))
            {
                TotalGB = ms.ullTotalPhys / 1073741824.0; UsedGB = (ms.ullTotalPhys - ms.ullAvailPhys) / 1073741824.0;
                CommitLimitGB = ms.ullTotalPageFile / 1073741824.0; CommitUsedGB = (ms.ullTotalPageFile - ms.ullAvailPageFile) / 1073741824.0;
                Mem.Add(ms.dwMemoryLoad); Commit.Add(CommitLimitGB > 0 ? CommitUsedGB * 100 / CommitLimitGB : double.NaN);
            }
            try
            {
                if (pcIdle != null) { Disk.Add(Clamp(100 - pcIdle.NextValue())); DiskReadMBs = pcRead.NextValue() / 1048576.0; DiskWriteMBs = pcWrite.NextValue() / 1048576.0; }
                else Disk.Add(double.NaN);
            }
            catch { Disk.Add(double.NaN); }
            NetworkInterface ni; lock (L) ni = Primary;
            try
            {
                if (ni != null)
                {
                    var st = ni.GetIPStatistics(); long rx = st.BytesReceived, tx = st.BytesSent; DateTime now = Clock.Mono;
                    if (pRx >= 0 && rx >= pRx && tx >= pTx)
                    {
                        double sec = Math.Max(0.2, (now - pNetT).TotalSeconds);
                        Down.Add((rx - pRx) * 8 / sec / 1e6); Up.Add((tx - pTx) * 8 / sec / 1e6); DownBytesTotal += rx - pRx;
                    }
                    pRx = rx; pTx = tx; pNetT = now;
                }
            }
            catch { pRx = -1; }
            try
            {
                var gp = IPGlobalProperties.GetIPGlobalProperties();
                var t4 = gp.GetTcpIPv4Statistics();
                long retx = t4.SegmentsResent, rst = t4.ResetsSent, fail = t4.FailedConnectionAttempts;
                try { var t6 = gp.GetTcpIPv6Statistics(); retx += t6.SegmentsResent; rst += t6.ResetsSent; fail += t6.FailedConnectionAttempts; } catch { }
                long unr = gp.GetIcmpV4Statistics().DestinationUnreachableMessagesReceived;
                long udp = gp.GetUdpIPv4Statistics().IncomingDatagramsDiscarded;
                if (pRetx >= 0) { Retx.Add(Math.Max(0, retx - pRetx)); Rst.Add(Math.Max(0, rst - pRst)); Fail.Add(Math.Max(0, fail - pFail)); Unreach.Add(Math.Max(0, unr - pUnr)); UdpDrop.Add(Math.Max(0, udp - pUdp)); }
                pRetx = retx; pRst = rst; pFail = fail; pUnr = unr; pUdp = udp;
            }
            catch { }
        }
        static double Clamp(double v) { return v < 0 ? 0 : v > 100 ? 100 : v; }

        // ---------- probe: pings, DNS, Wi-Fi, processes, call detection
        void ProbeLoop()
        {
            int netFails = 0, ti = 0; string[] targets = { "1.1.1.1", "8.8.8.8" };
            var lastCpu = new Dictionary<int, TimeSpan>(); DateTime lastProcT = Clock.Mono, lastNet = DateTime.MinValue, lastProc = DateTime.MinValue, lastWifi = DateTime.MinValue, lastIf = Clock.Mono, lastGw = DateTime.MinValue, lastDns = DateTime.MinValue;
            while (!stop)
            {
                DateTime t0 = Clock.Mono;
                try
                {
                    bool fast = ViewOpen || CallLikely;
                    // Every interval comes from Settings; the open engineer view (or a call) always gets the fast pace.
                    int gwSec = Cfg.GwSec == 0 ? 0 : Waking(t0) && double.IsNaN(wakeGw) ? 1 : ViewOpen ? (Cfg.GwSec >= 30 ? 5 : 2) : Cfg.GwSec,   /* Penny stays light with the window open too */   /* just woke: every second until the router answers */   /* Off means off, view open or not */ netSec = Cfg.NetSec == 0 || Policy.NoInternetCheck ? 0 : Waking(t0) && !double.IsNaN(wakeGw) ? 1 : fast ? Math.Min(5, Cfg.NetSec) : Cfg.NetSec,   /* the open view or a call: every 5 s at most */   /* the internet check off means off, view open or not */ wifiSec = Cfg.WifiSec == 0 ? 0 : ViewOpen ? 10 : Cfg.WifiSec, appsSec = Cfg.AppsSec == 0 ? 0 : ViewOpen ? 2 : Cfg.AppsSec, procSec = Cfg.AppsSec == 0 ? 0 : ViewOpen ? (ProcsTab ? 2 : 4) : Cfg.AppsSec;
                    GwSecNow = gwSec; NetSecNow = netSec;   // the pace the rings fill at (the overlay plots them by time)
                    if (gwSec > 0 && (t0 - lastGw).TotalSeconds >= gwSec - 0.2)
                    {
                        lastGw = t0;
                        string gw; lock (L) gw = Gateway;
                        double g = PingMs(gw); WakeStep(t0, !double.IsNaN(g), false);
                        lock (L)
                        {
                            GwPing.Add(g); GwAt = Clock.Mono; if (!double.IsNaN(g)) GwEverReplied = true; GwAnswering = !double.IsNaN(g);
                            DateTime up;
                            if (!double.IsNaN(g) && Primary != null && ethUp.TryGetValue(Primary.Id, out up) && up != DateTime.MinValue)
                            {
                                ethUp[Primary.Id] = DateTime.MinValue; double secs = (DateTime.Now - up).TotalSeconds;
                                if (secs >= 20) { LinkWait = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture) + " - cable in, " + secs.ToString("0", CultureInfo.CurrentCulture) + " s until the network answered"; Events.Add(1, "lan", "Cable plugged in: " + secs.ToString("0", CultureInfo.CurrentCulture) + " s until the network answered (spanning tree without port fast?)"); }
                            }
                        }
                    }
                    if (netSec > 0 && (t0 - lastNet).TotalSeconds >= netSec - 0.2)
                    {
                        lastNet = t0;
                        string tgt; lock (L) tgt = NetTarget;
                        double n = PingMs(tgt);
                        if (!double.IsNaN(n)) { icmpMiss = 0; lock (L) { NetIcmpEver = true; NetViaTcp = false; } }
                        // ping answered before but not now: after 3 misses in a row try TCP 443 too - ICMP blocked later (a new
                        // firewall rule, a VPN) while the web still works must not read as "the internet went quiet"
                        else if (!NetIcmpEver || ++icmpMiss >= 3) { IPAddress tip; if (IPAddress.TryParse(tgt, out tip)) { n = GamePing.TcpMs(tip, 443, 1500); if (!double.IsNaN(n)) lock (L) NetViaTcp = true; } }
                        lock (L) { NetPing.Add(n); netAt = Clock.Mono; if (!double.IsNaN(n)) NetEverReplied = true; if (NetEverReplied) { Loss.Add(double.IsNaN(n) ? 1 : 0); NetTries++; if (double.IsNaN(n)) NetLostN++; } }
                        if (NetEverReplied)
                        {
                            if (double.IsNaN(n)) { if (++netDown == 3) { netDownSince = DateTime.Now; Events.Add(2, "inet", Voice.P("The internet went quiet - no replies from " + tgt + ".", "Internet unreachable (no reply from " + tgt + ").")); } }
                            else { if (netDown >= 3) Events.Add(0, "inet", "Internet back after " + Timeline.Ago(netDownSince) + " (down " + netDownSince.ToString("HH:mm", CultureInfo.CurrentCulture) + " - " + DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture) + ")"); netDown = 0; }
                        }
                        if (!NetEverReplied && double.IsNaN(n) && ++netFails >= 2) { netFails = 0; ti = (ti + 1) % targets.Length; lock (L) NetTarget = targets[ti]; }
                        string gwNow; lock (L) gwNow = Gateway != "" ? Gateway : Gateway6;
                        // the DNS lookup skips the cache, so it reaches the DNS server: at most once a minute, every 15 s with the view open
                        if ((gwNow != "" || VpnUp) && ((t0 - lastDns).TotalSeconds >= (ViewOpen ? 15 : 60) - 0.2 || (Waking(t0) && !double.IsNaN(wakeGw)))) { lastDns = t0; ProbeDns(); bool dok; lock (L) dok = !DnsFailedLast; WakeStep(t0, false, dok); }   // no way out: a DNS failure would only be noise
                    }
                    // internet check turned off: forget what it measured, so old pings and loss are never judged as now
                    if (netSec == 0 && Cfg.NetSec == 0 && netDown >= 3) { Events.Add(1, "inet", "Internet check switched off while the internet was quiet - the outage log stops here"); netDown = 0; }
                    if (netSec == 0 && Cfg.NetSec == 0) lock (L) if (NetEverReplied || !double.IsNaN(NetPing.Last)) { NetPing.Clear(); Loss.Clear(); NetTries = 0; NetLostN = 0; NetEverReplied = false; NetIcmpEver = false; NetViaTcp = false; netDown = 0; }
                    if (netChanged && netDown >= 3) Events.Add(1, "inet", "Network changed while the internet was quiet (down since " + netDownSince.ToString("HH:mm", CultureInfo.CurrentCulture) + ") - checking afresh");
                    if (netChanged) { netChanged = false; lastIf = DateTime.MinValue; icmpMiss = 0; lock (L) { NetPing.Clear(); Loss.Clear(); NetTries = 0; NetLostN = 0; NetEverReplied = false; NetIcmpEver = false; NetViaTcp = false; netDown = 0; GwPing.Clear(); GwEverReplied = false; } gwMacKey = ""; gwMac = "";   /* a new network: its router is judged on its own, never on the last network's */ /* a new network starts its internet history afresh */ lastIf = t0; lastWifi = t0; RefreshInterfaces(); if (wifiSec > 0) ProbeWifi(); }
                    if ((t0 - lastIf).TotalSeconds >= IfEvery) { lastIf = t0; RefreshInterfaces(); WatchGatewayMac(); LearnIsp(); }
                    if (wifiSec > 0 && (t0 - lastWifi).TotalSeconds >= wifiSec - 0.2) { lastWifi = t0; ProbeWifi(); }
                    if (appsSec > 0 && (t0 - lastConnT).TotalSeconds >= appsSec - 0.2) ProbeConns();
                    if (procSec > 0 && (t0 - lastProc).TotalSeconds >= procSec - 0.2) { lastProc = t0; ProbeProcesses(lastCpu, ref lastProcT); }
                    if (Cfg.Call || ViewOpen) ProbeCall(); else lock (L) CallLikely = false;
                }
                catch (Exception e) { Program.Log("probe: " + e.Message); }
                // Sleep until the next job is due (2 s with the view open or a call; 1 s just after waking; up to 30 s on Penny).
                int pace = Waking(Clock.Mono) ? 1 : ViewOpen || CallLikely ? 2 : new[] { Cfg.GwSec, Cfg.NetSec, Cfg.WifiSec, Cfg.AppsSec, IfEvery }.Where(v => v > 0).Min();
                int wait = Math.Max(Waking(Clock.Mono) ? 1 : 2, pace) * 1000 - (int)(Clock.Mono - t0).TotalMilliseconds;
                if (wait > 0) wake.WaitOne(wait);
            }
        }

        public static double PingMs(string host)
        {
            if (string.IsNullOrEmpty(host)) return double.NaN;
            try { using (var p = new Ping()) { var r = p.Send(host, 1000); return r.Status == IPStatus.Success ? Math.Max(1, r.RoundtripTime) : double.NaN; } }
            catch { return double.NaN; }
        }

        static readonly string[] vpnWords = { "vpn", "anyconnect", "globalprotect", "pangp", "juniper", "pulse", "fortinet", "forti", "wireguard", "tap-windows", "zscaler", "openvpn", "netskope", "f5 ", "ivanti", "checkpoint", "check point", "tailscale", "wintun", "cloudflare", "nordlynx", "zerotier", "mullvad", "protonvpn", "expressvpn", "surfshark", "sonicwall", "barracuda" };
        public static bool IsVpn(NetworkInterface n)
        {
            string d = (n.Description + " " + n.Name).ToLowerInvariant();
            // dial-up style links: VPNs (PPTP, L2TP, SSTP, IKEv2) - but a PPPoE broadband link is the internet itself, not a VPN
            if (n.NetworkInterfaceType == NetworkInterfaceType.Ppp) return d.IndexOf("pppoe", StringComparison.Ordinal) < 0 && d.IndexOf("broadband", StringComparison.Ordinal) < 0;
            foreach (var w in vpnWords) if (d.Contains(w)) return true;
            return false;
        }

        public void RefreshInterfaces()
        {
            try
            {
                NetworkInterface best = null, vpn = null; string gw = ""; bool winBest = false;
                var all = NetworkInterface.GetAllNetworkInterfaces();
                lock (L)
                {
                    foreach (var n in all.Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                        if (n.OperationalStatus != OperationalStatus.Up) ethUp.Remove(n.Id);
                        else if (!ethUp.ContainsKey(n.Id)) ethUp[n.Id] = ethSeen ? DateTime.Now : DateTime.MinValue;   // up when AdeeboMeter started = unknown
                    ethSeen = true;
                }
                // the adapter Windows itself would use toward the internet wins (route metrics included): a USB link to a lab
                // switch that hands out a gateway must not become "your network" while the internet goes over Wi-Fi
                int winIdx = Routes.WindowsPick(IPAddress.Parse("1.1.1.1"));
                foreach (var n in all)
                {
                    if (n.OperationalStatus != OperationalStatus.Up) continue;
                    if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (IsVpn(n)) { if (vpn == null) vpn = n; continue; }   // (tunnel adapters too - they are often the VPN)
                    if (n.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    string g = "";
                    foreach (var ga in n.GetIPProperties().GatewayAddresses)
                        if (ga.Address.AddressFamily == AddressFamily.InterNetwork && !ga.Address.Equals(IPAddress.Any)) { g = ga.Address.ToString(); break; }
                    if (g == "") continue;
                    int idx = -1; try { var p4 = n.GetIPProperties().GetIPv4Properties(); if (p4 != null) idx = p4.Index; } catch (NetworkInformationException) { }
                    if (winIdx > 0 && idx == winIdx) { best = n; gw = g; winBest = true; continue; }
                    if (winBest) continue;
                    if (best == null || (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet && best.NetworkInterfaceType != NetworkInterfaceType.Ethernet)) { best = n; gw = g; }
                }
                // no IPv4 way out anywhere: an IPv6-only network (NAT64 hotspots, some company SSIDs) is still a network - its
                // adapter with an IPv6 default gateway and a global address counts as connected, never "Wi-Fi is off"
                string gw6 = "";
                if (best == null)
                    foreach (var n in all)
                    {
                        if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback || n.NetworkInterfaceType == NetworkInterfaceType.Tunnel || IsVpn(n)) continue;
                        IPInterfaceProperties ip6; try { ip6 = n.GetIPProperties(); } catch (NetworkInformationException) { continue; }
                        var g6 = ip6.GatewayAddresses.Select(x => x.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6 && !a.Equals(IPAddress.IPv6Any));
                        bool global = ip6.UnicastAddresses.Any(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal && !u.Address.IsIPv6SiteLocal && !IPAddress.IsLoopback(u.Address));
                        if (g6 != null && global) { best = n; gw6 = g6.ToString(); break; }
                    }
                lock (L)
                {
                    if (best == null || Primary == null || best.Id != Primary.Id) pRx = -1;
                    Primary = best;
                    if (best != null)
                    {
                        Adapter = best.Name; AdapterKind = best.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "LAN";
                        LinkSpeed = best.Speed > 0 ? (best.Speed >= 1000000000 ? (best.Speed / 1e9).ToString("0.#", CultureInfo.CurrentCulture) + " Gb/s" : (best.Speed / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s") : "";
                        Gateway = gw; Gateway6 = gw6; IfaceId = best.Id;
                        try { PrimaryMtu = best.GetIPProperties().GetIPv4Properties().Mtu; } catch { PrimaryMtu = 0; }
                    }
                    else { Adapter = ""; Gateway = ""; Gateway6 = ""; LinkSpeed = ""; IfaceId = ""; }
                    VpnUp = vpn != null; VpnName = vpn != null ? vpn.Description : ""; VpnMtu = 0;
                    if (vpn != null) try { VpnMtu = vpn.GetIPProperties().GetIPv4Properties().Mtu; } catch { }
                    var dnsFrom = vpn ?? best; DnsServer = "";
                    if (dnsFrom != null)
                        foreach (var d in dnsFrom.GetIPProperties().DnsAddresses)
                            if (d.AddressFamily == AddressFamily.InterNetwork) { DnsServer = d.ToString(); break; }
                    LocalIp = "";
                    if (best != null)
                        foreach (var ua in best.GetIPProperties().UnicastAddresses)
                            if (ua.Address.AddressFamily == AddressFamily.InterNetwork) { LocalIp = ua.Address.ToString(); break; }
                }
                string a2, k2, g2, d2, v2, ip2; bool vp2; lock (L) { a2 = Adapter; k2 = AdapterKind; g2 = Gateway; d2 = DnsServer; vp2 = VpnUp; v2 = VpnName; ip2 = LocalIp; }
                IfEvents(a2, k2, g2, d2, vp2, v2, ip2);
                try { var c = new DriveInfo("C"); string cf = (c.AvailableFreeSpace / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB free of " + (c.TotalSize / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB"; lock (L) CFree = cf; } catch { }
            }
            catch (Exception e) { Program.Log("interfaces: " + e.Message); }
        }

        void IfEvents(string adapter, string kind, string gw, string dns, bool vpn, string vpnName, string ip)
        {
            if (pAdapter == null)
            {
                Events.Add(adapter == "" ? 2 : 3, "net", adapter == "" ? "No network connection" : "On " + kind + " (" + adapter + ")" + (gw != "" ? ", gateway " + gw : "") + (vpn ? ", VPN up" : ""));
            }
            else
            {
                if (adapter != pAdapter) Events.Add(adapter == "" ? 2 : 1, "net", adapter == "" ? Voice.P("Lost the network - you're offline, boss.", "Network connection lost.") : "Switched to " + kind + " (" + adapter + ")");
                else
                {
                    if (ip != "" && ip != pIp) Events.Add(1, "net", "IP address changed" + (pIp != "" ? " " + pIp + " → " + ip : " to " + ip));
                    if (gw != "" && gw != pGw) Events.Add(1, "net", "Gateway changed to " + gw);
                }
                if (vpn != pVpn) Events.Add(vpn ? 0 : 1, "vpn", vpn ? "VPN connected (" + Draw.Trunc(vpnName, 30) + ")" : Voice.P("VPN dropped.", "VPN disconnected."));
                if (dns != "" && dns != pDns) Events.Add(1, "dns", "DNS server changed to " + dns + (vpn ? " (via VPN)" : ""));
            }
            pAdapter = adapter; pGw = gw; pDns = dns; pVpn = vpn; pIp = ip;
        }

        // Self-test only: replays a realistic sequence of changes through the same code paths.
        public string SimulateForTest(StringBuilder log)
        {
            IfEvents("Wi-Fi", "Wi-Fi", "10.0.0.1", "64.71.255.204", false, "", "10.0.0.82");
            IfEvents("Wi-Fi", "Wi-Fi", "10.0.0.1", "10.8.0.53", true, "Example VPN Client Adapter", "10.0.0.82");
            IfEvents("Wi-Fi", "Wi-Fi", "10.0.0.1", "10.8.0.53", false, "", "10.0.0.90");
            IfEvents("Ethernet", "LAN", "192.168.1.1", "192.168.1.1", false, "", "192.168.1.20");
            WifiEvents("connected", "aa:bb:cc:00:00:01", "36", 80, 866);
            WifiEvents("connected", "aa:bb:cc:00:00:02", "36", 78, 866);
            WifiEvents("connected", "aa:bb:cc:00:00:02", "36", 40, 144);
            WifiEvents("disconnected", "", "", -1, -1);
            lock (L) { HasWifi = true; WifiSignal = 40; }
            for (int i = 0; i < 10; i++) Retx.Add(12);
            Judge();
            foreach (var e in Events.Recent(50)) log.AppendLine("      [" + e.Kind + "] " + e.Text);
            return hint;
        }

        // adapters, the router's MAC and the ISP: every 10 s, every minute on Penny (a cable or Wi-Fi change still wakes the loop at once)
        static int IfEvery { get { return Cfg.Preset == "penny" ? 60 : 10; } }
        void ProbeDns()
        {
            double ms; int rc = DnsTime("www.microsoft.com", out ms);
            lock (L) { DnsFailedLast = rc != 0 && rc != 9003 && rc != 9501; Dns.Add(DnsFailedLast ? double.NaN : ms); }
        }
        // A real lookup that skips the local cache, so it measures the DNS server itself.
        public static int DnsTime(string name, out double ms)
        {
            IntPtr res; var sw = Stopwatch.StartNew();
            int rc = Native.DnsQuery(name, 1, 0x8, IntPtr.Zero, out res, IntPtr.Zero);
            ms = sw.Elapsed.TotalMilliseconds;
            if (res != IntPtr.Zero) Native.DnsRecordListFree(res, 1);
            return rc;
        }

        // Wi-Fi from netsh, read in any Windows language: whether it IS Wi-Fi comes from the adapter, not from the text;
        // the signal is the one "NN%" value; SSID / BSSID are never translated; channel and state know the common translations.
        // A location-blocked netsh (Windows 11) keeps Wi-Fi as Wi-Fi with the signal unknown - never "LAN", never "weak".
        static readonly string[] ChanKeys = { "channel", "kanal", "canal", "canale", "kanaal", "kanał", "チャネル", "канал", "信道" }, StateKeys = { "state", "status", "état", "etat", "estado", "stato", "zustand", "staat", "stan", "状態", "состояние", "durum", "状态" };
        public static string NormState(string v)
        {
            v = (v ?? "").Trim().ToLowerInvariant();
            foreach (var d in new[] { "disconn", "getrennt", "déconnect", "deconnect", "desconect", "disconness", "niet verbonden", "rozłącz" }) if (v.Contains(d)) return "disconnected";
            foreach (var d in new[] { "切断", "отключ", "bağlantı kesildi", "已断开" }) if (v.Contains(d)) return "disconnected";
            foreach (var c in new[] { "connected", "verbunden", "connecté", "connecte", "conectado", "connesso", "verbonden", "połączon", "接続", "подключ", "bağlandı", "bağlı", "已连接" }) if (v.Contains(c)) return "connected";
            return v;
        }
        public static void ParseWifi(string outp, out int sig, out string ssid, out string ch, out string bssid, out string state, out int rate)
        {
            sig = -1; rate = -1; ssid = ""; ch = ""; bssid = ""; state = "";
            foreach (var line in (outp ?? "").Split('\n'))
            {
                var m = Regex.Match(line, @"^\s*([^:]+?)\s*:\s*(.+?)\s*$"); if (!m.Success) continue;
                string k = m.Groups[1].Value.Trim(), kl = k.ToLowerInvariant(), v = m.Groups[2].Value;
                var pct = Regex.Match(v, @"^(?:(\d{1,3})\s*%|%\s*(\d{1,3}))$");   // "74%", or Turkish "%74"
                if (pct.Success && sig < 0) { int x; if (int.TryParse(pct.Groups[1].Success ? pct.Groups[1].Value : pct.Groups[2].Value, out x) && x <= 100) sig = x; }
                else if (k == "SSID") ssid = Program.Plain(v);
                else if ((k == "BSSID" || k == "AP BSSID" || k.IndexOf("BSSID", StringComparison.OrdinalIgnoreCase) >= 0) && Regex.IsMatch(v, "^[0-9a-fA-F]{2}([:-][0-9a-fA-F]{2}){5}$")) bssid = v;   // "BSSID du point d'accès" too
                else if (ChanKeys.Contains(kl)) ch = v;
                else if (StateKeys.Contains(kl)) state = NormState(v);
                else if (kl.StartsWith("receive rate", StringComparison.Ordinal) || kl.StartsWith("vitesse de réception", StringComparison.Ordinal) || kl.StartsWith("empfangsrate", StringComparison.Ordinal) || kl.StartsWith("velocidad de recepción", StringComparison.Ordinal) || kl.StartsWith("taxa de recepção", StringComparison.Ordinal))   /* English, French, German, Spanish, Portuguese */ { double r; if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r)) rate = (int)r; }
            }
        }
        public volatile bool WifiBlocked;
        public static volatile bool GwAnswering;   // did the router's last ping answer? (alerts word themselves from it)   // Windows hides Wi-Fi details until Location is allowed
        void ProbeWifi()
        {
            bool wifi; lock (L) wifi = AdapterKind == "Wi-Fi";
            if (!wifi) { lock (L) { HasWifi = false; WifiSignal = -1; } WifiBlocked = false; return; }
            try
            {
                int sig, rate; string ssid, ch, bssid, state;
                // Windows' Wi-Fi service first: no program started. netsh only when this Windows can't answer that way.
                WlanApi.Now now = null; int rc = wlanApiOk ? WlanApi.Current(out now) : -1;
                if (rc == 5) { WifiBlocked = true; wifiEmpty = 0; lock (L) { HasWifi = true; WifiSignal = -1; } return; }
                if (rc == 0 && now != null) { wlanApiFails = 0; WifiBlocked = false; sig = now.Signal; ssid = now.Ssid; ch = now.Channel; bssid = now.Bssid; state = now.State; rate = now.Rate; }
                else
                {
                    if (rc != 0 && wlanApiOk && ++wlanApiFails >= 3) { wlanApiOk = false; Program.Log("Wi-Fi: Windows' Wi-Fi service didn't answer (" + rc + ") - reading netsh instead"); }
                    string outp = Job.Run(Job.Netsh, "wlan show interfaces", 3000);
                    if (LinkInfo.Blocked(outp)) { WifiBlocked = true; wifiEmpty = 0; lock (L) { HasWifi = true; WifiSignal = -1; } return; }
                    WifiBlocked = false;
                    outp = LinkInfo.ActiveBlock(outp); ParseWifi(outp, out sig, out ssid, out ch, out bssid, out state, out rate);
                }
                // netsh that timed out or answered nothing (a busy PC) is not "Wi-Fi gone": keep the last reading for up to three
                // empty answers (the adapter itself going away is seen at once - it stops being the Wi-Fi adapter above)
                if (state == "" && sig < 0 && ssid == "") { if (++wifiEmpty < 3) return; lock (L) { HasWifi = true; WifiSignal = -1; Ssid = ""; Bssid = ""; WifiChannel = ""; RxRate = -1; } return; }   /* nothing seen 3 times: no stale network name */
                wifiEmpty = 0;
                lock (L) { HasWifi = true; WifiSignal = sig; Ssid = ssid; WifiChannel = ch; Bssid = bssid; WifiState = state; RxRate = rate; }
                WifiEvents(state, bssid, ch, sig, rate);
            }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.ComponentModel.Win32Exception || e is IOException || e is TimeoutException)) throw; if (++wifiEmpty >= 3) lock (L) WifiSignal = -1; }   /* netsh failing isn't "no network": the name stays */
        }
        int wifiEmpty, wlanApiFails; bool wlanApiOk = true;   // three failed answers from the Wi-Fi service: netsh from then on

        // ARP spoofing alarm: the router's hardware address should not change while you stay on the same network.
        string gwMacKey = "", gwMac = ""; readonly Dictionary<string, DateTime> gwMacSeen = new Dictionary<string, DateTime>();
        // after sleep the PC may wake on another network with the same router address: start fresh, as on a network change
        public void Resumed() { netChanged = true; wakeAt = Clock.Mono; wakeGw = double.NaN; wake.Set(); }
        // the wake-up report: seconds from waking until the router, then DNS, answered - logged once per wake as a "wake" event
        DateTime wakeAt = DateTime.MinValue; double wakeGw = double.NaN;
        bool Waking(DateTime t0) { return wakeAt != DateTime.MinValue && (t0 - wakeAt).TotalSeconds < 120; }
        void WakeStep(DateTime t0, bool gwOk, bool dnsOk)
        {
            if (wakeAt == DateTime.MinValue) return;
            if (gwOk && double.IsNaN(wakeGw)) wakeGw = (t0 - wakeAt).TotalSeconds;
            bool timeout = (t0 - wakeAt).TotalSeconds >= 120;
            if ((dnsOk && (!double.IsNaN(wakeGw) || !GwEverReplied)) || timeout)   /* a router that never answers ping: DNS alone says the network is back */
            {
                var c = CultureInfo.InvariantCulture; double dn = dnsOk ? (t0 - wakeAt).TotalSeconds : double.NaN;
                if (!double.IsNaN(wakeGw)) Events.Add(wakeGw > 30 || double.IsNaN(dn) ? 1 : 3, "wake", "Back online after waking: router " + wakeGw.ToString("0.#", c) + " s" + (double.IsNaN(dn) ? " · DNS didn't answer within 2 minutes" : " · DNS " + dn.ToString("0.#", c) + " s"));
                else if (!double.IsNaN(dn)) Events.Add(dn > 30 ? 1 : 3, "wake", "Back online after waking: DNS " + dn.ToString("0.#", c) + " s (the router doesn't answer ping, so its time isn't known)");
                else Events.Add(1, "wake", GwEverReplied ? "Woke up, but the router didn't answer within 2 minutes" : "Woke up, but neither DNS nor the router answered within 2 minutes");
                wakeAt = DateTime.MinValue;
            }
        }
        // Your provider's name: the first public router after your own is theirs, and its reverse-DNS name usually says so
        // (what "nslookup <hop>" would show). Learned once per network: a 6-hop trace toward 1.1.1.1 and one name lookup.
        public volatile string Isp = ""; string ispKey = ""; volatile bool ispBusy;
        int ispTries; string ispNet = "";   // retries for one network (a failed trace is tried again, 3 times at most)
        void LearnIsp()
        {
            string key; bool vpn; lock (L) { key = IfaceId + "|" + Gateway + "|" + Ssid; vpn = VpnUp; }
            if (key == ispKey || ispBusy || Cfg.NetSec == 0 || vpn || Gateway == "") return;
            if (key != ispNet) { ispNet = key; ispTries = 0; } ispKey = key; ispBusy = true; Isp = "";
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var hops = DestCheck.TraceHops(IPAddress.Parse("1.1.1.1"), 6);
                    var first = hops.Select(h => h.Item1).FirstOrDefault(ip => ip != null && DestCheck.Kind(ip) == "public");
                    if (first != null)
                    {
                        string name = ""; var t = Task.Factory.StartNew(() => { try { return Native.PtrName(first); } catch { return ""; } });
                        if (t.Wait(3000)) name = t.Result;
                        string owner = name != first ? DestCheck.OwnerOf(name) : null;
                        if (owner != null && owner != "cloudflare.com" && owner != "Cloudflare") { Isp = owner; Program.Log("provider: " + owner); }
                    }
                }
                catch (Exception e) { Program.Log("isp: " + e.Message); }
                if (Isp == "" && ispTries++ < 3) ispKey = "";   // nothing learned (a transient failure): try again next time, up to 3 times per network
                ispBusy = false;
            });
        }
        void WatchGatewayMac()
        {
            string gw, key; lock (L) { gw = Gateway; key = IfaceId + "|" + Gateway + "|" + Ssid; }
            if (gw == "") { gwMacKey = ""; gwMac = ""; return; }
            string mac = ""; try { mac = Arp.MacOf(gw); } catch { }
            if (mac == "") return;
            if (key != gwMacKey) { gwMacKey = key; gwMac = mac; return; }   // new network: just learn it
            if (mac != gwMac && !(gwMacSeen.ContainsKey(mac) && (Clock.Mono - gwMacSeen[mac]).TotalMinutes < 10))
            {
                // changed once: a replaced router, a failover pair, or another network that happens to share the address - say so plainly
                Events.Add(1, "net", "Your router's hardware address changed (" + gwMac + " → " + mac + ") - a new or restarted router, a failover pair, or a different network with the same address.");
                gwMacSeen[gwMac] = Clock.Mono; gwMac = mac;
            }
            else if (mac != gwMac)
            {
                gwMacSeen[gwMac] = Clock.Mono;
                Events.Add(2, "net", Voice.P("Your router's hardware address keeps flipping (" + gwMac + " → " + mac + ") on the same network. If nobody swapped the router, someone may be pretending to be it (ARP spoofing). Use the VPN until you know.",
                                             "Gateway MAC flips " + gwMac + " → " + mac + " on the same network - possible ARP spoofing."));
                gwMac = mac;
            }
        }
        // After a drop, ask Windows' own log why (a few seconds later, so the event is written).
        void ExplainWifiDrop()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(4000);
                string err; var rows = EvLog.WifiDrops(0.05, out err);
                if (rows.Count > 0) Events.Add(1, "wifi", "Why: " + rows[0][1]);
            });
        }
        void WifiEvents(string state, string bssid, string ch, int sig, int rate)
        {
            var now = DateTime.Now;
            if (pState != null)
            {
                if (state != pState && state.StartsWith("disconnected", StringComparison.Ordinal)) { Events.Add(2, "wifi", Voice.P("Wi-Fi dropped.", "Wi-Fi disconnected.")); ExplainWifiDrop(); }
                else if (state != pState && pState.StartsWith("disconnected", StringComparison.Ordinal) && state == "connected") Events.Add(0, "wifi", "Wi-Fi reconnected");
                if (bssid != "" && pBssid != "" && bssid != pBssid) { lastRoam = now; Events.Add(1, "wifi", Voice.P("Wi-Fi hopped to another access point", "Wi-Fi roamed to another access point") + " (ch " + ch + ", signal " + sig + "%, from " + pBssid + " to " + bssid + ")"); }
                else if (ch != "" && pCh != "" && ch != pCh) Events.Add(1, "wifi", "Wi-Fi channel changed " + pCh + " → " + ch);
                if (rate > 0 && pRate >= 50 && rate < pRate * 0.5) Events.Add(1, "wifi", "Wi-Fi link rate dropped " + pRate + " → " + rate + " Mb/s");
                if (sig >= 0 && pSig >= 0 && pSig - sig >= 25) { lastSigDrop = now; Events.Add(1, "wifi", "Wi-Fi signal fell to " + sig + "% (was " + pSig + "%)"); }
            }
            pState = state; pBssid = bssid; pCh = ch; pRate = rate; pSig = sig;
        }

        // Connection bursts and connections stuck knocking (SYN_SENT), from the socket table - no capture.
        void ProbeConns()
        {
            var now = Clock.Mono; double sec = Math.Max(1, (now - lastConnT).TotalSeconds); lastConnT = now;
            Dictionary<int, string> names; Dictionary<string, string> ipn; lock (L) { names = PidNames; ipn = IpNames; }
            List<Conn> list; try { list = Tables.Tcp(false, names); } catch { return; }
            if (prevKeys != null)
                foreach (var g in list.Where(c => !prevKeys.Contains(c.Key)).GroupBy(c => c.Proc))
                {
                    int n = g.Count();
                    if (n >= 30) Once("burst|" + g.Key, 300, 1, "conn", g.Key + Voice.P(" went on a spree: ", " opened ") + n + " new connections in " + sec.ToString("0", CultureInfo.CurrentCulture) + " s");
                }
            var syn = new HashSet<string>();
            foreach (var c in list.Where(x => x.State == "SYN_SENT"))
            {
                syn.Add(c.Key); DateTime first;
                if (!synFirst.TryGetValue(c.Key, out first)) synFirst[c.Key] = now;
                else if ((now - first).TotalSeconds >= 6)
                {
                    string h; string host = !string.IsNullOrEmpty(c.RemoteIp) && ipn.TryGetValue(c.RemoteIp, out h) ? h : c.RemoteIp;
                    Once("syn|" + c.Proc + "|" + c.RemoteIp + "|" + c.RemotePort, 300, 1, "conn", c.Proc + " is stuck connecting to " + host + ":" + c.RemotePort + Voice.P(" - knocking, nobody answers.", " (SYN_SENT)."));
                }
            }
            foreach (var k in synFirst.Keys.Where(x => !syn.Contains(x)).ToList()) synFirst.Remove(k);
            prevKeys = new HashSet<string>(list.Select(c => c.Key));
            try { Watch.Conns(this, list); } catch (Exception e) { Program.Log("watch: " + e.Message); }
        }

        static readonly Dictionary<string, string> friendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ms-teams", "Teams" }, { "teams", "Teams" }, { "chrome", "Chrome" }, { "msedge", "Edge" }, { "outlook", "Outlook" }, { "olk", "Outlook" },
            { "onedrive", "OneDrive" }, { "MsMpEng", "Defender" }, { "firefox", "Firefox" }, { "Code", "VS Code" }, { "WINWORD", "Word" },
            { "EXCEL", "Excel" }, { "POWERPNT", "PowerPoint" }, { "putty", "PuTTY" }, { "explorer", "Explorer" }, { "slack", "Slack" },
            { "Zoom", "Zoom" }, { "pycharm64", "PyCharm" }, { "AdeeboMeter", "AdeeboMeter" }, { "FoxitPDFReader", "Foxit PDF" }
        };
        public static string Friendly(string n) { string f; return friendly.TryGetValue(n, out f) ? f : n; }
        public static string RawOf(string fn) { foreach (var kv in friendly) if (kv.Value == fn) return kv.Key; return fn; }

        void ProbeProcesses(Dictionary<int, TimeSpan> lastCpu, ref DateTime lastT)
        {
            DateTime now = Clock.Mono; double el = (now - lastT).TotalSeconds; lastT = now;
            var groups = new Dictionary<string, AppStat>(); var names = new Dictionary<int, string>(); var cur = new Dictionary<int, TimeSpan>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == 0) continue;
                    string fn = Friendly(p.ProcessName); names[p.Id] = fn;
                    AppStat a; if (!groups.TryGetValue(fn, out a)) { a = new AppStat { Name = fn }; groups[fn] = a; }
                    a.Count++; if (a.Pid == 0) a.Pid = p.Id;
                    try { a.Mem += p.WorkingSet64; } catch { }
                    try { TimeSpan t = p.TotalProcessorTime; cur[p.Id] = t; TimeSpan pt; if (el > 0 && lastCpu.TryGetValue(p.Id, out pt)) a.Cpu += Math.Max(0, (t - pt).TotalSeconds / el / Cores * 100); } catch { }
                }
                catch { }
                finally { p.Dispose(); }
            }
            lastCpu.Clear(); foreach (var kv in cur) lastCpu[kv.Key] = kv.Value;
            lock (L) { Apps = groups.Values.ToList(); PidNames = names; }
        }

        void ProbeCall()
        {
            // Teams opens UDP media sockets (default 50000-50059) only while a call is running.
            bool call = false; Dictionary<int, string> names; lock (L) names = PidNames;
            if (!names.ContainsValue("Teams")) { lock (L) CallLikely = false; if (prevCall) Events.Add(0, "call", "Teams call ended (Teams closed)"); prevCall = false; return; }
            foreach (var u in Tables.Udp())
            {
                string n; if (!names.TryGetValue(u.Value, out n) || n != "Teams") continue;
                if (u.Key >= 50000 && u.Key <= 50059) { call = true; break; }
            }
            lock (L) CallLikely = call;
            if (call != prevCall) Events.Add(0, "call", call ? "Teams call started" : "Teams call ended");
            prevCall = call;
        }

        // Names for remote IPs, read from Windows' own DNS cache - no network traffic.
        public void RefreshIpNames()
        {
            var map = new Dictionary<string, string>();
            IntPtr table;
            try
            {
                if (!Native.DnsGetCacheDataTable(out table)) return;
                IntPtr e = table; int count = 0; int P = IntPtr.Size;
                while (e != IntPtr.Zero)
                {
                    IntPtr next = Marshal.ReadIntPtr(e, 0), pName = Marshal.ReadIntPtr(e, P);
                    int type = Marshal.ReadInt16(e, 2 * P) & 0xFFFF;
                    string name = pName != IntPtr.Zero ? Marshal.PtrToStringUni(pName) : null;
                    if (name != null && (type == 1 || type == 28) && count++ < 3000) LookupCached(name, (short)type, map);
                    if (pName != IntPtr.Zero) Native.DnsFree(pName, 0);
                    Native.DnsFree(e, 0);
                    e = next;
                }
            }
            catch (Exception ex) { Program.Log("dns cache: " + ex.Message); }
            // Some Windows builds hand back an empty table from that API. Then read the same cache the way Windows' own
            // ipconfig /displaydns shows it - still offline, only while the engineer view is open, at most every 30 s.
            if (map.Count == 0 && !dnsCacheLocked && (Clock.Mono - lastDisplayDns).TotalSeconds >= (emptyDisplayDns >= 2 ? 300 : 30))   /* two empty answers in a row: every 5 min */
            {
                lastDisplayDns = Clock.Mono;
                try
                {
                    string outp = Job.Run(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/displaydns", 5000) ?? "";
                    // Some Windows builds keep the DNS cache admin-only. Then stop asking: names come from live capture instead.
                    if (outp.IndexOf("elevation", StringComparison.OrdinalIgnoreCase) >= 0) { dnsCacheLocked = true; Program.Log("DNS cache is admin-only on this PC"); }
                    else { ParseDisplayDns(outp, map); emptyDisplayDns = map.Count == 0 ? emptyDisplayDns + 1 : 0; }
                }
                catch (Exception ex) { Program.Log("displaydns: " + ex.Message); }
            }
            if (map.Count == 0) { lock (L) if (IpNames.Count > 0) return; }   // keep the last good names rather than blanking them
            lock (L) IpNames = map;
        }
        DateTime lastDisplayDns = DateTime.MinValue; int emptyDisplayDns; public volatile bool dnsCacheLocked;
        static readonly char[] IpChars = { '.', ':' };
        // Each block starts with the queried name over a dashed line; any record line whose value is an IP maps that IP to the name.
        public static void ParseDisplayDns(string text, Dictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(text)) return;
            string header = null, prev = null; int n = 0;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("----", StringComparison.Ordinal)) { header = prev; continue; }
                if (line != "") prev = line;
                if (header == null || header.Length > 253) continue;
                int c = line.LastIndexOf(" : ", StringComparison.Ordinal); if (c < 0) continue;
                string v = line.Substring(c + 3).Trim(); IPAddress ip;
                if (v.IndexOfAny(IpChars) < 0 || !IPAddress.TryParse(v, out ip)) continue;
                string k = ip.ToString();
                if (!map.ContainsKey(k)) map[k] = header.TrimEnd('.');
                if (++n > 5000) break;
            }
        }
        static void LookupCached(string name, short type, Dictionary<string, string> map)
        {
            IntPtr res;
            if (Native.DnsQuery(name, type, 0x10 /*NO_WIRE_QUERY: cache only*/, IntPtr.Zero, out res, IntPtr.Zero) != 0 || res == IntPtr.Zero) return;
            try
            {
                int P = IntPtr.Size, typeOff = 2 * P, dataOff = 2 * P + 16;
                for (IntPtr r = res; r != IntPtr.Zero; r = Marshal.ReadIntPtr(r, 0))
                {
                    int t = Marshal.ReadInt16(r, typeOff) & 0xFFFF;
                    string ip = null;
                    if (t == 1) ip = new IPAddress((uint)Marshal.ReadInt32(r, dataOff)).ToString();
                    else if (t == 28) { var b = new byte[16]; Marshal.Copy(r + dataOff, b, 0, 16); ip = new IPAddress(b).ToString(); }
                    if (ip != null && !map.ContainsKey(ip)) map[ip] = name;
                }
            }
            finally { Native.DnsRecordListFree(res, 1); }
        }

        public List<Conn> Connections(bool includeIdle)
        {
            Dictionary<int, string> names; Dictionary<string, string> ipn; lock (L) { names = PidNames; ipn = IpNames; }
            var list = Tables.Tcp(includeIdle, names);
            if (includeIdle)
                foreach (var u in Tables.Udp())
                {
                    string n; names.TryGetValue(u.Value, out n);
                    list.Add(new Conn { Key = "U|" + u.Key + "|" + u.Value, Proto = "UDP", State = "BOUND", Local = "*:" + u.Key, Remote = "*", RemoteIp = "", LocalPort = u.Key, Pid = u.Value, Proc = n ?? "pid " + u.Value });
                }
            foreach (var c in list)
            {
                string h;
                if (!string.IsNullOrEmpty(c.RemoteIp) && ipn.TryGetValue(c.RemoteIp, out h)) { c.Host = h; c.HostFrom = "DNS cache"; }
                else if (c.State == "LISTEN") c.Host = "listening";
                else if (c.State == "BOUND") c.Host = "bound, not connected";
                else c.Host = c.RemoteIp;
            }
            return list;
        }

        // ---------- verdict, every 10 s
        public List<AppStat> TopApps(bool byCpu, int n, bool closableOnly)
        {
            List<AppStat> apps; lock (L) apps = Apps;
            return apps.Where(a => a.Name != "Idle" && (!closableOnly || !notClosable.Contains(a.Name)))
                       .OrderByDescending(a => byCpu ? a.Cpu : a.Mem).Take(n).ToList();
        }
        static readonly HashSet<string> notClosable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Idle", "System", "Registry", "Memory Compression", "svchost", "Defender", "csrss", "dwm", "Explorer", "lsass", "AdeeboMeter", "smss", "wininit", "services", "MsSense", "SearchHost", "Secure System", "MsMpEng" };

        void Judge()
        {
            double cpu = Cpu.Avg(10), mem = Mem.Avg(10), com = Commit.Avg(10);
            double gw, net, dns, loss, jit; bool hasWifi, vpn, netEver, gwEver, call, dnsFail; int sig; string gwAddr;
            lock (L)
            {
                gwAddr = Gateway != "" ? Gateway : Gateway6;
                gw = GwPing.Avg(5); net = NetPing.Avg(3); dns = Dns.Last; loss = NetEverReplied ? Loss.Avg(10) * 100 : double.NaN; jit = NetPing.Jitter(10);
                hasWifi = HasWifi; sig = WifiSignal; vpn = VpnUp; netEver = NetEverReplied; gwEver = GwEverReplied; call = CallLikely; dnsFail = DnsFailedLast;
            }
            int pc = (cpu > 88 || mem > 90 || com > 92) ? 2 : (cpu > 60 || mem > 80) ? 1 : 0;
            int link = 3;
            if (hasWifi && sig >= 0) link = sig < 30 ? 2 : sig < 55 ? 1 : 0;   // signal unknown (hidden by Windows): no verdict from it
            if (!double.IsNaN(gw)) link = Math.Max(link == 3 ? 0 : link, gw > 50 ? 2 : gw > 15 ? 1 : 0);
            else if (!hasWifi) link = gwEver && double.IsNaN(net) ? 2 : 3;   // the router went quiet but the internet still answers: it just ignores ping now - not a dragging link
            bool offline = gwAddr == "" && !vpn;   // no way out at all: Wi-Fi off, cable out, airplane mode
            if (offline) link = 2;
            int isp = 3;
            if (!double.IsNaN(net) && !double.IsNaN(gw)) { double d = net - gw; isp = d > 100 ? 2 : d > 40 ? 1 : 0; }
            else if (!double.IsNaN(net)) isp = net > 150 ? 2 : net > 60 ? 1 : 0;
            int dn = dnsFail ? 2 : double.IsNaN(dns) ? 3 : dns > 300 ? 2 : dns > 100 ? 1 : 0;
            int inet = !netEver ? 3 : loss >= 20 ? 2 : loss >= 10 ? 1 : 0;
            // not checked (Settings › Internet check off) or nothing measured lately: grey, never a verdict
            bool netStale; lock (L) netStale = Cfg.NetSec == 0 || (Clock.Mono - netAt).TotalSeconds > 3 * Math.Max(Cfg.NetSec, 15) + 10;
            if (netStale) { isp = 3; inet = 3; }
            if (link == 2) { isp = 3; dn = 3; inet = 3; }
            path = new[] { pc, link, isp, dn, inet };

            var top = TopApps(false, 2, true); var topC = TopApps(true, 1, true);
            string t1 = top.Count > 0 ? top[0].Name + " (" + Draw.MemStr(top[0].Mem) + ")" : "your apps", t2 = top.Count > 1 ? top[1].Name : "";
            string hogs = t1 + (t2 != "" ? " and " + t2 : "");
            string linkName = hasWifi ? "Wi-Fi" : "local network";
            if (offline) Set(2, Voice.Red("You're not connected to any network, boss - Wi-Fi is off, the cable is out, or airplane mode is on.", "Not connected to any network (no Wi-Fi, no cable, or airplane mode)."));
            else if (link == 2 && hasWifi && sig >= 0 && sig < 30) Set(2, Voice.Red("Boss, your Wi-Fi is whispering (signal " + sig + "%). Not the PC's fault - move closer or plug in a cable.", "Wi-Fi signal is weak (" + sig + "%). Move closer to the access point or use a cable."));
            else if (link == 2) Set(2, Voice.Red("Found it: your " + linkName + " is dragging (gateway " + Draw.Fmt(gw, "0", " ms") + "). The PC is innocent.", "The " + linkName + " is slow (gateway " + Draw.Fmt(gw, "0", " ms") + "). The PC is not the cause."));
            else if (pc == 2 && (mem > 90 || com > 92)) Set(2, Voice.Red("Your PC is cooked, boss - memory is full. " + hogs + " are the hungry ones; close something.", "Memory is almost full. " + hogs + " use the most; close something."));
            else if (pc == 2) Set(2, Voice.Red("CPU is running a marathon (" + cpu.ToString("0", CultureInfo.CurrentCulture) + "%). " + (topC.Count > 0 ? topC[0].Name : "Something") + " is doing the running.", "CPU is pegged (" + cpu.ToString("0", CultureInfo.CurrentCulture) + "%). " + (topC.Count > 0 ? topC[0].Name : "An app") + " uses the most."));
            else if (call && (jit > 30 || loss > 2)) Set(1, Voice.P("Call getting wobbly (jitter " + Draw.Fmt(jit, "0", " ms") + "). If faces freeze, turn video off - audio will survive.", "Call quality dropping (jitter " + Draw.Fmt(jit, "0", " ms") + "). Turn video off if it stutters."));
            else if (isp == 2 || inet == 2) Set(2, Voice.Red("Not you, not your PC - the internet past your router" + (vpn ? " / VPN" : "") + " is having a bad day.", "The internet past your router" + (vpn ? " / VPN" : "") + " is slow. Your PC and " + linkName + " are fine."));
            else if (dn == 2) Set(2, dnsFail ? Voice.Red("The internet's there but DNS lost the phone book - sites won't open by name.", "DNS lookups are failing; sites won't open by name.") : Voice.Red("DNS is taking the scenic route (" + dns.ToString("0", CultureInfo.CurrentCulture) + " ms) - pages will be slow to start.", "DNS is slow (" + dns.ToString("0", CultureInfo.CurrentCulture) + " ms); pages will be slow to start."));
            else if (call) Set(0, Voice.P("Call looks smooth. Go impress them.", "Call quality is good."));
            else if (pc == 1) Set(1, Voice.P("Busy, but holding. Maybe don't open another big app just yet.", "Busy but fine. Avoid opening big apps for now."));
            else if (link == 1 || isp == 1 || dn == 1 || inet == 1) Set(1, Voice.P("Network's a bit sleepy; your PC is fine.", "Network a bit slow; your PC is fine."));
            else Set(0, Voice.P("All quiet. Nothing to fix - go get coffee.", "All quiet."));
            // the Investigate check that fits the headline's cause: a red PC first, then a lost or weak link (drops) vs a slow gateway (slow), then DNS, then a call in progress
            bool linkDown = offline || link == 2 && (double.IsNaN(gw) || hasWifi && sig >= 0 && sig < 30);
            flow = level == 0 ? "" : pc == 2 ? "pc" : linkDown ? "drops" : link == 2 ? "slow" : dn == 2 ? "site" : call ? "calls" : pc >= 1 && isp < 2 && inet < 2 ? "pc" : "slow";

            // gateway latency jumps (against its own calm baseline)
            if (!double.IsNaN(gw))
            {
                if (double.IsNaN(gwBase)) gwBase = gw;
                if (!gwFlag && gw > Math.Max(30, gwBase * 3)) { gwFlag = true; Events.Add(1, "gw", "Gateway latency jumped to " + gw.ToString("0", CultureInfo.CurrentCulture) + " ms (normally " + gwBase.ToString("0", CultureInfo.CurrentCulture) + " ms)"); }
                else if (gwFlag && gw < Math.Max(15, gwBase * 2)) { gwFlag = false; Events.Add(0, "gw", "Gateway latency back to " + gw.ToString("0", CultureInfo.CurrentCulture) + " ms"); }
                if (!gwFlag) gwBase = gwBase * 0.9 + gw * 0.1;
            }
            // red in / out
            if (prevLevel >= 0 && level == 2 && prevLevel != 2) Events.Add(2, "verdict", headline);
            else if (prevLevel == 2 && level != 2) Events.Add(0, "verdict", Voice.P("Back to normal.", "Recovered.") + " " + headline);
            prevLevel = level;

            // cause hints: put two facts side by side, only when both are true now
            double retx = Retx.Avg(10); var now = DateTime.Now; string h = "";
            bool roamed = (now - lastRoam).TotalSeconds < 90, sigDropped = (now - lastSigDrop).TotalSeconds < 90;
            if (retx >= 5)
            {
                if (hasWifi && sig >= 0 && (sig < 50 || sigDropped)) h = "TCP retransmits (" + retx.ToString("0", CultureInfo.CurrentCulture) + "/s) started when Wi-Fi signal fell to " + sig + "%.";
                else if (roamed) h = "TCP retransmits (" + retx.ToString("0", CultureInfo.CurrentCulture) + "/s) right after a Wi-Fi roam - should settle in a few seconds.";
                else if (!double.IsNaN(gw) && gw > 30) h = "TCP retransmits with a slow gateway (" + gw.ToString("0", CultureInfo.CurrentCulture) + " ms): the local link is struggling, not the internet.";
                else if (cpu > 88) h = "TCP retransmits while the CPU is pegged: the PC may be too busy to keep up.";
                else if (!double.IsNaN(gw) && gw < 15) h = "TCP retransmits (" + retx.ToString("0", CultureInfo.CurrentCulture) + "/s) with a healthy local link: the loss is further out (ISP, VPN or the server).";
            }
            else if (dn >= 1 && vpn) h = "DNS goes through the VPN - the slow lookups are on the VPN side.";
            else if (inet >= 1 && roamed) h = "Packet loss right after a Wi-Fi roam - should settle in a few seconds.";
            else if (call && hasWifi && sig >= 0 && sig < 50) h = "Teams call on weak Wi-Fi (" + sig + "%): a cable or moving closer helps the most.";
            if (h != "" && h != hint) Once("hint|" + h.Substring(0, Math.Min(30, h.Length)), 600, 1, "hint", h);
            hint = h;
        }
        void Set(int l, string h) { level = l; headline = h; }
        string flow = ""; DateTime netAt;
        public volatile int GwSecNow = 2, NetSecNow = 5;
        public DateTime GwAt; public DateTime NetAt { get { return netAt; } }   // steady-clock time of the newest router / internet sample (old after sleep)

        Snap BuildSnap()
        {
            var s = new Snap { Level = level, Headline = headline, Flow = flow, Countdown = countdown, Path = path, Hint = hint, Recent = Events.Recent(3) };
            s.Chip = level == 2 ? Voice.P("Needs you", "Problem") : level == 1 ? "Busy" : Voice.P("All quiet", "OK");
            s.Cpu = Cpu.Last; s.Mem = Mem.Last; s.Disk = Disk.Last; s.Down = Down.Last; s.Up = Up.Last;
            s.DownHist = Down.Values(60); s.UpHist = Up.Values(60);
            s.MemText = UsedGB.ToString("0.0", CultureInfo.CurrentCulture) + " / " + TotalGB.ToString("0", CultureInfo.CurrentCulture) + " GB";
            s.DiskText = Draw.Fmt(s.Disk, "0", "% active");
            string gwS, adapter, speed, dnsSrv, vpnName, ssid, ch, target, cfree; bool hasWifi, vpn, netEver, viaTcp; int sig, vmtu, pmtu;
            double gw, net, dns, loss, jit, readMB, writeMB, downTot;
            lock (L)
            {
                gwS = Gateway; adapter = Adapter; speed = LinkSpeed; dnsSrv = DnsServer; vpnName = VpnName; ssid = Ssid; ch = WifiChannel; target = NetTarget; cfree = CFree;
                hasWifi = HasWifi; vpn = VpnUp; netEver = NetEverReplied; sig = WifiSignal; vmtu = VpnMtu; pmtu = PrimaryMtu; viaTcp = NetViaTcp;
                gw = GwPing.Last; net = NetPing.Last; dns = Dns.Last; loss = NetEverReplied ? Loss.Avg(10) * 100 : double.NaN; jit = NetPing.Jitter(10);
                s.Call = CallLikely; readMB = DiskReadMBs; writeMB = DiskWriteMBs; downTot = DownBytesTotal;
            }
            s.Ping = net; s.Jitter = jit; s.LossPct = loss; s.DnsMs = dns; s.GwMs = gw; s.NetHist = NetPing.Values(60);
            s.PathLabel = new[] { "PC", hasWifi ? "Wi-Fi" : "LAN", vpn ? "VPN" : "ISP", "DNS", "Internet" };
            string[] word = { "OK", "Slow", "Problem here", "Unknown" };
            s.PathMain = new[]
            {
                "CPU " + Draw.Fmt(s.Cpu, "0", "%") + " · RAM " + Draw.Fmt(s.Mem, "0", "%"),
                (hasWifi ? "signal " + sig + "% · " : "") + "gw " + Draw.Fmt(gw, "0", " ms"),
                !double.IsNaN(net) && !double.IsNaN(gw) ? "+" + Math.Max(0, net - gw).ToString("0", CultureInfo.CurrentCulture) + " ms past router" : (vpn ? "tunnel up" : "-"),
                Draw.Fmt(dns, "0", " ms"),
                netEver ? Draw.Fmt(net, "0", " ms") + (viaTcp ? " (TCP)" : "") + " · loss " + Draw.Fmt(loss, "0", "%") : "no reply yet"
            };
            s.PathSub = new[]
            {
                word[path[0]] + " · " + Cores + " threads · " + TotalGB.ToString("0", CultureInfo.CurrentCulture) + " GB",
                word[path[1]] + " · " + (hasWifi ? (ssid != "" ? ssid : "Wi-Fi") + (ch != "" ? " ch " + ch : "") : adapter) + (gwS != "" ? " · " + gwS : ""),
                word[path[2]] + " · " + (vpn ? Draw.Trunc(vpnName, 24) + (vmtu > 0 ? " · MTU " + vmtu : "") : (Isp != "" ? Draw.Trunc(Isp, 22) : "no VPN") + (pmtu > 0 ? " · MTU " + pmtu : "")),
                word[path[3]] + " · " + (dnsSrv != "" ? dnsSrv : "no DNS server"),
                word[path[4]] + " · probe " + target
            };
            s.TopMem = TopApps(false, 6, false); s.TopCpu = TopApps(true, 6, false);
            Func<List<AppStat>, bool, List<string[]>> rows = delegate(List<AppStat> l, bool c) { return l.Take(3).Select(a => new[] { a.Name + (a.Count > 1 ? " (" + a.Count + ")" : ""), c ? a.Cpu.ToString("0", CultureInfo.CurrentCulture) + "%" : Draw.MemStr(a.Mem) }).ToList(); };
            s.Details["cpu"] = new Detail { Title = "CPU", TopTitle = "TOP BY CPU", Top = rows(s.TopCpu, true), Note = Voice.P("Above 88% for 10 s and I start nagging.", "Above 88% for 10 s turns AdeeboMeter red."),
                Lines = new List<string[]> { new[] { "Now", Draw.Fmt(s.Cpu, "0", "%") }, new[] { "10 s average", Draw.Fmt(Cpu.Avg(10), "0", "%") }, new[] { "10 s peak", Draw.Fmt(Cpu.Max(10), "0", "%") }, new[] { "Logical cores", Cores.ToString(CultureInfo.CurrentCulture) } } };
            s.Details["ram"] = new Detail { Title = "Memory", TopTitle = "TOP BY MEMORY", Top = rows(s.TopMem, false), Note = "Commit is the real \"can I open another app\" number.",
                Lines = new List<string[]> { new[] { "In use", UsedGB.ToString("0.0", CultureInfo.CurrentCulture) + " of " + TotalGB.ToString("0.0", CultureInfo.CurrentCulture) + " GB" }, new[] { "Commit", CommitUsedGB.ToString("0.0", CultureInfo.CurrentCulture) + " of " + CommitLimitGB.ToString("0.0", CultureInfo.CurrentCulture) + " GB" }, new[] { "Available", (TotalGB - UsedGB).ToString("0.0", CultureInfo.CurrentCulture) + " GB" }, new[] { "10 s peak", Draw.Fmt(Mem.Max(10), "0", "%") } } };
            s.Details["disk"] = new Detail { Title = "Disk", Note = "A busy disk often explains slow app launches.",
                Lines = new List<string[]> { new[] { "Active time", Draw.Fmt(s.Disk, "0", "%") }, new[] { "Read / write", Draw.Fmt(readMB, "0.0", "") + " / " + Draw.Fmt(writeMB, "0.0", " MB/s") }, new[] { "C:", cfree } } };
            s.Details["net"] = new Detail { Title = "Traffic", Note = "Who's talking to whom: click the tray icon.",
                Lines = new List<string[]> { new[] { "Adapter", Draw.Trunc(adapter, 24) }, new[] { "Link speed", speed == "" ? "-" : speed }, new[] { "Down / up now", Draw.Fmt(s.Down, "0.0", "") + " / " + Draw.Fmt(s.Up, "0.0", " Mb/s") }, new[] { "Since start", Draw.MemStr((long)downTot) + " down" } } };
            var pd = new Detail { Title = "Is it me or the network?", Note = "Gateway fast but internet slow = past your PC and " + (hasWifi ? "Wi-Fi" : "LAN") + ". Gateway slow = Wi-Fi or cable." };
            for (int i = 0; i < 5; i++) pd.Lines.Add(new[] { s.PathLabel[i], s.PathMain[i], path[i].ToString(CultureInfo.CurrentCulture) });
            pd.TopTitle = "WHAT CHANGED"; foreach (var e in s.Recent) pd.Top.Add(new[] { e.Text, e.T.ToString("HH:mm", CultureInfo.CurrentCulture) });
            s.Details["path"] = pd;
            s.Details["call"] = new Detail { Title = "Meeting mode", Note = "On only while Teams has call media sockets open (UDP 50000-50059).",
                Lines = new List<string[]> { new[] { "Upload", Draw.Fmt(s.Up, "0.0", " Mb/s") }, new[] { "Jitter", Draw.Fmt(jit, "0", " ms") }, new[] { "Loss", Draw.Fmt(loss, "0", "%") } } };
            AddHealth(s, "TCP retransmits", Retx, true, " /s", 2, 8);
            AddHealth(s, "TCP resets sent", Rst, true, " /s", 1, 3);
            AddHealth(s, "Failed connection attempts", Fail, false, " /min", 5, 20);
            AddHealth(s, "ICMP unreachable received", Unreach, false, " /min", 5, 20);
            AddHealth(s, "UDP datagrams dropped", UdpDrop, false, " /min", 10, 50);
            return s;
        }
        static void AddHealth(Snap s, string label, Ring r, bool perSec, string unit, double yl, double rl)
        {
            double v = perSec ? r.Avg(5) : r.Sum(60);
            int lv = double.IsNaN(v) ? 3 : v >= rl ? 2 : v >= yl ? 1 : 0;
            s.Health.Add(new[] { label, Draw.Fmt(v, perSec ? "0.0" : "0", unit), lv.ToString(CultureInfo.CurrentCulture) });
            s.HealthHist.Add(r.Values(60));
        }
    }

    // ================================================================== socket tables (no admin)
    static class Tables
    {
        static int Port(uint p) { return (int)(((p & 0xFF) << 8) | ((p >> 8) & 0xFF)); }
        static string State(int st)
        {
            switch (st)
            {
                case 1: return "CLOSED"; case 2: return "LISTEN"; case 3: return "SYN_SENT"; case 4: return "SYN_RCVD"; case 5: return "ESTABLISHED";
                case 6: return "FIN_WAIT1"; case 7: return "FIN_WAIT2"; case 8: return "CLOSE_WAIT"; case 9: return "CLOSING"; case 10: return "LAST_ACK";
                case 11: return "TIME_WAIT"; case 12: return "DELETE_TCB"; case 100: return "BOUND";
                default:
                    lock (oddStates) if (oddStates.Add(st)) Program.Log("TCP state number " + st + " seen (not in Windows' documented list)");
                    return "STATE " + st;
            }
        }
        static IntPtr Get(bool tcp, int af)
        {
            int len = 0, cls = tcp ? 5 : 1;
            uint size = tcp ? Native.GetExtendedTcpTable(IntPtr.Zero, ref len, true, af, cls, 0) : Native.GetExtendedUdpTable(IntPtr.Zero, ref len, true, af, cls, 0);
            if (size != 0 && size != 122 /*ERROR_INSUFFICIENT_BUFFER*/) return IntPtr.Zero;
            for (int i = 0; i < 3; i++)
            {
                len += 4096; IntPtr buf = Marshal.AllocHGlobal(len);
                uint rc = tcp ? Native.GetExtendedTcpTable(buf, ref len, true, af, cls, 0) : Native.GetExtendedUdpTable(buf, ref len, true, af, cls, 0);
                if (rc == 0) return buf;
                Marshal.FreeHGlobal(buf);
            }
            return IntPtr.Zero;
        }
        public static List<KeyValuePair<int, int>> Udp()
        {
            var r = new List<KeyValuePair<int, int>>();
            foreach (int af in new[] { 2, 23 })
            {
                IntPtr b = Get(false, af); if (b == IntPtr.Zero) continue;
                try
                {
                    int n = Marshal.ReadInt32(b), size = af == 2 ? 12 : 28;
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr row = b + 4 + i * size;
                        r.Add(new KeyValuePair<int, int>(Port((uint)Marshal.ReadInt32(row, af == 2 ? 4 : 20)), Marshal.ReadInt32(row, af == 2 ? 8 : 24)));
                    }
                }
                finally { Marshal.FreeHGlobal(b); }
            }
            return r;
        }
        public static List<Conn> Tcp(bool includeIdle, Dictionary<int, string> names)
        {
            var list = new List<Conn>();
            Func<int, string> nm = delegate(int pid) { string s; return names.TryGetValue(pid, out s) ? s : (pid == 4 ? "System" : pid == 0 ? "Windows (no owner)" : "pid " + pid); };
            IntPtr b = Get(true, 2);   // v4 row: state, laddr, lport, raddr, rport, pid
            if (b != IntPtr.Zero)
                try
                {
                    int n = Marshal.ReadInt32(b);
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr row = b + 4 + i * 24; int st = Marshal.ReadInt32(row, 0);
                        if (st == 0 || (st == 1 && Marshal.ReadInt32(row, 12) == 0)) st = 100;   // undocumented 0 (or CLOSED) with no remote end: an idle bound socket
                        bool idle = st == 2 || st == 100;
                        if (!includeIdle && idle) continue;
                        var la = new IPAddress((uint)Marshal.ReadInt32(row, 4)); var ra = new IPAddress((uint)Marshal.ReadInt32(row, 12));
                        int lp = Port((uint)Marshal.ReadInt32(row, 8)), rp = Port((uint)Marshal.ReadInt32(row, 16)), pid = Marshal.ReadInt32(row, 20);
                        if (!idle && IPAddress.IsLoopback(ra)) continue;
                        list.Add(new Conn { Key = "T|" + la + ":" + lp + "|" + ra + ":" + rp + "|" + pid, Proto = "TCP", State = State(st), Local = la + ":" + lp, Remote = idle ? "*" : ra + ":" + rp, RemoteIp = idle ? "" : ra.ToString(), RemotePort = rp, LocalPort = lp, Pid = pid, Proc = nm(pid) });
                    }
                }
                finally { Marshal.FreeHGlobal(b); }
            b = Get(true, 23);         // v6 row: laddr16, scope, lport, raddr16, scope, rport, state, pid (56 bytes)
            if (b != IntPtr.Zero)
                try
                {
                    int n = Marshal.ReadInt32(b);
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr row = b + 4 + i * 56; int st = Marshal.ReadInt32(row, 48);
                        if (st == 0 || (st == 1 && Marshal.ReadInt64(row, 24) == 0 && Marshal.ReadInt64(row, 32) == 0)) st = 100;
                        bool idle = st == 2 || st == 100;
                        if (!includeIdle && idle) continue;
                        byte[] l = new byte[16], r = new byte[16]; Marshal.Copy(row, l, 0, 16); Marshal.Copy(row + 24, r, 0, 16);
                        var la = new IPAddress(l); var ra = new IPAddress(r);
                        int lp = Port((uint)Marshal.ReadInt32(row, 20)), rp = Port((uint)Marshal.ReadInt32(row, 44)), pid = Marshal.ReadInt32(row, 52);
                        if (!idle && IPAddress.IsLoopback(ra)) continue;
                        list.Add(new Conn { Key = "T6|" + la + "|" + lp + "|" + ra + "|" + rp + "|" + pid, Proto = "TCP6", State = State(st), Local = "[" + la + "]:" + lp, Remote = idle ? "*" : "[" + ra + "]:" + rp, RemoteIp = idle ? "" : ra.ToString(), RemotePort = rp, LocalPort = lp, Pid = pid, Proc = nm(pid) });
                    }
                }
                finally { Marshal.FreeHGlobal(b); }
            return list;
        }
        public static readonly HashSet<int> oddStates = new HashSet<int>();
        public static bool OwnListener()
        {
            int me; using (var p = Process.GetCurrentProcess()) me = p.Id;
            return Tcp(true, new Dictionary<int, string>()).Any(c => c.Pid == me && c.State == "LISTEN");
        }
    }
}
