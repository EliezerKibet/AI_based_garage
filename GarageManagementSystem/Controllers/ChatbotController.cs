using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RestSharp;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GarageManagementSystem.Data;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using GarageManagementSystem.Models;

[Route("api/chatbot")]
[ApiController]
[Authorize]
public class ChatbotController : ControllerBase
{
    private readonly ILogger<ChatbotController> _logger;
    private readonly string _apiKey;
    private readonly string _apiUrl = "https://api.groq.com/openai/v1/chat/completions";
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public ChatbotController(IConfiguration config,
        ILogger<ChatbotController> logger,
        AppDbContext context,
        IMemoryCache cache)
    {
        _logger = logger;
        _apiKey = config["ApiKey"];
        _context = context;
        _cache = cache;
    }

    // Get initial welcome message and prompts
    [HttpGet("welcome")]
    public async Task<IActionResult> GetWelcomeMessage()
    {
        var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "User";
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var welcomeData = await GenerateWelcomeMessage(userRole, userId);
        return Ok(welcomeData);
    }

    // Get fresh prompts without asking a question
    [HttpGet("refresh-prompts")]
    public async Task<IActionResult> RefreshPrompts()
    {
        var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "User";
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var prompts = await GenerateContextualPrompts("general", userRole, userId);
        var quickStats = await GetQuickStats(userRole, userId);

        return Ok(new
        {
            prompts = prompts,
            quickStats = quickStats,
            timestamp = DateTime.Now
        });
    }

    [HttpPost("ask")]
    public async Task<IActionResult> AskChatbot([FromBody] ChatbotRequest requestBody)
    {
        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Question))
        {
            _logger.LogWarning("User submitted an empty question.");
            return BadRequest(new { message = "Please enter a valid question." });
        }

        _logger.LogInformation("Chatbot request received: {Question}", requestBody.Question);

        var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "User";
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Get direct database answer
        var directResult = await GetDirectDatabaseAnswer(requestBody.Question, userRole, userId);

        string finalAnswer;
        if (!string.IsNullOrEmpty(directResult.Answer))
        {
            finalAnswer = directResult.Answer;
        }
        else
        {
            // OPTION: Disable AI for mechanics or return "no data found"
            if (userRole == "Mechanic")
            {
                finalAnswer = "I don't have specific information about that in our database. Please ask about your appointments, assigned cars, or completed reports.";
            }
            else
            {
                // Use AI for other roles
                finalAnswer = await GetAIResponse(requestBody.Question, userRole, userId);
            }
        }

        // Generate fresh contextual prompts based on the question asked
        var contextualPrompts = await GenerateContextualPrompts(requestBody.Question, userRole, userId);
        var quickStats = await GetQuickStats(userRole, userId);
        var relatedActions = GetRelatedActions(requestBody.Question, userRole);

        return Ok(new
        {
            answer = finalAnswer,
            prompts = contextualPrompts,
            quickStats = quickStats,
            relatedActions = relatedActions,
            questionCategory = directResult.Category,
            timestamp = DateTime.Now
        });
    }

    private async Task<object> GenerateWelcomeMessage(string userRole, string userId)
    {
        var user = await _context.Users.FindAsync(int.Parse(userId));
        var userName = user?.FullName ?? "User";

        string welcomeMessage = userRole switch
        {
            "Admin" => $"👋 **Welcome back, {userName}!**\n\n🎯 **Admin Dashboard Assistant**\nI'm here to help you manage your garage operations efficiently. Ask me about system statistics, performance metrics, staff management, or any operational insights you need.",
            "Customer" => $"👋 **Hello, {userName}!**\n\n🚗 **Your Personal Garage Assistant**\nI can help you with your vehicle appointments, service history, and any questions about our garage services.",
            "Mechanic" => $"👋 **Hi, {userName}!**\n\n🔧 **Mechanic Assistant**\nI'm here to help you with your work schedule, assigned vehicles, and any technical information you need for your daily tasks.",
            _ => $"👋 **Welcome, {userName}!**\n\nI'm your garage management assistant. How can I help you today?"
        };

        var initialPrompts = await GenerateContextualPrompts("welcome", userRole, userId);
        var quickStats = await GetQuickStats(userRole, userId);

        return new
        {
            message = welcomeMessage,
            prompts = initialPrompts,
            quickStats = quickStats,
            userRole = userRole,
            timestamp = DateTime.Now
        };
    }

    private async Task<(string Answer, string Category)> GetDirectDatabaseAnswer(string question, string userRole, string userIdStr)
    {
        if (!int.TryParse(userIdStr, out int userId))
        {
            _logger.LogWarning("Failed to parse userId: {UserIdStr}", userIdStr);
            return (string.Empty, "error");
        }

        var normalizedQuestion = question.ToLowerInvariant();

        if (userRole == "Admin")
        {
            // VEHICLE MANAGEMENT QUERIES
            if (normalizedQuestion.Contains("how many cars") || normalizedQuestion.Contains("total cars") || normalizedQuestion.Contains("count cars"))
            {
                var carCount = await _context.Cars.CountAsync();
                var assignedCount = await _context.CarMechanicAssignments.Select(a => a.CarId).Distinct().CountAsync();
                var unassignedCount = carCount - assignedCount;

                return ($"🚗 **Vehicle Overview**\n" +
                       $"• **Total Cars:** {carCount}\n" +
                       $"• **Assigned to Mechanics:** {assignedCount}\n" +
                       $"• **Unassigned:** {unassignedCount}\n" +
                       $"• **Assignment Rate:** {(carCount > 0 ? (assignedCount * 100 / carCount) : 0)}%", "vehicles");
            }

            if (normalizedQuestion.Contains("cars without service") || normalizedQuestion.Contains("cars need service"))
            {
                var carsWithoutRecentService = await _context.Cars
                    .Where(c => !_context.MechanicReports.Any(r => r.CarId == c.Id && r.DateReported >= DateTime.Today.AddDays(-30)))
                    .Include(c => c.Owner)
                    .Take(5)
                    .ToListAsync();

                var result = $"🔍 **{carsWithoutRecentService.Count} cars** haven't had service in the last 30 days.\n\n";

                if (carsWithoutRecentService.Any())
                {
                    result += "**Recent Examples:**\n";
                    foreach (var car in carsWithoutRecentService.Take(3))
                    {
                        result += $"• {car.Make} {car.Model} ({car.LicenseNumber}) - Owner: {car.Owner?.FullName ?? "Unknown"}\n";
                    }
                }

                return (result, "vehicles");
            }

            // CUSTOMER MANAGEMENT QUERIES
            if (normalizedQuestion.Contains("how many customers") || normalizedQuestion.Contains("total customers"))
            {
                var customerRoleId = await _context.Roles.Where(r => r.Name == "Customer").Select(r => r.Id).FirstOrDefaultAsync();
                var totalCustomers = await _context.UserRoles.Where(ur => ur.RoleId == customerRoleId).CountAsync();
                var activeCustomers = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == customerRoleId) &&
                               _context.Appointments.Any(a => a.UserId == u.Id && a.AppointmentDate >= DateTime.Today.AddDays(-90)))
                    .CountAsync();

                return ($"👥 **Customer Analytics**\n" +
                       $"• **Total Registered:** {totalCustomers}\n" +
                       $"• **Active (Last 90 days):** {activeCustomers}\n" +
                       $"• **Activity Rate:** {(totalCustomers > 0 ? (activeCustomers * 100 / totalCustomers) : 0)}%", "customers");
            }

            // STAFF MANAGEMENT QUERIES
            if (normalizedQuestion.Contains("how many mechanics") || normalizedQuestion.Contains("total mechanics"))
            {
                var mechanicRoleId = await _context.Roles.Where(r => r.Name == "Mechanic").Select(r => r.Id).FirstOrDefaultAsync();
                var totalMechanics = await _context.UserRoles.Where(ur => ur.RoleId == mechanicRoleId).CountAsync();
                var busyMechanics = await _context.Appointments
                    .Where(a => a.AppointmentDate >= DateTime.Today && a.MechanicName != null)
                    .Select(a => a.MechanicName)
                    .Distinct()
                    .CountAsync();

                return ($"🔧 **Mechanic Overview**\n" +
                       $"• **Total Mechanics:** {totalMechanics}\n" +
                       $"• **Currently Scheduled:** {busyMechanics}\n" +
                       $"• **Utilization Rate:** {(totalMechanics > 0 ? (busyMechanics * 100 / totalMechanics) : 0)}%", "staff");
            }

            // APPOINTMENT MANAGEMENT QUERIES
            if (normalizedQuestion.Contains("today's appointments") || normalizedQuestion.Contains("appointments today"))
            {
                var todayAppointments = await _context.Appointments
                    .Where(a => a.AppointmentDate == DateTime.Today)
                    .Include(a => a.Car)
                    .OrderBy(a => a.AppointmentTime)
                    .ToListAsync();

                var result = $"📅 **Today's Schedule ({DateTime.Today:MMM dd, yyyy})**\n" +
                           $"• **Total Appointments:** {todayAppointments.Count}\n\n";

                if (todayAppointments.Any())
                {
                    var statusCounts = todayAppointments.GroupBy(a => a.Status).ToDictionary(g => g.Key, g => g.Count());
                    result += "**Status Breakdown:**\n";
                    foreach (var status in statusCounts)
                    {
                        var emoji = status.Key?.ToLower() switch
                        {
                            "completed" => "✅",
                            "pending" => "⏳",
                            "cancelled" => "❌",
                            "approved" => "👍",
                            _ => "📋"
                        };
                        result += $"{emoji} {status.Key}: {status.Value}\n";
                    }

                    result += "\n**Next 3 Appointments:**\n";
                    foreach (var appt in todayAppointments.Where(a => a.AppointmentTime > DateTime.Now.TimeOfDay).Take(3))
                    {
                        result += $"• **{appt.AppointmentTime:HH:mm}** - {appt.Car?.Make} {appt.Car?.Model} ({appt.MechanicName ?? "Unassigned"})\n";
                    }
                }

                return (result, "appointments");
            }

            if (normalizedQuestion.Contains("upcoming appointments") || normalizedQuestion.Contains("future appointments"))
            {
                var upcomingAppointments = await _context.Appointments
                    .Where(a => a.AppointmentDate >= DateTime.Today && a.Status != "Cancelled")
                    .ToListAsync();

                var nextWeekCount = upcomingAppointments.Count(a => a.AppointmentDate <= DateTime.Today.AddDays(7));
                var nextMonthCount = upcomingAppointments.Count(a => a.AppointmentDate <= DateTime.Today.AddDays(30));

                return ($"📈 **Upcoming Appointments**\n" +
                       $"• **Next 7 Days:** {nextWeekCount}\n" +
                       $"• **Next 30 Days:** {nextMonthCount}\n" +
                       $"• **Total Future:** {upcomingAppointments.Count}", "appointments");
            }

            // FINANCIAL QUERIES
            if (normalizedQuestion.Contains("revenue") || normalizedQuestion.Contains("earnings") || normalizedQuestion.Contains("income"))
            {
                var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);

                var thisMonthRevenue = await _context.MechanicReports
                    .Where(r => r.DateReported >= thisMonth)
                    .SumAsync(r => r.ServiceFee);

                var lastMonthRevenue = await _context.MechanicReports
                    .Where(r => r.DateReported >= lastMonth && r.DateReported < thisMonth)
                    .SumAsync(r => r.ServiceFee);

                var partsRevenue = await _context.MechanicReportParts
                    .Where(p => p.MechanicReport.DateReported >= thisMonth)
                    .SumAsync(p => p.PartPrice * p.Quantity);

                var totalThisMonth = thisMonthRevenue + partsRevenue;
                var growthRate = lastMonthRevenue > 0 ? ((totalThisMonth - lastMonthRevenue) / lastMonthRevenue * 100) : 0;

                return ($"💰 **Revenue Dashboard**\n" +
                       $"• **This Month:** RM {totalThisMonth:F2}\n" +
                       $"• **Service Fees:** RM {thisMonthRevenue:F2}\n" +
                       $"• **Parts Sales:** RM {partsRevenue:F2}\n" +
                       $"• **Growth Rate:** {growthRate:+0.0;-0.0;0.0}% vs last month", "financial");
            }

            // PERFORMANCE QUERIES
            if (normalizedQuestion.Contains("busiest mechanic") || normalizedQuestion.Contains("top mechanic"))
            {
                var mechanicStats = await _context.Appointments
                    .Where(a => a.MechanicName != null && a.AppointmentDate >= DateTime.Today.AddDays(-30))
                    .GroupBy(a => a.MechanicName)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                var result = "🏆 **Top Mechanics (Last 30 Days)**\n\n";
                var medals = new[] { "🥇", "🥈", "🥉", "🏅", "⭐" };

                for (int i = 0; i < mechanicStats.Count; i++)
                {
                    result += $"{medals[i]} **{mechanicStats[i].Name}**: {mechanicStats[i].Count} appointments\n";
                }

                return (result, "performance");
            }

            // FAULT MANAGEMENT QUERIES
            if (normalizedQuestion.Contains("pending faults") || normalizedQuestion.Contains("unresolved faults"))
            {
                var pendingFaults = await _context.Faults
                    .Where(f => !f.ResolutionStatus)
                    .Include(f => f.Car)
                    .OrderByDescending(f => f.DateReportedOn)
                    .ToListAsync();

                var critical = pendingFaults.Where(f => f.DateReportedOn <= DateTime.Today.AddDays(-7)).Count();
                var recent = pendingFaults.Where(f => f.DateReportedOn > DateTime.Today.AddDays(-7)).Count();

                var result = $"⚠️ **Pending Faults Analysis**\n" +
                           $"• **Total Pending:** {pendingFaults.Count}\n" +
                           $"• **Critical (>7 days):** {critical}\n" +
                           $"• **Recent (<7 days):** {recent}\n\n";

                if (pendingFaults.Any())
                {
                    result += "**Most Recent Issues:**\n";
                    foreach (var fault in pendingFaults.Take(3))
                    {
                        var daysOld = (DateTime.Today - fault.DateReportedOn.Date).Days;
                        result += $"• {fault.Car?.Make} {fault.Car?.Model}: {fault.Description} ({daysOld} days old)\n";
                    }
                }

                return (result, "faults");
            }
        }
        else if (userRole == "Customer")
        {
            // CUSTOMER-SPECIFIC QUERIES
            if (normalizedQuestion.Contains("my appointments") || normalizedQuestion.Contains("my booking"))
            {
                var appointments = await _context.Appointments
                    .Where(a => a.UserId == userId && a.AppointmentDate >= DateTime.Today)
                    .Include(a => a.Car)
                    .OrderBy(a => a.AppointmentDate)
                    .ThenBy(a => a.AppointmentTime)
                    .ToListAsync();

                var result = $"📅 **Your Upcoming Appointments**\n" +
                           $"• **Total Scheduled:** {appointments.Count}\n\n";

                if (appointments.Any())
                {
                    result += "**Next Appointments:**\n";
                    foreach (var appt in appointments.Take(3))
                    {
                        result += $"• **{appt.AppointmentDate:MMM dd}** at **{appt.AppointmentTime:HH:mm}**\n";
                        result += $"  {appt.Car?.Make} {appt.Car?.Model} - {appt.MechanicName ?? "Mechanic TBD"}\n";
                        result += $"  Status: {appt.Status}\n\n";
                    }
                }
                else
                {
                    result += "No upcoming appointments scheduled.\n";
                }

                return (result, "appointments");
            }

            if (normalizedQuestion.Contains("my cars") || normalizedQuestion.Contains("my vehicles"))
            {
                var cars = await _context.Cars
                    .Where(c => c.OwnerId == userId)
                    .Include(c => c.Reports)
                    .Include(c => c.Faults)
                    .ToListAsync();

                var result = $"🚗 **Your Registered Vehicles**\n" +
                           $"• **Total Cars:** {cars.Count}\n\n";

                foreach (var car in cars)
                {
                    var lastService = car.Reports.OrderByDescending(r => r.DateReported).FirstOrDefault();
                    var pendingFaults = car.Faults.Count(f => !f.ResolutionStatus);

                    result += $"**{car.Make} {car.Model} ({car.Year})**\n";
                    result += $"• License: {car.LicenseNumber}\n";
                    result += $"• Last Service: {(lastService != null ? lastService.DateReported.ToString("MMM dd, yyyy") : "No service history")}\n";
                    result += $"• Pending Issues: {pendingFaults}\n\n";
                }

                return (result, "vehicles");
            }
        }
        else if (userRole == "Mechanic")
        {
            // MECHANIC-SPECIFIC QUERIES - ENHANCED VERSION

            // 1. WORK SCHEDULE QUERIES
            if (normalizedQuestion.Contains("my schedule") || normalizedQuestion.Contains("my appointments") ||
                normalizedQuestion.Contains("work schedule") || normalizedQuestion.Contains("upcoming work"))
            {
                var mechanic = await _context.Users.FindAsync(userId);
                if (mechanic == null) return (string.Empty, "error");

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var endOfWeek = today.AddDays(7 - (int)today.DayOfWeek);

                // Get today's appointments
                var todayAppointments = await _context.Appointments
                    .Where(a => a.MechanicId == userId && a.AppointmentDate == today)
                    .Include(a => a.Car)
                    .ThenInclude(c => c.Owner)
                    .OrderBy(a => a.AppointmentTime)
                    .ToListAsync();

                // Get tomorrow's appointments
                var tomorrowAppointments = await _context.Appointments
                    .Where(a => a.MechanicId == userId && a.AppointmentDate == tomorrow)
                    .Include(a => a.Car)
                    .ThenInclude(c => c.Owner)
                    .OrderBy(a => a.AppointmentTime)
                    .ToListAsync();

                // Get this week's appointments
                var weekAppointments = await _context.Appointments
                    .Where(a => a.MechanicId == userId &&
                               a.AppointmentDate > tomorrow &&
                               a.AppointmentDate <= endOfWeek)
                    .Include(a => a.Car)
                    .ThenInclude(c => c.Owner)
                    .OrderBy(a => a.AppointmentDate)
                    .ThenBy(a => a.AppointmentTime)
                    .ToListAsync();

                var result = new StringBuilder();
                result.AppendLine("🔧 **Your Work Schedule**\n");

                // Today's schedule
                if (todayAppointments.Any())
                {
                    result.AppendLine("**Today** 📅");
                    foreach (var appt in todayAppointments)
                    {
                        var timeRange = $"{appt.AppointmentTime:HH:mm}";
                        var carInfo = $"{appt.Car?.Make ?? "Unknown"} {appt.Car?.Model ?? "Vehicle"}";
                        var licenseInfo = !string.IsNullOrEmpty(appt.Car?.LicenseNumber) ?
                            $"({appt.Car.LicenseNumber})" : "";
                        var customerInfo = appt.Car?.Owner?.FullName ?? "Customer";
                        var serviceNotes = !string.IsNullOrEmpty(appt.Notes) ? appt.Notes : "Standard service";

                        result.AppendLine($"• **{timeRange}** - {serviceNotes}");
                        result.AppendLine($"  Vehicle: **{carInfo}** {licenseInfo}");
                        result.AppendLine($"  Customer: {customerInfo}");
                        result.AppendLine($"  Status: {appt.Status}\n");
                    }
                }
                else
                {
                    result.AppendLine("**Today** 📅");
                    result.AppendLine("• No appointments scheduled\n");
                }

                // Tomorrow's schedule
                if (tomorrowAppointments.Any())
                {
                    result.AppendLine("**Tomorrow** 📅");
                    foreach (var appt in tomorrowAppointments)
                    {
                        var timeRange = $"{appt.AppointmentTime:HH:mm}";
                        var carInfo = $"{appt.Car?.Make ?? "Unknown"} {appt.Car?.Model ?? "Vehicle"}";
                        var licenseInfo = !string.IsNullOrEmpty(appt.Car?.LicenseNumber) ?
                            $"({appt.Car.LicenseNumber})" : "";
                        var serviceNotes = !string.IsNullOrEmpty(appt.Notes) ? appt.Notes : "Standard service";

                        result.AppendLine($"• **{timeRange}** - {serviceNotes}");
                        result.AppendLine($"  Vehicle: **{carInfo}** {licenseInfo}\n");
                    }
                }
                else
                {
                    result.AppendLine("**Tomorrow** 📅");
                    result.AppendLine("• No appointments scheduled\n");
                }

                // This week's schedule
                if (weekAppointments.Any())
                {
                    result.AppendLine("**This Week** 📆");
                    foreach (var appt in weekAppointments)
                    {
                        var dayName = appt.AppointmentDate.ToString("dddd");
                        var timeRange = $"{appt.AppointmentTime:HH:mm}";
                        var carInfo = $"{appt.Car?.Make ?? "Unknown"} {appt.Car?.Model ?? "Vehicle"}";
                        var serviceNotes = !string.IsNullOrEmpty(appt.Notes) ? appt.Notes : "Standard service";

                        result.AppendLine($"• **{dayName}** at **{timeRange}** - {serviceNotes}");
                        result.AppendLine($"  Vehicle: **{carInfo}**\n");
                    }
                }

                // Add pending tasks if any
                var pendingReports = await _context.Appointments
                    .Where(a => a.MechanicId == userId &&
                               a.AppointmentDate < today &&
                               a.Status == "Completed" &&
                               !_context.MechanicReports.Any(r => r.MechanicId == userId &&
                                                                 r.CarId == a.CarId &&
                                                                 r.DateReported.Date == a.AppointmentDate))
                    .Include(a => a.Car)
                    .Take(3)
                    .ToListAsync();

                if (pendingReports.Any())
                {
                    result.AppendLine("**Pending Tasks** ⏰");
                    foreach (var task in pendingReports)
                    {
                        var carInfo = $"{task.Car?.Make ?? "Unknown"} {task.Car?.Model ?? "Vehicle"}";
                        result.AppendLine($"• Complete service report for **{carInfo}** from {task.AppointmentDate:MMM dd}");
                    }
                }

                var totalUpcoming = todayAppointments.Count + tomorrowAppointments.Count + weekAppointments.Count;
                if (totalUpcoming == 0)
                {
                    result.AppendLine("**Status**: No upcoming appointments scheduled.");
                }
                else
                {
                    result.AppendLine($"**Total Upcoming**: {totalUpcoming} appointments");
                }

                return (result.ToString(), "schedule");
            }

            // 2. ASSIGNED CARS QUERIES
            if (normalizedQuestion.Contains("assigned cars") || normalizedQuestion.Contains("which cars") ||
                normalizedQuestion.Contains("my cars") || normalizedQuestion.Contains("cars assigned"))
            {
                var assignedCars = await _context.CarMechanicAssignments
                    .Where(a => a.MechanicId == userId)
                    .Include(a => a.Car)
                    .ThenInclude(c => c.Owner)
                    .Include(a => a.Car)
                    .ThenInclude(c => c.Faults)
                    .ToListAsync();

                var result = new StringBuilder();
                result.AppendLine($"🚗 **Your Assigned Vehicles**\n");
                result.AppendLine($"• **Total Assigned Cars:** {assignedCars.Count}\n");

                if (assignedCars.Any())
                {
                    result.AppendLine("**Vehicle Details:**");
                    foreach (var assignment in assignedCars)
                    {
                        var car = assignment.Car;
                        var pendingFaults = car.Faults?.Count(f => !f.ResolutionStatus) ?? 0;
                        var lastReport = await _context.MechanicReports
                            .Where(r => r.CarId == car.Id && r.MechanicId == userId)
                            .OrderByDescending(r => r.DateReported)
                            .FirstOrDefaultAsync();

                        result.AppendLine($"• **{car.Make} {car.Model} ({car.Year})**");
                        result.AppendLine($"  License: {car.LicenseNumber}");
                        result.AppendLine($"  Owner: {car.Owner?.FullName ?? "Unknown"}");
                        result.AppendLine($"  Assigned: {assignment.AssignedDate:MMM dd, yyyy}");
                        result.AppendLine($"  Pending Issues: {pendingFaults}");
                        result.AppendLine($"  Last Service: {(lastReport != null ? lastReport.DateReported.ToString("MMM dd, yyyy") : "No service yet")}\n");
                    }
                }
                else
                {
                    result.AppendLine("No cars currently assigned to you.");
                }

                return (result.ToString(), "vehicles");
            }

            // 3. REPORTS & EARNINGS QUERIES
            if (normalizedQuestion.Contains("my reports") || normalizedQuestion.Contains("completed work") ||
                normalizedQuestion.Contains("how many reports") || normalizedQuestion.Contains("reports completed"))
            {
                var firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                var allReports = await _context.MechanicReports
                    .Where(r => r.MechanicId == userId)
                    .Include(r => r.Car)
                    .OrderByDescending(r => r.DateReported)
                    .ToListAsync();

                var thisMonthReports = allReports.Where(r => r.DateReported >= firstDayOfMonth).ToList();
                var totalEarnings = allReports.Sum(r => r.ServiceFee);
                var thisMonthEarnings = thisMonthReports.Sum(r => r.ServiceFee);

                var result = new StringBuilder();
                result.AppendLine("📊 **Your Work Summary**\n");
                result.AppendLine($"**Total Reports Completed**: {allReports.Count}");
                result.AppendLine($"**This Month ({DateTime.Today:MMM yyyy})**: {thisMonthReports.Count} reports");
                result.AppendLine($"**Total Earnings**: RM {totalEarnings:F2}");
                result.AppendLine($"**This Month Earnings**: RM {thisMonthEarnings:F2}\n");

                if (allReports.Any())
                {
                    result.AppendLine("**Recent Completed Work:**");
                    foreach (var report in allReports.Take(5))
                    {
                        var carInfo = $"{report.Car?.Make ?? "Unknown"} {report.Car?.Model ?? "Vehicle"}";
                        result.AppendLine($"• **{report.DateReported:MMM dd}** - {carInfo}");
                        result.AppendLine($"  Service: {report.ServiceDetails ?? "Standard service"}");
                        result.AppendLine($"  Fee: RM {report.ServiceFee:F2}\n");
                    }
                }

                return (result.ToString(), "reports");
            }

            // 4. EARNINGS SPECIFIC QUERIES
            if (normalizedQuestion.Contains("earnings") || normalizedQuestion.Contains("total earnings") ||
                normalizedQuestion.Contains("monthly earnings") || normalizedQuestion.Contains("income"))
            {
                var firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);

                var thisMonthEarnings = await _context.MechanicReports
                    .Where(r => r.MechanicId == userId && r.DateReported >= firstDayOfMonth)
                    .SumAsync(r => r.ServiceFee);

                var lastMonthEarnings = await _context.MechanicReports
                    .Where(r => r.MechanicId == userId &&
                               r.DateReported >= firstDayOfLastMonth &&
                               r.DateReported < firstDayOfMonth)
                    .SumAsync(r => r.ServiceFee);

                var totalEarnings = await _context.MechanicReports
                    .Where(r => r.MechanicId == userId)
                    .SumAsync(r => r.ServiceFee);

                var thisMonthReportCount = await _context.MechanicReports
                    .Where(r => r.MechanicId == userId && r.DateReported >= firstDayOfMonth)
                    .CountAsync();

                var averagePerReport = thisMonthReportCount > 0 ? thisMonthEarnings / thisMonthReportCount : 0;
                var growthRate = lastMonthEarnings > 0 ? ((thisMonthEarnings - lastMonthEarnings) / lastMonthEarnings * 100) : 0;

                var result = $"💰 **Your Earnings Summary**\n" +
                           $"• **This Month**: RM {thisMonthEarnings:F2}\n" +
                           $"• **Last Month**: RM {lastMonthEarnings:F2}\n" +
                           $"• **Total Lifetime**: RM {totalEarnings:F2}\n" +
                           $"• **Reports This Month**: {thisMonthReportCount}\n" +
                           $"• **Average per Report**: RM {averagePerReport:F2}\n" +
                           $"• **Growth vs Last Month**: {growthRate:+0.0;-0.0;0.0}%";

                return (result, "earnings");
            }

            // 5. FAULTS QUERIES
            if (normalizedQuestion.Contains("pending faults") || normalizedQuestion.Contains("faults on my cars") ||
                normalizedQuestion.Contains("issues") || normalizedQuestion.Contains("problems"))
            {
                var assignedCarIds = await _context.CarMechanicAssignments
                    .Where(a => a.MechanicId == userId)
                    .Select(a => a.CarId)
                    .ToListAsync();

                var pendingFaults = await _context.Faults
                    .Where(f => assignedCarIds.Contains(f.CarId) && !f.ResolutionStatus)
                    .Include(f => f.Car)
                    .OrderByDescending(f => f.DateReportedOn)
                    .ToListAsync();

                var result = new StringBuilder();
                result.AppendLine("⚠️ **Pending Faults on Your Cars**\n");
                result.AppendLine($"• **Total Pending Issues**: {pendingFaults.Count}\n");

                if (pendingFaults.Any())
                {
                    var critical = pendingFaults.Where(f => f.DateReportedOn <= DateTime.Today.AddDays(-7)).Count();
                    var recent = pendingFaults.Where(f => f.DateReportedOn > DateTime.Today.AddDays(-7)).Count();

                    result.AppendLine($"• **Critical (>7 days old)**: {critical}");
                    result.AppendLine($"• **Recent (<7 days old)**: {recent}\n");

                    result.AppendLine("**Issue Details:**");
                    foreach (var fault in pendingFaults.Take(5))
                    {
                        var carInfo = $"{fault.Car?.Make ?? "Unknown"} {fault.Car?.Model ?? "Vehicle"}";
                        var daysOld = (DateTime.Today - fault.DateReportedOn.Date).Days;
                        var urgency = daysOld > 7 ? "🔴 URGENT" : "🟡";

                        result.AppendLine($"• {urgency} **{carInfo}** ({fault.Car?.LicenseNumber})");
                        result.AppendLine($"  Issue: {fault.Description}");
                        result.AppendLine($"  Reported: {fault.DateReportedOn:MMM dd, yyyy} ({daysOld} days ago)\n");
                    }

                    if (pendingFaults.Count > 5)
                    {
                        result.AppendLine($"...and {pendingFaults.Count - 5} more issues");
                    }
                }
                else
                {
                    result.AppendLine("✅ No pending faults on your assigned cars!");
                }

                return (result.ToString(), "faults");
            }

            // 6. NEXT APPOINTMENT QUERY
            if (normalizedQuestion.Contains("next appointment") || normalizedQuestion.Contains("upcoming appointment"))
            {
                var nextAppointment = await _context.Appointments
                    .Where(a => a.MechanicId == userId && a.AppointmentDate >= DateTime.Today)
                    .Include(a => a.Car)
                    .ThenInclude(c => c.Owner)
                    .OrderBy(a => a.AppointmentDate)
                    .ThenBy(a => a.AppointmentTime)
                    .FirstOrDefaultAsync();

                if (nextAppointment != null)
                {
                    var carInfo = $"{nextAppointment.Car?.Make ?? "Unknown"} {nextAppointment.Car?.Model ?? "Vehicle"}";
                    var customerInfo = nextAppointment.Car?.Owner?.FullName ?? "Customer";
                    var serviceNotes = !string.IsNullOrEmpty(nextAppointment.Notes) ? nextAppointment.Notes : "Standard service";
                    var timeUntil = nextAppointment.AppointmentDate.Subtract(DateTime.Today).Days;

                    var result = $"⏰ **Your Next Appointment**\n\n" +
                               $"• **Date**: {nextAppointment.AppointmentDate:dddd, MMM dd, yyyy}\n" +
                               $"• **Time**: {nextAppointment.AppointmentTime:HH:mm}\n" +
                               $"• **Vehicle**: {carInfo} ({nextAppointment.Car?.LicenseNumber})\n" +
                               $"• **Customer**: {customerInfo}\n" +
                               $"• **Service**: {serviceNotes}\n" +
                               $"• **Status**: {nextAppointment.Status}\n" +
                               $"• **In**: {(timeUntil == 0 ? "Today!" : $"{timeUntil} day(s)")}";

                    return (result, "appointments");
                }
                else
                {
                    return ("📅 **No upcoming appointments scheduled.**\n\nYou're all caught up! Check back later for new assignments.", "appointments");
                }
            }
        }
        return (string.Empty, "general");
    }

    private async Task<string> GetAIResponse(string question, string userRole, string userId)
    {
        string context = await RetrieveContextBasedOnQuery(question, userRole, userId);

        var client = new RestClient();
        var request = new RestRequest(_apiUrl, Method.Post);
        request.AddHeader("Authorization", $"Bearer {_apiKey}");
        request.AddHeader("Content-Type", "application/json");

        string systemPrompt = GetRoleSpecificSystemPrompt(userRole, context);

        var chatRequest = new
        {
            model = "llama-3.3-70b-versatile",
            max_tokens = 250,
            messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = question }
            }
        };

        request.AddJsonBody(chatRequest);

        try
        {
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                var jsonResponse = JsonConvert.DeserializeObject<ChatCompletionResponse>(response.Content);
                return jsonResponse?.choices?.FirstOrDefault()?.message?.content ?? "I'm sorry, I couldn't process your request.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI API request failed");
        }

        return "I'm sorry, I'm having trouble processing your request right now. Please try again later.";
    }

    private async Task<List<PromptSuggestion>> GenerateContextualPrompts(string lastQuestion, string userRole, string userId)
    {
        var prompts = new List<PromptSuggestion>();
        var questionContext = DetermineQuestionContext(lastQuestion);

        if (userRole == "Admin")
        {
            switch (questionContext)
            {
                case "vehicles":
                    prompts.AddRange(new[]
                    {
                        new PromptSuggestion { Text = "Which cars have pending faults?", Category = "Vehicles", Icon = "⚠️" },
                        new PromptSuggestion { Text = "Show me cars without recent service", Category = "Vehicles", Icon = "🔍" },
                        new PromptSuggestion { Text = "How many cars are assigned to mechanics?", Category = "Vehicles", Icon = "🔧" },
                        new PromptSuggestion { Text = "What's the average age of vehicles?", Category = "Vehicles", Icon = "📊" }
                    });
                    break;

                case "appointments":
                    prompts.AddRange(new[]
                    {
                        new PromptSuggestion { Text = "Which mechanic has the most appointments this week?", Category = "Schedule", Icon = "🏆" },
                        new PromptSuggestion { Text = "How many appointments are scheduled for tomorrow?", Category = "Schedule", Icon = "📅" },
                        new PromptSuggestion { Text = "Show me cancelled appointments this month", Category = "Schedule", Icon = "❌" },
                        new PromptSuggestion { Text = "What's our appointment completion rate?", Category = "Schedule", Icon = "✅" }
                    });
                    break;

                case "financial":
                    prompts.AddRange(new[]
                    {
                        new PromptSuggestion { Text = "What's our revenue growth this quarter?", Category = "Finance", Icon = "📈" },
                        new PromptSuggestion { Text = "Which services generate the most income?", Category = "Finance", Icon = "💎" },
                        new PromptSuggestion { Text = "Show me parts vs service revenue breakdown", Category = "Finance", Icon = "📊" },
                        new PromptSuggestion { Text = "What's the average transaction value?", Category = "Finance", Icon = "💰" }
                    });
                    break;

                case "staff":
                    prompts.AddRange(new[]
                    {
                        new PromptSuggestion { Text = "Which mechanic completed the most reports?", Category = "Staff", Icon = "📋" },
                        new PromptSuggestion { Text = "Show me mechanic workload distribution", Category = "Staff", Icon = "⚖️" },
                        new PromptSuggestion { Text = "How many faults were resolved this week?", Category = "Staff", Icon = "🔧" },
                        new PromptSuggestion { Text = "What's our staff utilization rate?", Category = "Staff", Icon = "📊" }
                    });
                    break;

                default:
                    // General admin prompts
                    prompts.AddRange(new[]
                    {
                        new PromptSuggestion { Text = "Show me today's business overview", Category = "Overview", Icon = "📊" },
                        new PromptSuggestion { Text = "How many pending faults need attention?", Category = "Operations", Icon = "⚠️" },
                        new PromptSuggestion { Text = "What's our busiest time of day?", Category = "Analytics", Icon = "⏰" },
                        new PromptSuggestion { Text = "Show me this week's revenue summary", Category = "Finance", Icon = "💰" },
                        new PromptSuggestion { Text = "Which customers have the most service history?", Category = "Customers", Icon = "👥" },
                        new PromptSuggestion { Text = "How many cars need immediate service?", Category = "Vehicles", Icon = "🚗" }
                    });
                    break;
            }
        }
        else if (userRole == "Customer")
        {
            prompts.AddRange(new[]
            {
                new PromptSuggestion { Text = "Show me my upcoming appointments", Category = "Appointments", Icon = "📅" },
                new PromptSuggestion { Text = "What's my service history?", Category = "History", Icon = "📋" },
                new PromptSuggestion { Text = "Tell me about my vehicles", Category = "Vehicles", Icon = "🚗" },
                new PromptSuggestion { Text = "How can I book a new appointment?", Category = "Booking", Icon = "📞" },
                new PromptSuggestion { Text = "What services do you offer?", Category = "Services", Icon = "🔧" },
                new PromptSuggestion { Text = "Do any of my cars have pending issues?", Category = "Issues", Icon = "⚠️" }
            });
        }
        else if (userRole == "Mechanic")
        {
            prompts.AddRange(new[]
            {
                new PromptSuggestion { Text = "Show me my work schedule", Category = "Schedule", Icon = "📅" },
                new PromptSuggestion { Text = "Which cars are assigned to me?", Category = "Assignments", Icon = "🚗" },
                new PromptSuggestion { Text = "How many reports have I completed?", Category = "Reports", Icon = "📊" },
                new PromptSuggestion { Text = "What's my next appointment?", Category = "Schedule", Icon = "⏰" },
                new PromptSuggestion { Text = "Show me pending faults on my cars", Category = "Issues", Icon = "⚠️" },
                new PromptSuggestion { Text = "What's my total earnings this month?", Category = "Earnings", Icon = "💰" }
            });
        }

        // Shuffle prompts to show variety
        var random = new Random();
        return prompts.OrderBy(x => random.Next()).ToList();
    }

    private string DetermineQuestionContext(string question)
    {
        if (string.IsNullOrEmpty(question)) return "general";

        var lowerQuestion = question.ToLower();

        if (lowerQuestion.Contains("car") || lowerQuestion.Contains("vehicle") || lowerQuestion.Contains("fleet"))
            return "vehicles";
        if (lowerQuestion.Contains("appointment") || lowerQuestion.Contains("schedule") || lowerQuestion.Contains("booking"))
            return "appointments";
        if (lowerQuestion.Contains("revenue") || lowerQuestion.Contains("earning") || lowerQuestion.Contains("income") || lowerQuestion.Contains("money"))
            return "financial";
        if (lowerQuestion.Contains("mechanic") || lowerQuestion.Contains("staff") || lowerQuestion.Contains("employee"))
            return "staff";
        if (lowerQuestion.Contains("customer") || lowerQuestion.Contains("client"))
            return "customers";
        if (lowerQuestion.Contains("fault") || lowerQuestion.Contains("issue") || lowerQuestion.Contains("problem"))
            return "faults";
        if (lowerQuestion.Contains("report") || lowerQuestion.Contains("service"))
            return "reports";

        return "general";
    }

    private async Task<QuickStatsModel> GetQuickStats(string userRole, string userId)
    {
        if (userRole == "Admin")
        {
            var today = DateTime.Today;
            var thisWeek = today.AddDays(-(int)today.DayOfWeek);
            var thisMonth = new DateTime(today.Year, today.Month, 1);

            var todayAppointments = await _context.Appointments
                .CountAsync(a => a.AppointmentDate == today && a.Status != "Cancelled");

            var pendingFaults = await _context.Faults
                .CountAsync(f => !f.ResolutionStatus);

            var weeklyRevenue = await _context.MechanicReports
                .Where(r => r.DateReported >= thisWeek)
                .SumAsync(r => r.ServiceFee);

            var activeCustomers = await _context.Users
                .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                    ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Customer").Id) &&
                    _context.Appointments.Any(a => a.UserId == u.Id && a.AppointmentDate >= thisMonth))
                .CountAsync();

            var totalCars = await _context.Cars.CountAsync();
            var completedAppointments = await _context.Appointments
                .CountAsync(a => a.AppointmentDate >= thisWeek && a.Status == "Completed");

            return new QuickStatsModel
            {
                UserRole = userRole,
                Stats = new List<StatItem>
                {
                    new StatItem { Label = "Today's Appointments", Value = todayAppointments.ToString(), Icon = "📅", Color = "primary" },
                    new StatItem { Label = "Pending Faults", Value = pendingFaults.ToString(), Icon = "⚠️", Color = pendingFaults > 0 ? "warning" : "success" },
                    new StatItem { Label = "Weekly Revenue", Value = $"RM {weeklyRevenue:F0}", Icon = "💰", Color = "success" },
                    new StatItem { Label = "Active Customers", Value = activeCustomers.ToString(), Icon = "👥", Color = "info" },
                    new StatItem { Label = "Total Vehicles", Value = totalCars.ToString(), Icon = "🚗", Color = "secondary" },
                    new StatItem { Label = "Completed This Week", Value = completedAppointments.ToString(), Icon = "✅", Color = "success" }
                }
            };
        }
        else if (userRole == "Customer")
        {
            if (!int.TryParse(userId, out int userIdInt)) return new QuickStatsModel();

            var upcomingAppointments = await _context.Appointments
                .CountAsync(a => a.UserId == userIdInt && a.AppointmentDate >= DateTime.Today && a.Status != "Cancelled");

            var totalCars = await _context.Cars
                .CountAsync(c => c.OwnerId == userIdInt);

            var lastServiceDate = await _context.MechanicReports
                .Where(r => _context.Cars.Any(c => c.Id == r.CarId && c.OwnerId == userIdInt))
                .OrderByDescending(r => r.DateReported)
                .Select(r => r.DateReported)
                .FirstOrDefaultAsync();

            var pendingIssues = await _context.Faults
                .Where(f => !f.ResolutionStatus && _context.Cars.Any(c => c.Id == f.CarId && c.OwnerId == userIdInt))
                .CountAsync();

            return new QuickStatsModel
            {
                UserRole = userRole,
                Stats = new List<StatItem>
                {
                    new StatItem { Label = "Upcoming Appointments", Value = upcomingAppointments.ToString(), Icon = "📅", Color = "primary" },
                    new StatItem { Label = "Your Vehicles", Value = totalCars.ToString(), Icon = "🚗", Color = "info" },
                    new StatItem { Label = "Last Service", Value = lastServiceDate != default ? lastServiceDate.ToString("MMM dd") : "None", Icon = "🔧", Color = "secondary" },
                    new StatItem { Label = "Pending Issues", Value = pendingIssues.ToString(), Icon = "⚠️", Color = pendingIssues > 0 ? "warning" : "success" }
                }
            };
        }
        else if (userRole == "Mechanic")
        {
            if (!int.TryParse(userId, out int userIdInt)) return new QuickStatsModel();

            var upcomingWork = await _context.Appointments
                .CountAsync(a => a.MechanicId == userIdInt && a.AppointmentDate >= DateTime.Today);

            var completedReports = await _context.MechanicReports
                .CountAsync(r => r.MechanicId == userIdInt && r.DateReported >= DateTime.Today.AddDays(-30));

            var assignedCars = await _context.CarMechanicAssignments
                .CountAsync(a => a.MechanicId == userIdInt);

            var firstDayOfCurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            var monthlyEarnings = await _context.MechanicReports
                .Where(r => r.MechanicId == userIdInt && r.DateReported >= firstDayOfCurrentMonth)
                .SumAsync(r => r.ServiceFee);

            return new QuickStatsModel
            {
                UserRole = userRole,
                Stats = new List<StatItem>
                {
                    new StatItem { Label = "Upcoming Work", Value = upcomingWork.ToString(), Icon = "📋", Color = "primary" },
                    new StatItem { Label = "Reports (30 days)", Value = completedReports.ToString(), Icon = "📊", Color = "success" },
                    new StatItem { Label = "Assigned Cars", Value = assignedCars.ToString(), Icon = "🚗", Color = "info" },
                    new StatItem { Label = "Monthly Earnings", Value = $"RM {monthlyEarnings:F0}", Icon = "💰", Color = "success" }
                }
            };
        }

        return new QuickStatsModel();
    }

    private List<RelatedAction> GetRelatedActions(string question, string userRole)
    {
        var actions = new List<RelatedAction>();
        var questionContext = DetermineQuestionContext(question);

        if (userRole == "Admin")
        {
            switch (questionContext)
            {
                case "vehicles":
                    actions.AddRange(new[]
                    {
                        new RelatedAction { Text = "View Car List", Url = "/Admin/CarList", Icon = "🚗" },
                        new RelatedAction { Text = "Assign Cars to Mechanics", Url = "/Admin/AssignCars", Icon = "🔧" },
                        new RelatedAction { Text = "View Car Reports", Url = "/Admin/CarReports", Icon = "📋" }
                    });
                    break;
                case "appointments":
                    actions.AddRange(new[]
                    {
                        new RelatedAction { Text = "Manage Appointments", Url = "/Admin/Appointments", Icon = "📅" },
                        new RelatedAction { Text = "View Calendar", Url = "/Admin/Calendar", Icon = "📆" },
                        new RelatedAction { Text = "Appointment Analytics", Url = "/Admin/Analytics", Icon = "📊" }
                    });
                    break;
                case "staff":
                    actions.AddRange(new[]
                    {
                        new RelatedAction { Text = "Mechanic Info", Url = "/Admin/MechanicInfo", Icon = "👨‍🔧" },
                        new RelatedAction { Text = "Staff Performance", Url = "/Admin/Performance", Icon = "📈" },
                        new RelatedAction { Text = "Assign Work", Url = "/Admin/AssignCars", Icon = "📋" }
                    });
                    break;
                case "financial":
                    actions.AddRange(new[]
                    {
                        new RelatedAction { Text = "Revenue Dashboard", Url = "/Admin/Dashboard", Icon = "💰" },
                        new RelatedAction { Text = "Parts Management", Url = "/Admin/PartsManagement", Icon = "🔧" },
                        new RelatedAction { Text = "Financial Reports", Url = "/Admin/Reports", Icon = "📊" }
                    });
                    break;
                default:
                    actions.AddRange(new[]
                    {
                        new RelatedAction { Text = "Dashboard Overview", Url = "/Admin/Dashboard", Icon = "🏠" },
                        new RelatedAction { Text = "Customer Management", Url = "/Admin/CustomerList", Icon = "👥" },
                        new RelatedAction { Text = "System Settings", Url = "/Admin/Settings", Icon = "⚙️" }
                    });
                    break;
            }
        }
        else if (userRole == "Customer")
        {
            actions.AddRange(new[]
            {
                new RelatedAction { Text = "Book Appointment", Url = "/Customer/BookAppointment", Icon = "📅" },
                new RelatedAction { Text = "My Vehicles", Url = "/Customer/MyCars", Icon = "🚗" },
                new RelatedAction { Text = "Service History", Url = "/Customer/ServiceHistory", Icon = "📋" },
                new RelatedAction { Text = "My Profile", Url = "/Customer/Profile", Icon = "👤" }
            });
        }
        else if (userRole == "Mechanic")
        {
            actions.AddRange(new[]
            {
                new RelatedAction { Text = "My Schedule", Url = "/Mechanic/Schedule", Icon = "📅" },
                new RelatedAction { Text = "Create Report", Url = "/Mechanic/CreateReport", Icon = "📝" },
                new RelatedAction { Text = "My Reports", Url = "/Mechanic/Reports", Icon = "📊" },
                new RelatedAction { Text = "Assigned Cars", Url = "/Mechanic/AssignedCars", Icon = "🚗" }
            });
        }

        return actions.Take(4).ToList(); // Limit to 4 actions
    }

    private async Task<string> RetrieveContextBasedOnQuery(string question, string userRole, string userIdStr)
    {
        if (!int.TryParse(userIdStr, out int userId))
        {
            _logger.LogWarning("Failed to parse userId: {UserIdStr}", userIdStr);
            return "No context available.";
        }

        StringBuilder contextBuilder = new StringBuilder();
        var normalizedQuestion = question.ToLower();

        if (userRole == "Admin")
        {
            // System statistics for context
            var totalCars = await _context.Cars.CountAsync();
            var totalCustomers = await _context.Users
                .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                           ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Customer").Id))
                .CountAsync();
            var totalAppointments = await _context.Appointments.CountAsync();
            var pendingFaults = await _context.Faults.CountAsync(f => !f.ResolutionStatus);

            contextBuilder.AppendLine($"System Overview: {totalCars} cars, {totalCustomers} customers, {totalAppointments} total appointments, {pendingFaults} pending faults");

            if (normalizedQuestion.Contains("revenue") || normalizedQuestion.Contains("financial"))
            {
                var thisMonthRevenue = await _context.MechanicReports
                    .Where(r => r.DateReported >= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))
                    .SumAsync(r => r.ServiceFee);
                contextBuilder.AppendLine($"This month's service revenue: RM {thisMonthRevenue:F2}");
            }

            if (normalizedQuestion.Contains("appointment") || normalizedQuestion.Contains("schedule"))
            {
                var todayAppointments = await _context.Appointments
                    .Where(a => a.AppointmentDate == DateTime.Today)
                    .Include(a => a.Car)
                    .OrderBy(a => a.AppointmentTime)
                    .Take(5)
                    .ToListAsync();

                if (todayAppointments.Any())
                {
                    contextBuilder.AppendLine("Today's appointments:");
                    foreach (var appt in todayAppointments)
                    {
                        contextBuilder.AppendLine($"- {appt.AppointmentTime:HH:mm}: {appt.Car?.Make} {appt.Car?.Model} ({appt.MechanicName ?? "Unassigned"})");
                    }
                }
            }
        }
        else if (userRole == "Customer")
        {
            var upcomingAppointments = await _context.Appointments
                .Where(a => a.UserId == userId && a.AppointmentDate >= DateTime.Today)
                .Include(a => a.Car)
                .OrderBy(a => a.AppointmentDate)
                .Take(3)
                .ToListAsync();

            if (upcomingAppointments.Any())
            {
                contextBuilder.AppendLine("Your upcoming appointments:");
                foreach (var appt in upcomingAppointments)
                {
                    contextBuilder.AppendLine($"- {appt.AppointmentDate:MMM dd} at {appt.AppointmentTime:HH:mm}: {appt.Car?.Make} {appt.Car?.Model}");
                }
            }

            var userCars = await _context.Cars
                .Where(c => c.OwnerId == userId)
                .ToListAsync();

            if (userCars.Any())
            {
                contextBuilder.AppendLine($"Your registered vehicles: {string.Join(", ", userCars.Select(c => $"{c.Make} {c.Model}"))}");
            }
        }
        else if (userRole == "Mechanic")
        {
            var upcomingWork = await _context.Appointments
                .Where(a => a.MechanicId == userId && a.AppointmentDate >= DateTime.Today)
                .Include(a => a.Car)
                .OrderBy(a => a.AppointmentDate)
                .Take(3)
                .ToListAsync();

            if (upcomingWork.Any())
            {
                contextBuilder.AppendLine("Your upcoming work:");
                foreach (var work in upcomingWork)
                {
                    contextBuilder.AppendLine($"- {work.AppointmentDate:MMM dd} at {work.AppointmentTime:HH:mm}: {work.Car?.Make} {work.Car?.Model}");
                }
            }
        }

        return contextBuilder.ToString();
    }

    private string GetRoleSpecificSystemPrompt(string userRole, string context)
    {
        string basePrompt = $@"You are an AI assistant for a garage management system. The user is a {userRole}.
        
Current system context:
{context}

Provide helpful, accurate, and concise responses. Use emojis appropriately and format important information clearly.";

        return userRole switch
        {
            "Admin" => basePrompt + "\nFor admins: Focus on business insights, operational efficiency, and actionable recommendations. Use metrics and data to support your responses.",
            "Customer" => basePrompt + "\nFor customers: Be friendly and helpful. Focus on their vehicles, appointments, and services. Explain technical terms simply.",
            "Mechanic" => basePrompt + "\nFor mechanics: Be direct and practical. Focus on work schedules, technical details, and assigned tasks. Use automotive terminology appropriately.",
            _ => basePrompt + "\nProvide general garage service information in a helpful manner."
        };
    }

    string ExtractMechanicNameFromQuestion(string question)
    {
        var match = Regex.Match(question, @"mechanic\s+([a-zA-Z\s]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}

// Response Models
public class ChatbotRequest
{
    public string Question { get; set; } = string.Empty;
}

public class ChatCompletionResponse
{
    public List<Choice> choices { get; set; } = new();
}

public class Choice
{
    public Message message { get; set; } = new();
}

public class Message
{
    public string role { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

public class PromptSuggestion
{
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = "primary";
}

public class QuickStatsModel
{
    public string UserRole { get; set; } = string.Empty;
    public List<StatItem> Stats { get; set; } = new();
}

public class StatItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = "primary";
}

public class RelatedAction
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}