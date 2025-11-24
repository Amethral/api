using Microsoft.AspNetCore.Mvc;
using Amethral.Api.Services;
using Amethral.Common.DTOs;
using Microsoft.AspNetCore.Authorization;

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
        public async Task<IActionResult> LinkExistingAccount([FromBody] WebTokenRequest request)
        {
            // On récupère l'ID du User depuis son JWT Web (automatique grâce à [Authorize])
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null) return Unauthorized();

            var userId = Guid.Parse(userIdClaim.Value);

            // On appelle une méthode simple (à créer dans AuthService) qui fait juste l'UPDATE
            var success = await _authService.ForceLinkUserToToken(request.DeviceId, userId); // deviceId ici = le token string, nommage un peu confus, utilise le champ Token
            
            // Correction: utilise plutôt request.WebToken si tu as adapté le DTO, 
            // ou réutilise WebTokenRequest en considérant que DeviceId contient le token, 
            // ou mieux : crée un simple DTO { string Token }
        
            if (!success) return BadRequest("Token invalid or expired");

            return Ok(new { message = "Linked successfully" });
        }
    }
}