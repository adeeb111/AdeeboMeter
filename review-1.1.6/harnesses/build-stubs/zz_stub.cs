// mono stub: ECDiffieHellmanCng is Windows-only; P-384 paths throw PlatformNotSupportedException under this harness build.
namespace System.Security.Cryptography {
  public enum ECDiffieHellmanKeyDerivationFunction { Hash, Hmac, Tls }
  public sealed class ECDiffieHellmanCng : IDisposable {
    public ECDiffieHellmanCng(int s) { throw new PlatformNotSupportedException("stub"); }
    public ECDiffieHellmanCng(CngKey k) { throw new PlatformNotSupportedException("stub"); }
    public CngKey Key { get { return null; } }
    public ECDiffieHellmanKeyDerivationFunction KeyDerivationFunction { get; set; }
    public CngAlgorithm HashAlgorithm { get; set; }
    public byte[] DeriveKeyMaterial(CngKey k) { throw new PlatformNotSupportedException("stub"); }
    public ECDiffieHellmanPublicKey PublicKey { get { return null; } }
    public byte[] SecretPrepend { get; set; } public byte[] SecretAppend { get; set; }
    public void Dispose() { }
  } }
