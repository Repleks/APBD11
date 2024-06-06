using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace JWT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JWTDbContext _context;

        public AuthController(IConfiguration config, UserManager<IdentityUser> userManager,
            JWTDbContext context)
        {
            _config = config;
            _userManager = userManager;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestModel model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_config["JWT:Key"]);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new Claim[]
                    {
                        new Claim(ClaimTypes.Name, user.UserName)
                    }),
                    Expires = DateTime.UtcNow.AddMinutes(15),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var stringToken = tokenHandler.WriteToken(token);

                var refreshToken = GenerateRefreshToken();
                _context.RefreshTokens.Add(new RefreshToken
                {
                    Token = refreshToken,
                    UserName = user.UserName,
                    ExpiryDate = DateTime.UtcNow.AddDays(7)
                });
                await _context.SaveChangesAsync();

                return Ok(new LoginResponseModel
                {
                    Token = stringToken,
                    RefreshToken = refreshToken
                });
            }

            return Unauthorized("Invalid username or password");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequestModel model)
        {
            var user = new IdentityUser { UserName = model.UserName };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                return Ok("User registered successfully");
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshTokenRequestModel model)
        {
            var refreshToken = await _context.RefreshTokens.SingleOrDefaultAsync(rt => rt.Token == model.RefreshToken);
            if (refreshToken == null || refreshToken.ExpiryDate <= DateTime.UtcNow)
            {
                return Unauthorized("Invalid or expired refresh token");
            }

            var user = await _userManager.FindByNameAsync(refreshToken.UserName);
            if (user == null)
            {
                return Unauthorized("Invalid refresh token");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["JWT:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.UserName)
                }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var stringToken = tokenHandler.WriteToken(token);

            var newRefreshToken = GenerateRefreshToken();
            refreshToken.Token = newRefreshToken;
            refreshToken.ExpiryDate = DateTime.UtcNow.AddDays(7);
            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();

            return Ok(new LoginResponseModel
            {
                Token = stringToken,
                RefreshToken = newRefreshToken
            });
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
    }

    public class LoginRequestModel
    {
        [Required] public string UserName { get; set; }
        [Required] public string Password { get; set; }
    }

    public class RegisterRequestModel
    {
        [Required] public string UserName { get; set; }
        [Required] public string Password { get; set; }
    }

    public class LoginResponseModel
    {
        public string Token { get; set; }
        public string RefreshToken { get; set; }
    }

    public class RefreshTokenRequestModel
    {
        public string RefreshToken { get; set; }
    }

    public class RefreshToken
    {
        public int Id { get; set; }
        public string Token { get; set; }
        public string UserName { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}