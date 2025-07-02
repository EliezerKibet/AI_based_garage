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
using System.Linq;
using Microsoft.EntityFrameworkCore;


namespace GarageManagementSystem.Controllers
{
    [Authorize]
    public class CustomerController : BaseCustomerController
    {

        private readonly PhoneCallService _phoneCallService;
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerController> _logger;
        private readonly IAlertService _alertService;
        private readonly NotificationService _notificationService;
        public DataTable MyTable { get; set; } = new();

        public CustomerController(UserManager<Users> userManager, 
            AppDbContext context,
            ILogger<CustomerController> logger,
            PhoneCallService phoneCallService, 
            IAlertService alertService,
            NotificationService notificationService)
            : base(context, userManager, logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _phoneCallService = phoneCallService;
            _alertService = alertService;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> CustomerSearch(string query, string userId = null)
        {
            // Handle empty search query
            if (string.IsNullOrWhiteSpace(query))
            {
                return View(new CustomerSearchViewModel { Query = query });
            }

            // Get the current logged-in user ID if not explicitly provided
            if (string.IsNullOrEmpty(userId))
            {
                userId = _userManager.GetUserId(User);
            }

            // Normalize query
            query = query?.ToLower() ?? string.Empty;

            try
            {
                // Get car IDs owned by this user (for filtering related entities)
                var userCarIds = await _context.Cars
                    .Where(c => c.OwnerId.ToString() == userId)
                    .Select(c => c.Id)
                    .ToListAsync();

                if (userCarIds.Count == 0)
                {
                    // No cars found for this user, return empty results
                    return View(new CustomerSearchViewModel
                    {
                        Query = query,
                        MatchingCars = new List<Car>(),
                        MatchingMechanics = new List<Users>(),
                        MatchingFaults = new List<Fault>(),
                        MatchingReports = new List<MechanicReport>(),
                        MatchingAppointments = new List<Appointment>()
                    });
                }

                // Get only the user's cars that match the query - FULLY NULL-SAFE VERSION
                var cars = await _context.Cars
                    .Where(c => c.OwnerId.ToString() == userId)
                    .Where(c =>
                        (c.Make != null && EF.Functions.Like(c.Make, $"%{query}%")) ||
                        (c.Model != null && EF.Functions.Like(c.Model, $"%{query}%")) ||
                        (c.LicenseNumber != null && EF.Functions.Like(c.LicenseNumber, $"%{query}%")) ||
                        (c.Color != null && EF.Functions.Like(c.Color, $"%{query}%")) ||
                        (c.Year.ToString().Contains(query)) ||
                        (c.ChassisNumber != null && EF.Functions.Like(c.ChassisNumber, $"%{query}%"))
                    )
                    .Include(c => c.Owner)
                    .Take(50)
                    .ToListAsync();

                // Get only mechanics who have worked on the user's cars
                var mechanicIds = await _context.CarMechanicAssignments
                    .Where(r => userCarIds.Contains(r.CarId))
                    .Select(r => r.MechanicId)
                    .Distinct()
                    .ToListAsync();

                var mechanics = new List<Users>();
                if (mechanicIds.Any())
                {
                    mechanics = await _userManager.Users
                        .Where(u => mechanicIds.Contains(u.Id))
                        .Where(u =>
                            (u.FullName != null && EF.Functions.Like(u.FullName, $"%{query}%")) ||
                            (u.Email != null && EF.Functions.Like(u.Email, $"%{query}%")) ||
                            (u.PhoneNumber != null && EF.Functions.Like(u.PhoneNumber, $"%{query}%"))
                        )
                        .Take(20)
                        .ToListAsync();
                }

                // Get only faults related to user's cars
                var faults = await _context.Faults
                    .Where(f => userCarIds.Contains(f.CarId))
                    .Where(f =>
                        (f.Description != null && EF.Functions.Like(f.Description, $"%{query}%"))
                    )
                    .Include(f => f.Car)
                    .OrderByDescending(f => f.DateReportedOn)
                    .Take(50)
                    .ToListAsync();

                // Also check for faults with matching car properties
                var carMatchingFaults = await _context.Faults
                    .Where(f => userCarIds.Contains(f.CarId))
                    .Include(f => f.Car)
                    .Where(f =>
                        (f.Car.Make != null && EF.Functions.Like(f.Car.Make, $"%{query}%")) ||
                        (f.Car.Model != null && EF.Functions.Like(f.Car.Model, $"%{query}%")) ||
                        (f.Car.LicenseNumber != null && EF.Functions.Like(f.Car.LicenseNumber, $"%{query}%"))
                    )
                    .OrderByDescending(f => f.DateReportedOn)
                    .Take(50)
                    .ToListAsync();

                // Combine the fault results (distinct by ID)
                var faultIds = faults.Select(f => f.Id).ToHashSet();
                foreach (var fault in carMatchingFaults)
                {
                    if (!faultIds.Contains(fault.Id))
                    {
                        faults.Add(fault);
                        faultIds.Add(fault.Id);
                    }
                }

                // Get only reports related to user's cars - using null-safe queries
                var reports = new List<MechanicReport>();
                try
                {
                    // First get reports that match basic criteria
                    reports = await _context.MechanicReports
                        .Where(r => userCarIds.Contains(r.CarId))
                        .Where(r =>
                            (r.ServiceDetails != null && EF.Functions.Like(r.ServiceDetails, $"%{query}%")) ||
                            (r.AdditionalNotes != null && EF.Functions.Like(r.AdditionalNotes, $"%{query}%"))
                        )
                        .Include(r => r.Car)
                        .Include(r => r.Mechanic)
                        .OrderByDescending(r => r.DateReported)
                        .Take(50)
                        .ToListAsync();

                    // Also check for reports with matching mechanics
                    var mechanicMatchingReports = await _context.MechanicReports
                        .Where(r => userCarIds.Contains(r.CarId))
                        .Where(r => r.Mechanic != null && r.Mechanic.FullName != null &&
                               EF.Functions.Like(r.Mechanic.FullName, $"%{query}%"))
                        .Include(r => r.Car)
                        .Include(r => r.Mechanic)
                        .OrderByDescending(r => r.DateReported)
                        .Take(20)
                        .ToListAsync();

                    // Combine the results (distinct by ID)
                    var reportIds = reports.Select(r => r.Id).ToHashSet();
                    foreach (var report in mechanicMatchingReports)
                    {
                        if (!reportIds.Contains(report.Id))
                        {
                            reports.Add(report);
                            reportIds.Add(report.Id);
                        }
                    }

                    // Now load the parts for all reports
                    if (reports.Any())
                    {
                        var allReportIds = reports.Select(r => r.Id).ToList();
                        var parts = await _context.MechanicReportParts
                            .Where(p => allReportIds.Contains(p.MechanicReportId))
                            .ToListAsync();

                        // Assign parts to reports
                        foreach (var report in reports)
                        {
                            report.Parts = parts
                                .Where(p => p.MechanicReportId == report.Id)
                                .ToList();
                        }
                    }

                    // Now find reports with matching parts in a completely separate query
                    if (!string.IsNullOrEmpty(query))
                    {
                        var partMatchingReportIds = await _context.MechanicReportParts
                            .Where(p =>
                                (p.PartName != null && EF.Functions.Like(p.PartName, $"%{query}%")) ||
                                p.PartPrice.ToString().Contains(query) ||
                                p.Quantity.ToString().Contains(query)
                            )
                            .Select(p => p.MechanicReportId)
                            .Distinct()
                            .ToListAsync();

                        if (partMatchingReportIds.Any())
                        {
                            // Filter to only include user car reports and exclude reports we already have
                            var existingReportIds = reports.Select(r => r.Id).ToHashSet();
                            var partReports = await _context.MechanicReports
                                .Where(r => userCarIds.Contains(r.CarId) &&
                                        partMatchingReportIds.Contains(r.Id) &&
                                        !existingReportIds.Contains(r.Id))
                                .Include(r => r.Car)
                                .Include(r => r.Mechanic)
                                .OrderByDescending(r => r.DateReported)
                                .Take(20)
                                .ToListAsync();

                            if (partReports.Any())
                            {
                                // Load parts for these reports
                                var partReportIds = partReports.Select(r => r.Id).ToList();
                                var partReportParts = await _context.MechanicReportParts
                                    .Where(p => partReportIds.Contains(p.MechanicReportId))
                                    .ToListAsync();

                                // Assign parts
                                foreach (var report in partReports)
                                {
                                    report.Parts = partReportParts
                                        .Where(p => p.MechanicReportId == report.Id)
                                        .ToList();
                                }

                                reports.AddRange(partReports);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error with reports query: {ex.Message}");
                    reports = new List<MechanicReport>();
                }

                // Fix for CS1503: Argument 1: cannot convert from 'int?' to 'int'
                // Update the code to handle nullable integers by using the Value property or null-coalescing operator.

                var appointments = await _context.Appointments
                    .Where(a => a.CarId.HasValue && userCarIds.Contains(a.CarId.Value))
                    .Include(a => a.Car)
                    .Select(a => new Appointment
                    {
                        Id = a.Id,
                        CarId = a.CarId,
                        Car = a.Car,
                        AppointmentDate = a.AppointmentDate,
                        AppointmentTime = a.AppointmentTime,
                        Notes = a.Notes,
                        Status = a.Status,
                        MechanicId = a.MechanicId,
                        MechanicName = a.MechanicName,
                        UserId = a.UserId
                    })
                    .ToListAsync();

                var viewModel = new CustomerSearchViewModel
                {
                    Query = query,
                    MatchingCars = cars,
                    MatchingMechanics = mechanics,
                    MatchingFaults = faults,
                    MatchingReports = reports,
                    MatchingAppointments = appointments
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error in customer search: {ex.Message}");

                // Return a simple error message
                return Content($"Search error: {ex.Message}");
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetAllAppointments()
        {
            // Retrieve the currently logged-in user
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Fetch car IDs owned by the current user
            var userCarIds = await _context.Cars
                .Where(c => c.OwnerId == user.Id)
                .Select(c => c.Id)
                .ToListAsync();

            // Fix for CS1503: Argument 1: cannot convert from 'int?' to 'int'
            // Update the code to handle nullable integers by using the Value property or null-coalescing operator.

            var appointments = await _context.Appointments
                .Where(a => userCarIds.Contains(a.CarId ?? 0)) // Use null-coalescing operator to handle nullable CarId
                .Include(a => a.Car)
                .Select(a => new
                {
                    id = a.Id,
                    title = $"{a.Car.Make} {a.Car.Model}",
                    start = a.AppointmentDate.ToString("yyyy-MM-dd") + "T" + a.AppointmentTime.ToString(@"hh\:mm"),
                    description = $"Notes: {a.Notes}"
                })
                .ToListAsync();

            return Json(appointments);
        }


        [HttpGet]
        public async Task<IActionResult> EditAppointment(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            // Get the appointment and ensure it belongs to the current user
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("BookAppointment");
            }

            var model = new AppointmentViewModel
            {
                Id = appointment.Id,
                CarId = appointment.CarId,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                Notes = appointment.Notes,
                // Load available cars for the dropdown
                Cars = await _context.Cars
                            .Where(c => c.OwnerId == Convert.ToInt32(user.Id))
                            .Select(c => new CarViewModel
                            {
                                Id = c.Id,
                                Make = c.Make,
                                Model = c.Model,
                                LicenseNumber = c.LicenseNumber
                            }).ToListAsync()
            };

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> EditAppointment(AppointmentViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (!ModelState.IsValid)
            {
                // Reload the cars list if validation fails
                model.Cars = await _context.Cars
                                .Where(c => c.OwnerId == Convert.ToInt32(user.Id))
                                .Select(c => new CarViewModel
                                {
                                    Id = c.Id,
                                    Make = c.Make,
                                    Model = c.Model,
                                    LicenseNumber = c.LicenseNumber
                                }).ToListAsync();
                return View(model);
            }

            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == model.Id && a.UserId == Convert.ToInt32(user.Id));
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("BookAppointment");
            }

            // Optionally: only allow edit if status is still "Pending"
            if (appointment.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending appointments can be edited.";
                return RedirectToAction("BookAppointment");
            }

            // Convert the appointment time string to TimeSpan
            if (!TimeSpan.TryParse(model.AppointmentTime, out TimeSpan timeSpan))
            {
                ModelState.AddModelError("AppointmentTime", "Invalid time format.");
                model.Cars = await _context.Cars
                                .Where(c => c.OwnerId == Convert.ToInt32(user.Id))
                                .Select(c => new CarViewModel
                                {
                                    Id = c.Id,
                                    Make = c.Make,
                                    Model = c.Model,
                                    LicenseNumber = c.LicenseNumber
                                }).ToListAsync();
                return View(model);
            }

            // Update appointment details
            appointment.CarId = model.CarId;
            appointment.AppointmentDate = model.AppointmentDate;
            appointment.AppointmentTime = timeSpan;
            appointment.Notes = model.Notes;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Appointment updated successfully.";
            return RedirectToAction("BookAppointment");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.UserId == Convert.ToInt32(user.Id));
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("BookAppointment");
            }

            // Optionally, check if the appointment can be deleted (e.g., only pending appointments)
            if (appointment.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending appointments can be deleted.";
                return RedirectToAction("BookAppointment");
            }

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Appointment deleted successfully.";
            return RedirectToAction("BookAppointment");
        }


        [HttpGet]
        public async Task<IActionResult> BookAppointment()
        {
            ViewData["IsAppointments"] = true;
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Unauthorized access attempt to BookAppointment");
                    return Unauthorized();
                }

                // Assuming user.Id can be converted to int (or adjust as needed)
                int userId = Convert.ToInt32(user.Id);
                _logger.LogInformation("User {UserId} accessing BookAppointment form", userId);

                var model = new AppointmentViewModel
                {
                    // Only load cars owned by the user that are assigned (i.e. have a CarMechanicAssignment)
                    Cars = await _context.Cars
                        .Where(c => c.OwnerId == userId &&
                                    _context.CarMechanicAssignments.Any(cma => cma.CarId == c.Id))
                        .Select(c => new CarViewModel
                        {
                            Id = c.Id,
                            Make = c.Make,
                            Model = c.Model,
                            LicenseNumber = c.LicenseNumber
                        })
                        .ToListAsync(),

                    Appointments = await _context.Appointments
                        .Where(a => a.UserId == userId)
                        .Include(a => a.Car)
                        .Select(a => new AppointmentViewModel
                        {
                            CarId = a.CarId,
                            AppointmentDate = a.AppointmentDate,
                            AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
                            Notes = a.Notes,
                            Status = a.Status,
                            MechanicName = a.MechanicName ?? "Not Assigned",
                            Car = new CarViewModel
                            {
                                Make = a.Car.Make,
                                Model = a.Car.Model
                            }
                        })
                        .ToListAsync()
                };

                // If there are no assigned cars, you may choose to display a message.
                if (!model.Cars.Any())
                {
                    TempData["InfoMessage"] = "No assigned cars available for booking an appointment.";
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading BookAppointment form for user {UserId}");
                TempData["ErrorMessage"] = "An error occurred while loading the appointment form.";
                return RedirectToAction("CustomerDashboard");
            }
        }




        [HttpPost]
        public async Task<IActionResult> BookAppointment(AppointmentViewModel model)
        {
            ViewData["IsAppointments"] = true;
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Unauthorized access!";
                return RedirectToAction("CustomerDashboard");
            }

            int userId = Convert.ToInt32(user.Id);

            // Log form submission values
            _logger.LogInformation("Form Data - CarId: {CarId}, AppointmentDate: {AppointmentDate}, AppointmentTime: {AppointmentTime}",
                model.CarId, model.AppointmentDate, model.AppointmentTime);

            // Check if ModelState is valid
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(e => e.Value.Errors.Count > 0)
                                       .Select(e => $"{e.Key}: {string.Join(", ", e.Value.Errors.Select(err => err.ErrorMessage))}")
                                       .ToList();

                _logger.LogWarning("Validation failed for User {UserId}. Errors: {Errors}", userId, string.Join(" | ", errors));

                // Reload cars for dropdown
                model.Cars = await _context.Cars
                    .Where(c => c.OwnerId == userId)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        LicenseNumber = c.LicenseNumber
                    })
                    .ToListAsync();

                TempData["ErrorMessage"] = "There were errors in your appointment form. Please try again.";
                return View(model);
            }

            // Convert string AppointmentTime to TimeSpan
            if (!TimeSpan.TryParse(model.AppointmentTime, out TimeSpan timeSpan))
            {
                _logger.LogWarning("Invalid time format: {AppointmentTime} for User {UserId}", model.AppointmentTime, userId);
                TempData["ErrorMessage"] = "Invalid time format. Please enter a valid time (HH:mm).";

                // Reload cars for dropdown
                model.Cars = await _context.Cars
                    .Where(c => c.OwnerId == userId)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        LicenseNumber = c.LicenseNumber
                    })
                    .ToListAsync();

                return View(model);
            }

            // Check for an existing active appointment for this car and user.
            // You can adjust the status filter as needed.
            var existingAppointment = await _context.Appointments
                .Where(a => a.UserId == userId && a.CarId == model.CarId &&
                            (a.Status == "Pending" || a.Status == "Approved" || a.Status == "Rescheduled"))
                .FirstOrDefaultAsync();

            if (existingAppointment != null)
            {
                TempData["ErrorMessage"] = "You already have an active appointment booked for this car.";
                // Reload cars for dropdown
                model.Cars = await _context.Cars
                    .Where(c => c.OwnerId == userId)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        LicenseNumber = c.LicenseNumber
                    })
                    .ToListAsync();
                return View(model);
            }

            // 🔹 Fetch the assigned mechanic for the selected car
            var assignedMechanic = await _context.CarMechanicAssignments
                .Include(ca => ca.Mechanic)
                .FirstOrDefaultAsync(ca => ca.CarId == model.CarId);

            int? mechanicId = assignedMechanic?.Mechanic?.Id;

            if (mechanicId == null)
            {
                TempData["ErrorMessage"] = "No mechanic is assigned to this car.";

                // Reload cars for dropdown
                model.Cars = await _context.Cars
                    .Where(c => c.OwnerId == userId)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        LicenseNumber = c.LicenseNumber
                    })
                    .ToListAsync();

                return View(model);
            }

            // 🔒 Check if the mechanic is already booked at that date and time
            bool isMechanicBooked = await _context.Appointments
                .AnyAsync(a =>
                    a.MechanicId == mechanicId &&
                    a.AppointmentDate == model.AppointmentDate &&
                    a.AppointmentTime == timeSpan &&
                    a.Status != "Cancelled" &&
                    a.Status != "Completed");

            if (isMechanicBooked)
            {
                TempData["ErrorMessage"] = "The assigned mechanic is already booked at that time. Please choose another time.";

                // Reload cars for dropdown
                model.Cars = await _context.Cars
                    .Where(c => c.OwnerId == userId)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        LicenseNumber = c.LicenseNumber
                    })
                    .ToListAsync();

                return View(model);
            }

            string mechanicName = assignedMechanic?.Mechanic?.FullName ?? "Not Assigned";

            var appointment = new Appointment
            {
                UserId = userId,
                CarId = model.CarId,
                AppointmentDate = model.AppointmentDate,
                AppointmentTime = timeSpan,
                Notes = model.Notes,
                Status = "Pending",
                MechanicName = mechanicName,  // 🔹 Include mechanic name
                MechanicId = mechanicId ?? throw new Exception("No mechanic assigned to the selected car.")

            };

            try
            {
                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();
                await _notificationService.SendNotificationToAdminAndMechanicOnAppointmentBooking(appointment.Id);
                TempData["SuccessMessage"] = $"Appointment booked successfully with {mechanicName}!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                _logger.LogError(ex, "Error booking appointment for user {UserId}", userId);
            }

            return RedirectToAction("CustomerDashboard");
        }



        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            _logger.LogInformation("Settings page accessed by user: {UserId}", User.Identity?.Name);

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("User not found for Settings page.");
                return NotFound();
            }

            _logger.LogInformation("Fetched user data: ID={UserId}, FullName={FullName}, Email={Email}, PhoneNumber={PhoneNumber}",
                user.Id, user.FullName, user.Email, user.PhoneNumber);

            var model = new EditUserViewModel
            {
                ProfilePicture = user.ProfilePicture,
                Id = user.Id.ToString(),
                FullName = user.FullName,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Settings(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            // Update Profile Picture (if a new one is uploaded)
            if (model.ProfilePictureFile != null && model.ProfilePictureFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profiles");
                Directory.CreateDirectory(uploadsFolder); // Ensure folder exists

                var fileName = $"{Guid.NewGuid()}_{model.ProfilePictureFile.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePictureFile.CopyToAsync(stream);
                }

                user.ProfilePicture = "/images/profiles/" + fileName;
                _logger.LogInformation("Profile picture updated for user: {UserId}", user.Id);
            }

            // Update user details
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.PhoneNumber = model.PhoneNumber;

            // Change Password (if provided)
            if (!string.IsNullOrWhiteSpace(model.NewPassword) && !string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                var passwordCheck = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
                if (!passwordCheck)
                {
                    ModelState.AddModelError("CurrentPassword", "Incorrect current password.");
                    return View(model);
                }

                var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }

                _logger.LogInformation("Password changed for user: {UserId}", user.Id);
            }

            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Settings");
        }


        public async Task<IActionResult> MyCarReports()
        {
            ViewData["IsCurrentReports"] = true;



            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);



            // Fetch all cars along with their reports
            var cars = await _context.Cars
                .AsNoTracking()
                .Include(c => c.Owner)
                .Where(c => _context.Cars
                    .Any(a => a.OwnerId.ToString() == userId))
                .Select(c => new CarViewModel
                {
                    Id = c.Id,
                    LicenseNumber = c.LicenseNumber,
                    Make = c.Make,
                    Model = c.Model,
                    Year = c.Year,
                    Color = c.Color,
                    OwnerName = c.Owner != null ? c.Owner.FullName : "Unknown",
                    AssignedMechanicName = _context.CarMechanicAssignments
                        .Where(a => a.CarId == c.Id)
                        .Select(a => a.Mechanic.FullName)
                        .FirstOrDefault() ?? "Not Assigned",
                    Reports = _context.MechanicReports
                        .Where(r => r.CarId == c.Id)
                        .Select(r => new MechanicReportViewModel
                        {
                            Id = r.Id,
                            DateReported = r.DateReported,
                            ServiceDetails = r.ServiceDetails,
                            AdditionalNotes = r.AdditionalNotes,
                            ServiceFee = r.ServiceFee,
                            TotalPrice = r.Parts != null
                                ? r.Parts.Sum(mp => mp.PartPrice * mp.Quantity) + r.ServiceFee
                                : r.ServiceFee,
                            Parts = r.Parts
                                .Select(mp => new MechanicReportPartViewModel
                                {
                                    PartName = mp.PartName,
                                    PartPrice = mp.PartPrice,
                                    Quantity = mp.Quantity
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToListAsync();

            var model = new MechanicDashboardViewModel
            {
                Cars = cars
            };

            return View(model);
        }




        [HttpGet]
        public async Task<IActionResult> AssignFault(int id)
        {
            var car = await _context.Cars.FindAsync(id);
            if (car == null)
            {
                return NotFound("No car with id exists");
            }

            var faultViewModel = new FaultViewModel
            {
                CarId = car.Id,
                DateReportedOn = DateTime.Now,
                ResolutionStatus = false,
                Description = string.Empty,
            };

            return View(faultViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AssignFault(FaultViewModel model)
        {
            if (ModelState.IsValid)
            {
                var fault = new Fault
                {
                    CarId = model.CarId,
                    DateReportedOn = model.DateReportedOn,
                    ResolutionStatus = model.ResolutionStatus,
                    Description = model.Description,
                };

                _context.Add(fault);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Fault reported successfully";
                return RedirectToAction("CustomerDashboard", "Customer");

            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AssignFaultsToCar(FaultAssignmentViewModel model)
        {
            // Retrieve the car by its ID
            var car = await _context.Cars
                .Include(c => c.Faults) // Include faults to update their MechanicId
                .FirstOrDefaultAsync(c => c.Id == model.CarId);

            if (car == null)
            {
                return NotFound("Car not found");
            }

            // Assign the MechanicId to the car
            car.MechanicId = (int)model.MechanicId;

            // Assign the MechanicId to all associated faults
            foreach (var fault in car.Faults)
            {
                fault.MechanicId = model.MechanicId;
            }
            Console.WriteLine($"Car retrieved: {car?.Id}, Faults count: {car?.Faults.Count}");

            foreach (var fault in car.Faults)
            {
                Console.WriteLine($"Updating FaultId {fault.Id} with MechanicId {model.MechanicId}");
            }


            // Save the changes
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "Admin");
        }






        [HttpGet]
        public async Task<IActionResult> EditCar(int id)
        {
            ViewData["IsMyCars"] = true;
            var car = await _context.Cars.FindAsync(id);
            if (car == null)
            {
                return NotFound("The car with the specified ID does not exist.");
            }

            // Create a list of CarViewModels to pass to the view
            var carViewModels = new CarViewModel
            {
                Id = car.Id,
                Make = car.Make,
                Model = car.Model,
                Year = car.Year,
                Color = car.Color,
                ChassisNumber = car.ChassisNumber,
                FuelType = car.FuelType,
                LicenseNumber = car.LicenseNumber

            };


            return View(carViewModels);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCar(int id, CarViewModel carViewModel)
        {
            if (id != carViewModel.Id)
            {
                return BadRequest("The car ID mismatch.");
            }

            // Await the FindAsync method to get the car
            var car = await _context.Cars.FindAsync(id);
            if (car == null)
            {
                return RedirectToAction("CustomerDashboard", "Customer");
            }

            // Update car details from the CarViewModel
            car.Make = carViewModel.Make;
            car.Model = carViewModel.Model;
            car.Year = carViewModel.Year;
            car.Color = carViewModel.Color;
            car.ChassisNumber = carViewModel.ChassisNumber;
            car.FuelType = carViewModel.FuelType;
            car.LicenseNumber = carViewModel.LicenseNumber;

            // Save the changes to the database
            await _context.SaveChangesAsync();

            // Redirect to the customer dashboard after successful update
            TempData["SuccessMessage"] = "Car updated succesfully";
            return RedirectToAction("CustomerDashboard", "Customer");


        }


        [HttpPost]
        public async Task<IActionResult> DeleteCar(int id)
        {
            try
            {
                // Use FirstOrDefaultAsync with explicit columns instead of FindAsync to avoid lazy loading
                var car = await _context.Cars
                    .Where(c => c.Id == id)
                    .Select(c => new Car
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        LicenseNumber = c.LicenseNumber,
                        OwnerId = c.OwnerId
                    })
                    .FirstOrDefaultAsync();

                if (car == null)
                {
                    return NotFound();
                }

                // Check if current user is the owner
                string currentUserId = _userManager.GetUserId(User);
                if (car.OwnerId.ToString() != currentUserId)
                {
                    return Forbid();
                }

                // First check for dependencies without loading the full entities
                bool hasFaults = await _context.Faults.AnyAsync(f => f.CarId == id);
                bool hasReports = await _context.MechanicReports.AnyAsync(r => r.CarId == id);
                bool hasAppointments = await _context.Appointments.AnyAsync(a => a.CarId == id);

                if (hasFaults || hasReports || hasAppointments)
                {
                    // Car has dependencies - we need to handle this gracefully
                    TempData["ErrorMessage"] = "Cannot delete this car because it has associated records. Please remove all faults, reports, and appointments first.";
                    return RedirectToAction("MyCars");
                }

                // Lookup fresh entity for deletion (without loading navigation properties)
                var carToDelete = new Car { Id = id };
                _context.Cars.Attach(carToDelete);
                _context.Cars.Remove(carToDelete);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Your {car.Make} {car.Model} ({car.LicenseNumber}) has been deleted successfully.";
                return RedirectToAction("MyCars");
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error deleting car: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while deleting the car. Please try again.";
                return RedirectToAction("MyCars");
            }
        }


        [HttpGet]
        public async Task<IActionResult> CustomerDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            int userId = Convert.ToInt32(user.Id);

            var cars = await _context.Cars
                .Where(c => c.OwnerId == userId)
                .Include(c => c.Faults)
                .Include(c => c.Reports)
                .Select(c => new CarViewModel
                {
                    Id = c.Id,
                    Make = c.Make,
                    Model = c.Model,
                    Year = c.Year,
                    Color = c.Color,
                    ChassisNumber = c.ChassisNumber ?? "Unknown",
                    FuelType = c.FuelType,
                    LicenseNumber = c.LicenseNumber,
                    HasReport = c.Reports.Any(),
                    Faults = c.Faults.Select(f => new FaultViewModel
                    {
                        Description = f.Description,
                        DateReportedOn = f.DateReportedOn,
                        ResolutionStatus = f.ResolutionStatus
                    }).ToList(),
                    OwnerName = user.FullName, // Assuming you want to show the current user's name
                    AssignedMechanicName = (from a in _context.CarMechanicAssignments
                                            join u in _context.Users on a.MechanicId equals u.Id
                                            where a.CarId == c.Id
                                            select u.FullName).FirstOrDefault() ?? "Not Assigned"
                })
                .ToListAsync();

            var upcomingAppointmentsCount = await _context.Appointments
                .Where(a => a.UserId == userId && a.AppointmentDate > DateTime.Now && a.Status != "Cancelled")
                .CountAsync();

            var reportCount = await _context.Cars
                .Where(c => c.OwnerId == userId)
                .SelectMany(c => c.Reports)
                .CountAsync();

            var viewModel = new CustomerDashboardViewModel
            {
                Cars = cars,
                CarCount = cars.Count,
                ReportCount = reportCount,
                UpcomingAppointmentsCount = upcomingAppointmentsCount,
                MyTable = new DataTable()
            };

            // Set up columns
            viewModel.MyTable.Columns.Add("Make", typeof(string));
            viewModel.MyTable.Columns.Add("Model", typeof(string));
            viewModel.MyTable.Columns.Add("Year", typeof(int));
            viewModel.MyTable.Columns.Add("Color", typeof(string));
            viewModel.MyTable.Columns.Add("Chassis Number", typeof(string));
            viewModel.MyTable.Columns.Add("Fuel Type", typeof(string));
            viewModel.MyTable.Columns.Add("License Number", typeof(string));
            viewModel.MyTable.Columns.Add("Mechanic", typeof(string));

            // Fill rows
            foreach (var car in cars)
            {
                viewModel.MyTable.Rows.Add(car.Make, car.Model, car.Year, car.Color, car.ChassisNumber, car.FuelType, car.LicenseNumber, car.AssignedMechanicName);
            }

            return View(viewModel);
        }



        [HttpGet]
        public async Task<IActionResult> MyCars()
        {
            ViewData["IsMyCars"] = true;
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            int userId = Convert.ToInt32(user.Id);

            // Fetch all cars owned by the user
            var cars = await _context.Cars
                .Where(c => c.OwnerId == userId)
                .Include(c => c.Faults)
                .Include(c => c.Reports)
                .Select(c => new CarViewModel
                {
                    Id = c.Id,
                    Make = c.Make,
                    Model = c.Model,
                    Year = c.Year,
                    Color = c.Color,
                    ChassisNumber = c.ChassisNumber ?? "Unknown",
                    FuelType = c.FuelType,
                    LicenseNumber = c.LicenseNumber,
                    HasReport = c.Reports.Any(),
                    Faults = c.Faults.Select(f => new FaultViewModel
                    {
                        Description = f.Description,
                        DateReportedOn = f.DateReportedOn,
                        ResolutionStatus = f.ResolutionStatus
                    }).ToList(),
                    MechanicName = (from a in _context.CarMechanicAssignments
                                            join u in _context.Users on a.MechanicId equals u.Id
                                            where a.CarId == c.Id
                                            select u.FullName).FirstOrDefault() ?? "Not Assigned"
                })
                .ToListAsync();

            var upcomingAppointmentsCount = await _context.Appointments
            .Where(a => a.UserId == userId && a.AppointmentDate > DateTime.Now)
            .CountAsync();

            // Total reports for all cars owned by this user
            var reportCount = await _context.Cars
                .Where(c => c.OwnerId == userId)
                .SelectMany(c => c.Reports)
                .CountAsync();


            // Count the number of cars
            int carCount = cars.Count;
            var viewModel = new CustomerDashboardViewModel
            {
                Cars = cars,
                CarCount = cars.Count,
                ReportCount = reportCount,
                UpcomingAppointmentsCount = upcomingAppointmentsCount,
                MyTable = new DataTable()
            };

            // Set up columns
            viewModel.MyTable.Columns.Add("Id", typeof(int));
            viewModel.MyTable.Columns.Add("Make", typeof(string));
            viewModel.MyTable.Columns.Add("Model", typeof(string));
            viewModel.MyTable.Columns.Add("Year", typeof(int));
            viewModel.MyTable.Columns.Add("Color", typeof(string));
            viewModel.MyTable.Columns.Add("Chassis Number", typeof(string));
            viewModel.MyTable.Columns.Add("Fuel Type", typeof(string));
            viewModel.MyTable.Columns.Add("License Number", typeof(string));
            viewModel.MyTable.Columns.Add("Mechanic", typeof(string));

            // Fill rows
            foreach (var car in cars)
            {
                viewModel.MyTable.Rows.Add(car.Id,car.Make, car.Model, car.Year, car.Color, car.ChassisNumber, car.FuelType, car.LicenseNumber, car.MechanicName);
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> AddCar()
        {
            ViewData["IsMyCars"] = true;
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Unauthorized access attempt.");
                return Unauthorized();
            }

            // Log user information
            _logger.LogInformation($"User {user.FullName} accessed the AddCar page.");

            // Create an empty CarViewModel instance with default values.
            var model = new CarViewModel
            {
                Year = DateTime.Now.Year,
                FuelType = "Petrol" // Set default fuel type to Petrol.
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCar(CarViewModel carviewmodel)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Unauthorized access attempt during AddCar post.");
                return Unauthorized();
            }

            _logger.LogInformation($"User {user.FullName} is attempting to add a car.");

            if (!ModelState.IsValid)
            {
                _logger.LogError("CarViewModel validation failed.");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogError($"Validation error: {error.ErrorMessage}");
                }

                return View(carviewmodel);
            }

            carviewmodel.OwnerId = Convert.ToInt32(user.Id);
            carviewmodel.UserId = user.Id;
            carviewmodel.OwnerName = user.FullName;
            
            Car car = new Car()
            {
                Id = carviewmodel.Id,
                Make = carviewmodel.Make,
                Model = carviewmodel.Model,
                Year = carviewmodel.Year,
                Color = carviewmodel.Color,
                OwnerId = carviewmodel.OwnerId,
                ChassisNumber = carviewmodel.ChassisNumber,
                FuelType = carviewmodel.FuelType,
                LicenseNumber = carviewmodel.LicenseNumber,
            };

            try
            {
                _context.Cars.Add(car);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Car added successfully with ID: " + car.Id);
                TempData["SuccessMessage"] = "Car added successfully";

                await _notificationService.SendNotificationToAdminOnCarAddition(car.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding car: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while adding the car.";
                return View(carviewmodel);
            }

            return RedirectToAction("CustomerDashboard");
        }

        [HttpGet]
        public async Task<IActionResult> CustomerFaults(int carId)
        {
            ViewData["IsMyCars"] = true;

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);;
            var myCars = await _context.Cars
                .Where(cma => cma.OwnerId == userId)
                .Select(cma => new OwnerCarViewModel
                {
                    CarId = cma.Id,
                    CarMake = cma.Make,
                    CarModel = cma.Model,
                    LicenseNumber = cma.LicenseNumber,
                    Year = cma.Year,
                    Color = cma.Color,
                    MechanicId = cma.MechanicId.GetValueOrDefault(),
                    MechanicFullName = cma.Mechanic.FullName,
                    HasReport = cma.Reports.Any(),
                    Faults = cma.Faults.Select(f => new FaultViewModel
                    {
                        Id = f.Id,
                        Description = f.Description,
                        DateReportedOn = f.DateReportedOn,
                        ResolutionStatus = f.ResolutionStatus
                    }).ToList()
                }).ToListAsync();

            var faults = await _context.Faults
                .Where(f => f.CarId == carId)
                .Select(f => new FaultViewModel
                {
                    Id = f.Id,
                    Description = f.Description,
                    DateReportedOn = f.DateReportedOn,
                    ResolutionStatus = f.ResolutionStatus
                })
                .ToListAsync();

            var reportFaultViewModel = new FaultViewModel
            {
                CarId = carId,
                DateReportedOn = DateTime.Now,
                ResolutionStatus = false,
                Description = string.Empty,
            };

            


            var model = new AssignFaultViewModel
            {
                Faults = faults,
                NewFault = reportFaultViewModel,
                MyCars = myCars
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ReportFault(int id)
        {
            var car = await _context.Cars.FindAsync(id);
            if (car == null)
            {
                return NotFound("No car with id exists");
            }

            var faultViewModel = new FaultViewModel
            {
                CarId = car.Id,
                DateReportedOn = DateTime.Now,
                ResolutionStatus = false,
                Description = string.Empty,
            };

            return View(faultViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddFault(int CarId, string Description)
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                return Json(new { success = false, message = "Description cannot be empty." });
            }

            var fault = new Fault
            {
                CarId = CarId,
                Description = Description,
                DateReportedOn = DateTime.Now,
                ResolutionStatus = false
            };

            _context.Faults.Add(fault);
            await _context.SaveChangesAsync();

            // ✅ Notify the assigned mechanic about the fault
            await _notificationService.SendNotificationToMechanicOnAddedFault(fault.Id);

            var updatedFaults = await _context.Faults
                .Where(f => f.CarId == CarId)
                .Select(f => new FaultViewModel
                {
                    Id = f.Id,
                    Description = f.Description,
                    DateReportedOn = f.DateReportedOn,
                    ResolutionStatus = f.ResolutionStatus
                })
                .ToListAsync();

            return Json(new { success = true, faults = updatedFaults });
        }



        [HttpGet]
        public IActionResult ModifyResolutionStatus(int carId)
        {
            var faults = _context.Faults
            .Where(f => f.CarId == carId)
            .Select(f => new {
                id = f.Id,
                description = f.Description,
                resolutionStatus = f.ResolutionStatus,
                dateReportedOn = f.DateReportedOn.ToString("yyyy-MM-ddTHH:mm:ss") // ISO 8601 format!
            })
            .ToList();

            return Json(faults);
            // Return faults in JSON format for the frontend to process
        }

        [HttpPost]
        public IActionResult ModifyResolutionStatus(int carId, List<int> resolvedFaults)
        {
            var faults = _context.Faults.Where(f => f.CarId == carId).ToList();

            foreach (var fault in faults)
            {
                fault.ResolutionStatus = resolvedFaults.Contains(fault.Id);
            }

            _context.SaveChanges();

            // Fetch the updated list of faults for the car
            var updatedFaults = _context.Faults
                .Where(f => f.CarId == carId)
                .Select(f => new FaultViewModel
                {
                    Id = f.Id,
                    Description = f.Description,
                    DateReportedOn = f.DateReportedOn,
                    ResolutionStatus = f.ResolutionStatus
                })
                .ToList();

            return Json(new { success = true, faults = updatedFaults });
        }

        [HttpGet]
        public async Task<IActionResult> CustomerAppointments()
        {
            ViewData["IsAppointments"] = true;

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User is null. Unauthorized access attempt.");
                return Unauthorized();
            }

            _logger.LogInformation("Logged-in customer ID: {CustomerId}", user.Id);

            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .ThenInclude(c => c.Owner)
                .Where(a => a.Car.OwnerId == user.Id)
                .ToListAsync();

            foreach (var appointment in appointments)
            {
                _logger.LogInformation("Appointment ID: {AppointmentId}, CarID: {CarId}, Date: {Date}, Time: {Time}",
                    appointment.Id, appointment.CarId, appointment.AppointmentDate, appointment.AppointmentTime);
            }

            var viewModel = new CustomerAppointmentsViewModel
            {
                Appointments = appointments.Select(a => new AppointmentViewModel
                {
                    Id = a.Id,
                    CarId = a.CarId,
                    AppointmentDate = a.AppointmentDate,
                    AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
                    Notes = a.Notes,
                    Status = a.Status,
                    MechanicName = a.MechanicName,
                    Car = new CarViewModel
                    {
                        Id = a.Car.Id,
                        Make = a.Car.Make,
                        Model = a.Car.Model,
                        LicenseNumber = a.Car.LicenseNumber
                    }
                }).ToList()
            };

            // Log status counts
            viewModel.StatusCounts = viewModel.Appointments
                .GroupBy(a => a.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var status in viewModel.StatusCounts)
            {
                _logger.LogInformation("Status: {Status}, Count: {Count}", status.Key, status.Value);
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> CustomerAppointmentDetails(int id)
        {
            ViewData["IsAppointments"] = true;

            var customer = await _userManager.GetUserAsync(User);
            if (customer == null)
            {
                return Unauthorized();
            }

            // Include the mechanic if relationship exists
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                    .ThenInclude(c => c.Owner)
                .Include(a => a.Mechanic) // Ensure this is mapped in your model
                .FirstOrDefaultAsync(a => a.Id == id && a.Car.OwnerId == customer.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found or does not belong to you.";
                return RedirectToAction("BookAppointment");
            }

            var model = new AppointmentViewModel
            {
                Id = appointment.Id,
                CarId = appointment.CarId,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                Notes = appointment.Notes,
                Status = appointment.Status,
                MechanicName = appointment.Mechanic?.FullName, // from related user
                MechanicPhone = appointment.Mechanic?.PhoneNumber,
                MechanicEmail = appointment.Mechanic?.Email,
                Car = new CarViewModel
                {
                    Id = appointment.Car.Id,
                    Make = appointment.Car.Make,
                    Model = appointment.Car.Model,
                    LicenseNumber = appointment.Car.LicenseNumber
                },
                CustomerName = appointment.Car.Owner?.FullName,
                CustomerPhone = appointment.Car.Owner?.PhoneNumber,
                CustomerEmail = appointment.Car.Owner?.Email
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ProposeNewDate(int id)
        {
            ViewData["IsAppointments"] = true;

            var customer = await _userManager.GetUserAsync(User);
            if (customer == null)
            {
                return Unauthorized();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                    .ThenInclude(c => c.Owner)
                .Include(a => a.Mechanic)
                .FirstOrDefaultAsync(a => a.Id == id && a.Car.OwnerId == customer.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments");
            }

            var model = new AppointmentViewModel
            {
                Id = appointment.Id,
                CarId = appointment.CarId,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                Notes = appointment.Notes,
                Status = appointment.Status,
                MechanicName = appointment.Mechanic?.FullName,
                MechanicPhone = appointment.Mechanic?.PhoneNumber,
                MechanicEmail = appointment.Mechanic?.Email,
                Car = new CarViewModel
                {
                    Id = appointment.Car.Id,
                    Make = appointment.Car.Make,
                    Model = appointment.Car.Model,
                    LicenseNumber = appointment.Car.LicenseNumber
                }
            };

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> ProposeNewDate(AppointmentViewModel model)
        {
            var customer = await _userManager.GetUserAsync(User);
            if (customer == null)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .FirstOrDefaultAsync(a => a.Id == model.Id && a.Car.OwnerId == customer.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments");
            }

            if (!TimeSpan.TryParse(model.AppointmentTime, out TimeSpan newTime))
            {
                ModelState.AddModelError("AppointmentTime", "Invalid time format. Please use HH:mm.");
                return View(model);
            }

            // ⏰ Restrict time range between 08:00 and 18:00
            if (newTime < TimeSpan.FromHours(8) || newTime > TimeSpan.FromHours(18))
            {
                ModelState.AddModelError("AppointmentTime", "Time must be between 08:00 and 18:00.");
                return View(model);
            }

            // 🔒 Check mechanic availability at the new time
            bool isMechanicBusy = await _context.Appointments.AnyAsync(a =>
                a.MechanicId == appointment.MechanicId &&
                a.Id != appointment.Id &&
                a.AppointmentDate == model.AppointmentDate &&
                a.AppointmentTime == newTime &&
                a.Status != "Cancelled" &&
                a.Status != "Completed");

            if (isMechanicBusy)
            {
                ModelState.AddModelError("AppointmentTime", "The mechanic is already booked at this time.");
                return View(model);
            }

            // ✅ Update with proposed values
            appointment.AppointmentDate = model.AppointmentDate;
            appointment.AppointmentTime = newTime;
            appointment.Notes = model.Notes;
            appointment.Status = "Rescheduled";

            await _context.SaveChangesAsync();
            await _notificationService.SendNotificationToAdminAndMechanicOnAppointmentPostponing(appointment.Id);
            TempData["SuccessMessage"] = "Your proposed appointment date has been submitted.";
            return RedirectToAction("CustomerAppointmentDetails", new { id = appointment.Id });
        }


        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int id, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .FirstOrDefaultAsync(a => a.Id == id && a.Car.OwnerId == user.Id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("CustomerAppointments");
            }

            // Prevent cancellation if already completed
            if (appointment.Status == "Completed")
            {
                TempData["ErrorMessage"] = "You cannot cancel a completed appointment.";
                return RedirectToAction("CustomerAppointments");
            }


            // Prevent cancellation if within 1 hour of appointment time
            var appointmentDateTime = appointment.AppointmentDate
                .Add(appointment.AppointmentTime);

            if (DateTime.Now >= appointmentDateTime.AddMinutes(-60))
            {
                TempData["ErrorMessage"] = "Appointments can only be cancelled more than 1 hour in advance.";
                return RedirectToAction("CustomerAppointments");
            }

            appointment.Status = "Cancelled";
            appointment.Notes = $"Cancelled by customer. Reason: {reason}";
            await _notificationService.SendNotificationToAdminAndMechanicOnAppointmentCancelling(appointment.Id);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment cancelled successfully.";
            return RedirectToAction("CustomerAppointments");
        }


        [HttpGet]
        public async Task<IActionResult> CustomerNotificationViewPage()
        {
            var user = await _userManager.GetUserAsync(User);
            // If administrators have profile pictures, include the following line:
            // ViewBag.ProfilePicturePath = user.ProfilePicture;
            return View();
        }

    }


}

