﻿using Microsoft.AspNetCore.Identity;

namespace Hotel_Backend_API.Models
{
    public class AppUser : IdentityUser
    {
        public string? imgUser { get; set; }
    }
}
