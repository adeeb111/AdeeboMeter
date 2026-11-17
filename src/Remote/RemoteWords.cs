// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Security.Cryptography;
using System.Text;

namespace AdeeboMeter
{
    // Matching words (Adeeb, 17 Nov - "how do I make sure people are not getting into other people's room"): before you
    // press Allow, the PC and the phone each show 4 words worked out from both one-time keys and the QR code's secret.
    // Same words on both = it's your phone, and nothing sits in the middle. A stranger with a leaked QR code gets words on
    // their own screen, never yours. The words are never sent - each side works them out. The list and the recipe are the
    // same in docs/app.html (WORDS) - change both together.
    static class RemoteWords
    {
        public static readonly string[] List = "acorn amber anchor apple arrow atlas autumn bacon badge bagel ballad bamboo banjo barn basil beach beacon bear beaver bell berry bison blade blanket bloom boat bonfire boot bottle breeze brick bridge brook brush bucket bugle button cabin cactus cake camel camera candle canoe canyon carpet carrot castle cedar cello chalk cherry chess cider circle clock cloud clover coast cobalt cocoa comet compass copper coral cotton cougar crane crayon cricket crown crystal cup daisy delta desert diamond dolphin donkey dragon drum eagle echo elbow ember engine falcon feather fern ferry fiddle field fig flag flame flute forest fossil fox galaxy garden garlic gecko ginger glacier globe goat gold grape gravel guitar hammer harbor harp hazel helmet heron hill honey horse igloo island ivory jacket jaguar jasmine jelly jungle kayak kettle kite koala ladder lagoon lake lamp lantern lemon lily lime lion lizard llama lotus magnet mango maple marble meadow melon mint mirror moon moose mosaic moss museum nectar needle nest noodle oak oasis ocean olive onion orange orbit orchid otter owl paddle panda paper parrot peach peanut pearl pebble pepper piano pickle pigeon pillow pine planet plum pond popcorn potato prism puzzle quartz quill rabbit radar rainbow raven reef ribbon river robin rocket ruby saddle salmon sandal satin scarf shell silver sketch sled snow sofa spark spider spoon squash star stone storm sugar summit sunset swan tablet tango temple thunder tiger timber tomato topaz torch tower tulip tunnel turtle valley velvet violin volcano wagon walnut walrus water whale wheat willow window winter wolf yarn zebra zipper".Split(' ');
        public static string Of(byte[] secret, byte[] phonePk, byte[] pcPk)
        {
            byte[] h;
            using (var sha = SHA256.Create())
            {
                var tag = Encoding.ASCII.GetBytes("adeebometer words v1");
                var all = new byte[tag.Length + secret.Length + phonePk.Length + pcPk.Length]; int o = 0;
                foreach (var p in new[] { tag, secret, phonePk, pcPk }) { Buffer.BlockCopy(p, 0, all, o, p.Length); o += p.Length; }
                h = sha.ComputeHash(all);
            }
            return List[h[0]] + " · " + List[h[1]] + " · " + List[h[2]] + " · " + List[h[3]];
        }
    }
}
