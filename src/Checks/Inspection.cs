// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace AdeeboMeter
{
    // Is HTTPS being opened and inspected on the way? Public websites' certificates chain up to a root from Microsoft's
    // public list (the "Third-Party Root Certification Authorities" store). A company proxy or a man-in-the-middle re-signs
    // traffic with its own root, which someone added to this PC's trusted roots by hand or by policy - and which is NOT on
    // Microsoft's public list. Read-only: nothing is changed or sent.
    static class Inspection
    {
        // "" = not inspected (or can't tell); otherwise the name of the root that signed it
        public static string InspectedBy(X509Certificate2 leaf)
        {
            try
            {
                using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;   // no network: this is only about who signed it
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                    chain.Build(leaf);
                    if (chain.ChainElements.Count == 0) return "";
                    var root = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                    if (OnPublicList(root)) return "";
                    return root.GetNameInfo(X509NameType.SimpleName, false);
                }
            }
            catch { return ""; }
        }
        static bool OnPublicList(X509Certificate2 root)
        {
            foreach (var loc in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
                try
                {
                    using (var st = new X509Store(StoreName.AuthRoot, loc))
                    {
                        st.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                        if (st.Certificates.Cast<X509Certificate2>().Any(c => c.Thumbprint == root.Thumbprint)) return true;
                    }
                }
                catch { }
            // Windows fills the public list on demand, so a root missing from it can also just be "not downloaded yet":
            // only call it inspection when the root sits in the machine's or user's own trusted roots
            foreach (var loc in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
                try
                {
                    using (var st = new X509Store(StoreName.Root, loc))
                    {
                        st.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                        var mine = st.Certificates.Cast<X509Certificate2>().FirstOrDefault(c => c.Thumbprint == root.Thumbprint);
                        if (mine != null) return IsWellKnown(root);
                    }
                }
                catch { }
            return true;
        }
        // the big public roots every Windows ships with - never call these "inspection" even if the public list is empty
        static readonly string[] Public = { "DigiCert", "ISRG", "GlobalSign", "Sectigo", "USERTrust", "Baltimore", "Microsoft", "Amazon", "GTS Root", "Google Trust", "Entrust", "GoDaddy", "Starfield", "Certum", "IdenTrust", "QuoVadis", "SSL.com", "Comodo", "AAA Certificate", "Go Daddy", "Verisign", "VeriSign", "Thawte", "Buypass", "Actalis", "HARICA", "SwissSign", "T-TeleSec", "D-TRUST" };
        static bool IsWellKnown(X509Certificate2 root)
        {
            string n = root.GetNameInfo(X509NameType.SimpleName, false) ?? "";
            return Public.Any(p => n.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
