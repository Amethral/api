using Microsoft.AspNetCore.Mvc;
using Amethral.Api.Services;
using Amethral.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace Amethral.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("init")]
        public async Task<IActionResult> Init([FromBody] WebTokenRequest request)
        {
            var result = await _authService.CreateWebTokenAsync(request.DeviceId);
            return Ok(result);
        }

        [HttpPost("register-email")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var success = await _authService.RegisterWithEmailAsync(request);
            if (!success) return BadRequest("Email already exists.");
    
            // On renvoie un succès générique
            return Ok(new { message = "Registration successful." });
        }

        [HttpPost("login-email")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var token = await _authService.LoginWithEmailAsync(request);
            if (token == null) return Unauthorized("Invalid credentials.");

            return Ok(new { token });
        }

        [HttpPost("finalize")]
        public async Task<IActionResult> Finalize([FromBody] TokenStatusRequest request)
        {
            var result = await _authService.FinalizeAuthAsync(request.WebToken, request.DeviceId);
            
            if (result == null)
            {
                // 202 Accepted signifie : "La requête est reçue, mais le traitement n'est pas fini"
                // C'est le standard pour le Polling.
                return Accepted(new { status = "waiting_for_user_login" });
            }

            return Ok(result);
        }

        [HttpPost("link-existing-account")]
        [Authorize] 
        public async Task<IActionResult> LinkExistingAccount([FromBody] TokenStatusRequest request)
        {
            // On récupère l'ID du User depuis son JWT Web (automatique grâce à [Authorize])
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null) return Unauthorized();

            var userId = Guid.Parse(userIdClaim.Value);

            // On lie le token Unity (WebToken) à l'utilisateur connecté
            var success = await _authService.ForceLinkUserToToken(request.WebToken, userId);
            
            // Correction: utilise plutôt request.WebToken si tu as adapté le DTO, 
            // ou réutilise WebTokenRequest en considérant que DeviceId contient le token, 
            // ou mieux : crée un simple DTO { string Token }
        
            if (!success) return BadRequest("Token invalid or expired");

            return Ok(new { message = "Linked successfully" });
        }

        // OAuth Endpoints

        [HttpGet("oauth/{provider}/login")]
        public IActionResult OAuthLogin(string provider, [FromQuery] string? webToken = null, [FromQuery] string? deviceId = null)
        {
            // Capitalize provider name to match registered authentication schemes (Google, Discord, etc.)
            var providerScheme = char.ToUpper(provider[0]) + provider.Substring(1).ToLower();
            
            // Store webToken and deviceId in state parameter for callback
            var state = string.Empty;
            if (!string.IsNullOrEmpty(webToken) && !string.IsNullOrEmpty(deviceId))
            {
                state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{webToken}:{deviceId}"));
            }

            var redirectUrl = Url.Action("OAuthCallback", "Auth", new { provider }, Request.Scheme);
            var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                RedirectUri = redirectUrl,
                Items = { { "state", state } }
            };

            return Challenge(properties, providerScheme);
        }

        [HttpGet("oauth/{provider}/callback")]
        public async Task<IActionResult> OAuthCallback(string provider)
        {
            // Capitalize provider name to match registered authentication schemes
            var providerScheme = char.ToUpper(provider[0]) + provider.Substring(1).ToLower();
            
            var authenticateResult = await HttpContext.AuthenticateAsync(providerScheme);

            if (!authenticateResult.Succeeded)
            {
                return BadRequest("OAuth authentication failed.");
            }

            var claims = authenticateResult.Principal?.Claims;
            var providerKey = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(providerKey) || string.IsNullOrEmpty(email))
            {
                return BadRequest("Failed to retrieve user information from OAuth provider.");
            }

            // Generate username from email if name not provided
            var username = name ?? email.Split('@')[0];

            // Find or create user
            var user = await _authService.FindOrCreateOAuthUserAsync(provider, providerKey, email, username);

            // Generate JWT for the web session
            var jwtToken = GenerateJwtForUser(user);

            // Check if there's a game session to link
            var stateParam = authenticateResult.Properties?.Items.TryGetValue("state", out var state) == true ? state : null;
            if (!string.IsNullOrEmpty(stateParam))
            {
                try
                {
                    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(stateParam));
                    var parts = decoded.Split(':');
                    if (parts.Length == 2)
                    {
                        var webToken = parts[0];
                        var deviceId = parts[1];

                        // Link the user to the game session
                        await _authService.ForceLinkUserToToken(webToken, user.Id);

                        // Redirect to frontend with success and JWT
                        var frontendUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/oauth/success?token={jwtToken}";
                        return Redirect(frontendUrl);
                    }
                }
                catch
                {
                    // State parsing failed, continue with normal flow
                }
            }

            // Return JWT token for web session
            return Ok(new { token = jwtToken, message = "OAuth login successful" });
        }


        [HttpPost("oauth/link")]
        [Authorize]
        public async Task<IActionResult> LinkOAuthAccount([FromBody] OAuthLinkRequest request)
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null) return Unauthorized();

            var userId = Guid.Parse(userIdClaim.Value);

            var success = await _authService.LinkOAuthToUserAsync(userId, request.Provider, request.ProviderKey);

            if (!success)
            {
                return BadRequest("This OAuth account is already linked to another user.");
            }

            return Ok(new { message = "OAuth account linked successfully" });
        }

        // Helper method to generate JWT (reuses logic from AuthService)
        private string GenerateJwtForUser(Amethral.Api.Data.Entities.User user)
        {
            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["JwtSettings:SecretKey"]!)
            );
            var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, user.Email),
                new System.Security.Claims.Claim("username", user.Username)
            };

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: config["JwtSettings:Issuer"],
                audience: config["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

// DTO for OAuth link request
namespace Amethral.Common.DTOs
{
    public class OAuthLinkRequest
    {
        public string Provider { get; set; } = string.Empty;
        public string ProviderKey { get; set; } = string.Empty;
    }
}