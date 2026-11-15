// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdeeboMeter
{
    // Learn › Games, the round games (1.1.5): six made-up situations, four answers each, the why after every pick.
    // Streaming: how video reaches you and whose fault the buffering is. Investing basics: learning, never advice -
    // no game says what to buy or sell. The first win (5 of 6) earns game.rounds.<id>.
    static class LearnRounds
    {
        public const int Win = 5;
        // id, name, one line, the Learn group it belongs to
        public static readonly string[][] Games = {
            new[] { "buffer", "Buffer race", "Six streams, six speeds: keep the picture moving.", "streaming" },
            new[] { "fault", "Whose fault?", "Read the evidence for each dip and blame the right hop.", "streaming" },
            new[] { "k4", "4K or not?", "Six homes, six lines: what quality will really play?", "streaming" },
            new[] { "scam", "Spot the scam", "Six messages about money. Which ones are traps?", "investing" },
            new[] { "penny", "The penny-stock trap", "Why +300% lists are full of tiny, risky companies.", "investing" },
            new[] { "delay", "Delayed or live?", "Which clock is each price on?", "investing" },
            new[] { "fxtrip", "Currency trip", "A US stock bought in Canadian dollars - follow the money.", "investing" },
            new[] { "lag", "Lag lab", "Your connection, your broker and what a slow moment really changes.", "investing" },
            new[] { "bullbear", "Bull or bear?", "Six made-up headlines and charts: bull, bear, dip or recession?", "investing" } };
        // per game: situation, the right answer, three wrong ones, why
        public static readonly Dictionary<string, string[][]> Rounds = new Dictionary<string, string[][]>
        {
            { "buffer", new[] {
                new[] { "Your line gives a steady 6 Mb/s. Which quality plays without stopping?", "HD (about 5 Mb/s)", "4K (about 25 Mb/s)", "Full HD (about 8 Mb/s)", "Any of them - the buffer covers it", "A stream needs a little more than its own rate, all the time. The buffer hides short dips, not a line that's always too slow." },
                new[] { "30 seconds of video are buffered. Your Wi-Fi drops out for 10 seconds. What do you see?", "Nothing - it keeps playing from the buffer", "It stops at once", "It jumps to 4K", "The app restarts", "The buffer is video already downloaded. A 10-second gap uses 10 seconds of it, and the player refills it afterwards." },
                new[] { "Your speed falls from 20 to 4 Mb/s for two minutes. What does a modern player usually do?", "Picks a lower quality so it keeps playing", "Stops and waits", "Keeps 4K and spins", "Switches to sound only for good", "Adaptive streaming picks each chunk's quality from the speed it just saw - blurrier beats spinning." },
                new[] { "Why does live sport stall more easily than a film?", "It keeps only a few seconds ahead, so short dips show", "Sport is a bigger file", "Live video can't use Wi-Fi", "Films are stored in your router", "The next minute of a live match hasn't happened yet, so there's little buffer to hide a dip." },
                new[] { "Which helps a buffering stream most at home?", "A cable, or sitting closer to the router", "Opening more browser tabs", "Adding another Wi-Fi extender far away", "Restarting the app every minute", "Most home buffering is weak or crowded Wi-Fi; a cable or a shorter hop fixes the first link in the chain." },
                new[] { "Your stream needs 25 Mb/s on a 50 Mb/s plan. Someone starts uploading a big video. What can happen?", "The upload fills the queue and your stream stutters", "Nothing - uploads never affect downloads", "Your stream gets faster", "The plan doubles for a while", "A full upload makes the router's queue long, delaying the small replies a download needs - often called bufferbloat." } } },
            { "fault", new[] {
                new[] { "Router 180 ms, Wi-Fi signal 25%, internet 210 ms, stream server 230 ms.", "Your Wi-Fi", "Your provider", "The stream's server", "This PC", "Trouble starts at the very first hop, with a weak signal. Everything after it is slow because of it." },
                new[] { "Router 2 ms. The internet check loses 4 of 10 replies. The stream server doesn't answer.", "Your provider", "Your Wi-Fi", "The stream's server", "This PC", "Your home answers instantly; the trouble begins past your router." },
                new[] { "Router 2 ms, internet 18 ms, stream server jumps from 40 to 260 ms.", "The stream's server", "Your provider", "Your Wi-Fi", "This PC", "Everything up to the internet is normal; only the service's own server slowed down." },
                new[] { "Processor at 99% (a game is updating). Router 3 ms, internet 20 ms.", "This PC", "Your provider", "The stream's server", "Your Wi-Fi", "The network is fine end to end - the PC is too busy to keep up with the video." },
                new[] { "On a cable, the router stops answering for 20 seconds, then everything comes back.", "Your home network (the router)", "Your provider", "The stream's server", "Your Wi-Fi", "No Wi-Fi involved and the very first box went quiet - look at the router (or its power, or its cable)." },
                new[] { "Every evening 20:00-22:00: router 2 ms all the time, internet 120 ms with some loss.", "Your provider's busy hours", "Your Wi-Fi", "This PC", "The stream's server", "A healthy router and an evening-only slowdown beyond it is the classic busy-hours pattern - the monthly report is your evidence." } } },
            { "k4", new[] {
                new[] { "A steady 40 Mb/s on a cable.", "4K is safe", "Only SD will play", "HD at most", "4K needs a fibre plan", "4K wants about 25 Mb/s; 40 steady leaves room to spare." },
                new[] { "18 Mb/s on Wi-Fi in the bedroom.", "HD is safe, 4K is risky", "4K is safe", "Nothing will play", "Only sound will play", "18 carries HD and Full HD easily but sits below the ~25 that 4K needs." },
                new[] { "A steady 4 Mb/s.", "SD is safe, HD will struggle", "4K is safe", "Full HD is safe", "Nothing will play", "SD needs about 3 Mb/s and HD about 5 - 4 is in between." },
                new[] { "30 Mb/s most of the time, dropping to 8 every few minutes.", "4K will step down to lower quality now and then", "4K is perfect", "Nothing will play", "It will be SD all the time", "Adaptive streaming follows the dips: sharp most of the time, blurry for a while when it drops." },
                new[] { "A 100 Mb/s plan, and four people stream 4K at the same time.", "Tight - four 4K streams use about all of it", "Plenty of room", "Only one can watch", "4K doesn't share a line", "Four times ~25 is ~100: no headroom left for anything else." },
                new[] { "A phone hotspot giving 12 Mb/s.", "Full HD is about the limit - and it eats mobile data", "4K is safe", "Nothing will play", "Only SD is possible", "12 covers Full HD (~8). A 2-hour Full HD film can use several gigabytes of your data plan." } } },
            { "scam", new[] {
                new[] { "A stranger on social media: \"Guaranteed 30% a month. Send crypto to my wallet to join.\"", "Scam", "A real opportunity", "Normal for crypto", "Fine if they show screenshots", "Nobody can guarantee returns. Guarantees plus \"send crypto\" is the classic pattern - screenshots are easy to fake." },
                new[] { "A chat group: \"Everyone buy XYZ at 3 pm, it will 10x!\" XYZ trades a few thousand shares a day.", "A pump-and-dump - the organisers sell into the rush", "Insider tip worth following", "A normal market event", "A government programme", "Tiny, thinly traded stocks are easy to push up for a few minutes; the people who planned it sell to everyone who followed." },
                new[] { "You open your broker's official app, which you installed from the store, and log in. It shows your balance.", "Probably fine - you went to them", "Scam", "Phishing", "Pump-and-dump", "You started the contact, through the real app. Trouble usually starts when someone contacts you first." },
                new[] { "An email: \"Your account is locked - log in here\" with a link to wealthsimple-secure-login.co.", "Phishing - the address isn't the real one", "A real security notice", "Probably fine", "A pump-and-dump", "Look at the address, not the logo. Go to your broker by typing their real address or opening their app yourself." },
                new[] { "A \"recovery service\" offers to get back money you lost to a scam - for an upfront fee.", "Another scam aimed at past victims", "A real lawyer service", "A government refund", "Probably fine", "Recovery scams target people who were already scammed. Report losses to the police and your bank instead." },
                new[] { "A video of a famous person \"recommends\" a trading app you've never heard of.", "Likely a fake ad - check the regulator's warning list", "A trustworthy endorsement", "A normal sponsorship", "Probably fine", "Celebrity videos are easy to fake. Check the company with your securities regulator before sending anything." } } },
            { "penny", new[] {
                new[] { "A $0.40 stock is up 300% today on very few trades. What usually explains it?", "A small, thinly traded company - a few orders move it a lot", "The company tripled its profit today", "It's sure to keep rising", "Big pension funds are buying", "When few shares trade, a handful of orders can move the price a long way - in both directions." },
                new[] { "Why can it be hard to sell a penny stock?", "Few buyers - the price you see may not be the price you get", "Brokers forbid selling them", "They can only be sold on Fridays", "They're always worth more tomorrow", "With little trading, there may be nobody to buy at the shown price; selling can push it down." },
                new[] { "Why are \"biggest gains\" lists full of tiny companies?", "Small prices move by big percentages easily", "Tiny companies are the best investments", "Big companies never move", "The lists are paid ads", "Going from $0.10 to $0.30 is +200%; a large company rarely moves more than a few percent in a day." },
                new[] { "A company does a 1-for-20 reverse split. Your 1,000 shares at $0.25 become...", "50 shares at about $5 - the same total value", "1,000 shares at $5", "20 shares at $0.25", "Worth 20 times more", "A split only cuts the same value into fewer, bigger pieces - $250 before, about $250 after." },
                new[] { "Which is a warning sign?", "Lots of hype posts and no real financial reports", "Quarterly results published on time", "A long history on a major exchange", "An index that includes it", "Real companies publish results. Hype without numbers is how pump-and-dumps begin." },
                new[] { "Before you buy any company's shares, a calm first step is...", "Read what the company actually does and earns", "Buy before the price runs away", "Follow the loudest chat group", "Put everything into one stock", "Learning what you'd own comes first. AdeeboMeter never says what to buy - that decision is yours." } } },
            { "delay", new[] {
                new[] { "Free websites often show stock prices...", "About 15 minutes late", "Live to the second", "From last year", "Only on weekends", "Exchanges charge for live prices, so many free sites show them delayed - check the time stamp." },
                new[] { "AdeeboMeter's Trading summary shows...", "End-of-day numbers, after the close", "Live prices", "Prices from next week", "Only crypto", "It fetches once after the market closes - a quick look, not a ticker." },
                new[] { "Before you trust a price on your broker's order screen, check...", "Its time stamp - and whether your account gets live or delayed quotes", "Nothing - an order screen is always live", "The colour of the number", "How many tabs are open", "Brokers can take orders while showing delayed quotes (live data is often a subscription). The quote's time and your data plan tell you how fresh it is." },
                new[] { "The Bank of Canada's daily exchange rates are...", "Indicative daily rates, not for trading", "The rate your bank must give you", "Live rates to the second", "Next week's forecast", "The Bank publishes them once a day as a reference; banks and apps set their own rates." },
                new[] { "At 18:00 on a weekday, a US stock shows \"last updated 16:00\". That price is...", "The closing price - the market closed at 16:00", "Broken", "Two hours delayed by mistake", "Tomorrow's opening price", "Regular trading ends at 16:00 Eastern; the last price of the day is the close." },
                new[] { "Crypto prices at 3 a.m. on a Sunday are...", "Still moving - crypto trades around the clock", "Frozen since Friday", "Not published at all", "Only for banks", "Crypto markets never close, which is why its summary is a snapshot at a time you pick." } } },
            { "fxtrip", new[] {
                new[] { "You buy a US stock for US$100 when 1 USD = 1.35 CAD. What did it cost in Canadian dollars?", "$135", "$100", "$74", "$1.35", "Multiply by the rate: 100 x 1.35 = 135 (before any fees)." },
                new[] { "The stock stays at US$100, but the US dollar falls to 1.30 CAD. Your holding in CAD is now...", "$130 - $5 lost on the currency alone", "$135 - nothing changed", "$140", "$100", "The stock didn't move, but each US dollar now buys fewer Canadian dollars." },
                new[] { "USD/CAD goes from 1.35 to 1.40. For a Canadian buying something priced in US dollars...", "It costs more in CAD", "It costs less in CAD", "Nothing changes", "It becomes free", "Each US dollar now takes 1.40 Canadian dollars instead of 1.35." },
                new[] { "Your bank converts at 1.38 while the day's rate is 1.35. The 0.03 difference is...", "The bank's conversion fee (the spread)", "A government tax", "A mistake in your favour", "Interest you earned", "Banks and apps earn on the gap between their rate and the market's - it's a fee, even when nothing says \"fee\"." },
                new[] { "Sending money from Canada to family abroad, what decides how much arrives?", "The exchange rate used and the transfer fees", "Only the amount you send", "The weather", "The receiver's phone model", "Two things matter: the rate the service gives you and what it charges on top. Compare both." },
                new[] { "A \"no fee\" conversion offers a rate much worse than the day's rate. It...", "Still costs you - the fee is hidden in the rate", "Is free", "Pays you extra", "Is illegal", "A worse rate is a fee by another name. Compare with the day's rate in Trading." } } },
            { "bullbear", new[] {
                new[] { "An index rose 25% from its low over 18 months, and people are cheerful about shares.", "A bull market", "A bear market", "A recession", "A correction", "Up 20% or more from a low, with confidence rising - the textbook bull market." },
                new[] { "An index fell 11% in three weeks, then went sideways. The economy is still growing.", "A correction (a dip)", "A bear market", "A recession", "A bull market", "A fall of 10-20% is a correction - common, and usually recovered within months." },
                new[] { "An index is 27% below its high after eight months of falls. The news is full of fear.", "A bear market", "A correction", "A bull market", "Normal daily noise", "20% or more below the last high is a bear market." },
                new[] { "The country's output shrank two quarters in a row; job losses are rising.", "A recession", "A bull market", "A correction", "Inflation", "Two quarters of shrinking GDP is the common rule of thumb for a recession." },
                new[] { "Groceries, rent and fuel all cost 7% more than a year ago.", "High inflation", "A recession", "A bear market", "A bull market", "Prices rising across the board is inflation - here well above the Bank of Canada's 2% target." },
                new[] { "Shares fell 2% today on one piece of news and recovered by the close.", "Normal daily noise", "A bear market", "A recession", "A crash", "A 2% day is ordinary. Labels like bear or correction are about falls lasting weeks or months." } } },
            { "lag", new[] {
                new[] { "Your delay to your broker is 30 ms. When you click Buy, the order reaches them in...", "A small fraction of a second - the network isn't the problem", "About a minute", "Tomorrow", "Never", "30 ms is three hundredths of a second. At that speed the network is not what you should worry about." },
                new[] { "The delay jumps to 300 ms for a few seconds at the open. For a casual trader that means...", "The order arrives a moment later; in a fast market the price can move a little", "The order is cancelled", "You pay double", "Nothing at all, ever", "A third of a second is noticeable in very fast markets, rarely for a casual order - but it's worth knowing when it happens." },
                new[] { "A reply is lost and the order screen just spins. The safest move is...", "Check your orders list before clicking again", "Click Buy five more times", "Restart the router immediately", "Close the app and forget it", "A spinning screen doesn't mean the order failed. A second click can mean a second order." },
                new[] { "AdeeboMeter shows your router fine, your provider fine, and your broker slow. Whose side is it?", "Past your provider - the far path or the broker's end, cause not proven", "Your Wi-Fi's", "Your PC's", "Your provider's", "When every hop up to your provider is normal, the slowness is further out: the route to the broker or the broker's own door. A probe can't tell which - or whether their servers are busy." },
                new[] { "What can AdeeboMeter's broker check NOT tell you?", "How fast the broker fills your order", "Your delay to the broker", "Whether your Wi-Fi drops", "Whether your provider loses packets", "It times the path to the broker's door. What happens inside the broker - matching, filling - it can't see." },
                new[] { "Your Wi-Fi signal is 20% while you trade. The best fix is...", "A cable, or sitting closer to the router", "A faster phone", "Trading only at night", "More browser tabs", "A weak first hop makes everything after it slow and unsteady; fix the first link." } } }
        };
        public static string Group(string id) { var g = Games.FirstOrDefault(z => z[0] == id); return g != null ? g[3] : ""; }
        public static bool Is(string id) { return Rounds.ContainsKey(id); }
    }
}
