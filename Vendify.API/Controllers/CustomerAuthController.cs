using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Customers;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/customer")]
    public class CustomerAuthController : ControllerBase
    {
        private readonly ICustomerAuthService _auth;

        public CustomerAuthController(ICustomerAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register/{storeSlug}")]
        public async Task<IActionResult> Register(
            string storeSlug, [FromBody] CustomerRegisterRequest request) =>
            Map(await _auth.RegisterAsync(storeSlug, request));

        [HttpPost("verify/{storeSlug}")]
        public async Task<IActionResult> Verify(
            string storeSlug, [FromBody] CustomerVerifyRequest request) =>
            Map(await _auth.VerifyEmailAsync(storeSlug, request));

        [HttpPost("resend/{storeSlug}")]
        public async Task<IActionResult> Resend(
            string storeSlug, [FromBody] CustomerEmailRequest request) =>
            Map(await _auth.ResendVerificationAsync(storeSlug, request.Email));

        [HttpPost("login/{storeSlug}")]
        public async Task<IActionResult> Login(
            string storeSlug, [FromBody] CustomerLoginRequest request) =>
            Map(await _auth.LoginAsync(storeSlug, request));

        [HttpPost("forgot/{storeSlug}")]
        public async Task<IActionResult> Forgot(
            string storeSlug, [FromBody] CustomerEmailRequest request) =>
            Map(await _auth.ForgotPasswordAsync(storeSlug, request.Email));

        [HttpPost("reset/{storeSlug}")]
        public async Task<IActionResult> Reset(
            string storeSlug, [FromBody] CustomerResetRequest request) =>
            Map(await _auth.ResetPasswordAsync(storeSlug, request));

        [HttpPost("change-password/{storeSlug}")]
        public async Task<IActionResult> ChangePassword(
            string storeSlug, [FromBody] CustomerChangePasswordRequest request) =>
            Map(await _auth.ChangePasswordAsync(storeSlug, request));

        [HttpPost("me/{storeSlug}")]
        public async Task<IActionResult> Me(
            string storeSlug, [FromBody] CustomerTokenRequest request) =>
            Map(await _auth.GetProfileAsync(storeSlug, request.Token));

        [HttpPost("orders/{storeSlug}")]
        public async Task<IActionResult> Orders(
            string storeSlug, [FromBody] CustomerTokenRequest request) =>
            Map(await _auth.GetOrdersAsync(storeSlug, request.Token));

        // 200 = worked, 401 = session over, 403 = email not verified yet
        private IActionResult Map<T>(ApiResponse<T> r)
        {
            if (r.Success) return Ok(r);
            if (r.Message == CustomerMessages.SessionExpired)
                return Unauthorized(r);
            if (r.Message == CustomerMessages.NotVerified)
                return StatusCode(403, r);
            return BadRequest(r);
        }
    }
}