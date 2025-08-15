using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ResAuthApi.Api.Utils;

namespace ResAuthApi.Api.Controllers
{
    [ApiController]
    [Route("")]
    public class KeysController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        public KeysController(IConfiguration cfg) => _cfg = cfg;

        [HttpGet("publickey")]
        public IActionResult GetPublicKey()
        {
            var path = _cfg["Jwt:PublicKeyPath"]!;
            var (n, e) = KeyLoader.LoadPublicParamsFromPem(path);
            return Ok(new
            {
                kty = "RSA",
                use = "sig",
                alg = "RS256",
                n = Base64UrlEncoder.Encode(n),
                e = Base64UrlEncoder.Encode(e),
            });
        }
    }
}
