using GarageManagementSystem.Data;
using GarageManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace GarageManagementSystem.Services
{
    public class AlertService : IAlertService
    {
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;
        private readonly ILogger<AlertService> _logger;

        public AlertService(UserManager<Users> userManager, AppDbContext context, ILogger<AlertService> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        public async Task SendNotificationAsync(string userId, string message)
        {
            if (!int.TryParse(userId, out int parsedUserId))
            {
                _logger.LogError($"Invalid userId: {userId}");
                return; // Exit if userId is not a valid integer
            }

            var notification = new Notification
            {
                UserId = parsedUserId, // Now it's a valid integer
                Message = message,
                DateCreated = DateTime.UtcNow, // Change CreatedAt to DateCreated
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Notification stored for user {userId}: {message}");
        }


        public async Task NotifyAppointmentBookingAsync(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment != null)
            {
                string userMessage = $"Your appointment on {appointment.AppointmentDate} has been booked successfully.";
                await SendNotificationAsync(appointment.User.Id.ToString(), userMessage);

                // Notify Admin
                string adminMessage = $"New Appointment: {appointment.User.FullName} booked an appointment for {appointment.AppointmentDate}.";
                await NotifyAdminAsync(adminMessage);
            }
        }

        public async Task NotifyCarAddedAsync(int carId)
        {
            var car = await _context.Cars
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.Id == carId);

            if (car != null && car.Owner != null)
            {
                string userMessage = $"Your car {car.Model} ({car.LicenseNumber}) has been added to the system.";
                await SendNotificationAsync(car.Owner.Id.ToString(), userMessage);

                // Notify Admin
                string adminMessage = $"New Car Added: {car.Owner.FullName} added a {car.Model} ({car.LicenseNumber}).";
                await NotifyAdminAsync(adminMessage);
            }
        }

        private async Task NotifyAdminAsync(string message)
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");

            foreach (var admin in adminUsers)
            {
                await SendNotificationAsync(admin.Id.ToString(), message);
            }
        }
    }
}
