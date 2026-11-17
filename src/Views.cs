// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Views (Adeeb, 14 Nov): one switch at the top changes the app
    // for what you're doing - which pages show, how hard it watches, what the corner widget shows, Details on or off. Five
    // ready-made (Everyday, Gamer, Streamer, Trader, Engineer) and any you make. Each view keeps your own changes: tweak the
    // Gamer view, switch to Trader and back, and it's as you left it. Learn is in every view. Replaces the Simple / Pro switch.
    static class Views
    {
        public static readonly string[][] BuiltIn = {
            new[] { "everyday", "Everyday", "Your PC and internet with the lightest checks. Speed + status in the corner." },
            new[] { "gamer", "Gamer", "The Gamer Zone and the games you add, watched. Their ping in the corner." },
            new[] { "streamer", "Streamer", "Streaming quality and whose fault a dip was. Speed + status in the corner." },
            new[] { "trader", "Trader", "Your line to your broker and its delay, on the Trading page. Speed + status in the corner." },
            new[] { "engineer", "Engineer", "Tools first: Details on, the PC-to-internet strip, the engineer's card in the corner." },
            new[] { "everything", "Everything", "Every page up front - gaming, streaming and trading too. The corner follows what you're doing." },
            new[] { "play-watch", "Gamer + Streamer", "Your games and your streams. The corner follows what you're doing." },
            new[] { "play-trade", "Gamer + Trader", "Your games and your broker - or learning to trade. The corner follows what you're doing." },
            new[] { "watch-trade", "Streamer + Trader", "Your streams and your broker. Speed + status in the corner." } };
        // what makes a view: mode / hidden pages / preset / widget / details / calls / the pace (router, internet, Wi-Fi,
        // apps, game ping, game servers) - one line, '/' between
        public sealed class State
        {
            public string Mode = "simple", Hidden = "", Preset = "balanced", Widget = "auto", Nav = ""; public bool Details, Call = true;   // Nav: the bar's order ("" = leave it)
            public int Gw = 2, Net = 15, Wifi = 30, Apps = 5, GameBg = 30, GameSrv = 3;
            public string Line() { return string.Join("/", Mode, Hidden.Replace(",", "+"), Preset, Widget, Details ? "1" : "0", Call ? "1" : "0", I(Gw), I(Net), I(Wifi), I(Apps), I(GameBg), I(GameSrv), Nav.Replace(",", "+")); }
            static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
            public static State Parse(string l)
            {
                var p = (l ?? "").Split('/'); if (p.Length < 12) return null;
                int[] n = new int[6]; for (int i = 0; i < 6; i++) if (!int.TryParse(p[6 + i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i]) || n[i] < 0 || n[i] > 3600) return null;
                return new State { Mode = p[0] == "pro" ? "pro" : "simple", Hidden = string.Join(",", p[1].Split('+').Where(z => Cfg.Hideable.Contains(z)).Distinct()), Preset = p[2] == "penny" || p[2] == "gamer" || p[2] == "custom" ? p[2] : "balanced", Widget = WidgetOk(p[3]), Details = p[4] == "1", Call = p[5] == "1", Gw = n[0], Net = n[1], Wifi = n[2], Apps = n[3], GameBg = n[4], GameSrv = Math.Max(1, Math.Min(5, n[5])), Nav = p.Length > 12 && p[12] != "" ? Cfg.NavClean(p[12].Replace("+", ",")) : "" };
            }
        }
        static string WidgetOk(string w) { return w == "everyday" || w == "gamer" || w == "calls" || w == "engineer" || w == "speed" || w == "speedstatus" || w == "speedapps" ? w : "auto"; }
        // a ready-made view as it starts (the person's changes are kept on top of it)
        public static State Default(string id)
        {
            State s; string hideAll = "stream,trade,gamer";
            switch (id)
            {
                case "everyday": s = new State { Hidden = hideAll, Widget = "speedstatus", Nav = "home,toolbox,time,learn" }; Pace(s, "penny"); break;
                case "gamer": s = new State { Hidden = "stream,trade", Widget = "gamer", Nav = "home,gamer,toolbox,time,learn" }; Pace(s, "gamer"); break;
                case "streamer": s = new State { Hidden = "trade,gamer", Widget = "speedstatus", Nav = "home,stream,toolbox,time,learn" }; Pace(s, "balanced"); break;
                case "trader": s = new State { Hidden = "stream,gamer", Widget = "speedstatus", Nav = "home,trade,toolbox,time,learn" }; Pace(s, "balanced"); break;
                case "engineer": s = new State { Mode = "pro", Hidden = "", Widget = "engineer", Details = true, Nav = "home,toolbox,dest,security,story,time,learn" }; Pace(s, "balanced"); break;
                case "everything": s = new State { Mode = "pro", Hidden = "", Widget = "auto", Nav = "home,gamer,stream,trade,toolbox,time,learn" }; Pace(s, "balanced"); break;
                case "play-watch": s = new State { Hidden = "trade", Widget = "auto", Nav = "home,gamer,stream,toolbox,time,learn" }; Pace(s, "gamer"); break;
                case "play-trade": s = new State { Hidden = "stream", Widget = "auto", Nav = "home,gamer,trade,toolbox,time,learn" }; Pace(s, "gamer"); break;
                case "watch-trade": s = new State { Hidden = "gamer", Widget = "speedstatus", Nav = "home,stream,trade,toolbox,time,learn" }; Pace(s, "balanced"); break;
                default: return null;
            }
            return s;
        }
        static void Pace(State s, string preset)
        {
            // the same numbers Cfg.ApplyPreset uses
            s.Preset = preset;
            if (preset == "penny") { s.Gw = 30; s.Net = 300; s.Wifi = 120; s.Apps = 30; s.Call = false; s.GameBg = 60; s.GameSrv = 3; }
            else if (preset == "gamer") { s.Gw = 2; s.Net = 15; s.Wifi = 10; s.Apps = 5; s.Call = true; s.GameBg = 10; s.GameSrv = 5; }
            else { s.Gw = 2; s.Net = 15; s.Wifi = 30; s.Apps = 5; s.Call = true; s.GameBg = 30; s.GameSrv = 3; }
        }
        public static State Current()
        {
            return new State { Nav = Cfg.Nav, Mode = Cfg.Mode == "pro" ? "pro" : "simple", Hidden = Cfg.HiddenSecs, Preset = Cfg.Preset, Widget = Cfg.WidgetStyle, Details = Cfg.Details, Call = Cfg.Call, Gw = Cfg.GwSec, Net = Cfg.NetSec, Wifi = Cfg.WifiSec, Apps = Cfg.AppsSec, GameBg = Cfg.GameBgSec, GameSrv = Cfg.GameBgServers };
        }
        public static void Put(State s)
        {
            if (s.Nav != "") Cfg.Nav = s.Nav;
            Cfg.Mode = s.Mode; Cfg.HiddenSecs = s.Hidden; Cfg.Preset = s.Preset; Cfg.WidgetStyle = s.Widget; Cfg.Details = s.Details; Cfg.Call = s.Call;
            Cfg.GwSec = s.Gw; Cfg.NetSec = s.Net; Cfg.WifiSec = s.Wifi; Cfg.AppsSec = s.Apps; Cfg.GameBgSec = s.GameBg; Cfg.GameBgServers = s.GameSrv;
        }

        // ---------- the views kept: "id~name~state" between '|' (ready-made ones only once changed; yours always)
        static readonly object k = new object();
        static readonly Dictionary<string, string[]> kept = new Dictionary<string, string[]>(StringComparer.Ordinal);   // id -> { name, state line }
        public static string Line()
        {
            lock (k) return string.Join("|", kept.Select(kv => kv.Key + "~" + kv.Value[0].Replace("~", "-").Replace("|", "-") + "~" + kv.Value[1]));
        }
        public static void Load(string line)
        {
            lock (k)
            {
                kept.Clear();
                foreach (var e in (line ?? "").Split('|'))
                {
                    var p = e.Split('~'); if (p.Length != 3 || !IdOk(p[0]) || State.Parse(p[2]) == null) continue;
                    if (kept.Count >= 20) break;
                    kept[p[0]] = new[] { Clean(p[1]), p[2] };
                }
            }
        }
        static bool IdOk(string id) { return id.Length > 0 && id.Length <= 24 && id.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'); }
        static string Clean(string n) { n = new string((n ?? "").Where(c => !char.IsControl(c) && c != '~' && c != '|' && c != '/').ToArray()).Trim(); return n.Length > 30 ? n.Substring(0, 30) : n; }
        public static bool IsBuiltIn(string id) { return BuiltIn.Any(b => b[0] == id); }
        public static string NameOf(string id)
        {
            var b = BuiltIn.FirstOrDefault(z => z[0] == id); if (b != null) return L.T(b[1]);
            lock (k) { string[] v; return kept.TryGetValue(id, out v) ? v[0] : id; }
        }
        // the views to offer: the ready-made five, then yours
        public static List<string[]> All()
        {
            var l = BuiltIn.Select(b => new[] { b[0], b[1], b[2] }).ToList();
            lock (k) foreach (var kv in kept.Where(z => !IsBuiltIn(z.Key))) l.Add(new[] { kv.Key, kv.Value[0], Summary(State.Parse(kv.Value[1])) });
            return l;
        }
        public static string Summary(State s)
        {
            if (s == null) return "";
            var shown = new List<string>(); if (!Has(s.Hidden, "gamer")) shown.Add("Gamer Zone"); if (!Has(s.Hidden, "stream")) shown.Add("Streaming"); if (!Has(s.Hidden, "trade")) shown.Add("Trading");
            return (shown.Count == 0 ? "PC and internet" : string.Join(", ", shown)) + " · " + (s.Preset == "custom" ? "your own pace" : s.Preset == "penny" ? "Penny" : s.Preset == "gamer" ? "Gamer pace" : "Balanced") + (s.Mode == "pro" ? " · every tool" : "");
        }
        static bool Has(string csv, string id) { return ("," + csv + ",").Contains("," + id + ","); }

        // remember the current settings under the view in use (called on every save, so a view keeps what you changed)
        public static void Remember()
        {
            string id = Cfg.View; if (id == "") return;
            lock (k) { string[] v; string name = kept.TryGetValue(id, out v) ? v[0] : NameOf(id); kept[id] = new[] { name, Current().Line() }; }
        }
        // switch: the current view keeps its state, the new one comes back as it was left (or as it starts)
        public static bool Switch(string id)
        {
            State s; lock (k) { string[] v; s = kept.TryGetValue(id, out v) ? State.Parse(v[1]) : null; }
            if (s == null) s = Default(id);
            if (s == null) return false;
            Remember();
            Cfg.View = id; Put(s); Policy.Apply();
            return true;
        }
        // "+ Make a view from what I have now"
        public static string Make(string name)
        {
            name = Clean(name); if (name == "") return null;
            string id; int i = 1; do { id = "my-" + i++; } while (IsBuiltIn(id) || Kept(id));
            lock (k) { if (kept.Count(z => !IsBuiltIn(z.Key)) >= 12) return null; kept[id] = new[] { name, Current().Line() }; }
            Cfg.View = id; return id;
        }
        // a ready-made view back as it starts (the one in use: its settings change now)
        public static void Reset(string id)
        {
            var d = Default(id); if (d == null) return;
            lock (k) kept.Remove(id);
            if (Cfg.View == id) { Put(d); Policy.Apply(); }
        }
        public static State KeptState(string id) { lock (k) { string[] v; return kept.TryGetValue(id, out v) ? State.Parse(v[1]) : null; } }
        static bool Kept(string id) { lock (k) return kept.ContainsKey(id); }
        public static void Delete(string id)
        {
            if (IsBuiltIn(id)) { lock (k) kept.Remove(id); return; }   // a ready-made view: back to how it starts
            lock (k) kept.Remove(id);
            if (Cfg.View == id) { Cfg.View = ""; Switch("everyday"); }   // "" first: the removed view mustn't be remembered again on the way out
        }
        // the first view: an upgrade keeps exactly what it had as "My setup"; a fresh install gets one from the first question
        public static void EnsureStarted()
        {
            if (Cfg.View != "") return;
            if (Cfg.Mode == "") Cfg.Mode = "simple";
            if (Cfg.Mode == "pro" && Cfg.HiddenSecs == "" && Cfg.Details) { Cfg.View = "engineer"; Remember(); return; }
            Make(L.T("My setup"));
        }
        // the view that fits the first question's answer
        public static string FromUses(ICollection<string> uses)
        {
            var main = uses.Where(u => u == "game" || u == "stream" || u == "trade" || u == "fix").ToList();
            if (uses.Count == 0) return "everything";   // R2-10: nothing picked is "Show me everything" - every page, not fewer
            if (main.Count == 0) return "everyday";
            if (main.Count == 1) return main[0] == "game" ? "gamer" : main[0] == "stream" ? "streamer" : main[0] == "trade" ? "trader" : "engineer";
            if (main.Count == 2 && !main.Contains("fix")) return main.Contains("game") && main.Contains("stream") ? "play-watch" : main.Contains("game") ? "play-trade" : "watch-trade";
            return "everything";   // three or more, or troubleshooting with a group: every page
        }
        // the corner's status words in a view made for it (the widget never changes by itself otherwise): the Streamer view
        // names what's playing and how it's going, the Trader view the broker line while the market is open
        public static string ChipLine()
        {
            if (Cfg.Corner != Moments.FollowView) return null;   // the quiet corners never change their words by themselves
            if (Cfg.View == "streamer" && Streams.Active)
            {
                Streams.Live l; lock (Streams.k) l = Streams.Now; if (l == null) return null;
                int n = l.S.Dips.Count; string what = l.S.What.Length > 18 ? l.S.What.Substring(0, 18) : l.S.What;
                return what + " · " + (n == 0 ? L.T("smooth") : n + (n == 1 ? L.T(" dip") : L.T(" dips")));
            }
            if (Cfg.View == "trader" && Trading.Watching)
            {
                WatchKit.Sample last; lock (Trading.k) last = Trading.Samples.LastOrDefault();
                if (last == null || double.IsNaN(last.Far)) return null;
                return L.T("Broker ") + last.Far.ToString("0", CultureInfo.CurrentCulture) + " ms";
            }
            return null;
        }
    }
}
