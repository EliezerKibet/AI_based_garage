using GarageManagementSystem.Data;
using GarageManagementSystem.Models;
using GarageManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GarageManagementSystem.Services;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using GarageManagementSystem.Models.ViewModels;

namespace GarageManagementSystem.Controllers
{
    public class AdminController : BaseAdminController
    {
        private readonly PhoneCallService _phoneCallService;
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IAlertService _alertService;
        private readonly NotificationService _notificationService;
        public DataTable MyTable { get; set; } = new();

        public AdminController(UserManager<Users> userManager, AppDbContext context, ILogger<AdminController> logger, PhoneCallService phoneCallService, IAlertService alertService, NotificationService notificationService)
            : base(context, userManager, logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _phoneCallService = phoneCallService;
            _alertService = alertService;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> AdminSearch(string query)
        {
            // Handle null query
            if (query == null)
            {
                query = "";
            }
            else
            {
                query = query.ToLower();
            }
            try
            {
                // 1. First try to get users
                var users = new List<Users>();
                try
                {
                    // Filter at database level instead of loading all records
                    users = await _userManager.Users
                        .Where(u =>
                            EF.Functions.Like(u.FullName ?? "", $"%{query}%") ||
                            EF.Functions.Like(u.Email ?? "", $"%{query}%") ||
                            EF.Functions.Like(u.PhoneNumber ?? "", $"%{query}%")
                        )
                        .Take(100) // Limit results for performance
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // Log the error but continue
                    System.Diagnostics.Debug.WriteLine($"Error fetching users: {ex.Message}");
                }

                // 2. Try to get cars with owners
                var cars = new List<Car>();
                try
                {
                    // Use Include to load owners - works better with the UI requirements
                    cars = await _context.Cars
                        .Include(c => c.Owner)
                        .Where(c =>
                            EF.Functions.Like(c.Make ?? "", $"%{query}%") ||
                            EF.Functions.Like(c.Model ?? "", $"%{query}%") ||
                            EF.Functions.Like(c.LicenseNumber ?? "", $"%{query}%") ||
                            EF.Functions.Like(c.Color ?? "", $"%{query}%") ||
                            c.Year.ToString().Contains(query) ||
                            EF.Functions.Like(c.ChassisNumber ?? "", $"%{query}%") ||
                            EF.Functions.Like(c.Owner.FullName ?? "", $"%{query}%")
                        )
                        .Take(100) // Limit results
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // Try alternative approach if the Include causes issues
                    try
                    {
                        // First get all matching cars without includes
                        var allCars = await _context.Cars
                            .Where(c =>
                                EF.Functions.Like(c.Make ?? "", $"%{query}%") ||
                                EF.Functions.Like(c.Model ?? "", $"%{query}%") ||
                                EF.Functions.Like(c.LicenseNumber ?? "", $"%{query}%") ||
                                EF.Functions.Like(c.Color ?? "", $"%{query}%") ||
                                c.Year.ToString().Contains(query) ||
                                EF.Functions.Like(c.ChassisNumber ?? "", $"%{query}%")
                            )
                            .Take(100)
                            .ToListAsync();

                        // Get owner IDs from the filtered cars
                        var ownerIds = allCars
                            .Where(c => c.OwnerId.HasValue)
                            .Select(c => c.OwnerId.Value)
                            .Distinct()
                            .ToList();

                        // Get the owners in a separate query
                        if (ownerIds.Any())
                        {
                            var owners = await _userManager.Users
                                .Where(u => ownerIds.Contains(u.Id))
                                .ToDictionaryAsync(u => u.Id, u => u);

                            // Manually assign owners
                            foreach (var car in allCars)
                            {
                                if (car.OwnerId.HasValue && owners.ContainsKey(car.OwnerId.Value))
                                {
                                    car.Owner = owners[car.OwnerId.Value];
                                }
                            }
                        }

                        // Also check for owner name matches
                        if (!string.IsNullOrEmpty(query))
                        {
                            var ownersWithMatchingName = await _userManager.Users
                                .Where(u => EF.Functions.Like(u.FullName ?? "", $"%{query}%"))
                                .ToListAsync();

                            if (ownersWithMatchingName.Any())
                            {
                                var ownerNameIds = ownersWithMatchingName.Select(u => u.Id).ToList();
                                var carsWithMatchingOwners = await _context.Cars
                                    .Where(c => c.OwnerId.HasValue && ownerNameIds.Contains(c.OwnerId.Value))
                                    .Take(50)
                                    .ToListAsync();

                                // Add owners to these cars
                                var ownerDict = ownersWithMatchingName.ToDictionary(u => u.Id, u => u);
                                foreach (var car in carsWithMatchingOwners)
                                {
                                    if (car.OwnerId.HasValue && ownerDict.ContainsKey(car.OwnerId.Value))
                                    {
                                        car.Owner = ownerDict[car.OwnerId.Value];
                                    }
                                }

                                // Add these cars to our results (avoid duplicates)
                                var existingCarIds = allCars.Select(c => c.Id).ToHashSet();
                                foreach (var car in carsWithMatchingOwners)
                                {
                                    if (!existingCarIds.Contains(car.Id))
                                    {
                                        allCars.Add(car);
                                    }
                                }
                            }
                        }

                        cars = allCars;
                    }
                    catch (Exception ex2)
                    {
                        // If both approaches fail, log and continue with empty list
                        System.Diagnostics.Debug.WriteLine($"Error with fallback car query: {ex2.Message}");
                        cars = new List<Car>();
                    }
                }

                // 3. Try to get faults with car info
                var faults = new List<Fault>();
                try
                {
                    faults = await _context.Faults
                        .Include(f => f.Car)
                        .Where(f =>
                            EF.Functions.Like(f.Description ?? "", $"%{query}%") ||
                            EF.Functions.Like(f.Car.Make ?? "", $"%{query}%") ||
                            EF.Functions.Like(f.Car.Model ?? "", $"%{query}%") ||
                            EF.Functions.Like(f.Car.LicenseNumber ?? "", $"%{query}%")
                        )
                        .Take(100)
                        .OrderByDescending(f => f.DateReportedOn)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // Log the error but continue
                    System.Diagnostics.Debug.WriteLine($"Error fetching faults: {ex.Message}");
                }

                // 4. Try to get reports with car, mechanic and parts
                var reports = new List<MechanicReport>();
                try
                {
                    reports = await _context.MechanicReports
                        .Include(r => r.Car)
                        .Include(r => r.Mechanic)
                        .Include(r => r.Parts)
                        .Where(r =>
                            EF.Functions.Like(r.ServiceDetails ?? "", $"%{query}%") ||
                            EF.Functions.Like(r.AdditionalNotes ?? "", $"%{query}%") ||
                            EF.Functions.Like(r.Mechanic.FullName ?? "", $"%{query}%") ||
                            r.Parts.Any(p =>
                                EF.Functions.Like(p.PartName ?? "", $"%{query}%") ||
                                p.PartPrice.ToString().Contains(query) ||
                                p.Quantity.ToString().Contains(query)
                            )
                        )
                        .Take(100)
                        .OrderByDescending(r => r.DateReported)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // Log the error but continue
                    System.Diagnostics.Debug.WriteLine($"Error fetching reports: {ex.Message}");
                }

                // 5. Try to get appointments with car and user info
                var appointments = new List<Appointment>();
                try
                {
                    appointments = await _context.Appointments
                        .Include(a => a.Car)
                        .Include(a => a.User)
                        .Where(a =>
                            EF.Functions.Like(a.Status ?? "", $"%{query}%") ||
                            a.AppointmentDate.ToString().Contains(query) ||
                            EF.Functions.Like(a.Car.Make ?? "", $"%{query}%") ||
                            EF.Functions.Like(a.Car.Model ?? "", $"%{query}%") ||
                            EF.Functions.Like(a.Car.LicenseNumber ?? "", $"%{query}%") ||
                            EF.Functions.Like(a.User.FullName ?? "", $"%{query}%")
                        )
                        .Take(100)
                        .OrderBy(a => a.AppointmentDate)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // Log the error but continue
                    System.Diagnostics.Debug.WriteLine($"Error fetching appointments: {ex.Message}");
                }

                var viewModel = new AdminSearchViewModel
                {
                    Query = query,
                    MatchingUsers = users,
                    MatchingCars = cars,
                    MatchingFaults = faults,
                    MatchingReports = reports,
                    MatchingAppointments = appointments
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Return an error view if everything fails
                return Content($"Search error: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> LockUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User ID is required.";
                return RedirectToAction("Dashboard");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Dashboard");
            }

            // Lock the user account for 100 years (effectively permanent until unlocked)
            var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User {user.UserName} has been locked successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to lock user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> UnlockUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User ID is required.";
                return RedirectToAction("Dashboard");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Dashboard");
            }

            // Set lockout end date to null to unlock the account
            var result = await _userManager.SetLockoutEndDateAsync(user, null);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User {user.UserName} has been unlocked successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to unlock user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToAction("Dashboard");
        }


        public async Task<IActionResult> ViewAllCars()
        {
            var cars = await _context.Cars.Include(c => c.User).ToListAsync();
            return View(cars);
        }

        public async Task<IActionResult> ViewMechanics()
        {
            var mechanics = await _userManager.GetUsersInRoleAsync("Mechanic");
            return View(mechanics);
        }


        /// <summary>
        /// Fetches all appointments for Admin.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllAppointments()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .Select(a => new
                {
                    id = a.Id,
                    title = $"{a.Car.Make} {a.Car.Model}",
                    start = a.AppointmentDate.ToString("yyyy-MM-dd") + "T" + a.AppointmentTime.ToString(@"hh\:mm"),
                    description = $"Mechanic: {a.MechanicName ?? "Not Assigned"} | Notes: {a.Notes}"
                })
                .ToListAsync();

            return Json(appointments);
        }


        [HttpGet("appointments/details/{appointmentId}")]
        public async Task<IActionResult> GetAppointmentDetails(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Car) // Include Car details
                .FirstOrDefaultAsync(a => a.Id == appointmentId); // Fetch the appointment by ID

            if (appointment == null)
            {
                return NotFound(); // Return 404 if the appointment is not found
            }

            var appointmentDetails = new
            {
                Car = new
                {
                    Make = appointment.Car.Make,
                    Model = appointment.Car.Model,
                    LicenseNumber = appointment.Car.LicenseNumber
                },
                AppointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
                AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"), // Format the time as well
                Status = appointment.Status,
                MechanicName = appointment.MechanicName ?? "Not Assigned", // Default to "Not Assigned" if no mechanic
                Notes = appointment.Notes
            };

            return Json(appointmentDetails); // Return the appointment details as JSON
        }


        public ActionResult GetCarFaultsDetails(int carId, string carMake, string carModel)
        {
            try
            {
                // Get all faults for this car
                var faults = _context.Faults
                    .Where(f => f.CarId == carId)
                    .OrderByDescending(f => !f.ResolutionStatus) // Pending first
                    .ThenByDescending(f => f.DateReportedOn)     // Newest first
                    .ToList()
                    .Select(f => new FaultViewModel
                    {
                        Id = f.Id,
                        Description = f.Description,
                        DateReportedOn = f.DateReportedOn,
                        ResolutionStatus = f.ResolutionStatus,
                        CarId = f.CarId
                    })
                    .ToList();

                var model = new CarFaultViewModel
                {
                    CarId = carId,
                    CarMake = carMake,
                    CarModel = carModel,
                    Faults = faults
                };

                return PartialView("_CarFaultsDetails", model);
            }
            catch (Exception ex)
            {
                return Content($"<div class='alert alert-danger'>Error: {ex.Message}</div>");
            }
        }

        // Add this method to your AdminController
        [HttpGet]
        public JsonResult CheckIfCarHasPendingFaults(int carId)
        {
            // Check if the car has any pending faults
            bool hasPendingFaults = _context.Faults
                .Any(f => f.CarId == carId && !f.ResolutionStatus);

            return Json(hasPendingFaults);
        }

        // Add this method to your AdminController
        [HttpGet]
        public JsonResult GetCarFaultCounts(int carId)
        {
            var pendingCount = _context.Faults
                .Count(f => f.CarId == carId && !f.ResolutionStatus);

            var resolvedCount = _context.Faults
                .Count(f => f.CarId == carId && f.ResolutionStatus);

            return Json(new { pendingCount, resolvedCount });
        }
        public ActionResult GetAllResolvedFaults(int carId)
        {
            // Include the Car navigation property to ensure it's loaded
            var resolvedFaults = _context.Faults
                .Include(f => f.Car)  // Make sure to add this Include statement
                .Where(f => f.CarId == carId && f.ResolutionStatus)
                .ToList()
                .Select(f => new FaultViewModel
                {
                    Id = f.Id,
                    Description = f.Description,
                    DateReportedOn = f.DateReportedOn,
                    ResolutionStatus = f.ResolutionStatus,
                    CarId = f.CarId,
                    // Add null checks for Car properties
                    CarMake = f.Car != null ? f.Car.Make : string.Empty,
                    CarModel = f.Car != null ? f.Car.Model : string.Empty,
                    LicenseNumber = f.Car != null ? f.Car.LicenseNumber : string.Empty
                })
                .ToList();

            return PartialView("_AllResolvedFaults", resolvedFaults);
        }


        public ActionResult GetFaultDetails(int id)
        {
            var fault = _context.Faults.Include(f => f.Car).FirstOrDefault(f => f.Id == id);

            if (fault == null)
            {
                return NotFound();
            }

            var viewModel = new FaultViewModel
            {
                Id = fault.Id,
                Description = fault.Description,
                DateReportedOn = fault.DateReportedOn,
                ResolutionStatus = fault.ResolutionStatus,
                CarId = fault.CarId,
                CarMake = fault.Car.Make,
                CarModel = fault.Car.Model,
                LicenseNumber = fault.Car.LicenseNumber
            };

            return PartialView("_FaultDetails", viewModel);
        }
        /// <summary>
        /// Admin can propose a new appointment date.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProposeNewDate(int id)
        {
            ViewData["IsAppointments"] = true;

            _logger.LogInformation("ProposeNewDate GET invoked for appointment ID {Id}", id);

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment ID {Id} not found.", id);
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments");
            }

            var appointmentModel = new AppointmentViewModel
            {
                Id = appointment.Id,
                CarId = appointment.CarId,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime != TimeSpan.MinValue
                    ? appointment.AppointmentTime.ToString(@"hh\:mm")
                    : "00:00",
                Notes = appointment.Notes,
                Status = appointment.Status,
                MechanicName = appointment.MechanicName,
                Car = new CarViewModel
                {
                    Id = appointment.Car.Id,
                    Make = appointment.Car.Make,
                    Model = appointment.Car.Model,
                    LicenseNumber = appointment.Car.LicenseNumber
                }
            };

            await LoadNotificationsAsync();

            _logger.LogInformation("ProposeNewDate GET completed for appointment ID {Id}", id);

            return View(appointmentModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProposeNewDate(AppointmentViewModel appointment)
        {
            _logger.LogInformation("ProposeNewDate POST invoked for appointment ID {Id}", appointment.Id);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed for appointment ID {Id}. Errors: {Errors}",
                    appointment.Id, string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                TempData["ErrorMessage"] = "There was an error with your submission.";
                await LoadNotificationsAsync();
                return View(appointment);
            }

            var existingAppointment = await _context.Appointments.FindAsync(appointment.Id);

            if (existingAppointment == null)
            {
                _logger.LogWarning("Existing appointment not found for ID {Id}.", appointment.Id);
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments");
            }

            if (!TimeSpan.TryParse(appointment.AppointmentTime, out var parsedTime))
            {
                _logger.LogWarning("Invalid time format provided: {Time}", appointment.AppointmentTime);
                ModelState.AddModelError("AppointmentTime", "Invalid time format. Please use the HH:mm format.");
                TempData["ErrorMessage"] = "Invalid time format. Please use the HH:mm format.";
                await LoadNotificationsAsync();
                return View(appointment);
            }

            // 🔒 Time restriction: 08:00–18:00 with 30-minute intervals
            var minTime = new TimeSpan(8, 0, 0);
            var maxTime = new TimeSpan(18, 0, 0);

            if (parsedTime < minTime || parsedTime > maxTime || parsedTime.Minutes % 30 != 0)
            {
                ModelState.AddModelError("AppointmentTime", "Time must be between 08:00 and 18:00 in 30-minute intervals (e.g., 08:00, 08:30, 09:00).");
                TempData["ErrorMessage"] = "Please select a valid time between 08:00 and 18:00 in 30-minute steps.";
                await LoadNotificationsAsync();
                return View(appointment);
            }

            // 🔒 Mechanic availability check
            bool isMechanicBusy = await _context.Appointments.AnyAsync(a =>
                a.MechanicId == existingAppointment.MechanicId &&
                a.Id != existingAppointment.Id &&
                a.AppointmentDate == appointment.AppointmentDate &&
                a.AppointmentTime == parsedTime &&
                a.Status != "Cancelled" &&
                a.Status != "Completed");

            if (isMechanicBusy)
            {
                _logger.LogWarning("The mechanic is already booked at {Date} and {Time}", appointment.AppointmentDate, parsedTime);
                TempData["ErrorMessage"] = "The assigned mechanic is already booked at that date and time.";
                await LoadNotificationsAsync();
                return View(appointment);
            }

            // ✅ Update the appointment
            existingAppointment.AppointmentDate = appointment.AppointmentDate;
            existingAppointment.AppointmentTime = parsedTime;
            existingAppointment.Notes = appointment.Notes;

            await _context.SaveChangesAsync();
            await _notificationService.SendNotificationToCustomerAndMechanicOnAppointmentPostponing(appointment.Id);
            TempData["SuccessMessage"] = "Appointment date and time updated successfully.";
            _logger.LogInformation("Appointment ID {Id} successfully updated.", appointment.Id);

            return RedirectToAction("Appointments");
        }


        // DRY method to load notifications
        private async Task LoadNotificationsAsync()
        {
            var notifications = await _context.Notifications
                .Select(n => new NotificationViewModel
                {
                    Id = n.Id,
                    Message = n.Message,
                    DateCreated = n.DateCreated,
                    IsRead = n.IsRead
                }).ToListAsync();

            ViewBag.Notifications = notifications;
            ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);
        }



        /// <summary>
        /// Returns a view of all appointments.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Appointments()
        {
            ViewData["IsAppointments"] = true;

            // Fetch appointments
            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .ThenInclude(c => c.Owner)
                .ToListAsync();

            foreach (var appointment in appointments)
            {
                _logger.LogInformation("Appointment ID: {AppointmentId}, CarID: {CarId}, Date: {Date}, Time: {Time}",
                    appointment.Id, appointment.CarId, appointment.AppointmentDate, appointment.AppointmentTime);
            }

            var viewModel = new AdminAppointmentsViewModel
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
        public async Task<IActionResult> AdminAppointmentDetails(int id)
        {
            ViewData["IsAppointments"] = true;
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return Unauthorized();
            }

            // Fetch the specific appointment by id
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.Car.Owner) // Include the car owner (customer)
                .Include(a => a.Mechanic)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments"); // or another appropriate page
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
                // Passing customer details
                CustomerName = appointment.Car.Owner?.FullName,
                CustomerPhone = appointment.Car.Owner?.PhoneNumber,
                CustomerEmail = appointment.Car.Owner?.Email
            };

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> AssignCarToMechanic(int carId)
        {
            try
            {
                // Try to find the car with explicit loading
                var car = await _context.Cars
                    .Where(c => c.Id == carId)
                    .FirstOrDefaultAsync();

                if (car == null)
                {
                    TempData["ErrorMessage"] = "Car not found.";
                    return RedirectToAction("Dashboard");
                }

                // Use default values if the car details are null
                var carMake = string.IsNullOrEmpty(car.Make) ? "Unknown Make" : car.Make;
                var carModel = string.IsNullOrEmpty(car.Model) ? "Unknown Model" : car.Model;
                var carYear = car.Year; // If null, default to 0 or some other placeholder

                var mechanics = await _userManager.GetUsersInRoleAsync("Mechanic");

                var model = new CarAssignmentViewModel
                {
                    CarId = car.Id,
                    CarMake = carMake,
                    CarModel = carModel,
                    CarYear = carYear,
                    Mechanics = mechanics.Select(m => new SelectListItem
                    {
                        Value = m.Id.ToString(),
                        // Ensure FullName is not null or empty
                        Text = string.IsNullOrEmpty(m.FullName) ? "Unnamed Mechanic" : m.FullName
                    }).ToList()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error in AssignCarToMechanic: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading car data.";
                return RedirectToAction("Dashboard");
            }
        }


        [HttpPost]
        public async Task<IActionResult> AssignCarToMechanic(int carId, int mechanicId)
        {
            try
            {
                if (mechanicId <= 0)
                {
                    TempData["ErrorMessage"] = "Please select a valid mechanic.";
                    return RedirectToAction("AssignCars");
                }

                // Find the car by ID with a simpler query to avoid the null value issue
                var car = await _context.Cars
                    .Where(c => c.Id == carId)
                    .Select(c => new { c.Id })
                    .FirstOrDefaultAsync();

                if (car == null)
                {
                    TempData["ErrorMessage"] = "Car not found.";
                    return RedirectToAction("AssignCars");
                }

                // Use a direct SQL approach to check for existing assignments
                var existingAssignments = await _context.CarMechanicAssignments
                    .Where(a => a.CarId == carId)
                    .ToListAsync();

                if (existingAssignments.Any())
                {
                    // Remove all existing assignments for this car
                    _context.CarMechanicAssignments.RemoveRange(existingAssignments);
                    await _context.SaveChangesAsync();
                }

                // Create new assignment with explicit values
                var newAssignment = new CarMechanicAssignment
                {
                    CarId = carId,
                    MechanicId = mechanicId,
                    AssignedDate = DateTime.Now
                };

                // Add the new assignment
                await _context.CarMechanicAssignments.AddAsync(newAssignment);
                await _context.SaveChangesAsync();

                // Only try to send notification if the save was successful
                try
                {
                    // Directly call the notification service with just the car ID
                    // This avoids loading the car entity which is causing the null value issue
                    await _notificationService.SendNotificationToCustomerAndMechanicOnCarAssigning(carId);
                }
                catch (Exception notificationEx)
                {
                    // Log notification error but don't fail the whole operation
                    Console.WriteLine($"Warning: Notification failed but assignment succeeded: {notificationEx.Message}");
                }

                TempData["SuccessMessage"] = "Car assigned successfully.";
                return RedirectToAction("AssignCars");
            }
            catch (SqlNullValueException sqlEx)
            {
                // This is the specific exception we're seeing in the logs
                Console.WriteLine($"SQL Null Value Exception: {sqlEx.Message}");
                TempData["ErrorMessage"] = "Unable to assign car due to missing data in the database.";
                return RedirectToAction("AssignCars");
            }
            catch (Exception ex)
            {
                // Log the exception with more details
                Console.WriteLine($"Error in AssignCarToMechanic POST: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                TempData["ErrorMessage"] = "An error occurred while assigning the car. Please try again.";
                return RedirectToAction("AssignCars");
            }
        }


        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.DeleteAsync(user);
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new EditUserViewModel
            {
                Id = user.Id.ToString(),
                FullName = user.FullName,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);

            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Dashboard()
        {
            List<MechanicReportPartViewModel> partsUsed = new List<MechanicReportPartViewModel>();

            var model = new DashboardViewModel
            {
                Notifications = new List<NotificationViewModel>(),
                GarageUsers = new List<DashboardUserViewModel>(),
                Cars = new List<CarViewModel>(),
                CustomerCars = new List<CustomerCarViewModel>(),
                MechanicReports = new List<MechanicReportViewModel>(),
            };

            // ✅ Calculate total earnings from MechanicReportParts (parts total) for all reports
            var totalPartsEarnings = await _context.MechanicReportParts
                .Where(mrp => mrp.PartPrice != null && mrp.Quantity != null)
                .SumAsync(mrp => (decimal?)mrp.PartPrice * mrp.Quantity) ?? 0m;

            // ✅ Calculate total service fees from all MechanicReports
            var totalServiceFee = await _context.MechanicReports
                .SumAsync(mr => mr.ServiceFee);

            // ✅ Calculate the total earnings for all reports (parts earnings + service fees)
            decimal totalEarnings = totalPartsEarnings + totalServiceFee;

            // ✅ Round the total earnings
            ViewBag.TotalGarageEarnings = Math.Round(totalEarnings, 2);
            ViewBag.TotalServiceFee = Math.Round(totalServiceFee, 2);


            // ✅ Find role IDs for "Customer" and "Mechanic"
            var customerRoleId = await _context.Roles
                .Where(r => r.Name == "Customer")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var mechanicRoleId = await _context.Roles
                .Where(r => r.Name == "Mechanic")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            // ✅ Count users in each role
            var totalCustomers = await _context.UserRoles.CountAsync(ur => ur.RoleId == customerRoleId);
            var totalMechanics = await _context.UserRoles.CountAsync(ur => ur.RoleId == mechanicRoleId);

            // ✅ Round total earnings to 2 decimal places
            ViewBag.TotalGarageEarnings = Math.Round(totalEarnings, 2);
            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.TotalMechanics = totalMechanics;

            // ✅ Fetch notifications for the admin (Unread notifications)
            // Fetch notifications for display in the navbar
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

            model.Notifications = notifications;


            // Ensure MechanicId is valid before processing
            var reportsPerMechanic = await _context.MechanicReports
                .Where(r => r.MechanicId != null)  // Filter out reports where MechanicId is null
                .GroupBy(r => r.MechanicId)
                .Select(g => new { MechanicId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.MechanicId.Value, g => g.Count);  // Use .Value to access non-null MechanicId
                                                                            // 🚗 Fetch mechanics (only active ones)
            var mechanics = await _userManager.GetUsersInRoleAsync("Mechanic");

            model.GarageUsers = mechanics.Select(m => new DashboardUserViewModel
            {
                ProfilePicture = m.ProfilePicture,
                Id = m.Id.ToString(),
                FullName = m.FullName,
                Email = m.Email,
                UserName = m.UserName,
                PhoneNumber = m.PhoneNumber,
                Role = "Mechanic",
                Cars = _context.CarMechanicAssignments
                    .Where(a => a.MechanicId == m.Id) // Only consider assignments for existing mechanics
                    .Select(a => new CarViewModel
                    {
                        Id = a.Car.Id,
                        Make = a.Car.Make,
                        Model = a.Car.Model,
                        Year = a.Car.Year
                    }).ToList(),
                TotalReportsCompleted = reportsPerMechanic.ContainsKey(m.Id) ? reportsPerMechanic[m.Id] : 0
            }).ToList();


            

            // 👨‍👩‍👦 Fetch customers and their cars
            var customers = await _userManager.GetUsersInRoleAsync("Customer");
            model.CustomerCars = customers.Select(customer => new CustomerCarViewModel
            {
                ProfilePicture = customer.ProfilePicture,
                CustomerId = customer.Id.ToString(),
                FullName = customer.FullName,
                Email = customer.Email,
                UserName = customer.UserName,
                PhoneNumber = customer.PhoneNumber,
                Cars = _context.Cars
                    .Where(c => c.OwnerId == customer.Id)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        Year = c.Year,
                        Color = c.Color,
                        LicenseNumber = c.LicenseNumber,
                        ChassisNumber = c.ChassisNumber ?? "Unkown",
                        FuelType = c.FuelType,
                        IsAssigned = _context.CarMechanicAssignments.Any(a => a.CarId == c.Id)
                    }).ToList()
            }).ToList();

         

            // Fetch users excluding the admin role
            var users = await _context.Users
            .Where(u => !_context.UserRoles
            .Where(ur => ur.UserId == u.Id)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .Contains("Admin"))
            .ToListAsync();

            model.GarageUsers = users.Select(u => new DashboardUserViewModel
            {
                Id = u.Id.ToString(),
                FullName = u.FullName,
                Email = u.Email,
                UserName = u.UserName,
                PhoneNumber = u.PhoneNumber,
                LockoutEnd = u.LockoutEnd
            }).ToList();


            ViewBag.Notifications = model.Notifications;
            ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);
          

            return View(model); // Return the complete model to the view
        }

        public async Task<IActionResult> CustomerList()
        {
            ViewData["IsCustomerList"] = true;

            // Retrieve users who are assigned the 'Customer' role
            var customers = await _context.Users
                .Where(u => _context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .Contains("Customer"))
                .ToListAsync();

            // Create the customer view models with eager loading of cars
            var customerViewModels = customers.Select(u => new CustomerCarViewModel
            {
                Id = u.Id, // Add this to fix potential issues
                CustomerId = u.Id.ToString(),
                FullName = u.FullName ?? "No Name", // Handle possible null
                Email = u.Email ?? "No Email", // Handle possible null
                UserName = u.UserName ?? "No Username", // Handle possible null
                PhoneNumber = u.PhoneNumber,
                Cars = _context.Cars
                    .Where(c => c.OwnerId == u.Id)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        Year = c.Year,
                        OwnerName = u.FullName ?? "No Name",
                        AssignedMechanicName = (from a in _context.CarMechanicAssignments
                                                join m in _context.Users on a.MechanicId equals m.Id
                                                where a.CarId == c.Id
                                                select m.FullName).FirstOrDefault() ?? "Not Assigned"
                    }).ToList()
            }).ToList();

            // Pre-calculate statistics for the view
            var totalCustomers = customerViewModels.Count;
            var customersWithCars = customerViewModels.Count(c => c.Cars != null && c.Cars.Any());
            var customersWithoutCars = totalCustomers - customersWithCars;
            var totalVehicles = customerViewModels.Sum(c => c.Cars != null ? c.Cars.Count : 0);

            // Create the view model with all the data
            var model = new CustomerListViewModel
            {
                Customers = customerViewModels,
                TotalCustomers = totalCustomers,
                CustomersWithCars = customersWithCars,
                CustomersWithoutCars = customersWithoutCars,
                TotalVehicles = totalVehicles
            };

            // Fetch notifications for the admin
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

            model.Notifications = notifications;
            ViewBag.Notifications = notifications;
            ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);

            return View(model);
        }

        public async Task<IActionResult> CarList()
        {
            ViewData["IsCurrentCars"] = true;

            try
            {
                // Get all cars with their owners and mechanics
                var cars = await _context.Cars
                    .Include(c => c.Owner)
                    .Include(c => c.Mechanic)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make ?? "", // Ensure null properties are handled
                        Model = c.Model ?? "",
                        Year = c.Year,
                        Color = c.Color ?? "",
                        LicenseNumber = c.LicenseNumber ?? "",
                        ChassisNumber = c.ChassisNumber ?? "",
                        OwnerName = c.Owner != null ? (c.Owner.FullName ?? "N/A") : "N/A",
                        // Extract the mechanic name if it's assigned, otherwise use "Not Assigned"
                        AssignedMechanicName = (from a in _context.CarMechanicAssignments
                                                join u in _context.Users on a.MechanicId equals u.Id
                                                where a.CarId == c.Id
                                                select u.FullName).FirstOrDefault() ?? "Not Assigned"
                    })
                    .ToListAsync();

                // Get all faults for these cars
                var carIds = cars.Select(c => c.Id).ToList();
                var faults = await _context.Faults
                    .Where(f => carIds.Contains(f.CarId))
                    .Include(f => f.Car)
                    .Select(f => new FaultViewModel
                    {
                        Id = f.Id,
                        CarId = f.CarId,
                        Description = f.Description ?? "", // Handle null description
                        DateReportedOn = f.DateReportedOn,
                        ResolutionStatus = f.ResolutionStatus,
                        CarMake = f.Car != null ? (f.Car.Make ?? "") : "",
                        CarModel = f.Car != null ? (f.Car.Model ?? "") : "",
                        LicenseNumber = f.Car != null ? (f.Car.LicenseNumber ?? "") : ""
                    })
                    .ToListAsync();

                var viewModel = new CarListViewModel
                {
                    Cars = cars,
                    CarFaults = faults,
                    MyTable = new DataTable()
                };

                // Initialize the DataTable columns
                viewModel.MyTable.Columns.Add("CarId", typeof(int));
                viewModel.MyTable.Columns.Add("Make", typeof(string));
                viewModel.MyTable.Columns.Add("Model", typeof(string));
                viewModel.MyTable.Columns.Add("Year", typeof(int));
                viewModel.MyTable.Columns.Add("Color", typeof(string));
                viewModel.MyTable.Columns.Add("LicenseNumber", typeof(string));
                viewModel.MyTable.Columns.Add("Owner", typeof(string));
                viewModel.MyTable.Columns.Add("Assigned Mechanic", typeof(string));

                // Populate the DataTable
                foreach (var car in cars)
                {
                    viewModel.MyTable.Rows.Add(
                        car.Id,
                        car.Make,
                        car.Model,
                        car.Year,
                        car.Color,
                        car.LicenseNumber,
                        car.OwnerName,
                        car.AssignedMechanicName
                    );
                }

                return View(viewModel); // Path is resolved as Views/Admin/CarList.cshtml
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in CarList: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return a friendly error view
                TempData["ErrorMessage"] = "An error occurred while retrieving car data. Please try again later.";
                return RedirectToAction("Dashboard");
            }
        }



        // Add this method to your AdminController
        public async Task<IActionResult> ResolveFault(int id, string returnUrl = null)
        {
            var fault = await _context.Faults.FindAsync(id);

            if (fault == null)
            {
                TempData["ErrorMessage"] = "Fault not found.";
                return RedirectToAction(returnUrl == null ? "FaultList" : returnUrl);
            }

            fault.ResolutionStatus = true;

            _context.Update(fault);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Fault has been marked as resolved.";

            if (!string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/"))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(returnUrl ?? "FaultList");
        }

        public async Task<IActionResult> AssignCars(string filter = "all")
        {
            // Store the filter in ViewBag for active button highlighting
            ViewBag.Filter = filter;
            ViewData["IsCurrentCars"] = true;

            var model = new DashboardViewModel
            {
                Notifications = new List<NotificationViewModel>(),
                GarageUsers = new List<DashboardUserViewModel>(),
                Cars = new List<CarViewModel>(),
                ShowCars = true
            };

            try
            {
                // Fetch all mechanics
                var mechanics = await _userManager.GetUsersInRoleAsync("Mechanic");
                model.GarageUsers = mechanics.Select(m => new DashboardUserViewModel
                {
                    Id = m.Id.ToString(),
                    FullName = m.FullName,
                    Email = m.Email,
                    UserName = m.UserName,
                    PhoneNumber = m.PhoneNumber,
                    ProfilePicture = m.ProfilePicture,
                }).ToList();

                // Fetch all cars with assignment information
                var allCars = await _context.Cars
                    .Include(c => c.Owner)
                    .Select(c => new CarViewModel
                    {
                        Id = c.Id,
                        Make = c.Make,
                        Model = c.Model,
                        Year = c.Year,
                        Color = c.Color,
                        LicenseNumber = c.LicenseNumber,
                        ChassisNumber = c.ChassisNumber ?? "Unknown",
                        FuelType = c.FuelType,
                        OwnerName = c.Owner.FullName,
                        IsAssigned = _context.CarMechanicAssignments.Any(a => a.CarId == c.Id),
                        // Get the assigned mechanic's ID and name from the assignment
                        AssignedMechanicId = _context.CarMechanicAssignments
                                                .Where(a => a.CarId == c.Id)
                                                .Select(a => a.MechanicId)
                                                .FirstOrDefault(),
                        AssignedMechanicName = (from a in _context.CarMechanicAssignments
                                                join u in _context.Users on a.MechanicId equals u.Id
                                                where a.CarId == c.Id
                                                select u.FullName).FirstOrDefault() ?? "Not Assigned"
                    })
                    .ToListAsync();

                // Apply filtering based on the filter parameter
                model.Cars = filter switch
                {
                    "assigned" => allCars.Where(c => c.IsAssigned).ToList(),
                    "unassigned" => allCars.Where(c => !c.IsAssigned).ToList(),
                    _ => allCars // "all" or any other value defaults to showing all cars
                };

                // Set appropriate filter flags on the model
                model.ShowUnassignedCars = filter == "unassigned";

                // Fetch notifications for display in the navbar
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
                model.Notifications = notifications;

                // Pass notifications and unread count to the ViewBag for navbar display
                ViewBag.Notifications = notifications;
                ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);

                return View(model);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error in AssignCars: {ex.Message}");
                TempData["ErrorMessage"] = $"An error occurred while loading car data: {ex.Message}";

                // Return an empty model if there's an error
                return View(model);
            }
        }
        public async Task<IActionResult> CarReports()
        {
            ViewData["IsCurrentCars"] = true;

            // Fetch notifications for display in the navbar
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

            // Pass notifications and unread count to the ViewBag for navbar display
            ViewBag.Notifications = notifications;
            ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);

            // Fetch all cars along with their reports, parts, labour items, and inspection items
            var cars = await _context.Cars
                .AsNoTracking()
                .Include(c => c.Owner)
                .Select(c => new CarViewModel
                {
                    Id = c.Id,
                    LicenseNumber = c.LicenseNumber,
                    Make = c.Make,
                    Model = c.Model,
                    Year = c.Year,
                    Color = c.Color,
                    ChassisNumber = c.ChassisNumber,
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
                            PaymentMode = r.PaymentMode ?? "Cash",
                            CustomerRequest = r.CustomerRequest,
                            ActionTaken = r.ActionTaken,
                            NextServiceAdvice = r.NextServiceAdvice,
                            NextServiceKm = r.NextServiceKm,
                            NextServiceDate = r.NextServiceDate,
                            TaxRate = r.TaxRate,
                            MechanicName = r.Mechanic.FullName ?? "Unknown",

                            // Calculate total price including parts and labour
                            TotalPrice = (r.Parts != null ? r.Parts.Sum(mp => mp.PartPrice * mp.Quantity) : 0) +
                                       (r.LabourItems != null ? r.LabourItems.Sum(l => l.TotalAmountWithoutTax + l.TaxAmount) : 0) +
                                       r.ServiceFee,

                            // Parts information
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
                                .ToList(),

                            // Labour items
                            LabourItems = r.LabourItems
                                .Select(l => new LabourItemViewModel
                                {
                                    OperationCode = l.OperationCode,
                                    Description = l.Description,
                                    TotalAmountWithoutTax = l.TotalAmountWithoutTax,
                                    TaxRate = l.TaxRate,
                                    TaxAmount = l.TaxAmount
                                })
                                .ToList(),

                            // Inspection items
                            InspectionItems = r.InspectionItems
                                .Select(i => new InspectionItemViewModel
                                {
                                    ItemName = i.ItemName,
                                    Result = i.Result,
                                    Status = i.Status,
                                    Recommendations = i.Recommendations
                                })
                                .ToList()
                        })
                        .OrderByDescending(r => r.DateReported)
                        .ToList()
                })
                .Where(c => c.Reports.Any()) // Only include cars that have reports
                .ToListAsync();

            var model = new DashboardViewModel
            {
                Cars = cars,
                Notifications = notifications
            };

            return View(model);
        }



        public async Task<IActionResult> MechanicInfo()
        {
            ViewData["IsMechanicInfo"] = true;
            // Retrieve users who are assigned the 'Mechanic' role
            var mechanics = await _context.Users
                .Where(u => _context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .Contains("Mechanic"))
                .ToListAsync();

            var allFaults = await _context.Faults.ToListAsync();
            int totalFaults = allFaults.Count;
            int resolvedFaults = allFaults.Count(f => f.ResolutionStatus);
            var pendingFaults = allFaults.Where(f => !f.ResolutionStatus).ToList();

            // Calculate completion rate percentage
            int completionRate = totalFaults > 0 ? (int)Math.Round((double)resolvedFaults / totalFaults * 100) : 0;

            // Add to ViewBag for use in the view
            ViewBag.CompletionRate = completionRate;

            var model = new MechanicListViewModel
            {
                Mechanics = mechanics.Select(u => new MechanicCarViewModel
                {
                    MechanicId = u.Id.ToString(),
                    FullName = u.FullName,
                    Email = u.Email,
                    UserName = u.UserName,
                    PhoneNumber = u.PhoneNumber,
                    // Count faults resolved by this mechanic
                    FaultsResolved = _context.Faults
                        .Count(f => f.MechanicId == u.Id && f.ResolutionStatus),
                    // Count reports created by this mechanic directly
                    // This is more efficient as it uses the direct relationship
                    ReportsMade = _context.MechanicReports
                        .Count(r => r.MechanicId == u.Id),
                    Cars = _context.Cars
                        .Where(c => _context.CarMechanicAssignments
                            .Any(a => a.CarId == c.Id && a.MechanicId == u.Id))
                        .Select(c => new CarViewModel
                        {
                            Id = c.Id,
                            Make = c.Make,
                            Model = c.Model,
                            Year = c.Year,
                            OwnerName = (from o in _context.Users
                                         where o.Id == c.OwnerId
                                         select o.FullName).FirstOrDefault() ?? "Unknown Owner",
                            AssignedMechanicName = u.FullName
                        }).ToList()
                }).ToList()
            };

            return View(model);
        }

        // GET: Admin/EditMechanic/{id}

        [HttpGet]
        public async Task<IActionResult> EditMechanic(int id)
        {
            ViewData["IsMechanicInfo"] = true;
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Check if this user has the Mechanic role
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains("Mechanic"))
            {
                return BadRequest("Selected user is not a mechanic.");
            }

            // Get cars assigned to this mechanic
            var assignedCars = await _context.Cars
                .Where(c => _context.CarMechanicAssignments.Any(a => a.CarId == c.Id && a.MechanicId == id))
                .Select(c => new CarWithFaultsViewModel
                {
                    CarId = c.Id,
                    CarMake = c.Make,
                    CarModel = c.Model,
                    CarYear = c.Year.ToString(),
                    LicenseNumber = c.LicenseNumber,
                    // Ensure Faults is initialized even if there are no faults
                    Faults = _context.Faults
                            .Where(f => f.CarId == c.Id)
                            .Select(f => new FaultViewModel
                            {
                                Id = f.Id,
                                Description = f.Description,
                                DateReportedOn = f.DateReportedOn,
                                ResolutionStatus = f.ResolutionStatus
                            })
                            .ToList() ?? new List<FaultViewModel>() // Default to empty list if null
                })
                .ToListAsync();

            var model = new MechanicViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                // Initialize Cars if it's null
                Cars = assignedCars ?? new List<CarWithFaultsViewModel>()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMechanic(MechanicViewModel model)
        {
            ViewData["IsMechanicInfo"] = true;
            if (!ModelState.IsValid)
            {
                // Reload the cars data for the view
                model.Cars = await _context.Cars
                    .Where(c => _context.CarMechanicAssignments.Any(a => a.CarId == c.Id && a.MechanicId == model.UserId))
                    .Select(c => new CarWithFaultsViewModel
                    {
                        // Your mapping code here
                    })
                    .ToListAsync();

                return View(model);
            }

            // Update user details
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            // Set the user properties from the model
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;

            // Explicitly tell EF the user entity is modified
            _context.Users.Update(user);

            // Add debugging information
            Console.WriteLine($"Updating user: {user.Id}, Name: {user.FullName}, Email: {user.Email}");

            // Save changes and capture the number of rows affected
            int rowsAffected = await _context.SaveChangesAsync();
            Console.WriteLine($"Rows affected: {rowsAffected}");

            if (rowsAffected > 0)
            {
                TempData["SuccessMessage"] = "Mechanic details updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "No changes were saved. Please try again.";
            }

            return RedirectToAction("MechanicInfo");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleReadStatus(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null)
            {
                notification.IsRead = !notification.IsRead;
                await _context.SaveChangesAsync();
                return Json(new { success = true, isNowRead = notification.IsRead });
            }

            return Json(new { success = false });
        }





        public async Task<IActionResult> EditCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new EditUserViewModel
            {
                Id = user.Id.ToString(),
                FullName = user.FullName,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };
            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> EditCustomer(EditUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);

            return RedirectToAction("Dashboard");
        }




        [HttpPost]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Fetch cars associated with the user (assuming "OwnerId" represents the car owner)
            var userCars = _context.Cars.Where(c => c.OwnerId == user.Id).ToList();

            // For each car, remove associated CarMechanicAssignments
            foreach (var car in userCars)
            {
                var carAssignments = _context.CarMechanicAssignments.Where(a => a.CarId == car.Id).ToList();
                if (carAssignments.Any())
                {
                    _context.CarMechanicAssignments.RemoveRange(carAssignments);
                }
            }
            await _context.SaveChangesAsync();

            // Now remove the cars
            if (userCars.Any())
            {
                _context.Cars.RemoveRange(userCars);
                await _context.SaveChangesAsync();
            }

            // Remove appointments related to the user
            var userAppointments = _context.Appointments.Where(a => a.UserId == user.Id).ToList();
            if (userAppointments.Any())
            {
                _context.Appointments.RemoveRange(userAppointments);
                await _context.SaveChangesAsync();
            }

            // Finally, delete the user
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View("Error");
            }

            return RedirectToAction("Dashboard");
        }


        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int id, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Appointments");
            }

            // Prevent cancellation if already completed
            if (appointment.Status == "Completed")
            {
                TempData["ErrorMessage"] = "You cannot cancel a completed appointment.";
                return RedirectToAction("Appointments");
            }


            // Prevent cancellation if within 1 hour of appointment time
            var appointmentDateTime = appointment.AppointmentDate
                .Add(appointment.AppointmentTime);


            appointment.Status = "Cancelled";
            appointment.Notes = $"Cancelled by Admin. Reason: {reason}";
            await _notificationService.SendNotificationToCustomerAndMechanicOnAppointmentCancelling(appointment.Id);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment cancelled successfully.";
            return RedirectToAction("Appointments");
        }

        [HttpGet]
        public async Task<IActionResult> AdminNotificationViewPage()
        {
            var user = await _userManager.GetUserAsync(User);
            // If administrators have profile pictures, include the following line:
            // ViewBag.ProfilePicturePath = user.ProfilePicture;
            return View();
        }

        // Add these methods to your AdminController.cs

        

        #region Operation Codes Management

        /// <summary>
        /// Add new operation code
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AddOperationCode()
        {
            ViewData["IsPartsManagement"] = true;

            var model = new OperationCodeViewModel();
            await LoadNotificationsForViewBag();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOperationCode(OperationCodeViewModel model)
        {
            ViewData["IsPartsManagement"] = true;

            if (!ModelState.IsValid)
            {
                await LoadNotificationsForViewBag();
                return View(model);
            }

            try
            {
                // Check if operation code already exists
                var existingCode = await _context.OperationCodes
                    .FirstOrDefaultAsync(oc => oc.Code.ToUpper() == model.Code.ToUpper());

                if (existingCode != null)
                {
                    ModelState.AddModelError("Code", "An operation code with this code already exists.");
                    await LoadNotificationsForViewBag();
                    return View(model);
                }

                var operationCode = new OperationCode
                {
                    Code = model.Code.ToUpper(),
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive,
                    CreatedDate = DateTime.Now
                };

                _context.OperationCodes.Add(operationCode);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Operation code added successfully.";
                return RedirectToAction("PartsManagement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding operation code");
                ModelState.AddModelError("", "An error occurred while adding the operation code.");
                await LoadNotificationsForViewBag();
                return View(model);
            }
        }

        /// <summary>
        /// Edit operation code
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditOperationCode(int id)
        {
            ViewData["IsPartsManagement"] = true;

            var operationCode = await _context.OperationCodes
                .Include(oc => oc.OperationCodeParts)
                .ThenInclude(ocp => ocp.ServicePart)
                .FirstOrDefaultAsync(oc => oc.Id == id);

            if (operationCode == null)
            {
                TempData["ErrorMessage"] = "Operation code not found.";
                return RedirectToAction("PartsManagement");
            }

            var model = new OperationCodeViewModel
            {
                Id = operationCode.Id,
                Code = operationCode.Code,
                Name = operationCode.Name,
                Description = operationCode.Description,
                IsActive = operationCode.IsActive,
                AssociatedParts = operationCode.OperationCodeParts.Select(ocp => new ServicePartViewModel
                {
                    Id = ocp.ServicePart.Id,
                    PartNumber = ocp.ServicePart.PartNumber,
                    PartName = ocp.ServicePart.PartName,
                    Price = ocp.ServicePart.Price,
                    IsDefault = ocp.IsDefault
                }).ToList()
            };

            // Get all available parts for assignment
            ViewBag.AvailableParts = await _context.ServiceParts
                .Where(sp => sp.IsAvailable)
                .Select(sp => new SelectListItem
                {
                    Value = sp.Id.ToString(),
                    Text = $"{sp.PartNumber} - {sp.PartName} (RM {sp.Price:F2})"
                })
                .ToListAsync();

            await LoadNotificationsForViewBag();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOperationCode(OperationCodeViewModel model)
        {
            ViewData["IsPartsManagement"] = true;

            if (!ModelState.IsValid)
            {
                await LoadNotificationsForViewBag();
                return View(model);
            }

            try
            {
                var operationCode = await _context.OperationCodes.FindAsync(model.Id);
                if (operationCode == null)
                {
                    TempData["ErrorMessage"] = "Operation code not found.";
                    return RedirectToAction("PartsManagement");
                }

                // Check for duplicate codes (excluding current one)
                var existingCode = await _context.OperationCodes
                    .FirstOrDefaultAsync(oc => oc.Id != model.Id && oc.Code.ToUpper() == model.Code.ToUpper());

                if (existingCode != null)
                {
                    ModelState.AddModelError("Code", "An operation code with this code already exists.");
                    await LoadNotificationsForViewBag();
                    return View(model);
                }

                operationCode.Code = model.Code.ToUpper();
                operationCode.Name = model.Name;
                operationCode.Description = model.Description;
                operationCode.IsActive = model.IsActive;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Operation code updated successfully.";
                return RedirectToAction("PartsManagement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating operation code");
                ModelState.AddModelError("", "An error occurred while updating the operation code.");
                await LoadNotificationsForViewBag();
                return View(model);
            }
        }

        /// <summary>
        /// Delete operation code
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOperationCode(int id)
        {
            try
            {
                var operationCode = await _context.OperationCodes
                    .Include(oc => oc.OperationCodeParts)
                    .FirstOrDefaultAsync(oc => oc.Id == id);

                if (operationCode == null)
                {
                    TempData["ErrorMessage"] = "Operation code not found.";
                    return RedirectToAction("PartsManagement");
                }

                // Check if operation code is used in any reports
                var isUsed = await _context.MechanicReportParts
                    .AnyAsync(mrp => mrp.OperationCode == operationCode.Code) ||
                           await _context.MechanicReportLabours
                    .AnyAsync(li => li.OperationCode == operationCode.Code);

                if (isUsed)
                {
                    TempData["ErrorMessage"] = "Cannot delete operation code that has been used in service reports. Consider marking it as inactive instead.";
                    return RedirectToAction("PartsManagement");
                }

                // Remove associated part relationships first
                _context.OperationCodeParts.RemoveRange(operationCode.OperationCodeParts);
                _context.OperationCodes.Remove(operationCode);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Operation code deleted successfully.";
                return RedirectToAction("PartsManagement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting operation code");
                TempData["ErrorMessage"] = "An error occurred while deleting the operation code.";
                return RedirectToAction("PartsManagement");
            }
        }

        #endregion

        #region Service Parts Management

        /// <summary>
        /// Add new service part
        /// </summary>
        // Controller Methods for AddServicePart (Add to AdminController.cs)

        /// <summary>
        /// Add new service part - GET method
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AddServicePart()
        {
            ViewData["IsPartsManagement"] = true;

            var model = new ServicePartViewModel();
            await LoadNotificationsForViewBag();

            return View(model);
        }

        /// <summary>
        /// Add new service part - POST method
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddServicePart(ServicePartViewModel model)
        {
            ViewData["IsPartsManagement"] = true;

            if (!ModelState.IsValid)
            {
                await LoadNotificationsForViewBag();
                return View(model);
            }

            try
            {
                // Check if part number already exists
                var existingPart = await _context.ServiceParts
                    .FirstOrDefaultAsync(sp => sp.PartNumber.ToUpper() == model.PartNumber.ToUpper());

                if (existingPart != null)
                {
                    ModelState.AddModelError("PartNumber", "A part with this part number already exists.");
                    await LoadNotificationsForViewBag();
                    return View(model);
                }

                // Create new service part
                var servicePart = new ServicePart
                {
                    PartNumber = model.PartNumber.ToUpper(),
                    PartName = model.PartName,
                    PartDescription = model.PartDescription,
                    Price = model.Price,
                    IsAvailable = model.IsAvailable,
                    CreatedDate = DateTime.Now
                };

                _context.ServiceParts.Add(servicePart);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Service part added successfully.";
                return RedirectToAction("PartsManagement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding service part");
                ModelState.AddModelError("", "An error occurred while adding the service part.");
                await LoadNotificationsForViewBag();
                return View(model);
            }
        }

        /// <summary>
        /// Edit service part
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditServicePart(int id)
        {
            ViewData["IsPartsManagement"] = true;

            var servicePart = await _context.ServiceParts
                .Include(sp => sp.OperationCodeParts)
                .ThenInclude(ocp => ocp.OperationCode)
                .FirstOrDefaultAsync(sp => sp.Id == id);

            if (servicePart == null)
            {
                TempData["ErrorMessage"] = "Service part not found.";
                return RedirectToAction("PartsManagement");
            }

            var model = new ServicePartViewModel
            {
                Id = servicePart.Id,
                PartNumber = servicePart.PartNumber,
                PartName = servicePart.PartName,
                PartDescription = servicePart.PartDescription,
                Price = servicePart.Price,
                IsAvailable = servicePart.IsAvailable,
                AssociatedOperationCodes = servicePart.OperationCodeParts.Select(ocp => ocp.OperationCode.Code).ToList()
            };

            // Get all available operation codes for assignment
            ViewBag.AvailableOperationCodes = await _context.OperationCodes
                .Where(oc => oc.IsActive)
                .Select(oc => new SelectListItem
                {
                    Value = oc.Id.ToString(),
                    Text = $"{oc.Code} - {oc.Name}"
                })
                .ToListAsync();

            await LoadNotificationsForViewBag();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServicePart(ServicePartViewModel model)
        {
            ViewData["IsPartsManagement"] = true;

            if (!ModelState.IsValid)
            {
                await LoadNotificationsForViewBag();
                return View(model);
            }

            try
            {
                var servicePart = await _context.ServiceParts.FindAsync(model.Id);
                if (servicePart == null)
                {
                    TempData["ErrorMessage"] = "Service part not found.";
                    return RedirectToAction("PartsManagement");
                }

                // Check for duplicate part numbers (excluding current one)
                var existingPart = await _context.ServiceParts
                    .FirstOrDefaultAsync(sp => sp.Id != model.Id && sp.PartNumber.ToUpper() == model.PartNumber.ToUpper());

                if (existingPart != null)
                {
                    ModelState.AddModelError("PartNumber", "A part with this part number already exists.");
                    await LoadNotificationsForViewBag();
                    return View(model);
                }

                servicePart.PartNumber = model.PartNumber.ToUpper();
                servicePart.PartName = model.PartName;
                servicePart.PartDescription = model.PartDescription;
                servicePart.Price = model.Price;
                servicePart.IsAvailable = model.IsAvailable;
                servicePart.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Service part updated successfully.";
                return RedirectToAction("PartsManagement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service part");
                ModelState.AddModelError("", "An error occurred while updating the service part.");
                await LoadNotificationsForViewBag();
                return View(model);
            }
        }

        /// <summary>
        /// Delete service part
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteServicePart(int id)
        {
            try
            {
                var servicePart = await _context.ServiceParts
                    .Include(sp => sp.OperationCodeParts)
                    .FirstOrDefaultAsync(sp => sp.Id == id);

                if (servicePart == null)
                {
                    TempData["ErrorMessage"] = "Service part not found.";
                    return RedirectToAction("PartsManagement");
                }

                // Check if part is used in any reports
                var isUsed = await _context.MechanicReportParts
                    .AnyAsync(mrp => mrp.PartNumber == servicePart.PartNumber || mrp.PartName == servicePart.PartName);

                if (isUsed)
                {
                    TempData["ErrorMessage"] = "Cannot delete part that has been used in service reports. Consider marking it as unavailable instead.";
                    return RedirectToAction("PartsManagement");
                }

                // Remove associated operation code relationships first
                _context.OperationCodeParts.RemoveRange(servicePart.OperationCodeParts);
                _context.ServiceParts.Remove(servicePart);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Service part deleted successfully.";
                return RedirectToAction("PartsManagement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service part");
                TempData["ErrorMessage"] = "An error occurred while deleting the service part.";
                return RedirectToAction("PartsManagement");
            }
        }

        #endregion

        // Corrected version that matches your existing database structure
        // Add these methods to your AdminController.cs

        #region Parts and Operation Codes Management

        /// <summary>
        /// Display all parts and operation codes management page
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PartsManagement()
        {
            ViewData["IsPartsManagement"] = true;

            try
            {
                // Get all operation codes with their associated parts
                var operationCodes = await _context.OperationCodes
                    .Include(oc => oc.OperationCodeParts)
                    .ThenInclude(ocp => ocp.ServicePart)
                    .OrderBy(oc => oc.Code)
                    .ToListAsync();

                // Get all service parts
                var serviceParts = await _context.ServiceParts
                    .Include(sp => sp.OperationCodeParts)
                    .ThenInclude(ocp => ocp.OperationCode)
                    .OrderBy(sp => sp.PartName)
                    .ToListAsync();

                // Get usage statistics from mechanic reports
                var partUsageStats = await _context.MechanicReportParts
                    .GroupBy(mrp => new { mrp.PartName, mrp.PartNumber })
                    .Select(g => new {
                        PartName = g.Key.PartName,
                        PartNumber = g.Key.PartNumber,
                        TotalQuantityUsed = g.Sum(p => p.Quantity),
                        UsageCount = g.Count(),
                        AveragePrice = g.Average(p => p.PartPrice),
                        LastUsedDate = g.Max(p => p.MechanicReport.DateReported)
                    })
                    .ToListAsync();

                var model = new PartsManagementViewModel
                {
                    OperationCodes = operationCodes.Select(oc => new OperationCodeViewModel
                    {
                        Id = oc.Id,
                        Code = oc.Code,
                        Name = oc.Name,
                        Description = oc.Description,
                        IsActive = oc.IsActive,
                        CreatedDate = oc.CreatedDate,
                        AssociatedPartsCount = oc.OperationCodeParts.Count,
                        AssociatedParts = oc.OperationCodeParts.Select(ocp => new ServicePartViewModel
                        {
                            Id = ocp.ServicePart.Id,
                            PartNumber = ocp.ServicePart.PartNumber,
                            PartName = ocp.ServicePart.PartName,
                            Price = ocp.ServicePart.Price,
                            IsDefault = ocp.IsDefault
                        }).ToList()
                    }).ToList(),

                    ServiceParts = serviceParts.Select(sp => new ServicePartViewModel
                    {
                        Id = sp.Id,
                        PartNumber = sp.PartNumber,
                        PartName = sp.PartName,
                        PartDescription = sp.PartDescription,
                        Price = sp.Price,
                        IsAvailable = sp.IsAvailable,
                        CreatedDate = sp.CreatedDate,
                        LastUpdated = sp.LastUpdated,
                        AssociatedOperationCodes = sp.OperationCodeParts.Select(ocp => ocp.OperationCode.Code).ToList(),
                        // Add usage statistics
                        TotalQuantityUsed = partUsageStats.FirstOrDefault(p => p.PartName == sp.PartName)?.TotalQuantityUsed ?? 0,
                        UsageCount = partUsageStats.FirstOrDefault(p => p.PartName == sp.PartName)?.UsageCount ?? 0,
                        LastUsedDate = partUsageStats.FirstOrDefault(p => p.PartName == sp.PartName)?.LastUsedDate
                    }).ToList()
                };

                // Load notifications
                await LoadNotificationsForViewBag();

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading parts management data");
                TempData["ErrorMessage"] = "An error occurred while loading parts data.";
                return RedirectToAction("Dashboard");
            }
        }

        

        #region Operation Code - Part Relationships

        /// <summary>
        /// Assign part to operation code
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPartToOperationCode(int operationCodeId, int servicePartId, bool isDefault = false)
        {
            try
            {
                // Check if relationship already exists
                var existingRelation = await _context.OperationCodeParts
                    .FirstOrDefaultAsync(ocp => ocp.OperationCodeId == operationCodeId && ocp.ServicePartId == servicePartId);

                if (existingRelation != null)
                {
                    TempData["ErrorMessage"] = "This part is already assigned to the operation code.";
                    return RedirectToAction("EditOperationCode", new { id = operationCodeId });
                }

                var operationCodePart = new OperationCodePart
                {
                    OperationCodeId = operationCodeId,
                    ServicePartId = servicePartId,
                    IsDefault = isDefault,
                    AssignedDate = DateTime.Now
                };

                _context.OperationCodeParts.Add(operationCodePart);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Part assigned to operation code successfully.";
                return RedirectToAction("EditOperationCode", new { id = operationCodeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning part to operation code");
                TempData["ErrorMessage"] = "An error occurred while assigning the part.";
                return RedirectToAction("PartsManagement");
            }
        }

        /// <summary>
        /// Remove part from operation code
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePartFromOperationCode(int operationCodeId, int servicePartId)
        {
            try
            {
                var operationCodePart = await _context.OperationCodeParts
                    .FirstOrDefaultAsync(ocp => ocp.OperationCodeId == operationCodeId && ocp.ServicePartId == servicePartId);

                if (operationCodePart == null)
                {
                    TempData["ErrorMessage"] = "Relationship not found.";
                    return RedirectToAction("EditOperationCode", new { id = operationCodeId });
                }

                _context.OperationCodeParts.Remove(operationCodePart);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Part removed from operation code successfully.";
                return RedirectToAction("EditOperationCode", new { id = operationCodeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing part from operation code");
                TempData["ErrorMessage"] = "An error occurred while removing the part.";
                return RedirectToAction("EditOperationCode", new { id = operationCodeId });
            }
        }

        /// <summary>
        /// Toggle default status of part in operation code
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePartDefault(int operationCodeId, int servicePartId)
        {
            try
            {
                var operationCodePart = await _context.OperationCodeParts
                    .FirstOrDefaultAsync(ocp => ocp.OperationCodeId == operationCodeId && ocp.ServicePartId == servicePartId);

                if (operationCodePart == null)
                {
                    TempData["ErrorMessage"] = "Relationship not found.";
                    return RedirectToAction("EditOperationCode", new { id = operationCodeId });
                }

                operationCodePart.IsDefault = !operationCodePart.IsDefault;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Part default status {(operationCodePart.IsDefault ? "enabled" : "disabled")}.";
                return RedirectToAction("EditOperationCode", new { id = operationCodeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling part default status");
                TempData["ErrorMessage"] = "An error occurred while updating the part status.";
                return RedirectToAction("EditOperationCode", new { id = operationCodeId });
            }
        }

        #endregion

        /// <summary>
        /// Helper method to load notifications for ViewBag
        /// </summary>
        private async Task LoadNotificationsForViewBag()
        {
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
        }

        #endregion


        [HttpGet]
        public async Task<IActionResult> Chatbot()
        {
            ViewData["IsChatbot"] = true;
            await LoadNotificationsAsync(); // If you have this method
            return View();
        }
    }
}
