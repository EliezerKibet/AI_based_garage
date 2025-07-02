using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GarageManagementSystem.Models;
using GarageManagementSystem.Data;
using GarageManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GarageManagementSystem.Services;
using GarageManagementSystem.Models.ViewModels;
using Azure.AI.OpenAI;
using System.Data;


namespace GarageManagementSystem.Controllers
{
    [Authorize]
    public class MechanicController : BaseMechanicController
    {
        private readonly PhoneCallService _phoneCallService;
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;
        private readonly ILogger<MechanicController> _logger;
        private readonly IAlertService _alertService;
        private readonly NotificationService _notificationService;
        private readonly IEmailService _emailService;

        public MechanicController(
            UserManager<Users> userManager,
            AppDbContext context,
            ILogger<MechanicController> logger,
            PhoneCallService phoneCallService,
            IAlertService alertService,
            NotificationService notificationService,
            IEmailService emailService)
            : base(context, userManager, logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _phoneCallService = phoneCallService;
            _alertService = alertService;
            _notificationService = notificationService;
            _emailService = emailService;
        }

        public async Task<IActionResult> MechanicSearch(string query, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return View(new MechanicSearchViewModel { Query = query });
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
                var userCarIds = await _context.CarMechanicAssignments
                    .Where(c => c.MechanicId.ToString() == userId)
                    .Select(c => c.CarId)
                    .Distinct()
                    .ToListAsync();

                if (userCarIds.Count == 0)
                {
                    // No cars found for this user, return empty results
                    return View(new MechanicSearchViewModel
                    {
                        Query = query,
                        MatchingCars = new List<Car>(),
                        MatchingCustomers = new List<Users>(),
                        MatchingFaults = new List<Fault>(),
                        MatchingReports = new List<MechanicReport>(),
                        MatchingAppointments = new List<Appointment>()
                    });
                }

                // Get only the user's cars that match the query - FULLY NULL-SAFE VERSION
                var cars = await _context.Cars
                    .Where(c => userCarIds.Contains(c.Id))
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

                var customerIds = await _context.CarMechanicAssignments
                     .Where(r => userCarIds.Contains(r.CarId))
                     .Join(_context.Cars,
                           assignment => assignment.CarId,
                           car => car.Id,
                           (assignment, car) => car.OwnerId)
                     .Distinct()
                     .ToListAsync();

                var customers = new List<Users>();
                if (customerIds.Any())
                {
                    customers = await _userManager.Users
                        .Where(u => customerIds.Contains(u.Id))
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

                // Get appointments for user's cars - null-safe version
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

                var viewModel = new MechanicSearchViewModel
                {
                    Query = query,
                    MatchingCars = cars,
                    MatchingCustomers = customers,
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
            var mechanic = await _userManager.GetUserAsync(User);
            if (mechanic == null)
            {
                return Unauthorized();
            }

            var appointments = await _context.Appointments
            .Where(a => a.MechanicName == mechanic.FullName)
            .Include(a => a.Car)
            .Select(a => new
            {
                id = a.Id,
                title = $"{a.Car.Make} {a.Car.Model}",
                start = a.AppointmentDate.ToString("yyyy-MM-dd") + "T" + a.AppointmentTime.ToString(@"hh\:mm"),
                description = $"Mechanic: {a.MechanicName} | Notes: {a.Notes}"
            })
            .ToListAsync();
            return Json(appointments);

        }


        [HttpGet]
        public async Task<IActionResult> ProposeNewDate(int id)
        {
            var mechanic = await _userManager.GetUserAsync(User);
            if (mechanic == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("GET ProposeNewDate called for appointment {AppointmentId} by mechanic {MechanicId}", id, mechanic.Id);

            // Retrieve the appointment ensuring it belongs to the logged-in mechanic.
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .FirstOrDefaultAsync(a => a.Id == id && a.MechanicName == mechanic.FullName);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found for mechanic {MechanicId}", id, mechanic.Id);
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("MechanicAppointments");
            }

            _logger.LogInformation("Found appointment: Id={Id}, CarId={CarId}, Status={Status}, MechanicName={MechanicName}",
                appointment.Id, appointment.CarId, appointment.Status, appointment.MechanicName);

            var model = new AppointmentViewModel
            {
                Id = appointment.Id,
                CarId = appointment.CarId, // ✅ CRITICAL: Ensure this is set
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                Notes = appointment.Notes,
                Status = appointment.Status, // ✅ CRITICAL: Ensure this is set
                MechanicName = appointment.MechanicName, // ✅ CRITICAL: Ensure this is set
                Car = appointment.Car != null ? new CarViewModel
                {
                    Id = appointment.Car.Id,
                    Make = appointment.Car.Make,
                    Model = appointment.Car.Model,
                    LicenseNumber = appointment.Car.LicenseNumber
                } : null
            };

            _logger.LogInformation("Created model: Id={Id}, CarId={CarId}, Status={Status}, MechanicName={MechanicName}",
                model.Id, model.CarId, model.Status, model.MechanicName);

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ProposeNewDate(AppointmentViewModel model)
        {
            _logger.LogInformation("ProposeNewDate POST called for appointment: {AppointmentId}", model.Id);

            var mechanic = await _userManager.GetUserAsync(User);
            if (mechanic == null)
            {
                _logger.LogWarning("Mechanic not found in ProposeNewDate");
                return Unauthorized();
            }

            // Log received model values first
            _logger.LogInformation("Received Model Values - Id: {Id}, CarId: {CarId}, Date: {Date}, Time: {Time}, Notes: {Notes}, Status: {Status}, MechanicName: {MechanicName}",
                model.Id, model.CarId, model.AppointmentDate, model.AppointmentTime ?? "null", model.Notes ?? "null", model.Status ?? "null", model.MechanicName ?? "null");

            // QUICK FIX: If CarId is missing, get it from the database
            if (!model.CarId.HasValue || model.CarId.Value == 0)
            {
                _logger.LogWarning("CarId is missing, attempting to retrieve from database for appointment {AppointmentId}", model.Id);

                var appointmentForCarId = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.Id == model.Id);

                if (appointmentForCarId != null)
                {
                    model.CarId = appointmentForCarId.CarId;
                    _logger.LogInformation("CarId retrieved from database: {CarId}", model.CarId);
                }
            }

            // Clear any Car-related model state errors that are causing validation issues
            ModelState.Remove("Car.Color");
            ModelState.Remove("Car.Id");
            ModelState.Remove("Car.Make");
            ModelState.Remove("Car.Model");
            ModelState.Remove("Car.LicenseNumber");
            ModelState.Remove("Car.Year");
            ModelState.Remove("Car.ChassisNumber");
            ModelState.Remove("Car.FuelType");

            // ADD DETAILED MODELSTATE LOGGING
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid for appointment {AppointmentId}", model.Id);

                // Log all validation errors
                foreach (var modelError in ModelState)
                {
                    var key = modelError.Key;
                    var errors = modelError.Value.Errors;

                    _logger.LogError("Field '{Field}' has {ErrorCount} error(s). Attempted Value: '{AttemptedValue}'",
                        key, errors.Count, modelError.Value.AttemptedValue ?? "null");

                    foreach (var error in errors)
                    {
                        _logger.LogError("  -> Error: {ErrorMessage}", error.ErrorMessage);
                        if (error.Exception != null)
                        {
                            _logger.LogError("  -> Exception: {Exception}", error.Exception.Message);
                        }
                    }
                }

                // Log ModelState count and keys
                _logger.LogInformation("ModelState Keys: {Keys}", string.Join(", ", ModelState.Keys));
                _logger.LogInformation("Total ModelState Errors: {ErrorCount}", ModelState.ErrorCount);

                // Re-populate the model for the view if needed
                var appointment = await _context.Appointments
                    .Include(a => a.Car)
                    .FirstOrDefaultAsync(a => a.Id == model.Id && a.MechanicName == mechanic.FullName);

                if (appointment != null)
                {
                    // Re-populate required fields
                    model.CarId = appointment.CarId;
                    model.Status = appointment.Status;
                    model.MechanicName = appointment.MechanicName;

                    model.Car = new CarViewModel
                    {
                        Id = appointment.Car.Id,
                        Make = appointment.Car.Make,
                        Model = appointment.Car.Model,
                        LicenseNumber = appointment.Car.LicenseNumber
                    };
                }

                return View(model);
            }

            var existingAppointment = await _context.Appointments
                .Include(a => a.Car)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(a => a.Id == model.Id && a.MechanicName == mechanic.FullName);

            if (existingAppointment == null)
            {
                _logger.LogWarning("Appointment not found with id: {AppointmentId} for mechanic: {MechanicName}",
                    model.Id, mechanic.FullName);
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments");
            }

            if (!TimeSpan.TryParse(model.AppointmentTime, out TimeSpan newTime))
            {
                _logger.LogWarning("Invalid time format for appointment {AppointmentId}: {TimeString}",
                    model.Id, model.AppointmentTime);
                ModelState.AddModelError("AppointmentTime", "Invalid time format. Please use HH:mm.");
                return View(model);
            }

            // Restriction: 08:00 to 18:00 in 30-minute intervals
            var start = new TimeSpan(8, 0, 0);
            var end = new TimeSpan(18, 0, 0);

            if (newTime < start || newTime > end || newTime.Minutes % 30 != 0)
            {
                _logger.LogWarning("Invalid time slot for appointment {AppointmentId}: {Time}",
                    model.Id, newTime);
                ModelState.AddModelError("AppointmentTime",
                    "Please select a time between 08:00 and 18:00 in 30-minute intervals.");
                return View(model);
            }

            // Store old appointment details for email
            var oldDate = existingAppointment.AppointmentDate;
            var oldTime = existingAppointment.AppointmentTime;

            _logger.LogInformation("Rescheduling appointment {AppointmentId} from {OldDate} {OldTime} to {NewDate} {NewTime}",
                model.Id, oldDate, oldTime, model.AppointmentDate, newTime);

            // Update the appointment
            existingAppointment.AppointmentDate = model.AppointmentDate;
            existingAppointment.AppointmentTime = newTime;
            existingAppointment.Status = "Rescheduled";
            existingAppointment.Notes = model.Notes;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Appointment {AppointmentId} successfully updated in database", model.Id);

                // Send in-app notification
                try
                {
                    await _notificationService.SendNotificationToAdminAndCustomerOnAppointmentPostponing(existingAppointment.Id);
                    _logger.LogInformation("In-app notification sent for rescheduled appointment {AppointmentId}", model.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending in-app notification for rescheduled appointment {AppointmentId}", model.Id);
                }

                // Get customer information
                var customer = await _userManager.FindByIdAsync(existingAppointment.UserId.ToString());
                if (customer != null)
                {
                    var notificationMethods = new List<string>();

                    // Send Email Notification
                    if (!string.IsNullOrWhiteSpace(customer.Email))
                    {
                        _logger.LogInformation("Attempting to send reschedule email to: {CustomerEmail}", customer.Email);
                        try
                        {
                            var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                            await emailService.SendAppointmentRescheduleEmailAsync(customer, existingAppointment, oldDate, oldTime);

                            notificationMethods.Add("email");
                            _logger.LogInformation("✅ SUCCESS: Appointment reschedule email sent to customer {CustomerEmail} for appointment {AppointmentId}",
                                customer.Email, existingAppointment.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ ERROR: Failed to send reschedule email for appointment {AppointmentId} to {CustomerEmail}. Error: {ErrorMessage}",
                                model.Id, customer.Email, ex.Message);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Customer {CustomerId} does not have an email address", customer.Id);
                    }

                    // Send Phone Call Notification (optional)
                    if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
                    {
                        _logger.LogInformation("Attempting to make reschedule phone call to: {CustomerPhone}", customer.PhoneNumber);
                        try
                        {
                            _phoneCallService.MakeCall(customer.PhoneNumber,
                                $"Hello, your appointment has been rescheduled to {existingAppointment.AppointmentDate:MMMM dd} at {existingAppointment.AppointmentTime:hh\\:mm}. Please confirm if this works for you.");

                            notificationMethods.Add("phone call");
                            _logger.LogInformation("✅ SUCCESS: Appointment reschedule phone call made to {CustomerPhone} for appointment {AppointmentId}",
                                customer.PhoneNumber, existingAppointment.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ ERROR: Failed to make reschedule phone call for appointment {AppointmentId} to {CustomerPhone}. Error: {ErrorMessage}",
                                model.Id, customer.PhoneNumber, ex.Message);
                        }
                    }

                    // Set success message
                    if (notificationMethods.Any())
                    {
                        var message = $"New appointment date proposed successfully and customer notified via {string.Join(" and ", notificationMethods)}.";
                        TempData["SuccessMessage"] = message;
                        _logger.LogInformation("✅ OVERALL SUCCESS: {Message}", message);
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "New appointment date proposed successfully.";
                        TempData["WarningMessage"] = "Customer could not be notified - please contact them directly.";
                        _logger.LogWarning("⚠️ WARNING: Appointment rescheduled but customer notifications failed");
                    }
                }
                else
                {
                    _logger.LogError("Customer not found for appointment {AppointmentId}, UserId: {UserId}",
                        model.Id, existingAppointment.UserId);
                    TempData["SuccessMessage"] = "New appointment date proposed successfully.";
                    TempData["WarningMessage"] = "Customer information not found.";
                }

                return RedirectToAction("AppointmentDetails", new { id = model.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving appointment changes for {AppointmentId}: {ErrorMessage}",
                    model.Id, ex.Message);
                TempData["ErrorMessage"] = "An error occurred while saving the appointment changes.";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteAppointment(int id, string completionNotes)
        {
            if (id == 0)
                return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
                return NotFound();

            appointment.Status = "Completed";

            // Append or update notes
            if (!string.IsNullOrEmpty(completionNotes))
            {
                appointment.Notes = string.IsNullOrEmpty(appointment.Notes)
                    ? completionNotes
                    : appointment.Notes + "\n\n[Completion Notes]: " + completionNotes;
            }

            _context.Update(appointment);
            await _context.SaveChangesAsync();
            await _notificationService.SendNotificationToCustomerAndAdminOnAppointmentCompletion(appointment.Id);
            TempData["SuccessMessage"] = "Appointment marked as completed.";
            return RedirectToAction("CreateMechanicReport"); // or wherever you list appointments
        }


        [HttpPost]
        public async Task<IActionResult> ApproveAppointment(int id)
        {
            _logger.LogInformation("ApproveAppointment called with id: {AppointmentId}", id);

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment not found with id: {AppointmentId}", id);
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments");
            }

            if (appointment.Status == "Approved")
            {
                _logger.LogWarning("Appointment {AppointmentId} is already approved", id);
                TempData["ErrorMessage"] = "This appointment has already been approved.";
                return RedirectToAction("Appointments");
            }

            _logger.LogInformation("Updating appointment {AppointmentId} status to Approved", id);
            appointment.Status = "Approved";
            await _context.SaveChangesAsync();

            // Send in-app notification
            try
            {
                await _notificationService.SendNotificationToAdminAndCustomerOnAppointmentApproval(appointment.Id);
                _logger.LogInformation("In-app notification sent for appointment {AppointmentId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending in-app notification for appointment {AppointmentId}", id);
            }

            // Get customer information
            var customer = await _userManager.FindByIdAsync(appointment.UserId.ToString());
            if (customer == null)
            {
                _logger.LogError("Customer not found for appointment {AppointmentId}, UserId: {UserId}", id, appointment.UserId);
                TempData["ErrorMessage"] = "Customer not found.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            _logger.LogInformation("Customer found: {CustomerName}, Email: {CustomerEmail}, Phone: {CustomerPhone}",
                customer.FullName, customer.Email, customer.PhoneNumber);

            var notificationMethods = new List<string>();

            // Send Email Notification using SendGrid
            if (!string.IsNullOrWhiteSpace(customer.Email))
            {
                _logger.LogInformation("Attempting to send email to: {CustomerEmail}", customer.Email);
                try
                {
                    // Get the email service from DI container
                    var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                    _logger.LogInformation("Email service obtained: {ServiceType}", emailService.GetType().Name);

                    await emailService.SendAppointmentApprovalEmailAsync(customer, appointment);

                    notificationMethods.Add("email");
                    _logger.LogInformation("✅ SUCCESS: Appointment approval email sent to customer {CustomerEmail} for appointment {AppointmentId}",
                        customer.Email, appointment.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ ERROR: Failed to send approval email for appointment {AppointmentId} to {CustomerEmail}. Error: {ErrorMessage}",
                        id, customer.Email, ex.Message);

                    // Log SendGrid configuration for debugging
                    var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                    var apiKey = config["SendGrid:ApiKey"];
                    var fromEmail = config["SendGrid:FromEmail"];
                    var fromName = config["SendGrid:FromName"];

                    _logger.LogInformation("SendGrid Config - ApiKey exists: {HasApiKey}, FromEmail: {FromEmail}, FromName: {FromName}",
                        !string.IsNullOrEmpty(apiKey), fromEmail, fromName);
                }
            }
            else
            {
                _logger.LogWarning("Customer {CustomerId} does not have an email address", customer.Id);
            }

            // Send Phone Call Notification
            if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
            {
                _logger.LogInformation("Attempting to make phone call to: {CustomerPhone}", customer.PhoneNumber);
                try
                {
                    _phoneCallService.MakeCall(customer.PhoneNumber,
                        $"Hello, your appointment on {appointment.AppointmentDate:MMMM dd} at {appointment.AppointmentTime:hh\\:mm} has been approved.");

                    notificationMethods.Add("phone call");
                    _logger.LogInformation("✅ SUCCESS: Appointment approval phone call made to {CustomerPhone} for appointment {AppointmentId}",
                        customer.PhoneNumber, appointment.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ ERROR: Failed to make phone call for appointment {AppointmentId} to {CustomerPhone}. Error: {ErrorMessage}",
                        id, customer.PhoneNumber, ex.Message);
                }
            }
            else
            {
                _logger.LogWarning("Customer {CustomerId} does not have a phone number", customer.Id);
            }

            // Set success message based on what notifications were sent
            if (notificationMethods.Any())
            {
                var message = $"Appointment approved and customer notified via {string.Join(" and ", notificationMethods)}.";
                TempData["SuccessMessage"] = message;
                _logger.LogInformation("✅ OVERALL SUCCESS: {Message}", message);
            }
            else
            {
                var message = "Appointment approved but no notifications could be sent. Please check customer contact information.";
                TempData["WarningMessage"] = message;
                _logger.LogWarning("⚠️ WARNING: {Message}", message);
            }

            return RedirectToAction("AppointmentDetails", new { id });
        }

        // Add this test method to your MechanicController to test email sending

        [HttpGet]
        public async Task<IActionResult> TestEmail()
        {
            try
            {
                _logger.LogInformation("TestEmail method called");

                // Get email service
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                _logger.LogInformation("Email service obtained: {ServiceType}", emailService.GetType().Name);

                // Test simple email
                await emailService.SendEmailAsync(
                    "4223007431@student.unisel.edu.my", // Replace with your email
                    "Test Email from Garage System",
                    "<h1>Test Email</h1><p>This is a test email to verify SendGrid is working.</p>"
                );

                _logger.LogInformation("Test email sent successfully");
                TempData["SuccessMessage"] = "Test email sent successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test email: {ErrorMessage}", ex.Message);
                TempData["ErrorMessage"] = $"Failed to send test email: {ex.Message}";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public IActionResult TestEmailConfig()
        {
            try
            {
                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

                var result = new
                {
                    SendGridApiKey = !string.IsNullOrEmpty(config["SendGrid:ApiKey"]) ? "✅ Configured" : "❌ Missing",
                    SendGridFromEmail = config["SendGrid:FromEmail"] ?? "❌ Missing",
                    SendGridFromName = config["SendGrid:FromName"] ?? "❌ Missing",
                    EmailServiceRegistered = HttpContext.RequestServices.GetService<IEmailService>() != null ? "✅ Registered" : "❌ Not Registered",
                    EmailServiceType = HttpContext.RequestServices.GetService<IEmailService>()?.GetType().Name ?? "None"
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> AppointmentDetails(int id)
        {
            ViewData["IsAppointments"] = true;

            var mechanic = await _userManager.GetUserAsync(User);
            if (mechanic == null)
            {
                return Unauthorized();
            }

            // Add logging for debugging
            _logger.LogInformation("Attempting to retrieve appointment with ID: {AppointmentId} for mechanic: {MechanicName}",
                id, mechanic.FullName);

            // Retrieve the appointment, ensuring it belongs to the logged-in mechanic
            // Change this to use MechanicId instead of MechanicName
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .ThenInclude(c => c.Owner)
                .Where(a => a.Id == id && a.MechanicId == mechanic.Id)
                .FirstOrDefaultAsync();

            if (appointment == null)
            {
                _logger.LogWarning("Appointment not found with ID: {AppointmentId} for mechanic: {MechanicName}",
                    id, mechanic.FullName);
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("MechanicAppointments"); // Make sure this matches your actual action name
            }

            // Log if car is missing
            if (appointment.Car == null)
            {
                _logger.LogWarning("Appointment found but car is null. AppointmentId: {AppointmentId}", id);
            }

            try
            {
                // Safely create the view model with null checks
                var model = new AppointmentViewModel
                {
                    Id = appointment.Id,
                    CarId = appointment.CarId,
                    AppointmentDate = appointment.AppointmentDate,
                    AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                    Notes = appointment.Notes,
                    Status = appointment.Status,
                    MechanicName = appointment.MechanicName,

                    // Safely handle Car property with null check
                    Car = appointment.Car != null ? new CarViewModel
                    {
                        Id = appointment.Car.Id,
                        Make = appointment.Car.Make,
                        Model = appointment.Car.Model,
                        LicenseNumber = appointment.Car.LicenseNumber
                    } : null,

                    // Safely handle customer details with null checks
                    CustomerName = appointment.Car?.Owner?.FullName,
                    CustomerPhone = appointment.Car?.Owner?.PhoneNumber,
                    CustomerEmail = appointment.Car?.Owner?.Email
                };

                _logger.LogInformation("Successfully retrieved appointment details for ID: {AppointmentId}", id);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating view model for appointment: {AppointmentId}", id);
                TempData["ErrorMessage"] = "An error occurred while retrieving appointment details.";
                return RedirectToAction("MechanicAppointments");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingAppointments()
        {
            var mechanic = await _userManager.GetUserAsync(User);
            if (mechanic == null)
            {
                return Unauthorized();
            }

            var appointments = await _context.Appointments
                .Where(a => a.MechanicName == mechanic.FullName)
                .Include(a => a.Car)
                .Select(a => new
                {
                    id = a.Id,
                    title = $"{a.Car.Make} {a.Car.Model}",
                    start = a.AppointmentDate.ToString("yyyy-MM-dd") + "T" + a.AppointmentTime.ToString(@"hh\:mm"),
                    description = $"Mechanic: {a.MechanicName} | Notes: {a.Notes}"
                })
                .ToListAsync();

            return Json(appointments); // Only return once
        }

        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int id, string reason)
        {
            _logger.LogInformation("CancelAppointment called with id: {AppointmentId}, reason: {Reason}", id, reason);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(a => a.Id == id && a.MechanicId == user.Id);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment not found with id: {AppointmentId} for mechanic: {MechanicId}", id, user.Id);
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("MechanicAppointments");
            }

            // Prevent cancellation if already completed
            if (appointment.Status == "Completed")
            {
                _logger.LogWarning("Cannot cancel completed appointment: {AppointmentId}", id);
                TempData["ErrorMessage"] = "You cannot cancel a completed appointment.";
                return RedirectToAction("MechanicAppointments");
            }

            // Update appointment status
            _logger.LogInformation("Cancelling appointment {AppointmentId}", id);
            appointment.Status = "Cancelled";
            appointment.Notes = $"Cancelled by Mechanic. Reason: {reason}";
            await _context.SaveChangesAsync();

            // Send in-app notification
            try
            {
                await _notificationService.SendNotificationToCustomerAndAdminOnAppointmentCancelling(appointment.Id);
                _logger.LogInformation("In-app notification sent for cancelled appointment {AppointmentId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending in-app notification for cancelled appointment {AppointmentId}", id);
            }

            // Get customer information
            var customer = await _userManager.FindByIdAsync(appointment.UserId.ToString());
            if (customer != null)
            {
                var notificationMethods = new List<string>();

                // Send Email Notification
                if (!string.IsNullOrWhiteSpace(customer.Email))
                {
                    _logger.LogInformation("Attempting to send cancellation email to: {CustomerEmail}", customer.Email);
                    try
                    {
                        var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                        await emailService.SendAppointmentCancellationEmailAsync(customer, appointment, reason);

                        notificationMethods.Add("email");
                        _logger.LogInformation("✅ SUCCESS: Appointment cancellation email sent to customer {CustomerEmail} for appointment {AppointmentId}",
                            customer.Email, appointment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ ERROR: Failed to send cancellation email for appointment {AppointmentId} to {CustomerEmail}. Error: {ErrorMessage}",
                            id, customer.Email, ex.Message);
                    }
                }
                else
                {
                    _logger.LogWarning("Customer {CustomerId} does not have an email address", customer.Id);
                }

                // Send Phone Call Notification (optional)
                if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
                {
                    _logger.LogInformation("Attempting to make cancellation phone call to: {CustomerPhone}", customer.PhoneNumber);
                    try
                    {
                        _phoneCallService.MakeCall(customer.PhoneNumber,
                            $"Hello, your appointment on {appointment.AppointmentDate:MMMM dd} at {appointment.AppointmentTime:hh\\:mm} has been cancelled. Please contact us to reschedule.");

                        notificationMethods.Add("phone call");
                        _logger.LogInformation("✅ SUCCESS: Appointment cancellation phone call made to {CustomerPhone} for appointment {AppointmentId}",
                            customer.PhoneNumber, appointment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ ERROR: Failed to make cancellation phone call for appointment {AppointmentId} to {CustomerPhone}. Error: {ErrorMessage}",
                            id, customer.PhoneNumber, ex.Message);
                    }
                }

                // Set success message
                if (notificationMethods.Any())
                {
                    var message = $"Appointment cancelled successfully and customer notified via {string.Join(" and ", notificationMethods)}.";
                    TempData["SuccessMessage"] = message;
                    _logger.LogInformation("✅ OVERALL SUCCESS: {Message}", message);
                }
                else
                {
                    TempData["SuccessMessage"] = "Appointment cancelled successfully.";
                    TempData["WarningMessage"] = "Customer could not be notified - please contact them directly.";
                    _logger.LogWarning("⚠️ WARNING: Appointment cancelled but customer notifications failed");
                }
            }
            else
            {
                _logger.LogError("Customer not found for appointment {AppointmentId}, UserId: {UserId}", id, appointment.UserId);
                TempData["SuccessMessage"] = "Appointment cancelled successfully.";
                TempData["WarningMessage"] = "Customer information not found.";
            }

            return RedirectToAction("MechanicAppointments");
        }


        [HttpGet]
        public async Task<IActionResult> MechanicAppointments()
        {
            ViewData["IsAppointments"] = true;

            var mechanic = await _userManager.GetUserAsync(User);
            if (mechanic == null)
            {
                _logger.LogWarning("Mechanic is null. Unauthorized access attempt.");
                return Unauthorized();
            }

            _logger.LogInformation("Logged-in mechanic ID: {MechanicId}", mechanic.Id);

            // Option 1: Find by ID and MechanicId (preferred)
            // If you want all appointments for this mechanic
            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .ThenInclude(c => c.Owner)
                .Where(a => a.MechanicId == mechanic.Id)
                .ToListAsync();


            _logger.LogInformation("Appointments found for mechanic: {Count}", appointments.Count);

            foreach (var appointment in appointments)
            {
                _logger.LogInformation("Appointment ID: {AppointmentId}, CarID: {CarId}, Date: {Date}, Time: {Time}",
                    appointment.Id, appointment.CarId, appointment.AppointmentDate, appointment.AppointmentTime);

                if (appointment.Car == null)
                {
                    _logger.LogWarning("Appointment {AppointmentId} has no associated car!", appointment.Id);
                }
            }

            var viewModel = new MechanicAppointmentsViewModel
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
                    Car = a.Car != null ? new CarViewModel
                    {
                        Id = a.Car.Id,
                        Make = a.Car.Make,
                        Model = a.Car.Model,
                        LicenseNumber = a.Car.LicenseNumber
                    } : null // Handle null Car reference
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

            // Pass ProfilePicture to ViewData for layout
            ViewData["ProfilePicture"] = user.ProfilePicture ?? "/images/default-profile.png"; // Default picture if null

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

            // Check if new password and confirm password match
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "New Password and Confirm Password must match.");
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            // Handle profile picture update
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
            }

            // Update user details
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.PhoneNumber = model.PhoneNumber;

            // Change password if provided
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
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
            }

            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Settings");
        }

        public async Task<IActionResult> GetCarReports(string searchTerm)
        {
            ViewData["IsCurrentReports"] = true;
            // Store the search term in ViewData to use it in the view
            ViewData["SearchTerm"] = searchTerm;

            // Fetch notifications (your existing code)
            var notifications = await _context.Notifications
                .OrderByDescending(n => n.DateCreated)
                .Select(n => new NotificationViewModel
                {
                    Id = n.Id,
                    Message = n.Message,
                    DateCreated = n.DateCreated,
                    IsRead = n.IsRead
                })
                .ToListAsync();

            ViewBag.Notifications = notifications;
            ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Start with the base query
            var carsQuery = _context.Cars
                .AsNoTracking()
                .Include(c => c.Owner)
                .Where(c => _context.CarMechanicAssignments
                    .Any(a => a.CarId == c.Id && a.MechanicId.ToString() == userId));

            // Apply search filter if specified - ONLY for make and model
            if (!string.IsNullOrEmpty(searchTerm))
            {
                carsQuery = carsQuery.Where(c =>
                    c.Make.Contains(searchTerm) ||
                    c.Model.Contains(searchTerm)
                );
            }

            // Select and map to view model (your existing code)
            var cars = await carsQuery
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
                                    Quantity = mp.Quantity,
                                    OperationCode = mp.OperationCode,
                                    PartNumber = mp.PartNumber,
                                    PartDescription = mp.PartDescription
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToListAsync();

            var model = new MechanicDashboardViewModel
            {
                Cars = cars,
                Notifications = notifications
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateMechanicReport()
        {
            ViewData["IsCurrentReports"] = true;
            // Get logged-in mechanic ID
            int mechanicId;
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out mechanicId))
            {
                TempData["ErrorMessage"] = "Invalid mechanic ID.";
                return RedirectToAction("Dashboard", "Mechanic");
            }

            // Fetch assigned cars for the logged-in mechanic
            var assignedCars = await _context.CarMechanicAssignments
                .Where(a => a.MechanicId == mechanicId)
                .Select(a => new AssignedCarViewModel
                {
                    AssignmemtId = a.Id,
                    CarId = a.Car.Id,
                    CarMake = a.Car.Make,
                    CarModel = a.Car.Model,
                    LicenseNumber = a.Car.LicenseNumber,
                    Year = a.Car.Year,
                    Color = a.Car.Color,
                    MechanicId = a.MechanicId ?? 0,
                    MechanicFullName = a.Mechanic.FullName,
                    AssignedDate = a.AssignedDate,
                    HasReport = _context.MechanicReports.Any(mr => mr.CarId == a.Car.Id),
                    Faults = _context.Faults
                        .Where(f => f.CarId == a.Car.Id)
                        .Select(f => new FaultViewModel
                        {
                            Id = f.Id,
                            Description = f.Description,
                            ResolutionStatus = f.ResolutionStatus,
                            DateReportedOn = f.DateReportedOn
                        })
                        .ToList()
                })
                .ToListAsync();

            // Ensure assignedCars is never null
            var viewModel = new MechanicDashboardViewModel
            {
                MechanicId = mechanicId,
                AssignedCars = assignedCars ?? new List<AssignedCarViewModel>()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMechanicReport(
         int selectedCarId,
         string serviceDetails,
         string additionalNotes,
         List<MechanicReportPart> parts,
         decimal serviceFee,
         string paymentMode = "Cash",
         string customerRequest = null,
         string actionTaken = null,
         string nextServiceAdvice = null,
         int? nextServiceKm = null,
         DateTime? nextServiceDate = null,
         decimal taxRate = 6.00m,
         List<ServiceInspectionItem> inspectionItems = null)
            {
                Console.WriteLine($"Received serviceDetails: '{serviceDetails}' and Service Fee: {serviceFee}");

                // Get the logged-in mechanic's ID
                var mechanicIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(mechanicIdStr, out int mechanicId))
                {
                    TempData["ErrorMessage"] = "Invalid mechanic ID.";
                    return RedirectToAction("Dashboard", "Mechanic");
                }

                if (string.IsNullOrWhiteSpace(serviceDetails))
                {
                    TempData["ErrorMessage"] = "Service details cannot be empty.";
                    return RedirectToAction("CreateMechanicReport");
                }

                if (serviceFee < 0)
                {
                    TempData["ErrorMessage"] = "Service fee cannot be negative.";
                    return RedirectToAction("CreateMechanicReport");
                }

                try
                {
                    // Create the mechanic report
                    var mechanicReport = new MechanicReport
                    {
                        CarId = selectedCarId,
                        MechanicId = mechanicId,
                        ServiceDetails = serviceDetails,
                        AdditionalNotes = additionalNotes ?? string.Empty,
                        DateReported = DateTime.Now,
                        ServiceFee = serviceFee,
                        PaymentMode = paymentMode,
                        CustomerRequest = customerRequest,
                        ActionTaken = actionTaken,
                        NextServiceAdvice = nextServiceAdvice,
                        NextServiceKm = nextServiceKm,
                        NextServiceDate = nextServiceDate,
                        TaxRate = taxRate,
                        Parts = new List<MechanicReportPart>(),
                        LabourItems = new List<MechanicReportLabour>(),
                        InspectionItems = new List<ServiceInspectionItem>()
                    };

                    // Add parts to the report
                    if (parts?.Any() == true)
                    {
                        foreach (var part in parts.Where(p => !string.IsNullOrEmpty(p.PartName)))
                        {
                            // Get current price from database to ensure accuracy
                            var servicePart = await _context.ServiceParts
                                .FirstOrDefaultAsync(sp => sp.PartNumber == part.PartNumber);

                            var reportPart = new MechanicReportPart
                            {
                                PartName = part.PartName,
                                PartPrice = servicePart?.Price ?? part.PartPrice, // Use current DB price
                                Quantity = part.Quantity,
                                CarId = selectedCarId,
                                OperationCode = part.OperationCode,
                                PartNumber = part.PartNumber,
                                PartDescription = servicePart?.PartDescription ?? part.PartDescription
                            };

                            mechanicReport.Parts.Add(reportPart);
                        }
                    }

                    // Add inspection items (optional)
                    if (inspectionItems?.Any() == true)
                    {
                        foreach (var inspection in inspectionItems.Where(i => !string.IsNullOrEmpty(i.ItemName)))
                        {
                            mechanicReport.InspectionItems.Add(new ServiceInspectionItem
                            {
                                ItemName = inspection.ItemName,
                                Result = inspection.Result,
                                Status = inspection.Status,
                                Recommendations = inspection.Recommendations
                            });
                        }
                    }

                    _context.MechanicReports.Add(mechanicReport);
                    await _context.SaveChangesAsync();

                    await _notificationService.SendNotificationToCustomerAndAdminOnMechanicReport(mechanicReport.Id);

                    TempData["SuccessMessage"] = "Mechanic report created successfully.";
                    return RedirectToAction("GetCarReports", "Mechanic");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating mechanic report");
                    TempData["ErrorMessage"] = "An error occurred while creating the report.";
                    return RedirectToAction("CreateMechanicReport");
                }
            }



        [HttpGet]
        public async Task<IActionResult> GetPartsByOperationCode(string operationCode)
        {
            try
            {
                _logger.LogInformation("Fetching parts for operation code: {OperationCode}", operationCode);

                if (string.IsNullOrWhiteSpace(operationCode))
                {
                    _logger.LogWarning("Operation code is null or empty");
                    return Json(new List<object>());
                }

                // First, let's check if the operation code exists
                var operationCodeExists = await _context.OperationCodes
                    .AnyAsync(oc => oc.Code == operationCode && oc.IsActive);

                if (!operationCodeExists)
                {
                    _logger.LogWarning("Operation code {OperationCode} not found or inactive", operationCode);
                    return Json(new List<object>());
                }

                // Alternative approach: Start from OperationCodeParts junction table
                var parts = await _context.OperationCodeParts
                    .Include(ocp => ocp.OperationCode)
                    .Include(ocp => ocp.ServicePart)
                    .Where(ocp => ocp.OperationCode.Code == operationCode &&
                                 ocp.OperationCode.IsActive &&
                                 ocp.ServicePart.IsAvailable)
                    .Select(ocp => new
                    {
                        partNumber = ocp.ServicePart.PartNumber,
                        partName = ocp.ServicePart.PartName,
                        price = ocp.ServicePart.Price,
                        partDescription = ocp.ServicePart.PartDescription ?? "",
                        isDefault = ocp.IsDefault
                    })
                    .OrderBy(p => p.partName)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} parts for operation code {OperationCode}", parts.Count, operationCode);

                // Log each part for debugging
                foreach (var part in parts)
                {
                    _logger.LogDebug("Part: {PartNumber} - {PartName} - ${Price}",
                        part.partNumber, part.partName, part.price);
                }

                return Json(parts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving parts for operation code: {OperationCode}", operationCode);
                return Json(new List<object>());
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetPartDetails(string partNumber)
        {
            try
            {
                _logger.LogInformation("Fetching part details for part number: {PartNumber}", partNumber);

                if (string.IsNullOrWhiteSpace(partNumber))
                {
                    _logger.LogWarning("Part number is null or empty");
                    return Json(new { partName = "", price = 0.00m, partDescription = "" });
                }

                var part = await _context.ServiceParts
                    .Where(sp => sp.PartNumber == partNumber && sp.IsAvailable)
                    .Select(sp => new
                    {
                        partName = sp.PartName,
                        price = sp.Price,
                        partDescription = sp.PartDescription ?? ""
                    })
                    .FirstOrDefaultAsync();

                if (part != null)
                {
                    _logger.LogInformation("Found part: {PartName} - ${Price}", part.partName, part.price);
                    return Json(part);
                }
                else
                {
                    _logger.LogWarning("Part not found for part number: {PartNumber}", partNumber);
                    return Json(new { partName = "", price = 0.00m, partDescription = "" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving part details for part number: {PartNumber}", partNumber);
                return Json(new { partName = "", price = 0.00m, partDescription = "" });
            }
        }

        // Add this method to test database connectivity and seed data
        [HttpGet]
        public async Task<IActionResult> TestOperationCodeData()
        {
            try
            {
                var operationCodesCount = await _context.OperationCodes.CountAsync();
                var servicePartsCount = await _context.ServiceParts.CountAsync();
                var operationCodePartsCount = await _context.OperationCodeParts.CountAsync();

                var testData = new
                {
                    OperationCodesCount = operationCodesCount,
                    ServicePartsCount = servicePartsCount,
                    OperationCodePartsCount = operationCodePartsCount,
                    OperationCodes = await _context.OperationCodes
                        .Select(oc => new { oc.Id, oc.Code, oc.Name, oc.IsActive })
                        .ToListAsync(),
                    ServiceParts = await _context.ServiceParts
                        .Take(10)
                        .Select(sp => new { sp.Id, sp.PartNumber, sp.PartName, sp.Price, sp.IsAvailable })
                        .ToListAsync(),
                    OperationCodeParts = await _context.OperationCodeParts
                        .Include(ocp => ocp.OperationCode)
                        .Include(ocp => ocp.ServicePart)
                        .Take(10)
                        .Select(ocp => new
                        {
                            ocp.Id,
                            OperationCode = ocp.OperationCode.Code,
                            OperationCodeName = ocp.OperationCode.Name,
                            PartNumber = ocp.ServicePart.PartNumber,
                            PartName = ocp.ServicePart.PartName,
                            PartPrice = ocp.ServicePart.Price,
                            IsDefault = ocp.IsDefault
                        })
                        .ToListAsync(),
                    // Test specific operation code parts
                    FLRS10Parts = await _context.OperationCodeParts
                        .Include(ocp => ocp.OperationCode)
                        .Include(ocp => ocp.ServicePart)
                        .Where(ocp => ocp.OperationCode.Code == "FLRS10")
                        .Select(ocp => new
                        {
                            PartNumber = ocp.ServicePart.PartNumber,
                            PartName = ocp.ServicePart.PartName,
                            PartPrice = ocp.ServicePart.Price,
                            IsAvailable = ocp.ServicePart.IsAvailable
                        })
                        .ToListAsync()
                };

                return Json(testData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing operation code data");
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DebugOperationCodes()
        {
            try
            {
                // Test raw queries
                var operationCodes = await _context.OperationCodes.ToListAsync();
                var serviceParts = await _context.ServiceParts.ToListAsync();
                var junctionTable = await _context.OperationCodeParts
                    .Include(ocp => ocp.OperationCode)
                    .Include(ocp => ocp.ServicePart)
                    .ToListAsync();

                var debug = new
                {
                    OperationCodesRaw = operationCodes.Select(oc => new {
                        oc.Id,
                        oc.Code,
                        oc.Name,
                        oc.IsActive,
                        oc.Description
                    }),
                    ServicePartsRaw = serviceParts.Select(sp => new {
                        sp.Id,
                        sp.PartNumber,
                        sp.PartName,
                        sp.Price,
                        sp.IsAvailable,
                        sp.PartDescription
                    }),
                    JunctionTableRaw = junctionTable.Select(jt => new {
                        jt.Id,
                        OperationCodeId = jt.OperationCodeId,
                        OperationCode = jt.OperationCode?.Code,
                        ServicePartId = jt.ServicePartId,
                        PartNumber = jt.ServicePart?.PartNumber,
                        PartName = jt.ServicePart?.PartName,
                        PartPrice = jt.ServicePart?.Price,
                        jt.IsDefault
                    }),
                    // Test the problematic query
                    FLRS10Test = await _context.OperationCodeParts
                        .Where(ocp => ocp.OperationCode.Code == "FLRS10")
                        .Include(ocp => ocp.ServicePart)
                        .Select(ocp => new {
                            ocp.ServicePart.PartNumber,
                            ocp.ServicePart.PartName,
                            ocp.ServicePart.Price
                        })
                        .ToListAsync()
                };

                return Json(debug);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveOperationCodes()
        {
            try
            {
                _logger.LogInformation("Fetching active operation codes");

                var operationCodes = await _context.OperationCodes
                    .Where(oc => oc.IsActive)
                    .Select(oc => new
                    {
                        code = oc.Code,
                        name = oc.Name,
                        description = oc.Description
                    })
                    .OrderBy(oc => oc.code)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} active operation codes", operationCodes.Count);

                return Json(operationCodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active operation codes");
                return Json(new List<object>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReport(EditReportViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var report = await _context.MechanicReports
                .Include(r => r.Parts)
                .Include(r => r.LabourItems)
                .Include(r => r.InspectionItems)
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (report == null)
            {
                return NotFound();
            }

            // Update basic report details
            report.ServiceDetails = model.ServiceDetails;
            report.AdditionalNotes = model.AdditionalNotes;
            report.ServiceFee = model.ServiceFee;

            // Update receipt fields
            report.PaymentMode = model.PaymentMode ?? "Cash";
            report.CustomerRequest = model.CustomerRequest;
            report.ActionTaken = model.ActionTaken;
            report.NextServiceAdvice = model.NextServiceAdvice;
            report.NextServiceKm = model.NextServiceKm;
            report.NextServiceDate = model.NextServiceDate;
            report.TaxRate = model.TaxRate;

            // Update Parts
            var existingParts = report.Parts.ToList();
            foreach (var existingPart in existingParts)
            {
                if (!model.Parts.Any(p => p.PartName == existingPart.PartName))
                {
                    _context.MechanicReportParts.Remove(existingPart);
                }
            }

            foreach (var part in model.Parts)
            {
                var existingPart = existingParts.FirstOrDefault(p => p.PartName == part.PartName);
                if (existingPart != null)
                {
                    // Update existing part
                    existingPart.PartPrice = part.PartPrice;
                    existingPart.Quantity = part.Quantity;
                    existingPart.OperationCode = part.OperationCode;
                    existingPart.PartNumber = part.PartNumber;
                    existingPart.PartDescription = part.PartDescription;
                }
                else
                {
                    // Add new part
                    report.Parts.Add(new MechanicReportPart
                    {
                        PartName = part.PartName,
                        PartPrice = part.PartPrice,
                        Quantity = part.Quantity,
                        OperationCode = part.OperationCode,
                        PartNumber = part.PartNumber,
                        PartDescription = part.PartDescription,
                        MechanicReportId = report.Id
                    });
                }
            }

            // Update Labour Items
            if (model.LabourItems != null)
            {
                var existingLabour = report.LabourItems.ToList();
                foreach (var existing in existingLabour)
                {
                    if (!model.LabourItems.Any(l => l.OperationCode == existing.OperationCode))
                    {
                        _context.MechanicReportLabours.Remove(existing);
                    }
                }

                foreach (var labour in model.LabourItems)
                {
                    var existing = existingLabour.FirstOrDefault(l => l.OperationCode == labour.OperationCode);
                    if (existing != null)
                    {
                        // Update existing labour item
                        existing.Description = labour.Description;
                        existing.TotalAmountWithoutTax = labour.TotalAmountWithoutTax;
                        existing.TaxRate = labour.TaxRate;
                        existing.TaxAmount = labour.TotalAmountWithoutTax * (labour.TaxRate / 100);
                    }
                    else
                    {
                        // Add new labour item
                        report.LabourItems.Add(new MechanicReportLabour
                        {
                            OperationCode = labour.OperationCode,
                            Description = labour.Description,
                            TotalAmountWithoutTax = labour.TotalAmountWithoutTax,
                            TaxRate = labour.TaxRate,
                            TaxAmount = labour.TotalAmountWithoutTax * (labour.TaxRate / 100),
                            MechanicReportId = report.Id
                        });
                    }
                }
            }

            // Update Inspection Items
            if (model.InspectionItems != null)
            {
                var existingInspection = report.InspectionItems.ToList();
                foreach (var existing in existingInspection)
                {
                    if (!model.InspectionItems.Any(i => i.ItemName == existing.ItemName))
                    {
                        _context.ServiceInspectionItems.Remove(existing);
                    }
                }

                foreach (var inspection in model.InspectionItems)
                {
                    var existing = existingInspection.FirstOrDefault(i => i.ItemName == inspection.ItemName);
                    if (existing != null)
                    {
                        // Update existing inspection item
                        existing.Result = inspection.Result;
                        existing.Status = inspection.Status;
                        existing.Recommendations = inspection.Recommendations;
                    }
                    else
                    {
                        // Add new inspection item
                        report.InspectionItems.Add(new ServiceInspectionItem
                        {
                            ItemName = inspection.ItemName,
                            Result = inspection.Result,
                            Status = inspection.Status,
                            Recommendations = inspection.Recommendations,
                            MechanicReportId = report.Id
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            await _notificationService.SendNotificationToCustomerAndAdminOnMechanicReportEditing(report.Id);

            TempData["SuccessMessage"] = "Enhanced report updated successfully.";
            return RedirectToAction("GetCarReports", "Mechanic");
        }

        [HttpGet]
        public async Task<IActionResult> EditReport(int id)
        {
            ViewData["IsCurrentReports"] = true;

            // Ensure the user is a mechanic
            var mechanicId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(mechanicId))
                return Unauthorized();

            try
            {
                // Use Include to load related data and handle nulls properly
                var report = await _context.MechanicReports
                    .Include(r => r.Parts)
                    .Include(r => r.LabourItems)
                    .Include(r => r.InspectionItems)
                    .FirstOrDefaultAsync(r => r.Id == id && r.MechanicId.ToString() == mechanicId);

                if (report == null)
                    return NotFound();

                // Create the view model with proper null handling
                var vm = new EditReportViewModel
                {
                    Id = report.Id,
                    ServiceDetails = report.ServiceDetails ?? "",
                    AdditionalNotes = report.AdditionalNotes ?? "",
                    ServiceFee = report.ServiceFee,

                    // Receipt fields with null checks
                    PaymentMode = report.PaymentMode ?? "Cash",
                    CustomerRequest = report.CustomerRequest ?? "",
                    ActionTaken = report.ActionTaken ?? "",
                    NextServiceAdvice = report.NextServiceAdvice ?? "",
                    NextServiceKm = report.NextServiceKm,
                    NextServiceDate = report.NextServiceDate,
                    TaxRate = report.TaxRate,

                    // Parts with enhanced fields and null handling
                    Parts = report.Parts?.Select(p => new PartViewModel
                    {
                        PartName = p.PartName ?? "",
                        PartPrice = p.PartPrice,
                        Quantity = p.Quantity,
                        OperationCode = p.OperationCode ?? "",
                        PartNumber = p.PartNumber ?? "",
                        PartDescription = p.PartDescription ?? ""
                    }).ToList() ?? new List<PartViewModel>(),

                    // Labour items with null handling
                    LabourItems = report.LabourItems?.Select(l => new LabourItemViewModel
                    {
                        OperationCode = l.OperationCode ?? "",
                        Description = l.Description ?? "",
                        TotalAmountWithoutTax = l.TotalAmountWithoutTax,
                        TaxRate = l.TaxRate,
                        TaxAmount = l.TaxAmount
                    }).ToList() ?? new List<LabourItemViewModel>(),

                    // Inspection items with null handling
                    InspectionItems = report.InspectionItems?.Select(i => new InspectionItemViewModel
                    {
                        ItemName = i.ItemName ?? "",
                        Result = i.Result ?? "",
                        Status = i.Status ?? "OK",
                        Recommendations = i.Recommendations ?? ""
                    }).ToList() ?? new List<InspectionItemViewModel>()
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                _logger.LogError(ex, "Error retrieving mechanic report for editing. ReportId: {ReportId}, MechanicId: {MechanicId}", id, mechanicId);

                TempData["ErrorMessage"] = "An error occurred while loading the report for editing.";
                return RedirectToAction("GetCarReports");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // 🔹 Get the logged-in mechanic's ID
            var mechanicId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(mechanicId))
            {
                Console.WriteLine("MechanicId is null or empty. Ensure user is logged in.");
                return Unauthorized();
            }

            // 🔹 Fetch assigned cars for the mechanic from the database
            var assignedCars = await _context.CarMechanicAssignments
                .Where(cma => cma.MechanicId.ToString() == mechanicId)
                .Include(cma => cma.Car)
                    .ThenInclude(car => car.Faults) // ✅ Ensure faults are loaded
                .ToListAsync();

            // 🔹 Convert to AssignedCarViewModel
            var assignedCarViewModels = await _context.CarMechanicAssignments
              .Where(cma => cma.MechanicId.ToString() == mechanicId)
              .Select(cma => new AssignedCarViewModel
              {
                  AssignmemtId = cma.Id,
                  CarId = cma.CarId,
                  CarMake = cma.Car.Make ?? "Unknown",
                  CarModel = cma.Car.Model ?? "Unknown",
                  LicenseNumber = cma.Car.LicenseNumber ?? "N/A",
                  Year = cma.Car.Year,
                  Color = cma.Car.Color ?? "Unspecified", // ✅
                  ChassisNumber = cma.Car.ChassisNumber ?? "Unknown", // ✅
                  AssignedDate = cma.AssignedDate,
                  Faults = cma.Car.Faults.Select(f => new FaultViewModel
                  {
                      Id = f.Id,
                      CarId = f.CarId,
                      DateReportedOn = f.DateReportedOn,
                      ResolutionStatus = f.ResolutionStatus,
                      Description = f.Description ?? ""
                  }).ToList()
              })

              .ToListAsync();


           

            // 🔹 Convert to MechanicReportViewModel
            var reportViewModels = await _context.MechanicReports
            .Where(r => r.MechanicId.ToString() == mechanicId)
            .Select(r => new MechanicReportViewModel
            {
                Id = r.Id,
                CarId = r.CarId,
                CarMake = r.Car!.Make ?? "Unknown",
                CarModel = r.Car.Model ?? "Unknown",
                CarYear = r.Car.Year,
                MechanicId = r.MechanicId ?? 0,
                MechanicName = r.Mechanic!.FullName ?? "Unknown",
                DateReported = r.DateReported,
                ServiceDetails = r.ServiceDetails ?? "",
                AdditionalNotes = r.AdditionalNotes ?? "",
                Parts = r.Parts.Select(p => new MechanicReportPartViewModel
                {
                    PartName = p.PartName ?? "",
                    PartPrice = p.PartPrice,
                    Quantity = p.Quantity
                }).ToList(),
                TotalPrice = r.Parts.Sum(p => p.PartPrice * p.Quantity)
            })
            .ToListAsync();

            // now you have your final list — no need for .Include() or a second Select()


            // 🔹 Fetch notifications for the mechanic
            var notifications = await _context.Notifications
                .Where(n => n.UserId.ToString() == mechanicId)
                .OrderByDescending(n => n.DateCreated)
                .Select(n => new NotificationViewModel
                {
                    Id = n.Id,
                    Message = n.Message,
                    DateCreated = n.DateCreated,
                    IsRead = n.IsRead
                })
                .ToListAsync();

            // 🔹 Combine the data into a single view model
            var viewModel = new MechanicDashboardViewModel
            {
                AssignedCars = assignedCarViewModels,  // Use the converted list
                Reports = reportViewModels,            // Use the converted list
                Notifications = notifications,
                MyTable = new DataTable()
                // Pass the notifications to the view model
            };

            viewModel.MyTable.Columns.Add("Id", typeof(int));
            viewModel.MyTable.Columns.Add("Make", typeof(string));
            viewModel.MyTable.Columns.Add("Model", typeof(string));
            viewModel.MyTable.Columns.Add("LicenseNumber", typeof(string));
            viewModel.MyTable.Columns.Add("Year", typeof(int));
            viewModel.MyTable.Columns.Add("Color", typeof(string));           // ✅ New
            viewModel.MyTable.Columns.Add("ChassisNumber", typeof(string));   // ✅ New
            viewModel.MyTable.Columns.Add("AssignedDate", typeof(string));



            // Fill rows
            // Replace `ass` with the correct variable
            foreach (var car in assignedCarViewModels)
            {
                viewModel.MyTable.Rows.Add(
                    car.CarId,
                    car.CarMake,
                    car.CarModel,
                    car.LicenseNumber,
                    car.Year,
                    car.Color,
                    car.ChassisNumber,
                    car.AssignedDate.ToShortDateString()
                );
            }


            // Pass the data to the view
            ViewBag.TotalAssignedCars = assignedCars.Count();  // Count from the original list
            ViewBag.TotalReportsMade = reportViewModels.Count();  // Count from the converted list instead

            return View(viewModel); // ✅ Pass the correct model to the view
        }


        [HttpGet]
        public async Task<IActionResult> AssignedList(string searchTerm)
        {
            ViewData["IsMechanicList"] = true;
            var mechanicId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(mechanicId))
            {
                Console.WriteLine("MechanicId is null or empty. Ensure user is logged in.");
                return Unauthorized();
            }

            // Fetch assigned cars for the mechanic from the database
            var assignedCars = await _context.CarMechanicAssignments
                .Where(cma => cma.MechanicId.ToString() == mechanicId)
                .Include(cma => cma.Car)
                    .ThenInclude(car => car.Faults)
                .ToListAsync();

            // Convert to AssignedCarViewModel
            var assignedCarViewModels = assignedCars.Select(cma => new AssignedCarViewModel
            {
                AssignmemtId = cma.Id,
                CarId = cma.CarId,
                CarMake = cma.Car.Make,
                CarModel = cma.Car.Model,
                LicenseNumber = cma.Car.LicenseNumber,
                Year = cma.Car.Year,
                Color = cma.Car.Color,
                ChassisNumber = cma.Car.ChassisNumber,
                AssignedDate = cma.AssignedDate,
                Faults = cma.Car.Faults.Select(f => new FaultViewModel
                {
                    Id = f.Id,
                    CarId = f.CarId,
                    DateReportedOn = f.DateReportedOn,
                    ResolutionStatus = f.ResolutionStatus,
                    Description = f.Description
                }).ToList()
            }).ToList();

            // Apply search term filter if provided
            if (!string.IsNullOrEmpty(searchTerm))
            {
                assignedCarViewModels = assignedCarViewModels
                    .Where(c => c.CarMake.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                c.CarModel.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Pass filtered list to the view model
            var viewModel = new MechanicDashboardViewModel
            {
                AssignedCars = assignedCarViewModels,
                MyTable = new DataTable()
                // Pass the notifications to the view model
            };

            viewModel.MyTable.Columns.Add("Id", typeof(int));
            viewModel.MyTable.Columns.Add("Make", typeof(string));
            viewModel.MyTable.Columns.Add("Model", typeof(string));
            viewModel.MyTable.Columns.Add("LicenseNumber", typeof(string));
            viewModel.MyTable.Columns.Add("Year", typeof(int));
            viewModel.MyTable.Columns.Add("Color", typeof(string));           // ✅ New
            viewModel.MyTable.Columns.Add("ChassisNumber", typeof(string));   // ✅ New
            viewModel.MyTable.Columns.Add("AssignedDate", typeof(string));



            // Fill rows
            // Replace `ass` with the correct variable
            foreach (var car in assignedCarViewModels)
            {
                viewModel.MyTable.Rows.Add(
                    car.CarId,
                    car.CarMake,
                    car.CarModel,
                    car.LicenseNumber,
                    car.Year,
                    car.Color,
                    car.ChassisNumber,
                    car.AssignedDate.ToShortDateString()
                );
            }

            return View(viewModel);
        }

        public async Task<IActionResult> MechanicDashboardAsync(string sortOrder, string searchTerm)
        {
            // Get the logged-in mechanic's ID from Claims and convert it to int?
            var mechanicIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(mechanicIdString, out int mechanicId))
            {
                // Retrieve assigned cars for the logged-in mechanic
                var assignedCars = await _context.Cars
                    .Where(c => c.MechanicId == mechanicId)  // Filter by MechanicId
                    .Include(c => c.Mechanic)  // Optionally include the mechanic data
                    .Include(c => c.Owner)     // Optionally include the car owner data if needed
                    .ToListAsync();           // Fetch the cars assigned to the mechanic

                // Apply search term filter if provided
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    assignedCars = assignedCars
                        .Where(c => c.Make.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                    c.Model.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Sorting logic based on sortOrder
                switch (sortOrder)
                {
                    case "asc":
                        assignedCars = assignedCars.OrderBy(c => c.Make).ToList(); // Sort by CarMake in ascending order
                        break;
                    case "desc":
                        assignedCars = assignedCars.OrderByDescending(c => c.Make).ToList(); // Sort by CarMake in descending order
                        break;
                    default:
                        assignedCars = assignedCars.OrderBy(c => c.Make).ToList(); // Default sorting in ascending order
                        break;
                }

                // Map Car models to AssignedCarViewModel
                var assignedCarViewModels = assignedCars.Select(c => new AssignedCarViewModel
                {
                    Id = c.Id,
                    Make = c.Make,
                    Model = c.Model,
                    Year = c.Year,
                    Color = c.Color,
                    LicenseNumber = c.LicenseNumber
                    // Map other properties as needed
                }).ToList();

                // Create the view model for the dashboard
                var model = new MechanicDashboardViewModel
                {
                    AssignedCars = assignedCarViewModels
                };

                return View(model);
            }
            else
            {
                // Handle the case where the mechanicId is invalid (not a number)
                return BadRequest("Invalid mechanic ID.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Fault(int carId)
        {
            ViewData["IsMechanicList"] = true;

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var mechanicId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var assignedCars = await _context.CarMechanicAssignments
                .Where(cma => cma.MechanicId == userId)
                .Select(cma => new AssignedCarViewModel
                {
                    CarId = cma.CarId,
                    CarMake = cma.Car.Make,
                    CarModel = cma.Car.Model,
                    LicenseNumber = cma.Car.LicenseNumber,
                    Year = cma.Car.Year,
                    Color = cma.Car.Color,
                    AssignedDate = cma.AssignedDate,
                    MechanicId = cma.MechanicId.GetValueOrDefault(),
                    MechanicFullName = cma.Mechanic.FullName,
                    HasReport = cma.Car.Reports.Any(),
                    Faults = cma.Car.Faults.Select(f => new FaultViewModel
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

            // 🔹 Fetch notifications for the mechanic
            var notifications = await _context.Notifications
                .Where(n => n.UserId.ToString() == mechanicId)
                .OrderByDescending(n => n.DateCreated)
                .Select(n => new NotificationViewModel
                {
                    Id = n.Id,
                    Message = n.Message,
                    DateCreated = n.DateCreated,
                    IsRead = n.IsRead
                })
                .ToListAsync();

            
            var model = new AssignFaultViewModel
            {
                Faults = faults,
                NewFault = reportFaultViewModel,
                AssignedCars = assignedCars, // <<== directly reuse assignedCars
                Notifications = notifications
            };

            return View(model);
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

            // Fetch the updated list of faults for the car
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

            // Return the updated list as a JSON response
            return Json(new { success = true, faults = updatedFaults });
        }

        [HttpGet]
        public async Task<IActionResult> MechanicNotificationViewPage()
        {
            var user = await _userManager.GetUserAsync(User);
            // If administrators have profile pictures, include the following line:
            // ViewBag.ProfilePicturePath = user.ProfilePicture;
            return View();
        }
    }
}
