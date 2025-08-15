using System.Security.Cryptography;
using System.Text;

namespace ResAuthApi.Api.Utils
{
    public static class TokenHasher
    {
        public static string ComputeHash(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes); // base16
        }
    }
}
