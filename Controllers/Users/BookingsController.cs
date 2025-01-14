﻿using Hotel_Backend_API.Data;
using Hotel_Backend_API.DTO.Booking;
using Hotel_Backend_API.Models;
using Hotel_Backend_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Hotel_Backend_API.Controllers.Users
{
    [Route("Normal/[controller]")]
    [ApiController]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly ApplicationDbContext dbContext;
        private readonly NotificationService notificationService;
        private readonly UserManager<AppUser> userManager;
        private readonly totalPriceService totalPriceService;

        public BookingsController(ApplicationDbContext dbContext, NotificationService notificationService,
                                  UserManager<AppUser> userManager, totalPriceService totalPriceService)
        {
            this.dbContext = dbContext;
            this.notificationService = notificationService;
            this.userManager = userManager;
            this.totalPriceService = totalPriceService;
        }


        [HttpGet("GetAll/User")]
        public async Task<IActionResult> GetBookingsByUserId( int page = 1, int pageSize = 10)
        {
            try
            {
                var userClaims = User.Claims;
                var userIdClaim = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null)
                {
                    return Unauthorized("User ID not found in token.");
                }

                var username = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
                var guest = await dbContext.Guests
                    .FirstOrDefaultAsync(g => g.Name.Equals(username));
                if (guest == null)
                {
                    return NotFound("No guest found with the specified username.");
                }


                var totalBookings = await dbContext.Bookings
                         .CountAsync(b => b.GuestId == guest.Id);

                var bookings = await dbContext.Bookings
                    .Include(b => b.Guest)
                    .Include(b => b.Room)
                    .ThenInclude(r => r.Hotel)
                    .Include(b => b.Room)
                    .ThenInclude(r => r.RoomType)
                    .Where(b => b.GuestId == guest.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var bookingDtos = bookings.Select(b => new BookingDTO
                {
                    Id = b.Id,
                    GuestName = b.Guest.Name,
                    RoomName = b.Room.RoomType.Name,
                    hotelName = b.Room.Hotel.Name,
                    RoomNumber = b.Room.RoomNumber,
                    ImageURL = b.Room.RoomType.ImageURL,
                    CheckinDate = b.CheckinDate.ToString("yyyy-MM-dd"),
                    CheckoutDate = b.CheckoutDate.ToString("yyyy-MM-dd"),
                    TotalPrice = b.TotalPrice
                }).ToList();

                var response = new
                {
                    TotalCount = totalBookings,
                    PageSize = pageSize,
                    CurrentPage = page,
                    TotalPages = (int)Math.Ceiling(totalBookings / (double)pageSize),
                    Data = bookingDtos
                };

                return Ok(response);


            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while getting bookings for the specified user.");
            }
        }


        [HttpPost()]
        public async Task<IActionResult> AddBooking([FromBody] AddBookingDTO newBookingDto)
        {
            try
            {
                if (newBookingDto == null)
                    return BadRequest("Booking data is required.");

                if (!DateTime.TryParse(newBookingDto.CheckinDate, out DateTime checkinDate))
                    return BadRequest("Invalid check-in date format. Please use 'yyyy-MM-dd'.");

                if (!DateTime.TryParse(newBookingDto.CheckoutDate, out DateTime checkoutDate))
                    return BadRequest("Invalid check-out date format. Please use 'yyyy-MM-dd'.");

                if (checkoutDate <= checkinDate)
                    return BadRequest("Check-out date must be after check-in date.");

                var userClaims = User.Claims;

                var userIdClaim = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null)
                {
                    return Unauthorized("User ID not found in token.");
                }

                string userId = userIdClaim;

                var user = await userManager.FindByIdAsync(userIdClaim);

                var username = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                var email = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var phone = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone)?.Value;

                var serach = await dbContext.Guests
                 .FirstOrDefaultAsync(b => b.Email == email);

                if (serach == null)
                {
                    serach = new Guest
                    {
                        Name = email.Split("@")[0] ?? "Unknown",
                        Email = email ?? "Unknown",
                        Phone = phone ?? "Unknown",
                        imgUser = user.imgUser ?? null
                    };
                    await dbContext.Guests.AddAsync(serach);
                    await dbContext.SaveChangesAsync();
                }

                var room = await dbContext.Rooms.FindAsync(newBookingDto.RoomId);
                if (room == null)
                    return NotFound("The Room ID is not correct.");

                if (room.Status != "Available")
                    return BadRequest("The room is not available.");
                var roomtype = await dbContext.RoomTypes.FindAsync(room.RoomTypeId);

                decimal totalPrice = totalPriceService.CalculateTotalPrice(checkinDate, checkoutDate, roomtype.PricePerNight);

                var newBooking = new Booking
                {
                    GuestId = serach.Id,
                    RoomId = newBookingDto.RoomId,
                    CheckinDate = checkinDate,
                    CheckoutDate = checkoutDate,
                    TotalPrice = totalPrice
                };

                dbContext.Bookings.Add(newBooking);
                await dbContext.SaveChangesAsync();

                room.Status = "Occupied";
                await dbContext.SaveChangesAsync();

                var method = "Card";
                var Status = "YES";

                var newPayment = new Payment
                {
                    BookingId = newBooking.Id,
                    Amount = totalPrice,
                    PaymentDate = checkoutDate,
                    Method = method,
                    StatusDone = Status,
                };

                dbContext.Payments.Add(newPayment);
                await dbContext.SaveChangesAsync();

                string notificationMessage = $"New booking created for guest {serach.Name} in room {room.RoomNumber}.";
                await notificationService.CreateNotificationAsync(userId, notificationMessage);

                var bookingDto = new BookingDTO
                {
                    GuestName = serach.Name,
                    RoomNumber = room.RoomNumber,
                    CheckinDate = newBooking.CheckinDate.ToString("yyyy-MM-dd"),
                    CheckoutDate = newBooking.CheckoutDate.ToString("yyyy-MM-dd"),
                    TotalPrice = newBooking.TotalPrice
                };
                var response = new
                {
                    Message = "Add Booking Success",
                    Data = new
                    {
                        GuestName = serach.Name,
                        RoomNumber = room.RoomNumber,
                        CheckinDate = newBooking.CheckinDate.ToString("yyyy-MM-dd"),
                        CheckoutDate = newBooking.CheckoutDate.ToString("yyyy-MM-dd"),
                        TotalPrice = newBooking.TotalPrice
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while adding the booking.");
            }
        }

        [HttpGet("TotalPrice")]
        public async Task<IActionResult> calcTotalPrice(DateTime checkinDate, DateTime checkoutDate, int roomId)
        {
            var room = await dbContext.Rooms.FindAsync(roomId);

            var roomtype = await dbContext.RoomTypes.FindAsync(room.RoomTypeId);

            decimal totalPrice = totalPriceService.CalculateTotalPrice(checkinDate, checkoutDate, roomtype.PricePerNight);
            return Ok(new { totalPrice });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBooking(int id, [FromBody] UpdateBookingDTO updatedBookingDto)
        {
            try
            {
                if (updatedBookingDto == null)
                    return BadRequest("Invalid booking data.");

                var existingBooking = await dbContext.Bookings.FindAsync(id);
                if (existingBooking == null)
                    return NotFound("Booking not found.");

                if (!DateTime.TryParse(updatedBookingDto.CheckinDate, out DateTime checkinDate))
                    return BadRequest("Invalid check-in date format. Please use 'yyyy-MM-dd'.");

                if (!DateTime.TryParse(updatedBookingDto.CheckoutDate, out DateTime checkoutDate))
                    return BadRequest("Invalid check-out date format. Please use 'yyyy-MM-dd'.");

                if (checkoutDate <= checkinDate)
                    return BadRequest("Check-out date must be after check-in date.");


                var room = await dbContext.Rooms.FindAsync(existingBooking.RoomId);
                var roomtype = await dbContext.RoomTypes.FindAsync(room.RoomTypeId);

                decimal totalPrice = totalPriceService.CalculateTotalPrice(checkinDate, checkoutDate, roomtype.PricePerNight);

                existingBooking.CheckinDate = DateTime.Parse(updatedBookingDto.CheckinDate);
                existingBooking.CheckoutDate = DateTime.Parse(updatedBookingDto.CheckoutDate);
                existingBooking.TotalPrice = totalPrice;

                await dbContext.SaveChangesAsync();

                var userClaims = User.Claims;

                var userIdClaim = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null)
                {
                    return Unauthorized("User ID not found in token.");
                }

                string userId = userIdClaim;

                string notificationMessage = $"Update your booking with new info.";
                await notificationService.CreateNotificationAsync(userId, notificationMessage);

                return Ok("Update Booking Success");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while updating the booking.");
            }
        
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            try
            {
                var booking = await dbContext.Bookings.FindAsync(id);
                if (booking == null)
                    return NotFound("Booking not found.");

                dbContext.Bookings.Remove(booking);
                await dbContext.SaveChangesAsync();

                var userClaims = User.Claims;

                var userIdClaim = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null)
                {
                    return Unauthorized("User ID not found in token.");
                }

                string userId = userIdClaim;

                string notificationMessage = $"your booking is Deleted.";
                await notificationService.CreateNotificationAsync(userId, notificationMessage);

                return Ok("Dleleted Success");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while deleting the booking.");
            }
        }

    }
}
