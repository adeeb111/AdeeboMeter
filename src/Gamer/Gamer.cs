// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== Gamer Zone: games, their servers, and how we measure them
    // Rules: nothing is sent to a game company's servers until the user clicks that game (Cfg.GameIds = consent). Installed games
    // are found by READING the launchers' own install lists. Probes are ICMP echo or a TCP connect (timed, then closed) - never
    // game traffic, never listening. Caps: 8 servers at once, 1 probe a second each, 10 probes a second in total.
    sealed class GameServer
    {
        public string Name, Where, Host, Kind = "estimate";   // Kind: estimate (cloud region the game runs in) / exact (captured or typed) / valve (Valve's list)
        public int Port;                                       // 0 = Auto: ICMP, then TCP 443 if ICMP gets no answer; >0 = TCP to this port only; -1 = ICMP only
        public string Method { get { return Port > 0 ? "TCP " + Port : Port < 0 ? "ICMP" : "Auto"; } }
        public string Key(string game) { return game + "|" + Name; }
    }
    sealed class Game
    {
        public string Id, Name, Color = "#4B5563", Note = "";
        public string[] Exes = Array.Empty<string>();
        public int[] SteamIds = Array.Empty<int>(); public string[] EpicNames = Array.Empty<string>(); public string RiotId;
        public bool Valve, Soon, Custom;
        public List<GameServer> Servers = new List<GameServer>();
    }

    static class Games
    {
        // ---------- built-in list. Regions marked "estimate" measure the public cloud region the game runs in (or the nearest one);
        // the game companies don't publish game-server addresses, and "Find my match server" replaces an estimate with the real one.
        static GameServer Aws(string name, string where, string region) { return new GameServer { Name = name, Where = where, Host = "dynamodb." + region + ".amazonaws.com", Port = 443, Kind = "estimate" }; }
        static List<GameServer> L(params GameServer[] s) { return s.ToList(); }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "built once per load; each game owns its arrays")]
        public static List<Game> BuiltIn()
        {
            var na = L(Aws("NA East", "Virginia", "us-east-1"), Aws("NA Central", "Ohio", "us-east-2"), Aws("NA West", "Oregon", "us-west-2"));
            Func<List<GameServer>> world = () => na.Concat(L(Aws("Europe", "Frankfurt", "eu-central-1"), Aws("Europe West", "London", "eu-west-2"), Aws("Brazil", "Sao Paulo", "sa-east-1"), Aws("Asia", "Tokyo", "ap-northeast-1"), Aws("Southeast Asia", "Singapore", "ap-southeast-1"), Aws("India", "Mumbai", "ap-south-1"), Aws("Oceania", "Sydney", "ap-southeast-2"), Aws("Middle East", "Bahrain", "me-south-1"))).ToList();
            return new List<Game> {
                new Game { Id = "valorant", Name = "Valorant", Color = "#E5484D", Exes = new[] { "VALORANT-Win64-Shipping" }, RiotId = "valorant", Servers = world(), Note = "Riot's own network - regions are estimates until Find my match server finds yours." },
                new Game { Id = "league", Name = "League of Legends", Color = "#1F6FEB", Exes = new[] { "League of Legends" }, RiotId = "league_of_legends", Servers = world(), Note = "Riot's own network - regions are estimates." },
                new Game { Id = "cs2", Name = "Counter-Strike 2", Color = "#D29922", Exes = new[] { "cs2" }, SteamIds = new[] { 730 }, Valve = true, Note = "Valve's own server list - real relays, refreshed when you ask." },
                new Game { Id = "dota2", Name = "Dota 2", Color = "#B42318", Exes = new[] { "dota2" }, SteamIds = new[] { 570 }, Valve = true, Note = "Valve's own server list - real relays, refreshed when you ask." },
                new Game { Id = "fortnite", Name = "Fortnite", Color = "#8250DF", Exes = new[] { "FortniteClient-Win64-Shipping" }, EpicNames = new[] { "Fortnite" }, Servers = world(), Note = "Runs in public cloud regions - these are the regions it uses." },
                new Game { Id = "rocketleague", Name = "Rocket League", Color = "#0EA5E9", Exes = new[] { "RocketLeague" }, EpicNames = new[] { "Sugar" }, SteamIds = new[] { 252950 }, Servers = world() },
                new Game { Id = "apex", Name = "Apex Legends", Color = "#C2410C", Exes = new[] { "r5apex", "r5apex_dx12" }, SteamIds = new[] { 1172470 }, Servers = world() },
                new Game { Id = "overwatch", Name = "Overwatch 2", Color = "#E3A008", Exes = new[] { "Overwatch" }, SteamIds = new[] { 2357570 }, Servers = world(), Note = "Blizzard's own data centres - regions are estimates." },
                new Game { Id = "cod", Name = "Call of Duty", Color = "#4B5563", Exes = new[] { "cod" }, SteamIds = new[] { 1938090 }, Servers = world() },
                new Game { Id = "gtaonline", Name = "GTA Online", Color = "#2DA44E", Exes = new[] { "GTA5", "GTA5_Enhanced" }, SteamIds = new[] { 271590, 3240220 }, Servers = world(), Note = "Mostly player-hosted sessions: this shows your path to the region, not to other players." },
                new Game { Id = "gta6", Name = "GTA VI", Color = "#DB2777", Soon = true, Note = "Coming when it launches on PC." },
                new Game { Id = "roblox", Name = "Roblox", Color = "#64748B", Exes = new[] { "RobloxPlayerBeta" }, Servers = world(), Note = "Roblox's own data centres - regions are estimates." },
                new Game { Id = "marvelrivals", Name = "Marvel Rivals", Color = "#9333EA", Exes = new[] { "Marvel-Win64-Shipping" }, SteamIds = new[] { 2767030 }, Servers = world() },
                new Game { Id = "minecraft", Name = "Minecraft server", Color = "#3FB950", Exes = new[] { "javaw", "Minecraft.Windows" }, Note = "Add your server's address in Edit." } };
        }

        // ---------- the user's own games / servers / edits: games.txt in the data folder (never next to the exe)
        // line format: game|id|name|exe1;exe2   or   server|gameid|name|where|host|port|kind   (fields validated on read)
        static readonly object k = new object();
        static List<Game> all;
        public static string FilePath { get { return Path.Combine(Program.DataDir, "games.txt"); } }
        static readonly Regex HostRx = new Regex(@"^(?=.{1,253}$)([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$", RegexOptions.CultureInvariant);
        public static bool ValidHost(string h)
        {
            if (string.IsNullOrEmpty(h) || h.Length > 253) return false;
            IPAddress ip; if (IPAddress.TryParse(h, out ip)) return h.IndexOf('%') < 0;
            return HostRx.IsMatch(h);
        }
        static string Clean(string s, int max) { s = (s ?? "").Trim(); s = new string(s.Where(c => !char.IsControl(c) && c != '|').ToArray()); return s.Length > max ? s.Substring(0, max) : s; }
        public static List<Game> All
        {
            get
            {
                lock (k)
                {
                    if (all != null) return all;
                    all = BuiltIn();
                    try { if (File.Exists(FilePath)) ApplyFile(File.ReadLines(FilePath).Take(500)); } catch (Exception e) { Program.Log("games: " + e.Message); }
                    foreach (var g in all.Where(x => x.Valve)) g.Servers = Valve.Cached() ?? g.Servers;
                    return all;
                }
            }
        }
        public static void Reload() { lock (k) all = null; }
        // the file holds whole replacement server lists per edited game, plus custom games
        static void ApplyFile(IEnumerable<string> lines)
        {
            var edited = new HashSet<string>();
            foreach (var raw in lines)
            {
                var f = raw.Split('|');
                if (f[0] == "game" && f.Length >= 4)
                {
                    string id = Clean(f[1], 40).ToLowerInvariant(); if (id.Length == 0 || !id.All(c => char.IsLetterOrDigit(c) || c == '-')) continue;
                    var g = all.FirstOrDefault(x => x.Id == id);
                    if (g == null) { g = new Game { Id = id, Custom = true }; all.Add(g); }
                    string nm = Clean(f[2], 40); if (nm.Length > 0) g.Name = nm; else if (string.IsNullOrEmpty(g.Name)) g.Name = id;   // a nameless custom game is shown by its id, never null
                    var ex = f[3].Split(';').Select(x => Clean(x, 60)).Where(x => x.Length > 0 && x.IndexOfAny(Path.GetInvalidFileNameChars()) < 0).Take(4).ToArray();
                    if (ex.Length > 0 || g.Custom) g.Exes = ex;
                }
                else if (f[0] == "server" && f.Length >= 7)
                {
                    string id = Clean(f[1], 40).ToLowerInvariant(); var g = all.FirstOrDefault(x => x.Id == id); if (g == null) continue;
                    if (edited.Add(id)) g.Servers = new List<GameServer>();
                    string host = Clean(f[4], 253); int port; if (!ValidHost(host) || !int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < -1 || port > 65535) continue;
                    if (g.Servers.Count < 12) g.Servers.Add(new GameServer { Name = Clean(f[2], 30), Where = Clean(f[3], 40), Host = host, Port = port, Kind = f[6] == "exact" ? "exact" : f[6] == "valve" ? "valve" : "estimate" });
                }
            }
        }
        // write one game's servers (and its name / programs) back; other games keep what they have
        public static void SaveGame(Game g)
        {
            lock (k)
            {
                try
                {
                    var keep = File.Exists(FilePath) ? File.ReadLines(FilePath).Take(500).Where(l => { var f = l.Split('|'); return f.Length > 1 && !string.Equals(f[1], g.Id, StringComparison.OrdinalIgnoreCase); }).ToList() : new List<string>();
                    keep.Add("game|" + g.Id + "|" + Clean(g.Name, 40) + "|" + string.Join(";", g.Exes.Select(x => Clean(x, 60))));
                    foreach (var s in g.Servers.Take(12)) keep.Add("server|" + g.Id + "|" + Clean(s.Name, 30) + "|" + Clean(s.Where, 40) + "|" + Clean(s.Host, 253) + "|" + s.Port.ToString(CultureInfo.InvariantCulture) + "|" + s.Kind);
                    string tmp = FilePath + ".tmp"; File.WriteAllLines(tmp, keep);
                    Program.ReplaceFile(tmp, FilePath);
                }
                catch (Exception e) { Program.Log("games save: " + e.Message); }
                all = null;
            }
        }
        public static void Reset(string id)
        {
            lock (k)
            {
                try { if (File.Exists(FilePath)) File.WriteAllLines(FilePath, File.ReadLines(FilePath).Take(500).Where(l => { var f = l.Split('|'); return f.Length > 1 && !string.Equals(f[1], id, StringComparison.OrdinalIgnoreCase); }).ToList()); }
                catch (Exception e) { Program.Log("games reset: " + e.Message); }
                all = null;
            }
        }

        // ---------- consent: the games the user clicked
        static readonly char[] CommaSep = { ',' };
        public static HashSet<string> Monitored() { return new HashSet<string>((Cfg.GameIds ?? "").Split(CommaSep, StringSplitOptions.RemoveEmptyEntries)); }
        public static void SetMonitored(string id, bool on)
        {
            var m = Monitored(); if (on) m.Add(id); else m.Remove(id);
            Cfg.GameIds = string.Join(",", m.Take(20)); Cfg.Save();
        }
    }

    // ================================================================== which games are installed (read-only, nothing sent)
    static class GameFinder
    {
        static HashSet<string> found; static DateTime at;
        public static HashSet<string> Installed(List<Game> games)
        {
            if (found != null && (DateTime.UtcNow - at).TotalMinutes < 2) return found;
            var r = new HashSet<string>();
            var steam = SteamApps(); var epic = EpicApps(); var riot = RiotProducts();
            foreach (var g in games)
                if (g.SteamIds.Any(steam.Contains) || g.EpicNames.Any(n => epic.Contains(n)) || (g.RiotId != null && riot.Contains(g.RiotId))) r.Add(g.Id);
            found = r; at = DateTime.UtcNow; return r;
        }
        static string ReadSmall(string f, int max) { var fi = new FileInfo(f); return fi.Exists && fi.Length <= max ? File.ReadAllText(f) : ""; }
        // Steam: its folder from the user's registry, libraryfolders.vdf for the other libraries, appmanifest_<id>.acf per game
        public static HashSet<int> SteamApps()
        {
            var r = new HashSet<int>();
            try
            {
                string root = null;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) if (key != null) root = key.GetValue("SteamPath") as string;
                if (string.IsNullOrEmpty(root) || root.Length > 240) return r;
                var libs = new List<string> { root.Replace('/', '\\') };
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                foreach (Match m in Regex.Matches(ReadSmall(vdf, 256 * 1024), "\"path\"\\s+\"([^\"]{3,240})\"")) libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                foreach (var lib in libs.Distinct(StringComparer.OrdinalIgnoreCase).Take(16))
                {
                    string apps = Path.Combine(lib, "steamapps"); if (!Directory.Exists(apps)) continue;
                    foreach (var f in Directory.EnumerateFiles(apps, "appmanifest_*.acf").Take(2000))
                    {
                        int id; var n = Path.GetFileNameWithoutExtension(f).Substring("appmanifest_".Length);
                        if (int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) r.Add(id);
                    }
                }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is ArgumentException)) throw; }
            return r;
        }
        // Epic: one small JSON .item file per installed game in ProgramData (readable by every user)
        public static HashSet<string> EpicApps()
        {
            var r = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (!Directory.Exists(d)) return r;
                foreach (var f in Directory.EnumerateFiles(d, "*.item").Take(500))
                    foreach (Match m in Regex.Matches(ReadSmall(f, 512 * 1024), "\"AppName\"\\s*:\\s*\"([A-Za-z0-9_.-]{1,80})\"")) r.Add(m.Groups[1].Value);
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException)) throw; }
            return r;
        }
        // Riot: the client keeps one folder per installed product under ProgramData\Riot Games\Metadata (<product>.live)
        public static HashSet<string> RiotProducts()
        {
            var r = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "Metadata");
                if (!Directory.Exists(d)) return r;
                foreach (var sub in Directory.EnumerateDirectories(d).Take(50)) { var n = Path.GetFileName(sub); int dot = n.IndexOf('.'); r.Add(dot > 0 ? n.Substring(0, dot) : n); }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException)) throw; }
            return r;
        }
        // which monitored game is running right now (by its program name)
        public static Game Running(List<Game> games, HashSet<string> monitored)
        {
            try
            {
                var names = new HashSet<string>(Process.GetProcesses().Select(p => { try { using (p) return p.ProcessName; } catch (InvalidOperationException) { return ""; } }), StringComparer.OrdinalIgnoreCase);
                return games.FirstOrDefault(g => monitored.Contains(g.Id) && g.Exes.Any(names.Contains));
            }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.ComponentModel.Win32Exception)) throw; return null; }
        }
    }

    // ================================================================== Valve's public server list (CS2, Dota 2)
    // One HTTPS request to Valve's Steam Web API, only when the user clicks a Valve game or presses "Refresh list". Saved in the
    // data folder; read strictly (IPv4 relays and region names only).
    static class Valve
    {
        static string FilePath { get { return Path.Combine(Program.DataDir, "valve-servers.txt"); } }
        public static List<GameServer> Cached()
        {
            try { return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath)) : null; } catch (IOException) { return null; }
        }
        public static DateTime? CachedAt() { try { return File.Exists(FilePath) ? File.GetLastWriteTime(FilePath) : (DateTime?)null; } catch (IOException) { return null; } }
        // GetSDRConfig: "pops": { "iad": { "desc": "Sterling (Washington DC)", "geo": [...], "relays": [ { "ipv4": "1.2.3.4", ... } ] } ... }
        public static List<GameServer> Parse(string json)
        {
            var r = new List<GameServer>();
            if (string.IsNullOrEmpty(json) || json.Length > 2 * 1024 * 1024) return r;
            // each region starts with "<code>": { ... "desc": "...", ... "relays": [ { "ipv4": "..." - fields in any order
            var starts = Regex.Matches(json, "\"([a-z0-9]{2,8})\"\\s*:\\s*\\{");
            for (int i = 0; i < starts.Count && r.Count < 60; i++)
            {
                int from = starts[i].Index, to = i + 1 < starts.Count ? starts[i + 1].Index : json.Length;
                string seg = json.Substring(from, Math.Min(to - from, 6000)), code = starts[i].Groups[1].Value.ToUpperInvariant();
                var d = Regex.Match(seg, "\"desc\"\\s*:\\s*\"([^\"]{1,60})\""); var ip4 = Regex.Match(seg, "\"ipv4\"\\s*:\\s*\"([0-9.]{7,15})\"");
                IPAddress ip; if (!d.Success || !ip4.Success || !IPAddress.TryParse(ip4.Groups[1].Value, out ip) || ip.AddressFamily != AddressFamily.InterNetwork) continue;
                string desc = d.Groups[1].Value; int p = desc.IndexOf('('); string where = p > 0 ? desc.Substring(0, p).Trim() : desc;
                if (!r.Any(x => x.Name == code)) r.Add(new GameServer { Name = code, Where = where, Host = ip.ToString(), Port = -1, Kind = "valve" });
            }
            return r;
        }
        public static string Refresh(int appId)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=" + appId.ToString(CultureInfo.InvariantCulture));
                req.Timeout = 10000; req.ReadWriteTimeout = 10000; req.AllowAutoRedirect = false; req.UserAgent = "AdeeboMeter/" + Program.Version;
                string body;
                using (var resp = (HttpWebResponse)req.GetResponse()) using (var s = resp.GetResponseStream()) using (var rd = new StreamReader(s, Encoding.UTF8))
                {
                    var buf = new char[2 * 1024 * 1024]; int n = 0, got; while (n < buf.Length && (got = rd.Read(buf, n, buf.Length - n)) > 0) n += got;
                    body = new string(buf, 0, n);
                }
                var list = Parse(body);
                if (list.Count == 0) return "Valve's list came back without any servers - kept the old one.";
                File.WriteAllText(FilePath, body); Games.Reload();
                return "Got " + list.Count + " Valve regions.";
            }
            catch (Exception e) { if (!(e is WebException || e is IOException || e is UnauthorizedAccessException)) throw; return "Couldn't reach Valve: " + e.Message; }
        }
    }

    // ================================================================== measuring: one sample = one probe
    sealed class GameStat
    {
        public readonly string GameId; public readonly GameServer S;
        readonly List<double> ms = new List<double>(); readonly List<DateTime> at = new List<DateTime>();
        public string Used = "";   // "ICMP" or "TCP 443" - how it actually answered
        readonly object k = new object();
        public GameStat(string game, GameServer s) { GameId = game; S = s; }
        public void Add(double v) { lock (k) { ms.Add(v); at.Add(DateTime.Now); if (ms.Count > 900) { ms.RemoveAt(0); at.RemoveAt(0); } } }
        public double[] Recent(int n) { lock (k) return ms.Skip(Math.Max(0, ms.Count - n)).ToArray(); }
        public DateTime LastAt { get { lock (k) return at.Count > 0 ? at[at.Count - 1] : DateTime.MinValue; } }
        public static double Avg(double[] v) { var ok = v.Where(x => !double.IsNaN(x)).ToArray(); return ok.Length == 0 ? double.NaN : ok.Average(); }
        public static double Pct(double[] v, double p) { var ok = v.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray(); return ok.Length == 0 ? double.NaN : ok[Math.Min(ok.Length - 1, (int)Math.Ceiling(p * ok.Length) - 1 < 0 ? 0 : (int)Math.Ceiling(p * ok.Length) - 1)]; }
        public static double Jitter(double[] v)
        {
            var ok = v.Where(x => !double.IsNaN(x)).ToArray(); if (ok.Length < 2) return double.NaN;
            double s = 0; for (int i = 1; i < ok.Length; i++) s += Math.Abs(ok[i] - ok[i - 1]); return s / (ok.Length - 1);
        }
        public static double Loss(double[] v) { return v.Length == 0 ? double.NaN : 100.0 * v.Count(double.IsNaN) / v.Length; }
        // Competitive: steady and low; Casual: playable; Laggy: not
        public static string Grade(double avg, double jit, double loss)
        {
            if (double.IsNaN(avg)) return "No reply";
            if (avg <= 60 && (double.IsNaN(jit) || jit <= 5) && loss < 0.5) return "Competitive";
            if (avg <= 100 && (double.IsNaN(jit) || jit <= 15) && loss < 2) return "Casual";
            return "Laggy";
        }
    }
    sealed class Spike { public DateTime T; public string Game, Server, Blame, Busy = "", Hop = ""; public double Ms, Normal; }   // Busy: what this PC was doing; Hop: where the path jumped (match path recording)

    sealed class GamePing
    {
        public static readonly GamePing Me = new GamePing();
        readonly Dictionary<string, GameStat> stats = new Dictionary<string, GameStat>();
        public readonly List<Spike> Spikes = new List<Spike>();
        readonly object k = new object();
        public volatile string Playing;            // id of the monitored game running now (null = none)
        public DateTime PlayingSince;
        public volatile GameSession Session, LastSession;   // the open session (or null) and the one before
        public void StartSession(Game g, bool manual)
        {
            var s = Session; if (s != null && s.Open) { s.End = DateTime.Now; LastSession = s; }
            var b = Best(g) ?? (g.Servers.Count > 0 ? g.Servers[0] : null);
            var ns = new GameSession { GameId = g.Id, GameName = g.Name, Server = b != null ? b.Name : "-", Start = DateTime.Now, Manual = manual };
            Session = ns;
            // match path recording: the routers on the way, found once (a short trace), then timed every 5 s while the session is open
            if (b != null && Cfg.GameFast) System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    IPAddress ip; if (!IPAddress.TryParse(b.Host, out ip)) ip = Dns.GetHostAddresses(b.Host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (ip == null) return;
                    var hops = PathTools.Trace(ip, 16, () => !ns.Open || stop).Where(h => h.Ip != "").Take(12).Select(h => h.N + "|" + h.Ip).ToList();
                    ns.SetHops(hops);
                }
                catch (Exception e) { if (!(e is SocketException || e is ArgumentException || e is PingException || e is InvalidOperationException)) throw; }
            });
        }
        public void EndSession() { var s = Session; if (s != null && s.Open) { s.End = DateTime.Now; LastSession = s; } Session = null; }
        Thread th; volatile bool stop; Monitor mon;
        public const int MaxPerSecond = 10;
        public static int MaxServers { get { return Cfg.Preset == "gamer" ? 16 : 8; } }   // the Gamer preset watches twice as many

        public void Start(Monitor m) { if (th != null || Policy.NoGamer) return; mon = m; stop = false; th = new Thread(Loop) { IsBackground = true, Name = "GamePing", Priority = ThreadPriority.BelowNormal }; th.Start(); }
        public void Stop() { stop = true; var t = th; th = null; if (t != null) t.Join(3000); }
        public GameStat Stat(string game, GameServer s)
        {
            lock (k)
            {
                GameStat st; string key = s.Key(game) + "|" + s.Host + "|" + s.Port;
                if (!stats.TryGetValue(key, out st)) { st = new GameStat(game, s); stats[key] = st; }
                return st;
            }
        }
        public List<GameStat> StatsFor(string game) { lock (k) return stats.Values.Where(x => x.GameId == game).ToList(); }

        // which servers to probe now, and how often (seconds): the game being played - all its servers (up to 8), every second;
        // otherwise each monitored game's best region, up to Cfg.GameBgServers, every Cfg.GameBgSec
        public List<Tuple<string, GameServer, int>> Plan(List<Game> games, HashSet<string> monitored, string playing)
        {
            var r = new List<Tuple<string, GameServer, int>>();
            var p = playing == null ? null : games.FirstOrDefault(g => g.Id == playing);
            if (p != null && Cfg.GameFast && monitored.Contains(p.Id)) foreach (var s in p.Servers.Take(MaxServers)) r.Add(Tuple.Create(p.Id, s, 1));
            if (Cfg.GameBgSec > 0)
                foreach (var g in games.Where(g => monitored.Contains(g.Id) && !g.Soon && g.Servers.Count > 0 && (p == null || g.Id != p.Id || !Cfg.GameFast)))
                {
                    if (r.Count(x => x.Item3 > 1) >= Cfg.GameBgServers || r.Count >= MaxServers) break;   /* background: the best regions, at most GameBgServers */
                    r.Add(Tuple.Create(g.Id, Best(g) ?? g.Servers[0], Cfg.GameBgSec));
                }
            r = r.Take(MaxServers).ToList();
            // a slow survey of every other region (once every 5 minutes each, 30 on Penny) so "best region" compares like with like
            if (Cfg.GameBgSec > 0)
                foreach (var g in games.Where(g => monitored.Contains(g.Id) && !g.Soon))
                    foreach (var s in g.Servers.Take(12)) if (!r.Any(x => x.Item1 == g.Id && x.Item2.Name == s.Name)) r.Add(Tuple.Create(g.Id, s, SurveySec));
            return r;
        }
        public static int SurveySec { get { return Cfg.Preset == "penny" ? 1800 : 300; } }   // Penny surveys each region every 30 min
        // the best region: lowest 7-day average with little loss, from history; before there's history, the lowest recent average
        public GameServer Best(Game g)
        {
            var week = GameHistory.Week(g.Id);
            GameServer best = null; double score = double.MaxValue;
            foreach (var s in g.Servers)
            {
                double[] w; double sc;
                if (week.TryGetValue(s.Name, out w) && !double.IsNaN(w[0])) sc = w[0] + 20 * w[1] + w[2];   // avg + 20 x loss% + jitter
                else { var st = Stat(g.Id, s); var v = st.Recent(60); double a = GameStat.Avg(v); if (double.IsNaN(a)) continue; sc = a + 20 * GameStat.Loss(v) + 1000; }   // recent-only ranks below a known week
                if (sc < score) { score = sc; best = s; }
            }
            return best;
        }

        void Loop()
        {
            var due = new Dictionary<string, DateTime>(); bool idle = false; DateTime lastHops = DateTime.MinValue, lastRun = DateTime.MinValue, lastMinute = Clock.Mono;   // intervals on the clock that never jumps
            while (!stop)
            {
                try
                {
                    Thread.Sleep(idle ? 5000 : 1000);
                    var mon2 = Games.Monitored();
                    idle = mon2.Count == 0;
                    if (idle) { Playing = null; continue; }   // nothing clicked yet: nothing is sent, and the loop wakes only every 5 s
                    var games = Games.All;
                    if ((Clock.Mono - lastRun).TotalSeconds >= 5)
                    {
                        lastRun = Clock.Mono;
                        var g = GameFinder.Running(games, mon2); string id = g == null ? null : g.Id;
                        if (id != Playing)
                        {
                            Playing = id; PlayingSince = DateTime.Now;
                            var cur = Session;
                            if (g != null && Cfg.GameAuto && (cur == null || !cur.Manual)) StartSession(g, false);
                            else if (g == null && cur != null && !cur.Manual) EndSession();
                        }
                    }
                    var plan = Plan(games, mon2, Cfg.GameAuto ? Playing : null);
                    var now = Clock.Mono; var go = new List<Tuple<string, GameServer, int>>();
                    foreach (var p in plan)
                    {
                        string key = p.Item1 + "|" + p.Item2.Name; DateTime d;
                        if (!due.TryGetValue(key, out d) || d <= now) { go.Add(p); due[key] = now.AddSeconds(p.Item3); }
                        if (go.Count >= MaxPerSecond) break;
                    }
                    if (go.Count > 0) Task.WaitAll(go.Select(p => Task.Factory.StartNew(() => ProbeOne(p.Item1, p.Item2))).ToArray(), 2500);
                    Evidence(games);
                    var ses = Session; if (ses != null && ses.Open && ses.HopCount > 0 && (Clock.Mono - lastHops).TotalSeconds >= 5) { lastHops = Clock.Mono; System.Threading.Tasks.Task.Factory.StartNew(() => ses.TimeHops()); }
                    if ((Clock.Mono - lastMinute).TotalSeconds >= 60) { lastMinute = Clock.Mono; GameHistory.Minute(this, plan); }
                }
                catch (Exception e) { Program.Log("gameping: " + e.Message); }
            }
        }
        // one evidence row a second for the open session
        void Evidence(List<Game> games)
        {
            var ss = Session; if (ss == null || !ss.Open || mon == null) return;
            var g = games.FirstOrDefault(z => z.Id == ss.GameId); var srv = g == null ? null : g.Servers.FirstOrDefault(z => z.Name == ss.Server);
            double gm = double.NaN;
            if (srv != null) { var st = Stat(g.Id, srv); if ((DateTime.Now - st.LastAt).TotalSeconds < 3) { var v = st.Recent(1); if (v.Length > 0) gm = v[0]; } }
            var snap = mon.Latest;
            ss.Add(new GameSession.Sec { T = DateTime.Now, Game = gm, Router = mon.GwPing.Last, Net = mon.NetPing.Last, Signal = mon.WifiSignal, Down = snap.Down, Up = snap.Up, Cpu = snap.Cpu });
        }
        void ProbeOne(string game, GameServer s)
        {
            var st = Stat(game, s);
            string used; double v = Probe(s, out used); if (used != null) st.Used = used;
            var prev = st.Recent(60); double med = GameStat.Pct(prev, 0.5);
            st.Add(v);
            // a spike: double the usual and 30 ms more - who to blame is read from the home and internet checks at the same moment
            if (!double.IsNaN(v) && !double.IsNaN(med) && prev.Length >= 20 && v > med * 2 && v > med + 30)
            {
                double gw = mon != null ? mon.GwPing.Last : double.NaN, net = mon != null ? mon.NetPing.Last : double.NaN;
                int sig = mon != null ? mon.WifiSignal : -1;
                string blame = Blame(gw, net, sig, mon != null ? mon.GwPing.Avg(20) : double.NaN);
                string busy = "", hop = "";
                if (mon != null)
                {
                    var sn = mon.Latest; busy = "this PC: " + (double.IsNaN(sn.Down) ? "-" : sn.Down.ToString("0.#", CultureInfo.InvariantCulture)) + " Mbps down, " + (double.IsNaN(sn.Up) ? "-" : sn.Up.ToString("0.#", CultureInfo.InvariantCulture)) + " up";
                    try
                    {
                        var g0 = Games.All.FirstOrDefault(z => z.Id == game); var exes = g0 != null ? g0.Exes : Array.Empty<string>();
                        var top = mon.Connections(false).Where(c => !string.IsNullOrEmpty(c.Proc) && !exes.Any(x => c.Proc.StartsWith(x, StringComparison.OrdinalIgnoreCase))).GroupBy(c => c.Proc).OrderByDescending(gr => gr.Count()).Take(3).Select(gr => gr.Key + " (" + gr.Count() + ")").ToList();
                        if (top.Count > 0) busy += " · busiest apps by connections: " + string.Join(", ", top);
                    }
                    catch (Exception e) { if (!(e is InvalidOperationException || e is System.ComponentModel.Win32Exception)) throw; }
                }
                var ses = Session; if (ses != null && ses.Open && ses.GameId == game) hop = ses.HopJump();
                lock (Spikes) { Spikes.Insert(0, new Spike { T = DateTime.Now, Game = game, Server = s.Name, Ms = v, Normal = med, Blame = blame, Busy = busy, Hop = hop }); if (Spikes.Count > 50) Spikes.RemoveAt(Spikes.Count - 1); }
            }
        }
        public static string Blame(double gwNow, double netNow, int sig, double gwUsual)
        {
            if (!double.IsNaN(gwNow) && gwNow > Math.Max(20, (double.IsNaN(gwUsual) ? 5 : gwUsual) * 3)) return sig >= 0 && sig < 60 ? "your Wi-Fi (weak signal)" : sig >= 0 ? "your Wi-Fi" : "your home network";
            if (double.IsNaN(gwNow)) return "your home network (the router didn't answer)";
            if (!double.IsNaN(netNow) && netNow > 80) return "your provider";
            return "past your provider (game server side)";
        }
        // ICMP first (Auto / ICMP only), TCP connect timing when ping is ignored (Auto) or chosen
        public static double Probe(GameServer s, out string used)
        {
            used = null;
            IPAddress ip;
            try { ip = IPAddress.TryParse(s.Host, out ip) ? ip : Dns.GetHostAddresses(s.Host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? Dns.GetHostAddresses(s.Host).FirstOrDefault(); }
            catch (SocketException) { return double.NaN; } catch (ArgumentException) { return double.NaN; }
            if (ip == null) return double.NaN;
            if (s.Port <= 0)
            {
                try
                {
                    using (var p = new Ping())
                    {
                        var r = p.Send(ip, 1000, new byte[32]);
                        if (r.Status == IPStatus.Success) { used = "ICMP"; return r.RoundtripTime; }
                    }
                }
                catch (PingException) { } catch (InvalidOperationException) { }
                if (s.Port < 0) { used = "ICMP"; return double.NaN; }
            }
            int port = s.Port > 0 ? s.Port : 443;
            used = "TCP " + port;
            return TcpMs(ip, port, 1500);
        }
        public static double TcpMs(IPAddress ip, int port, int timeoutMs)
        {
            using (var sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var ar = sock.BeginConnect(ip, port, null, null);
                    using (var wh = ar.AsyncWaitHandle)   // its handle closed every time - hours of TCP probes left one per probe to the finalizer
                    {
                        if (!wh.WaitOne(timeoutMs)) return double.NaN;
                        sock.EndConnect(ar);
                        return sw.Elapsed.TotalMilliseconds;
                    }
                }
                catch (SocketException) { return double.NaN; }
                catch (ObjectDisposedException) { return double.NaN; }
            }
        }
    }

    // ================================================================== game history: one line a minute per measured server
    // history\games-yyyyMMdd.csv: time,game,server,avg,p95,loss%,jitter,samples,router ms. Kept Cfg.GameDays, only while history is on.
    static class GameHistory
    {
        static string File0(DateTime d) { return Path.Combine(History.Dir, "games-" + d.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv"); }
        static readonly object k = new object();
        static Dictionary<string, Dictionary<string, double[]>> weekCache = new Dictionary<string, Dictionary<string, double[]>>(); static DateTime weekAt;
        static string F(double v) { return double.IsNaN(v) ? "" : v.ToString("0.#", CultureInfo.InvariantCulture); }
        public static void Minute(GamePing gp, List<Tuple<string, GameServer, int>> plan)
        {
            if (!Cfg.History) return;
            var lines = new List<string>(); string t = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            double gw = double.NaN;
            foreach (var p in plan)
            {
                var st = gp.Stat(p.Item1, p.Item2); if ((DateTime.Now - st.LastAt).TotalSeconds > 70) continue;
                int n = p.Item3 <= 1 ? 60 : Math.Max(1, 60 / p.Item3);
                var v = st.Recent(n); if (v.Length == 0) continue;
                lines.Add(t + "," + p.Item1 + "," + p.Item2.Name.Replace(",", " ") + "," + F(GameStat.Avg(v)) + "," + F(GameStat.Pct(v, 0.95)) + "," + F(GameStat.Loss(v)) + "," + F(GameStat.Jitter(v)) + "," + v.Length + "," + F(gw));
            }
            if (lines.Count == 0) return;
            lock (k)
                try { Directory.CreateDirectory(History.Dir); File.AppendAllLines(File0(DateTime.Today), lines); Tidy(); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("game history: " + e.Message); }
        }
        static DateTime tidied;
        static void Tidy()
        {
            if ((DateTime.Now - tidied).TotalHours < 6) return; tidied = DateTime.Now;
            foreach (var f in Directory.GetFiles(History.Dir, "games-*.csv"))
            {
                DateTime d; if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f).Substring(6), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d < DateTime.Today.AddDays(-(Cfg.GameDays - 1)))
                    try { File.Delete(f); } catch (IOException) { }
            }
        }
        public sealed class Row { public DateTime T; public string Game, Server; public double Avg, P95, Loss, Jit; }
        public static List<Row> Read(DateTime day, string game)
        {
            var r = new List<Row>(); string f = File0(day);
            try
            {
                if (!File.Exists(f) || new FileInfo(f).Length > 8 * 1024 * 1024) return r;
                Func<string, double> P = s => { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
                foreach (var line in File.ReadLines(f).Take(60000))
                {
                    var c = line.Split(','); TimeSpan t; if (c.Length < 8 || c[1] != game || !TimeSpan.TryParse(c[0], CultureInfo.InvariantCulture, out t)) continue;
                    r.Add(new Row { T = day.Date + t, Game = c[1], Server = c[2], Avg = P(c[3]), P95 = P(c[4]), Loss = P(c[5]), Jit = P(c[6]) });
                }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            return r;
        }
        // per server over the last 7 days: { avg ms, loss %, jitter, evenings with trouble (of days with data), days with data }
        public static Dictionary<string, double[]> Week(string game)
        {
            lock (k)
            {
                Dictionary<string, double[]> w;
                if ((DateTime.UtcNow - weekAt).TotalMinutes < 10 && weekCache.TryGetValue(game, out w)) return w;
                if ((DateTime.UtcNow - weekAt).TotalMinutes >= 10) { weekCache.Clear(); weekAt = DateTime.UtcNow; }
                var rows = new List<Row>(); for (int i = 0; i < 7; i++) rows.AddRange(Read(DateTime.Today.AddDays(-i), game));
                w = new Dictionary<string, double[]>();
                foreach (var grp in rows.GroupBy(x => x.Server))
                {
                    var days = grp.GroupBy(x => x.T.Date).ToList();
                    int bad = days.Count(d => d.Where(x => x.T.Hour >= 18 && x.T.Hour <= 23).Any(x => x.Loss >= 1 || x.P95 > 2 * Math.Max(1, grp.Average(y => double.IsNaN(y.Avg) ? 0 : y.Avg))));
                    w[grp.Key] = new[] { GameStat.Avg(grp.Select(x => x.Avg).ToArray()), GameStat.Avg(grp.Select(x => x.Loss).ToArray()), GameStat.Avg(grp.Select(x => x.Jit).ToArray()), bad, days.Count };
                }
                weekCache[game] = w; return w;
            }
        }
    }
}

namespace AdeeboMeter
{
    // ================================================================== a gaming session: the evidence a player needs afterwards
    // Starts by itself when a game you added opens (or with Start session), ends when it closes. One row a second: the game's
    // ping, your router, the internet check, Wi-Fi signal, traffic and CPU - in memory only, kept until the next session starts.
    sealed class GameSession
    {
        public sealed class Sec { public DateTime T; public double Game, Router, Net, Down, Up, Cpu; public int Signal; }
        public string GameId, GameName, Server; public DateTime Start, End = DateTime.MaxValue; public bool Manual;
        public readonly List<Sec> Rows = new List<Sec>(); public readonly List<DateTime> Marks = new List<DateTime>();
        public bool Open { get { return End == DateTime.MaxValue; } }
        readonly object k = new object();
        public void Add(Sec s) { lock (k) { Rows.Add(s); if (Rows.Count > 4 * 3600) Rows.RemoveAt(0); } }
        public List<Sec> Around(DateTime t, int secs) { lock (k) return Rows.Where(r => Math.Abs((r.T - t).TotalSeconds) <= secs).ToList(); }
        public List<Sec> All() { lock (k) return Rows.ToList(); }
        public void Mark() { lock (k) if (Marks.Count < 50) Marks.Add(DateTime.Now); }
        // match path recording: each router on the way, timed every 5 s (one ping each) while the match runs
        List<string> hops = new List<string>(); readonly List<double[]> hopMs = new List<double[]>();
        public int HopCount { get { lock (k) return hops.Count; } }
        public void SetHops(List<string> h) { lock (k) hops = h; }
        volatile bool timing;
        public void TimeHops()
        {
            if (timing) return; timing = true;
            try
            {
                List<string> h; lock (k) h = hops.ToList(); var v = new double[h.Count];
                using (var p = new System.Net.NetworkInformation.Ping())
                    for (int i = 0; i < h.Count; i++)
                    {
                        IPAddress ip; v[i] = double.NaN; if (!IPAddress.TryParse(h[i].Split('|')[1], out ip)) continue;
                        try { var r = p.Send(ip, 800); if (r.Status == System.Net.NetworkInformation.IPStatus.Success) v[i] = r.RoundtripTime; } catch (System.Net.NetworkInformation.PingException) { }
                    }
                lock (k) { hopMs.Add(v); if (hopMs.Count > 4 * 720) hopMs.RemoveAt(0); }
            }
            finally { timing = false; }
        }
        // the first hop whose latest time is well above its own usual and stays up to the end: where the spike starts
        public string HopJump()
        {
            lock (k)
            {
                if (hopMs.Count < 6 || hops.Count == 0) return "";
                var last = hopMs[hopMs.Count - 1]; int n = Math.Min(hops.Count, last.Length);
                for (int i = 0; i < n; i++)
                {
                    int idx = i; var usual = hopMs.Take(hopMs.Count - 1).Select(x => idx < x.Length ? x[idx] : double.NaN).Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
                    if (usual.Count < 4 || double.IsNaN(last[i])) continue;
                    double med = usual[usual.Count / 2];
                    if (last[i] > med * 2 && last[i] > med + 25 && Enumerable.Range(i, n - i).All(j => double.IsNaN(last[j]) || last[j] > med + 15))
                        return "hop " + hops[i].Split('|')[0] + " (" + hops[i].Split('|')[1] + "): " + Math.Round(med) + " -> " + Math.Round(last[i]) + " ms";
                }
                return "";
            }
        }

        // the short report a player pastes into a chat or a ticket; internal addresses, hardware addresses and the Wi-Fi name removed
        public string Report(List<Spike> spikes, string ssid)
        {
            var rows = All(); var c = CultureInfo.CurrentCulture; var sb = new StringBuilder();
            var gv = rows.Select(r => r.Game).ToArray();
            DateTime end = Open ? DateTime.Now : End;
            sb.AppendLine("AdeeboMeter match report - " + GameName + " (" + Server + ")");
            sb.AppendLine(Start.ToString("ddd d MMM yyyy HH:mm", c) + " - " + end.ToString("HH:mm", c) + "  (" + Math.Round((end - Start).TotalMinutes) + " min, " + rows.Count + " samples, one a second)");
            sb.AppendLine("Game ping: avg " + Fx(GameStat.Avg(gv)) + " ms, 95% " + Fx(GameStat.Pct(gv, 0.95)) + " ms, jitter " + Fx(GameStat.Jitter(gv)) + " ms, loss " + Fx(GameStat.Loss(gv)) + "%");
            sb.AppendLine("Router: avg " + Fx(GameStat.Avg(rows.Select(r => r.Router).ToArray())) + " ms · Internet check: avg " + Fx(GameStat.Avg(rows.Select(r => r.Net).ToArray())) + " ms");
            var mine = spikes.Where(z => z.Game == GameId && z.T >= Start && z.T <= end).ToList();
            sb.AppendLine("Spikes: " + mine.Count + (mine.Count > 0 ? " - " + string.Join(", ", mine.GroupBy(z => z.Blame).Select(gr => gr.Count() + " " + gr.Key)) : ""));
            foreach (var z in mine.OrderByDescending(z => z.Ms).Take(5))
            {
                var at = Around(z.T, 0).FirstOrDefault();
                sb.AppendLine("  " + z.T.ToString("HH:mm:ss", c) + "  " + Fx(z.Normal) + " -> " + Fx(z.Ms) + " ms  · " + z.Blame + (at != null ? "  (router " + Fx(at.Router) + " ms, Wi-Fi " + (at.Signal >= 0 ? at.Signal + "%" : "-") + ", traffic down " + Fx(at.Down) + " / up " + Fx(at.Up) + " Mb/s, CPU " + Fx(at.Cpu) + "%)" : ""));
                if (z.Hop != "") sb.AppendLine("      the path jumped at " + z.Hop);
                if (z.Busy != "") sb.AppendLine("      " + z.Busy + " (connections, not bytes per app)");
            }
            foreach (var m in Marks) sb.AppendLine("  Marked as laggy by the player at " + m.ToString("HH:mm:ss", c));
            sb.AppendLine("Measured with AdeeboMeter (pings / TCP connects to the region - not the game's own in-game ping).");
            return Redact(sb.ToString(), ssid);
        }
        static string Fx(double v) { return double.IsNaN(v) ? "-" : v.ToString(v < 10 ? "0.0" : "0", CultureInfo.InvariantCulture); }
        // home and provider-inside addresses (CGNAT 100.64/10 too), link-local with its zone (fe80::1%12), and IPv6 addresses
        // built from the network card's MAC (EUI-64: ff:fe in the middle) - none of them belong in a report you send
        static readonly Regex PrivIp = new Regex(@"\b(10\.\d{1,3}\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3}|172\.(1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}|169\.254\.\d{1,3}\.\d{1,3}|100\.(6[4-9]|[7-9]\d|1[01]\d|12[0-7])\.\d{1,3}\.\d{1,3}|f[cd][0-9a-f]{2}:[0-9a-f:]+|fe80:[0-9a-f:]+(%\w+)?|[0-9a-f:]*:[0-9a-f]{1,2}ff:fe[0-9a-f]{2}:[0-9a-f]{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Mac = new Regex(@"\b([0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        public static string Redact(string text, string ssid)
        {
            string t = PrivIp.Replace(text, "[internal address]"); t = Mac.Replace(t, "[hardware address]");
            // the Wi-Fi name where the report shows it (quoted, after "Wi-Fi", "SSID", "network", or "On <name> (channel")
            // - any length, so "ATT" or "Bob" is hidden too. Everywhere else only when the name can't be an ordinary word:
            // a name like "Router", "Home" or "loss" must not erase those words all over the report (that would give it away).
            if (!string.IsNullOrEmpty(ssid) && ssid.Trim() != "")
            {
                string e = Regex.Escape(ssid);
                t = Regex.Replace(t, "\"" + e + "\"|(?<=\\b(?:Wi-Fi|WiFi|SSID|network|Network)[:]? )" + e + "(?![\\w-])|(?<=\\bOn )" + e + "(?= \\()", "[Wi-Fi name]");
                bool plainWord = Regex.IsMatch(ssid, "^[A-Za-z]+$");
                if (!plainWord && ssid.Length >= 3) t = Regex.Replace(t, @"(?<![\w-])" + e + @"(?![\w-])", "[Wi-Fi name]");
            }
            return t;
        }
    }
}

namespace AdeeboMeter
{
    // ================================================================== Find my match server
    // While the game is running: its UDP ports come from Windows' own table (no admin), then tshark listens for 8 seconds to just
    // those ports and counts packets per remote address. The busiest public address is the match server. Needs Wireshark + Npcap.
    static class MatchFinder
    {
        public static List<int> GamePorts(Game g)
        {
            var pids = new HashSet<int>();
            try
            {
                foreach (var exe in g.Exes)
                    foreach (var p in Process.GetProcessesByName(exe)) using (p) pids.Add(p.Id);
            }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.ComponentModel.Win32Exception)) throw; }
            return Tables.Udp().Where(kv => pids.Contains(kv.Value) && kv.Key > 0).Select(kv => kv.Key).Distinct().Take(20).ToList();
        }
        public static string Find(Game g, string ifaceGuid, out string ip)
        {
            ip = null; Guid gid;
            if (Policy.NoCapture) return "Capturing packets is turned off by your IT team (AdeeboMeter.policy).";
            if (!LiveCapture.Available) return "Needs Wireshark (with Npcap) installed - it's what lets AdeeboMeter see the game's packets.";
            if (!Guid.TryParse((ifaceGuid ?? "").Trim('{', '}'), out gid)) return "No active network adapter.";
            var ports = GamePorts(g);
            if (ports.Count == 0) return g.Name + " isn't running, or hasn't opened its game connection yet - start a match first.";
            string ifc = "\\Device\\NPF_{" + gid.ToString().ToUpperInvariant() + "}";
            string filter = "udp and (" + string.Join(" or ", ports.Select(p => "port " + p.ToString(CultureInfo.InvariantCulture))) + ")";
            string args = "-i \"" + ifc + "\" -n -a duration:8 -c 4000 -f \"" + filter + "\" -T fields -e ip.src -e ip.dst";
            string text;
            try { text = Job.Run(LiveCapture.Tshark, args, 20000) ?? ""; }
            catch (Exception e) { return "Capture failed: " + Draw.Trunc(e.Message, 80); }
            ip = Busiest(text);
            return ip == null ? "No game packets in 8 seconds - are you in a match (not the menu)?" : null;
        }
        // the public address that appears most often (either direction); private, multicast and broadcast addresses don't count
        public static string Busiest(string tsharkFields) { return Busiest(tsharkFields, MyAddresses()); }
        // this PC's own addresses: with a public IPv4 on the PC (bridge-mode modem, a university network) it would be in
        // every packet and "win"
        static HashSet<string> MyAddresses()
        {
            var me = new HashSet<string>();
            try { foreach (var n in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) foreach (var u in n.GetIPProperties().UnicastAddresses) me.Add(u.Address.ToString()); }
            catch (System.Net.NetworkInformation.NetworkInformationException) { }
            return me;
        }
        public static string Busiest(string tsharkFields, HashSet<string> mine)
        {
            var count = new Dictionary<string, int>();
            foreach (var line in (tsharkFields ?? "").Split('\n'))
                foreach (var f in line.Trim().Split('\t'))
                {
                    IPAddress a; if (!IPAddress.TryParse(f.Trim(), out a) || a.AddressFamily != AddressFamily.InterNetwork) continue;
                    var b = a.GetAddressBytes();
                    bool priv = b[0] == 10 || b[0] == 127 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || b[0] >= 224 || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
                    if (priv || (mine != null && mine.Contains(a.ToString()))) continue;
                    int c; count.TryGetValue(a.ToString(), out c); count[a.ToString()] = c + 1;
                }
            return count.Count == 0 ? null : count.OrderByDescending(kv => kv.Value).First().Key;
        }
    }
}
