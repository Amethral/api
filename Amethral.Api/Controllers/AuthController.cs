using Microsoft.AspNetCore.Mvc;
using Amethral.Api.Services;
using Amethral.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace Amethral.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null) return Unauthorized();

            var userId = Guid.Parse(userIdClaim.Value);
            var profile = await _authService.GetUserProfileAsync(userId);

            if (profile == null) return NotFound("User not found.");

            return Ok(profile);
        }

        // OAuth Endpoints

        [HttpGet("oauth/{provider}/login")]
        public IActionResult OAuthLogin(string provider)
        {
            // Capitalize provider name to match registered authentication schemes (Google, Discord, etc.)
            var providerScheme = char.ToUpper(provider[0]) + provider.Substring(1).ToLower();

            var redirectUrl = Url.Action("OAuthCallback", "Auth", new { provider }, Request.Scheme);
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
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
                var failureMessage = authenticateResult.Failure?.Message ?? "Unknown error";
                _logger.LogError(authenticateResult.Failure, "OAuth authentication failed for provider {Provider}. Reason: {Reason}", provider, failureMessage);
                return BadRequest($"OAuth authentication failed: {failureMessage}");
            }

            var claims = authenticateResult.Principal?.Claims;
            var providerKey = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(providerKey) || string.IsNullOrEmpty(email))
            {
                _logger.LogError("OAuth callback failed: Missing ProviderKey or Email for provider {Provider}.", provider);
                return BadRequest("Failed to retrieve user information from OAuth provider.");
            }

            // Generate username from email if name not provided
            var username = name ?? email.Split('@')[0];

            try
            {
                // Find or create user
                var user = await _authService.FindOrCreateOAuthUserAsync(provider, providerKey, email, username);

                // Generate JWT for the web session
                var jwtToken = _authService.GenerateWebJwt(user);

                // Redirect to frontend with success and JWT for Web clients
                var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:4200";
                return Redirect($"{frontendUrl}/oauth/success?token={jwtToken}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OAuth callback (FindOrCreateOAuthUserAsync) for provider {Provider}, email {Email}", provider, email);
                return BadRequest($"An error occurred while logging in with {provider}.");
            }
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
    }
}