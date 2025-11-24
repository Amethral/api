using Microsoft.AspNetCore.Mvc;
using Amethral.Api.Services;
using Amethral.Common.DTOs;

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
            if (!success) return BadRequest("Invalid token or email already exists.");
            return Ok(new { message = "User linked to token." });
        }

        [HttpPost("login-email")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var success = await _authService.LoginWithEmailAsync(request);
            if (!success) return Unauthorized("Invalid credentials or token.");
            return Ok(new { message = "User linked to token." });
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
    }
}