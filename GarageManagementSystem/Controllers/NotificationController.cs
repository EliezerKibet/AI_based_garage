using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GarageManagementSystem.Models;
using GarageManagementSystem.Data;
using GarageManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GarageManagementSystem.Services;
using System.Data;
using Microsoft.AspNetCore.Antiforgery;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GarageManagementSystem.Controllers
{

    [Authorize]
    public class NotificationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<NotificationController> _logger;
        private readonly IAntiforgery _antiforgery;
        private readonly JsonSerializerOptions _jsonOptions;


        public NotificationController(
            AppDbContext context,
            UserManager<Users> userManager,
            ILogger<NotificationController> logger,
            IAntiforgery antiforgery)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _antiforgery = antiforgery;

            // Configure JSON serialization options to handle circular references
            _jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve,
                MaxDepth = 64
            };
        }

        // Get Notifications for the logged-in user
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                // Retrieve the current logged-in user
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                // Fetch notifications where UserId matches
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == user.Id)
                    .OrderByDescending(n => n.DateCreated)
                    .Take(10) // Limit to the most recent 10 notifications
                    .Select(n => new // Project to DTO to avoid circular references
                    {
                        n.Id,
                        n.Message,
                        n.DateCreated,
                        n.IsRead,
                        n.UserId
                        // Don't include the User property
                    })
                    .ToListAsync();

                // Return notifications in JSON format
                return Json(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting notifications: {ex.Message}");
                return Json(new List<object>());
            }
        }

        // Send Notification to Admin when a new user registers
        public async Task SendAdminNotificationOnUserRegistration(int userId)
        {
            try
            {
                // Define the admin's email address
                string adminEmail = "admin@admin.com";

                // Retrieve the admin user
                var admin = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.Email == adminEmail);

                if (admin != null)
                {
                    // Create a new notification
                    var notification = new Notification
                    {
                        UserId = admin.Id,
                        Message = $"A new user with ID {userId} has registered.",
                        DateCreated = DateTime.UtcNow,
                        IsRead = false
                    };

                    // Add the notification to the database
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Log an error if the admin user is not found
                    _logger.LogError($"Admin user with email {adminEmail} not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending admin notification: {ex.Message}");
            }
        }

        public async Task SendNotificationToAdminOnCarAddition(int carId)
        {
            try
            {
                // Retrieve all admin users
                var admins = await _userManager.GetUsersInRoleAsync("Admin");

                // Create a new notification for each admin
                foreach (var admin in admins)
                {
                    var notification = new Notification
                    {
                        UserId = admin.Id,
                        Message = $"A new car with ID {carId} has been added by a customer.",
                        DateCreated = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Admin notification(s) sent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending admin notification: {ex.Message}");
            }
        }

        // Send notification to a specific user
        public async Task SendNotification(int userId, string message)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Message = message,
                    DateCreated = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending notification: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found" });
                }

                if (notification.IsRead)
                {
                    return Json(new { success = true, message = "Notification already read" });
                }

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Notification marked as read" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking notification as read: {ex.Message}");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReadStatus(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found" });
                }

                notification.IsRead = !notification.IsRead;
                await _context.SaveChangesAsync();

                return Json(new { success = true, isNowRead = notification.IsRead });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling notification status: {ex.Message}");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { count = 0 });
                }

                int count = await _context.Notifications
                    .Where(n => n.UserId == user.Id && !n.IsRead)
                    .CountAsync();

                return Json(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting unread count: {ex.Message}");
                return Json(new { count = 0 });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var unreadNotifications = await _context.Notifications
                    .Where(n => n.UserId == user.Id && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Marked {unreadNotifications.Count} notifications as read" });

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking all notifications as read: {ex.Message}");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotiPageMarkAllAsRead()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var unreadNotifications = await _context.Notifications
                    .Where(n => n.UserId == user.Id && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }

                await _context.SaveChangesAsync();

                // Return a JSON response with a success flag
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking all notifications as read: {ex.Message}");
                return Json(new { success = false, message = "An error occurred" });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetAllNotifications(int page = 1, int pageSize = 10, string filter = "all")
        {
            try
            {
                // Get current user
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                // Start with base query
                IQueryable<Notification> query = _context.Notifications
                    .Where(n => n.UserId == user.Id);

                // Apply filter
                switch (filter.ToLower())
                {
                    case "read":
                        query = query.Where(n => n.IsRead);
                        break;
                    case "unread":
                        query = query.Where(n => !n.IsRead);
                        break;
                    default: // "all" or any other value
                             // No additional filtering needed
                        break;
                }

                // Get total count for pagination
                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                // Ensure page number is valid
                page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

                // Perform pagination and ordering
                var notifications = await query
                    .OrderByDescending(n => n.DateCreated)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new
                    {
                        n.Id,
                        n.Message,
                        n.DateCreated,
                        n.IsRead,
                        n.UserId
                    })
                    .ToListAsync();

                // Return paginated result
                return Json(new
                {
                    notifications,
                    currentPage = page,
                    pageSize,
                    totalItems,
                    totalPages,
                    filter
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting all notifications: {ex.Message}");
                return Json(new
                {
                    notifications = new List<object>(),
                    currentPage = 1,
                    pageSize,
                    totalItems = 0,
                    totalPages = 0,
                    error = "An error occurred while retrieving notifications"
                });
            }
        }

        [HttpGet]
        public IActionResult AdminNotificationViewPage()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CustomerNotificationViewPageAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Customer"))
            {
                ViewBag.ProfilePicturePath = user.ProfilePicture;
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MechanicNotificationViewPageAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Mechanic"))
            {
                ViewBag.ProfilePicturePath = user.ProfilePicture;
            }
            return View();
        }

    }
}