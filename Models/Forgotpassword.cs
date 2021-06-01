using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace JWTAuthentication.WebApi.Models
{
    public class Forgotpassword
    {
        [Required]
        public string phonenumber { get; set; }
        [Required]
        public string Email { get; set; }
    }
}
