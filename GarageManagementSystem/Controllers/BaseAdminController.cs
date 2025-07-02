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
    public class BaseAdminController : Controller
    {
        protected readonly AppDbContext _context;
        protected readonly UserManager<Users> _userManager;
        protected readonly ILogger<BaseAdminController> _logger;

        public BaseAdminController(AppDbContext context, UserManager<Users> userManager, ILogger<BaseAdminController> logger)
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
                        .Where(n => n.UserId == user.Id)
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
                    ViewData["UnreadCount"] = notificationViewModels.Count(n => !n.IsRead);
                }
            }
        }

    }

}
