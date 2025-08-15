using System.Security.Cryptography;

namespace ResAuthApi.Api.Utils
{
    public static class KeyLoader
    {
        public static RSA LoadPrivateKeyFromPem(string pemPath)
        {
            var pem = File.ReadAllText(pemPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa;
        }

        public static (byte[] Modulus, byte[] Exponent) LoadPublicParamsFromPem(string pemPath)
        {
            var pem = File.ReadAllText(pemPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var p = rsa.ExportParameters(false);
            return (p.Modulus!, p.Exponent!);
        }
    }
}
