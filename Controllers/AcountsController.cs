﻿using Hotel_Backend_API.Data;
using Hotel_Backend_API.DTO.Acount;
using Hotel_Backend_API.Models;
using Hotel_Backend_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Office.Interop.Excel;
using OfficeOpenXml;
using System;

namespace Hotel_Backend_API.Controllers
{
    [Route("Admin/[controller]")]
    [ApiController]
    public class AdminAcountsController : ControllerBase
    {
        private readonly UserManager<AppUser> userManager;
        private readonly SignInManager<AppUser> signInManager;
        private readonly AuthService authService;
        private readonly ApplicationDbContext dbContext;

        public AdminAcountsController(UserManager<AppUser> userManager,
                 SignInManager<AppUser> signInManager,
                 AuthService authService,
                 ApplicationDbContext dbContext)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.authService = authService;
            this.dbContext = dbContext;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new AppUser
            {
                UserName = registerDto.Email.Split('@')[0],
                Email = registerDto.Email,
                PhoneNumber = registerDto.Phone
            };

            var result = await userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { Errors = errors });
            }

            return Ok(new { message = "Sign up Success" });
        }


        [HttpPost("Login")]
        public async Task<IActionResult> Login(LoginDTO loginDTO)
        {
            var user = await userManager.FindByEmailAsync(loginDTO.Email);
            if (user == null)
                return Unauthorized(new { message = "you need to sign up !!" });

            var reult = await signInManager.CheckPasswordSignInAsync(user, loginDTO.Password, false);

            if (!reult.Succeeded)
                return Unauthorized();

            var userRoles = await userManager.GetRolesAsync(user);
            var distinctRoles = userRoles.Distinct().ToList();

            AdminHotel adminHotel = null;
            if (userRoles.Contains("AdminHotel"))
            {
                adminHotel = await dbContext.AdminHotels
                    .FirstOrDefaultAsync(admin => admin.userName == user.UserName);

                return Ok(new
                {
                    Message = "Login Success",
                    UserInfo = new
                    {
                        Username = user.UserName,
                        Email = user.Email,
                        Phone = user.PhoneNumber,
                        Role = distinctRoles,
                        imageProfile = user.imgUser,
                        HotelId = adminHotel.HotelId,
                        Token = await authService.CreateTokenAsync(user, userManager)

                    }
                });
            }



            return Ok(new
            {
                Message = "Login Success",
                UserInfo = new
                {
                    Username = user.UserName,
                    Email = user.Email,
                    Phone = user.PhoneNumber,
                    Role = distinctRoles,
                    imageProfile = user.imgUser,
                    Token = await authService.CreateTokenAsync(user, userManager)

                }
            });

        }


        [HttpPut("AssignRoleToAdminHotel")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole(string username,int HotelID)
        {
            var user = await userManager.FindByNameAsync(username);

            if (user == null)
            {
                return NotFound("User not found");
            }
            if (dbContext.AdminHotels.AsEnumerable()
              .Any(h => h.userName.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest("This userName is already Admin for Hotel.");
            }
            if (dbContext.AdminHotels.AsEnumerable().Any(h => h.HotelId == HotelID))
            {
                return BadRequest("This Hotel is already has Admin.");
            }
           
            var result = await userManager.AddToRoleAsync(user, "AdminHotel");
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }
            var adminHotel = new AdminHotel
            {
                userName = user.UserName,
                HotelId = HotelID
            };
            await dbContext.AdminHotels.AddAsync(adminHotel);
            await dbContext.SaveChangesAsync();
            return Ok("Role assigned successfully");
        }



        [HttpPut("AssignRoleToAdmin")]
        public async Task<IActionResult> AssignRoleAdmin(string username)
        {
            var user = await userManager.FindByNameAsync(username);

            if (user == null)
            {
                return NotFound("User not found");
            }

            var result = await userManager.AddToRoleAsync(user, "Admin");
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }
           
            await dbContext.SaveChangesAsync();
            return Ok("Role assigned successfully");
        }


        [HttpGet]
        public async Task<IActionResult> getUsers()
        {
            var allUsers = await dbContext.Users.ToListAsync();
            var result = new List<UserDto>();

            foreach (var user in allUsers)
            {
                var roles = await userManager.GetRolesAsync(user);
                if (roles.Contains("Normal"))
                {
                    result.Add(new UserDto
                    {
                        Username = user.UserName,
                        Email = user.Email,
                        Phone = user.PhoneNumber,
                        ImageProfile = user.imgUser
                    });
                }
            }
            return Ok(new
            {
                size = result.Count,
                Users = result
            });
        }
    }


}
