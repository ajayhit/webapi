using JWTAuthentication.WebApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JWTAuthentication.WebApi.Services
{
    public interface IUserService
    {
        Task<dynamic> RegisterAsync(RegisterModel model);
        Task<AuthenticationModel> GetTokenAsync(TokenRequestModel model,string deviceid);
        Task<string> AddRoleAsync(AddRoleModel model);
        Task<AuthenticationModel> RefreshTokenAsync(string jwtToken,string deviceid);
        bool RevokeToken(string token);
        bool RevokeTokenAll(string Userid);
        Task<string> ChangePasswordAsync(ChangePassword Model);
        Task<string> ForgotPasswordAsync(Forgotpassword Model);
        Task<string> VerifyPasscode(Verifycode Model);
        ApplicationUser GetById(string id);
    }
}
