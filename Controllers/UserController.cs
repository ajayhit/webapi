using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using JWTAuthentication.WebApi.Models;
using JWTAuthentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthentication.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public UserController(IUserService userService, IHttpContextAccessor httpContextAccessor)
        {
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
        }
        [HttpPost("register")]
        //[ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult> RegisterAsync(RegisterModel model)
        {

            var result = await _userService.RegisterAsync(model);
            return Ok(result);
        }
        [HttpPost("token")]
        public async Task<IActionResult> GetTokenAsync(TokenRequestModel model)
        {
            string header =_httpContextAccessor.HttpContext.Request.Headers["X-Device-info"];
            var result = await _userService.GetTokenAsync(model, header);
            SetRefreshTokenInCookie(result.RefreshToken);
            return Ok(result);
        }
        [HttpPost("addrole")]
        public async Task<IActionResult> AddRoleAsync(AddRoleModel model)
        {
            var result = await _userService.AddRoleAsync(model);
            return Ok(result);
        }
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RevokeTokenRequest model)
        {
            string header = _httpContextAccessor.HttpContext.Request.Headers["X-Device-info"];
            var refreshToken =  model.Token;
        
            var response = await _userService.RefreshTokenAsync(refreshToken, header);
            if (!string.IsNullOrEmpty(response.RefreshToken))
                SetRefreshTokenInCookie(response.RefreshToken);
            return Ok(response);
        }
       
        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest model)
        {
            // accept token from request body or cookie
            var token = model.Token ?? Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Token is required" });

            var response = _userService.RevokeToken(token);

            if (!response)
                return NotFound(new { message = "Token not found" });

            return Ok(new { message = "Token revoked" });
        }

        [HttpPost("revoke-token-All")]
        public async Task<IActionResult> RevokeTokenAll([FromBody] RevokeTokenRequest model)
        {
            // a  // accept token from request body or cookie
            var token = model.Token ?? Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Token is required" });

            var response = _userService.RevokeTokenAll(token);

            if (!response)
                return NotFound(new { message = "Token not found" });

            return Ok(new { message = "Token revoked" });
        }

        [HttpPost("ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePassword model)
        {
            var response = _userService.ChangePasswordAsync(model);

            return Ok(new { message = response });
        }
        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] Forgotpassword model)
        {
            var response = _userService.ForgotPasswordAsync(model);
            return Ok(new { message = response });
        }
        [HttpPost("VerifyToken")]
        public async Task<IActionResult> VerifyToken([FromBody] Verifycode model)
        {
            var response = _userService.VerifyPasscode(model);
            return Ok(new { message = response });
        }



        private void SetRefreshTokenInCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(10),
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

        [Authorize]
        [HttpPost("tokens")]
        public IActionResult GetRefreshTokens(string id)
        {
            var user = _userService.GetById(id);
            return Ok(user.RefreshTokens);
        }

       
    }
}