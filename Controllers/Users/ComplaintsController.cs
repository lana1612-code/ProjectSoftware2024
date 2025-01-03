﻿using Hotel_Backend_API.Data;
using Hotel_Backend_API.DTO.Acount;
using Hotel_Backend_API.DTO.Complain;
using Hotel_Backend_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Hotel_Backend_API.Controllers.Users
{
    [Route("Normal/[controller]")]
    [ApiController]
    [Authorize]
    public class ComplaintsController : ControllerBase
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<AppUser> userManager;

        public ComplaintsController(ApplicationDbContext dbContext, UserManager<AppUser> userManager)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
        }

        [HttpPost("Add")]
        public async Task<IActionResult> SubmitComplaint([FromBody] ComplainDTO complaintDTO)
        {

            if (complaintDTO == null)
            {
                return BadRequest("Complaint data is required.");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
            {
                return Unauthorized("User ID not found in token.");
            }

            var complaint = new Complaint
            {
                UserId = userIdClaim,
                HotelId = complaintDTO.HotelId,
                Content = complaintDTO.Content,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Complaints.Add(complaint);
            await dbContext.SaveChangesAsync();

            return Ok("Complaint submitted successfully.");
        }

    /*
        [HttpGet("ComplainForHotel/{hotelId}")]
        [Authorize(Roles = "AdminHotel,Admin")]
        public async Task<IActionResult> GetComplaints(int hotelId)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
            {
                return Unauthorized("User ID not found in token.");
            }

            var complaints = await dbContext.Complaints
                  .Where(c => c.HotelId == hotelId)
                  .ToListAsync();

            if (complaints == null || !complaints.Any())
            {
                return NotFound("No complaints found for this hotel.");
            }
            List<ReturnComplainDTO> returnComplainDto = new List<ReturnComplainDTO>();
            foreach (var Complain in complaints)
            {
                var user = await userManager.FindByIdAsync(Complain.UserId);
                var complaindto = new ReturnComplainDTO
                {
                    Id = Complain.Id,
                    EmailUser = user.Email,
                    NameUser = user.UserName,
                    Content = Complain.Content,
                    CreatedAt = Complain.CreatedAt,
                };
                returnComplainDto.Add(complaindto);
            }

            return Ok(returnComplainDto);
        }
   */
    
    }
}
