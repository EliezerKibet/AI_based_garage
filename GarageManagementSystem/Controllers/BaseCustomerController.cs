using GarageManagementSystem.Data;
using GarageManagementSystem.Models;
using GarageManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;

namespace GarageManagementSystem.Controllers
{
    [Authorize]
    public class BaseCustomerController : Controller
    {
        protected readonly UserManager<Users> _userManager;
        protected readonly AppDbContext _context;
        protected readonly ILogger<CustomerController> _logger;

        public BaseCustomerController(AppDbContext context,
            UserManager<Users> userManager, 
            ILogger<CustomerController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // Override OnActionExecutionAsync for async handling instead of OnActionExecuted
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            await base.OnActionExecutionAsync(context, next);
            await LoadNotificationsAsync(); // Ensure async loading of notifications
            await LoadProfilePictureAsync(); // Load the profile picture for the layout
        }

        // Async method to load the profile picture for the logged-in user
        public async Task LoadProfilePictureAsync()
        {
            // Get the current logged-in user
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user != null)
                {
                    // Set profile picture or default image if not available
                    ViewData["ProfilePicture"] = user.ProfilePicture ?? "/images/default-profile.png";
                }
            }
        }

        // Async method to load notifications for the logged-in user
        public async Task LoadNotificationsAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user != null)
                {
                    var notifications = await _context.Notifications
                        .Where(n => n.UserId == user.Id && !n.IsRead)
                        .OrderByDescending(n => n.DateCreated)
                        .ToListAsync();

                    var notificationViewModels = notifications.Select(n => new NotificationViewModel
                    {
                        Id = n.Id,
                        Message = n.Message,
                        DateCreated = n.DateCreated,
                        IsRead = n.IsRead
                    }).ToList();

                    ViewData["Notifications"] = notificationViewModels;
                    ViewData["UnreadCount"] = notificationViewModels.Count;
                }
            }
        }
    }

}
