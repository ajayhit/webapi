﻿using JWTAuthentication.WebApi.Constants;
using JWTAuthentication.WebApi.Contexts;
using JWTAuthentication.WebApi.Entities;
using JWTAuthentication.WebApi.Models;
using JWTAuthentication.WebApi.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JWTAuthentication.WebApi.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly JWT _jwt;

        public UserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IOptions<JWT> jwt, ApplicationDbContext context)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _jwt = jwt.Value;
        }
        public async Task<string> RegisterAsync(RegisterModel model)
        {
            var user = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };
            var userWithSameEmail = await _userManager.FindByEmailAsync(model.Email);
            if (userWithSameEmail == null)
            {
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, Authorization.default_role.ToString());

                }
                return $"User Registered with username {user.UserName}";
            }
            else
            {
                return $"Email {user.Email } is already registered.";
            }
        }
        public async Task<AuthenticationModel> GetTokenAsync(TokenRequestModel model,string deviceid)
        {
            var authenticationModel = new AuthenticationModel();
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                authenticationModel.IsAuthenticated = false;
                authenticationModel.Message = $"No Accounts Registered with {model.Email}.";
                return authenticationModel;
            }
            if (await _userManager.CheckPasswordAsync(user, model.Password))
            {
                authenticationModel.IsAuthenticated = true;
                JwtSecurityToken jwtSecurityToken = await CreateJwtToken(user);
                authenticationModel.Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
                authenticationModel.Email = user.Email;
                authenticationModel.UserName = user.UserName;
                var rolesList = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
                authenticationModel.Roles = rolesList.ToList();


                if (user.RefreshTokens.Any(a =>  a.Deviceinfo == deviceid && a.IsActive))
                {
                    var activeRefreshToken = user.RefreshTokens.Where(a => a.IsActive == true).FirstOrDefault();
                    authenticationModel.RefreshToken = activeRefreshToken.Token;
                    authenticationModel.RefreshTokenExpiration = activeRefreshToken.Expires;
                    authenticationModel.Deviceinfo = deviceid;
                    
                }
                else
                {
                    var refreshToken = CreateRefreshToken(deviceid);
                    authenticationModel.RefreshToken = refreshToken.Token;
                    authenticationModel.RefreshTokenExpiration = refreshToken.Expires;
                    authenticationModel.Deviceinfo = deviceid;
                    user.RefreshTokens.Add(refreshToken);
                    _context.Update(user);
                    _context.SaveChanges();
                }

                return authenticationModel;
            }
            authenticationModel.IsAuthenticated = false;
            authenticationModel.Message = $"Incorrect Credentials for user {user.Email}.";
            return authenticationModel;
        }

        private RefreshToken CreateRefreshToken(string deviceid)
        {
            var randomNumber = new byte[32];
            using (var generator = new RNGCryptoServiceProvider())
            {
                generator.GetBytes(randomNumber);
                return new RefreshToken
                {
                    Token = Convert.ToBase64String(randomNumber),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Created = DateTime.UtcNow,
                    Deviceinfo = deviceid
                };

            }
        }

        private async Task<JwtSecurityToken> CreateJwtToken(ApplicationUser user)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            var roleClaims = new List<Claim>();

            for (int i = 0; i < roles.Count; i++)
            {
                roleClaims.Add(new Claim("roles", roles[i]));
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("uid", user.Id)
            }
            .Union(userClaims)
            .Union(roleClaims);

            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

            var jwtSecurityToken = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwt.DurationInMinutes),
                signingCredentials: signingCredentials);
            return jwtSecurityToken;
        }

        public async Task<string> AddRoleAsync(AddRoleModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return $"No Accounts Registered with {model.Email}.";
            }
            if (await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var roleExists = Enum.GetNames(typeof(Authorization.Roles)).Any(x => x.ToLower() == model.Role.ToLower());
                if (roleExists)
                {
                    var validRole = Enum.GetValues(typeof(Authorization.Roles)).Cast<Authorization.Roles>().Where(x => x.ToString().ToLower() == model.Role.ToLower()).FirstOrDefault();
                    await _userManager.AddToRoleAsync(user, validRole.ToString());
                    return $"Added {model.Role} to user {model.Email}.";
                }
                return $"Role {model.Role} not found.";
            }
            return $"Incorrect Credentials for user {user.Email}.";

        }
        public async Task<AuthenticationModel> RefreshTokenAsync(string token,string deviceid)
        {
            var authenticationModel = new AuthenticationModel();
            var user = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token && t.Deviceinfo==deviceid));
            if (user == null)
            {
                authenticationModel.IsAuthenticated = false;
                authenticationModel.Message = $"Token did not match any users.";
                return authenticationModel;
            }

            var refreshToken = user.RefreshTokens.Single(x => x.Token == token && x.Deviceinfo == deviceid);

            if (!refreshToken.IsActive)
            {
                authenticationModel.IsAuthenticated = false;
                authenticationModel.Message = $"Token Not Active.";
                return authenticationModel;
            }

            //Revoke Current Refresh Token
            //refreshToken.Revoked = DateTime.UtcNow;

            //Generate new Refresh Token and save to Database
            var newRefreshToken = CreateRefreshToken(deviceid);
            refreshToken.Token= newRefreshToken.Token;
            refreshToken.Expires = newRefreshToken.Expires;
            _context.Update(refreshToken);
            _context.SaveChanges();

            //Generates new jwt
            authenticationModel.IsAuthenticated = true;
            JwtSecurityToken jwtSecurityToken = await CreateJwtToken(user);
            authenticationModel.Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
            authenticationModel.Email = user.Email;
            authenticationModel.UserName = user.UserName;
            var rolesList = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            authenticationModel.Roles = rolesList.ToList();
            authenticationModel.RefreshToken = newRefreshToken.Token;
            authenticationModel.RefreshTokenExpiration = newRefreshToken.Expires;
            return authenticationModel;
        }
        public bool RevokeToken(string token)
        {
            var user = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token));

            // return false if no user found with token
            if (user == null) return false;

            var refreshToken = user.RefreshTokens.Single(x => x.Token == token);

            // return false if token is not active
            if (!refreshToken.IsActive) return false;

            // revoke token and save
            refreshToken.Revoked = DateTime.UtcNow;
            _context.Update(user);
            _context.SaveChanges();

            return true;
        }
        public bool RevokeTokenAll(string token)
        {
            var userid = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token)).Id;

            var userchk = _context.Users.Where(u => u.Id==userid).ToList();
            foreach (var item in userchk[0].RefreshTokens.Where(aa=>aa.Revoked==null))
            {
          
                string token1 = item.Token.ToString();
                var user = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token1));
                // return false if no user found with token
                if (user == null) return false;
                var refreshToken = user.RefreshTokens.Single(x => x.Token == token1);

                // return false if token is not active
                if (!refreshToken.IsActive) return false;

                // revoke token and save
                refreshToken.Revoked = DateTime.UtcNow;
                _context.Update(user);
                _context.SaveChanges();
            }

            return true;
        }
        public async Task<string> ChangePasswordAsync(ChangePassword Model)
        {
            var userinfo = await _userManager.FindByEmailAsync(Model.Email);
            if (userinfo == null)
            {
                return "No Accounts Registered with .";
            }
            else if(await _userManager.CheckPasswordAsync(userinfo, Model.Password))
            {
                var result = await _userManager.ChangePasswordAsync(userinfo,Model.Password, Model.NewPassword);
                if (result.Succeeded)
                {
                    var userid = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == Model.Token)).Id;

                    var userchk = _context.Users.Where(u => u.Id == userid).ToList();
                    foreach (var item in userchk[0].RefreshTokens.Where(aa => aa.Revoked == null))
                    {

                        string token1 = item.Token.ToString();
                        var user = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token1));
                        // return false if no user found with token
                        if (user == null)
                        {
                            return "Token Issue";
                        }
                        var refreshToken = user.RefreshTokens.Single(x => x.Token == token1);

                        // return false if token is not active
                        if (!refreshToken.IsActive)
                        {
                            return "Token Issue";
                        }

                        // revoke token and save
                        refreshToken.Revoked = DateTime.UtcNow;
                        _context.Update(user);
                        _context.SaveChanges();
                    }
                    return "DONE";
                }
                else
                {
                    return "Error .";
                }
            }
            else
            {
                return "Old Password Is Not Correct .";
            }
        }
        public async Task<string> ForgotPasswordAsync(Forgotpassword Model)
        {
            var userinfo = await _userManager.FindByEmailAsync(Model.Email);
            if (userinfo == null)
            {
                return "No Accounts Registered with .";
            }
            var code = await _userManager.GenerateChangePhoneNumberTokenAsync(userinfo, Model.phonenumber);
            return code;
        }
        public async Task<string> VerifyPasscode(Verifycode Model)
        {
            var userinfo = await _userManager.FindByEmailAsync(Model.Email);
            if (userinfo == null)
            {
                return "No Accounts Registered with .";
            }
            var result = await _userManager.VerifyChangePhoneNumberTokenAsync(userinfo, Model.Code, Model.phonenumber);
            if (result == true)
            {
                var code = await _userManager.GeneratePasswordResetTokenAsync(userinfo);
                var result1 = await _userManager.ResetPasswordAsync(userinfo, code, "Ajay@7720");
                var userid = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == Model.Token)).Id;

                var userchk = _context.Users.Where(u => u.Id == userid).ToList();
                foreach (var item in userchk[0].RefreshTokens.Where(aa => aa.Revoked == null))
                {

                    string token1 = item.Token.ToString();
                    var user = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token1));
                    // return false if no user found with token
                    if (user == null)
                    {
                        return "Token Issue";
                    }
                    var refreshToken = user.RefreshTokens.Single(x => x.Token == token1);

                    // return false if token is not active
                    if (!refreshToken.IsActive)
                    {
                        return "Token Issue";
                    }

                    // revoke token and save
                    refreshToken.Revoked = DateTime.UtcNow;
                    _context.Update(user);
                    _context.SaveChanges();
                }
                return "New Password Send .";
            }
            else
            {
                return "Otp MissMatch .";
            }
     
        }

        public ApplicationUser GetById(string id)
        {
            return _context.Users.Find(id);
        }

        //TODO : Update User Details
        //TODO : Remove User from Role 
    }
}
