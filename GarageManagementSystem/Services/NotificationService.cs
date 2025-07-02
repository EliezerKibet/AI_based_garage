using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GarageManagementSystem.Data;
using GarageManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

public class NotificationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly UserManager<Users> _userManager;

    public NotificationService(AppDbContext context, ILogger<NotificationService> logger, UserManager<Users> userManager)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
    }


    //Notification for admin
    public async Task SendAdminNotificationOnUserRegistration(int userId)
    {
        string adminEmail = "admin@admin.com";  // Define the admin email

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (admin != null)
        {
            // Fetch the newly registered user based on userId
            var newUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (newUser != null)
            {
                // Get the role of the newly registered user
                var roles = await _userManager.GetRolesAsync(newUser);
                var role = roles.FirstOrDefault(); // Assuming the user can have only one role

                // Create the notification message based on the user's role and name
                var notificationMessage = $"A new {role} has registered: {newUser.FullName}";

                // Get the current time in the user's local time zone
                var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();


                // Create a new notification for the admin
                var notification = new Notification
                {
                    UserId = admin.Id,  // Notification sent to the admin
                    Message = notificationMessage,  // Message includes role and name
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false  // New notification is unread initially
                };

                // Add the notification to the database and save it
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogError($"User with ID {userId} not found.");
            }
        }
        else
        {
            _logger.LogError($"Admin user with email {adminEmail} not found.");
        }
    }

    public async Task SendUserNotificationOnRegistration(int userId)
    {
        // Fetch the newly registered user based on userId
        var newUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (newUser != null)
        {
            // Create the notification message for the user
            var notificationMessage = $"Congratulations, {newUser.FullName}! You have successfully registered with the Garage Management System.";

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Create a new notification for the user
            var notification = new Notification
            {
                UserId = newUser.Id,  // Notification sent to the newly registered user
                Message = notificationMessage,  // Message includes the user's name
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false  // New notification is unread initially
            };

            // Add the notification to the database and save it
            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogError($"User with ID {userId} not found.");
        }
    }


    public async Task SendNotificationToAdminOnCarAddition(int carId)
    {
        string adminEmail = "admin@admin.com";  // Define the admin email

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (admin != null)
        {
            // Fetch the newly added car based on carId
            var newCar = await _context.Cars.Include(c => c.Owner).FirstOrDefaultAsync(c => c.Id == carId);

            if (newCar != null)
            {
                // Get the customer information
                var customerName = $"{newCar.Owner.FullName}";

                // Create the notification message for the admin
                var notificationMessage = $"A new car has been added by {customerName}: {newCar.Make} {newCar.Model}";

                // Get the current time in the user's local time zone
                var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

                // Create a new notification for the admin
                var notification = new Notification
                {
                    UserId = admin.Id,  // Notification sent to the admin
                    Message = notificationMessage,  // Message includes car details and customer name
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false  // New notification is unread initially
                };

                // Add the notification to the database and save it
                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogError($"Car with ID {carId} not found.");
            }
        }
        else
        {
            _logger.LogError($"Admin user with email {adminEmail} not found.");
        }
    }

    public async Task SendNotificationToAdminAndMechanicOnAppointmentBooking(int appointmentId)
    {
        string adminEmail = "admin@admin.com";  // Define the admin email

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (admin != null)
        {
            // Fetch the newly booked appointment based on appointmentId
            var newAppointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.Car.Owner)
                .Include(a => a.Mechanic)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (newAppointment != null)
            {
                var customerName = $"{newAppointment.Car.Owner.FullName}";
                var mechanicName = $"{newAppointment.Mechanic.FullName}";
                var carInfo = $"{newAppointment.Car.Make} {newAppointment.Car.Model}";

                var appointmentDateTime = new DateTime(
                    newAppointment.AppointmentDate.Year,
                    newAppointment.AppointmentDate.Month,
                    newAppointment.AppointmentDate.Day,
                    newAppointment.AppointmentTime.Hours,
                    newAppointment.AppointmentTime.Minutes,
                    0);

                var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
                var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

                var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

                // 🔔 Notification for admin
                var adminNotification = new Notification
                {
                    UserId = admin.Id,
                    Message = $"A new appointment has been booked by {customerName} for {carInfo} on {formattedAppointmentDateTime} with {mechanicName}.",
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };

                await _context.Notifications.AddAsync(adminNotification);

                // 🔔 Notification for mechanic
                if (newAppointment.Mechanic != null)
                {
                    var mechanicNotification = new Notification
                    {
                        UserId = newAppointment.Mechanic.Id,
                        Message = $"You have a new appointment for {carInfo} on {formattedAppointmentDateTime} booked by {customerName}.",
                        DateCreated = localNotificationCreatedDateTime,
                        IsRead = false
                    };

                    await _context.Notifications.AddAsync(mechanicNotification);
                }

                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogError($"Appointment with ID {appointmentId} not found.");
            }
        }
        else
        {
            _logger.LogError($"Admin user with email {adminEmail} not found.");
        }
    }



    public async Task SendNotificationToAdminAndMechanicOnAppointmentPostponing(int appointmentId)
    {
        string adminEmail = "admin@admin.com";  // Define the admin email

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (admin != null)
        {
            // Fetch the postponed appointment based on appointmentId
            var postponedAppointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.Car.Owner)
                .Include(a => a.Mechanic)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (postponedAppointment != null)
            {
                var customerName = $"{postponedAppointment.Car.Owner.FullName}";
                var mechanicName = $"{postponedAppointment.Mechanic.FullName}";
                var carInfo = $"{postponedAppointment.Car.Make} {postponedAppointment.Car.Model}";

                var appointmentDateTime = new DateTime(
                    postponedAppointment.AppointmentDate.Year,
                    postponedAppointment.AppointmentDate.Month,
                    postponedAppointment.AppointmentDate.Day,
                    postponedAppointment.AppointmentTime.Hours,
                    postponedAppointment.AppointmentTime.Minutes,
                    0);

                var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
                var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

                var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

                // 🔔 Admin notification
                var adminNotification = new Notification
                {
                    UserId = admin.Id,
                    Message = $"The appointment booked by {customerName} for {carInfo} (Assigned Mechanic: {mechanicName}) has been postponed to {formattedAppointmentDateTime}.",
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };

                await _context.Notifications.AddAsync(adminNotification);

                // 🔔 Mechanic notification
                if (postponedAppointment.Mechanic != null)
                {
                    var mechanicNotification = new Notification
                    {
                        UserId = postponedAppointment.Mechanic.Id,
                        Message = $"The appointment with {customerName} for {carInfo} has been postponed to {formattedAppointmentDateTime}.",
                        DateCreated = localNotificationCreatedDateTime,
                        IsRead = false
                    };

                    await _context.Notifications.AddAsync(mechanicNotification);
                }

                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogError($"Appointment with ID {appointmentId} not found.");
            }
        }
        else
        {
            _logger.LogError($"Admin user with email {adminEmail} not found.");
        }
    }


    public async Task SendNotificationToAdminAndCustomerOnAppointmentPostponing(int appointmentId)
    {
        string adminEmail = "admin@admin.com";

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        // Fetch the postponed appointment based on appointmentId
        var postponedAppointment = await _context.Appointments
            .Include(a => a.Car)
            .Include(a => a.Car.Owner)
            .Include(a => a.Mechanic)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (admin != null && postponedAppointment != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{postponedAppointment.Car.Owner.FullName}";
            var mechanicName = $"{postponedAppointment.Mechanic.FullName}";
            var carInfo = $"{postponedAppointment.Car.Make} {postponedAppointment.Car.Model}";

            // Create a DateTime object with the appointment date and time
            var appointmentDateTime = new DateTime(postponedAppointment.AppointmentDate.Year, postponedAppointment.AppointmentDate.Month, postponedAppointment.AppointmentDate.Day, postponedAppointment.AppointmentTime.Hours, postponedAppointment.AppointmentTime.Minutes, 0);

            // Convert the appointment date and time to the user's local time zone
            var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
            var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Create the notification message for the admin
            var adminNotificationMessage = $"The appointment booked by {customerName} for {carInfo} (Assigned Mechanic: {mechanicName}) has been postponed to {formattedAppointmentDateTime} by assigned mechanic : {mechanicName}.";

            // Create a new notification for the admin
            var adminNotification = new Notification
            {
                UserId = admin.Id,
                Message = adminNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Create the notification message for the customer
            var customerNotificationMessage = $"Your appointment for {carInfo} has been postponed to {formattedAppointmentDateTime} by the assigned mechanic.";

            // Create a new notification for the customer
            var customerNotification = new Notification
            {
                UserId = postponedAppointment.Car.Owner.Id,
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add the notifications to the database and save them
            await _context.Notifications.AddAsync(adminNotification);
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (admin == null)
            {
                _logger.LogError($"Admin user with email {adminEmail} not found.");
            }
            else if (postponedAppointment == null)
            {
                _logger.LogError($"Appointment with ID {appointmentId} not found.");
            }
        }
    }


    public async Task SendNotificationToCustomerAndMechanicOnAppointmentPostponing(int appointmentId)
    {
        // Fetch the postponed appointment based on appointmentId
        var postponedAppointment = await _context.Appointments
            .Include(a => a.Car)
            .Include(a => a.Car.Owner)
            .Include(a => a.Mechanic)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (postponedAppointment != null && postponedAppointment.Car.Owner != null && postponedAppointment.Mechanic != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{postponedAppointment.Car.Owner.FullName}";
            var mechanicName = $"{postponedAppointment.Mechanic.FullName}";
            var carInfo = $"{postponedAppointment.Car.Make} {postponedAppointment.Car.Model}";

            // Create a DateTime object with the appointment date and time
            var appointmentDateTime = new DateTime(
                postponedAppointment.AppointmentDate.Year,
                postponedAppointment.AppointmentDate.Month,
                postponedAppointment.AppointmentDate.Day,
                postponedAppointment.AppointmentTime.Hours,
                postponedAppointment.AppointmentTime.Minutes,
                0);

            // Convert the appointment date and time to the user's local time zone
            var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
            var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Notification message for the customer
            var customerNotificationMessage = $"The appointment booked for {carInfo} (Assigned Mechanic: {mechanicName}) has been postponed to {formattedAppointmentDateTime} by the admin.";

            // Notification message for the mechanic
            var mechanicNotificationMessage = $"Your assigned appointment for {carInfo} has been postponed to {formattedAppointmentDateTime} by the admin.";

            // Create the notifications
            var customerNotification = new Notification
            {
                UserId = postponedAppointment.Car.Owner.Id,  // Notification sent to the customer
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            var mechanicNotification = new Notification
            {
                UserId = postponedAppointment.Mechanic.Id,  // Notification sent to the mechanic
                Message = mechanicNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add both notifications and save
            await _context.Notifications.AddRangeAsync(customerNotification, mechanicNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (postponedAppointment == null)
            {
                _logger.LogError($"Appointment with ID {appointmentId} not found.");
            }
            else if (postponedAppointment.Car.Owner == null)
            {
                _logger.LogError($"Car owner not found for the appointment with ID {appointmentId}.");
            }
            else if (postponedAppointment.Mechanic == null)
            {
                _logger.LogError($"Mechanic not found for the appointment with ID {appointmentId}.");
            }
        }
    }



    public async Task SendNotificationToAdminAndMechanicOnAppointmentCancelling(int appointmentId)
    {
        string adminEmail = "admin@admin.com";  // Define the admin email

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        // Fetch the cancelled appointment
        var cancelledAppointment = await _context.Appointments
            .Include(a => a.Car)
            .Include(a => a.Car.Owner)
            .Include(a => a.Mechanic)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (admin == null)
        {
            _logger.LogError($"Admin user with email {adminEmail} not found.");
            return;
        }

        if (cancelledAppointment == null)
        {
            _logger.LogError($"Appointment with ID {appointmentId} not found.");
            return;
        }

        // Extract details
        var customer = cancelledAppointment.Car.Owner;
        var mechanic = cancelledAppointment.Mechanic;
        var carInfo = $"{cancelledAppointment.Car.Make} {cancelledAppointment.Car.Model}";
        var appointmentDateTime = cancelledAppointment.AppointmentDate.ToString("yyyy-MM-dd HH:mm");
        var now = DateTime.Now.ToLocalTime();

        // 🔔 Admin notification
        var adminNotification = new Notification
        {
            UserId = admin.Id,
            Message = $"The appointment booked for {carInfo} (Assigned Mechanic: {mechanic.FullName}) on Date: {appointmentDateTime} has been cancelled by {customer.FullName}.",
            DateCreated = now,
            IsRead = false
        };
        await _context.Notifications.AddAsync(adminNotification);

        // 🔔 Mechanic notification
        if (mechanic != null)
        {
            var mechanicNotification = new Notification
            {
                UserId = mechanic.Id,
                Message = $"The appointment for {carInfo} with {customer.FullName} on {appointmentDateTime} has been cancelled by the customer.",
                DateCreated = now,
                IsRead = false
            };
            await _context.Notifications.AddAsync(mechanicNotification);
        }

        await _context.SaveChangesAsync();
    }



    public async Task SendNotificationToCustomerAndMechanicOnCarAssigning(int carId)
    {
        // Fetch the car based on the carId
        var car = await _context.Cars
            .Include(c => c.Owner)
            .Include(c => c.CarMechanicAssignments)
            .ThenInclude(cma => cma.Mechanic)
            .FirstOrDefaultAsync(c => c.Id == carId);

        if (car != null && car.CarMechanicAssignments.Any())
        {
            // Get the most recent mechanic assigned to the car
            var latestMechanicAssignment = car.CarMechanicAssignments.OrderByDescending(cma => cma.AssignedDate).FirstOrDefault();
            var mechanic = latestMechanicAssignment?.Mechanic;

            if (car.Owner != null && mechanic != null)
            {
                var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

                // Create a notification for the customer (owner)
                var customerNotification = new Notification
                {
                    UserId = car.Owner.Id,
                    Message = $"Your car ({car.Make} {car.Model}) has been assigned to {mechanic.FullName}.",
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };

                // Create a notification for the mechanic
                var mechanicNotification = new Notification
                {
                    UserId = mechanic.Id,
                    Message = $"You have been assigned to a new car: {car.Make} {car.Model}.",
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };

                // Add both notifications and save
                await _context.Notifications.AddRangeAsync(customerNotification, mechanicNotification);
                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogError($"Car owner or assigned mechanic not found for the car with ID {carId}.");
            }
        }
        else
        {
            _logger.LogError($"Car with ID {carId} not found or no mechanic assignments found.");
        }
    }


    public async Task SendNotificationToCustomerAndMechanicOnAppointmentCancelling(int appointmentId)
    {
        // Fetch the cancelled appointment based on appointmentId
        var cancelledAppointment = await _context.Appointments
            .Include(a => a.Car)
            .Include(a => a.Car.Owner)
            .Include(a => a.Mechanic)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (cancelledAppointment != null && cancelledAppointment.Car.Owner != null && cancelledAppointment.Mechanic != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{cancelledAppointment.Car.Owner.FullName}";
            var mechanicName = $"{cancelledAppointment.Mechanic.FullName}";
            var carInfo = $"{cancelledAppointment.Car.Make} {cancelledAppointment.Car.Model}";

            // Create a DateTime object with the appointment date and time
            var appointmentDateTime = new DateTime(
                cancelledAppointment.AppointmentDate.Year,
                cancelledAppointment.AppointmentDate.Month,
                cancelledAppointment.AppointmentDate.Day,
                cancelledAppointment.AppointmentTime.Hours,
                cancelledAppointment.AppointmentTime.Minutes,
                0);

            // Convert the appointment date and time to the user's local time zone
            var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
            var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Notification message for the customer
            var customerNotificationMessage = $"The appointment booked for {carInfo} (Assigned Mechanic: {mechanicName}) has been cancelled by the admin.";

            // Notification message for the mechanic
            var mechanicNotificationMessage = $"Your appointment for {carInfo} scheduled on {formattedAppointmentDateTime} has been cancelled by the admin.";

            // Create notifications
            var customerNotification = new Notification
            {
                UserId = cancelledAppointment.Car.Owner.Id,  // Notification sent to the customer
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            var mechanicNotification = new Notification
            {
                UserId = cancelledAppointment.Mechanic.Id,  // Notification sent to the mechanic
                Message = mechanicNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add both notifications and save
            await _context.Notifications.AddRangeAsync(customerNotification, mechanicNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (cancelledAppointment == null)
            {
                _logger.LogError($"Appointment with ID {appointmentId} not found.");
            }
            else if (cancelledAppointment.Car.Owner == null)
            {
                _logger.LogError($"Car owner not found for the appointment with ID {appointmentId}.");
            }
            else if (cancelledAppointment.Mechanic == null)
            {
                _logger.LogError($"Mechanic not found for the appointment with ID {appointmentId}.");
            }
        }
    }

    public async Task SendNotificationToCustomerAndAdminOnAppointmentCompletion(int appointmentId)
    {
        string adminEmail = "admin@admin.com";

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        // Fetch the completed appointment based on appointmentId
        var completedAppointment = await _context.Appointments
            .Include(a => a.Car)
            .Include(a => a.Car.Owner)
            .Include(a => a.Mechanic)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (completedAppointment != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{completedAppointment.Car.Owner.FullName}";
            var mechanicName = $"{completedAppointment.Mechanic.FullName}";
            var carInfo = $"{completedAppointment.Car.Make} {completedAppointment.Car.Model}";

            // Create a DateTime object with the appointment date and time
            var appointmentDateTime = new DateTime(completedAppointment.AppointmentDate.Year, completedAppointment.AppointmentDate.Month, completedAppointment.AppointmentDate.Day, completedAppointment.AppointmentTime.Hours, completedAppointment.AppointmentTime.Minutes, 0);

            // Convert the appointment date and time to the user's local time zone
            var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
            var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Create the notification message for the admin
            var adminNotificationMessage = $"The appointment booked by {customerName} for {carInfo} (Assigned Mechanic: {mechanicName}) has been completed.";

            // Create a new notification for the admin
            Notification adminNotification = null;
            if (admin != null)
            {
                adminNotification = new Notification
                {
                    UserId = admin.Id, // Notification sent to the admin
                    Message = adminNotificationMessage,
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };
            }

            // Create the notification message for the customer
            var customerNotificationMessage = $"Your appointment for {carInfo} has been completed.";

            // Create a new notification for the customer
            var customerNotification = new Notification
            {
                UserId = completedAppointment.Car.Owner.Id,
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add the notifications to the database and save them
            if (adminNotification != null)
            {
                await _context.Notifications.AddAsync(adminNotification);
            }
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogError($"Appointment with ID {appointmentId} not found.");
        }
    }

    public async Task SendNotificationToAdminAndCustomerOnAppointmentApproval(int appointmentId)
    {
        string adminEmail = "admin@admin.com";

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        // Fetch the approved appointment based on appointmentId
        var approvedAppointment = await _context.Appointments
            .Include(a => a.Car)
            .Include(a => a.Car.Owner)
            .Include(a => a.Mechanic)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (approvedAppointment != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{approvedAppointment.Car.Owner.FullName}";
            var mechanicName = $"{approvedAppointment.Mechanic.FullName}";
            var carInfo = $"{approvedAppointment.Car.Make} {approvedAppointment.Car.Model}";

            // Create a DateTime object with the appointment date and time
            var appointmentDateTime = new DateTime(approvedAppointment.AppointmentDate.Year, approvedAppointment.AppointmentDate.Month, approvedAppointment.AppointmentDate.Day, 9, 30, 0);

            // Convert the appointment date and time to the user's local time zone
            var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
            var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Create the notification message for the admin
            var adminNotificationMessage = $"The appointment booked by {customerName} for {carInfo} (Assigned Mechanic: {mechanicName}) has been approved.";

            // Create a new notification for the admin
            Notification adminNotification = null;
            if (admin != null)
            {
                adminNotification = new Notification
                {
                    UserId = admin.Id, // Notification sent to the admin
                    Message = adminNotificationMessage,
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };
            }

            // Create the notification message for the customer
            var customerNotificationMessage = $"Your appointment for {carInfo} on {formattedAppointmentDateTime} has been approved by {mechanicName}.";

            // Create a new notification for the customer
            var customerNotification = new Notification
            {
                UserId = approvedAppointment.Car.Owner.Id,
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add the notifications to the database and save them
            if (adminNotification != null)
            {
                await _context.Notifications.AddAsync(adminNotification);
            }
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogError($"Appointment with ID {appointmentId} not found.");
        }
    }

    public async Task SendNotificationToCustomerAndAdminOnAppointmentCancelling(int appointmentId)
    {
        string adminEmail = "admin@admin.com";

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        // Fetch the approved appointment based on appointmentId
        var cancelledAppointment = await _context.Appointments
            .Include(a => a.Car)
            .Include(a => a.Car.Owner)
            .Include(a => a.Mechanic)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (cancelledAppointment != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{cancelledAppointment.Car.Owner.FullName}";
            var mechanicName = $"{cancelledAppointment.Mechanic.FullName}";
            var carInfo = $"{cancelledAppointment.Car.Make} {cancelledAppointment.Car.Model}";

            // Create a DateTime object with the appointment date and time
            var appointmentDateTime = new DateTime(cancelledAppointment.AppointmentDate.Year, cancelledAppointment.AppointmentDate.Month, cancelledAppointment.AppointmentDate.Day, 9, 30, 0);

            // Convert the appointment date and time to the user's local time zone
            var localAppointmentDateTime = TimeZoneInfo.ConvertTimeFromUtc(appointmentDateTime, TimeZoneInfo.Local);
            var formattedAppointmentDateTime = localAppointmentDateTime.ToString("yyyy-MM-dd hh:mm tt");

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Create the notification message for the admin
            var adminNotificationMessage = $"The appointment booked by {customerName} for {carInfo}  has been cancelled by (Assigned Mechanic: {mechanicName}) .";

            // Create a new notification for the admin
            Notification adminNotification = null;
            if (admin != null)
            {
                adminNotification = new Notification
                {
                    UserId = admin.Id, // Notification sent to the admin
                    Message = adminNotificationMessage,
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };
            }

            // Create the notification message for the customer
            var customerNotificationMessage = $"Your appointment for {carInfo} on {formattedAppointmentDateTime} has been cancelled by {mechanicName}.";

            // Create a new notification for the customer
            var customerNotification = new Notification
            {
                UserId = cancelledAppointment.Car.Owner.Id,
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add the notifications to the database and save them
            if (adminNotification != null)
            {
                await _context.Notifications.AddAsync(adminNotification);
            }
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogError($"Appointment with ID {appointmentId} not found.");
        }
    }


    public async Task SendNotificationToCustomerAndAdminOnMechanicReport(int mechanicReportsId)
    {
        string adminEmail = "admin@admin.com";

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        // Fetch the mechanic report based on the mechanicReportsId
        var mechanicReport = await _context.MechanicReports
            .Include(mr => mr.Car)
            .Include(mr => mr.Car.Owner)
            .Include(mr => mr.Mechanic)
            .FirstOrDefaultAsync(mr => mr.Id == mechanicReportsId);

        if (mechanicReport != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{mechanicReport.Car.Owner.FullName}";
            var mechanicName = $"{mechanicReport.Mechanic.FullName}";
            var carInfo = $"{mechanicReport.Car.Make} {mechanicReport.Car.Model}";

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Create the notification message for the admin
            var adminNotificationMessage = $"A mechanic report has been created for the appointment booked by {customerName} for {carInfo} (Assigned Mechanic: {mechanicName}).";

            Notification adminNotification = null;
            // Create a new notification for the admin
            if (admin != null)
            {
                adminNotification = new Notification
                {
                    UserId = admin.Id, // Notification sent to the admin
                    Message = adminNotificationMessage,
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };
            }

            // Create the notification message for the customer
            var customerNotificationMessage = $"A mechanic report has been created for your appointment for {carInfo}.";

            // Create a new notification for the customer
            var customerNotification = new Notification
            {
                UserId = mechanicReport.Car.Owner.Id,
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add the notifications to the database and save them
            await _context.Notifications.AddAsync(adminNotification);
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogError($"Mechanic report with ID {mechanicReportsId} not found.");
        }
    }


    public async Task SendNotificationToCustomerAndAdminOnMechanicReportEditing(int reportId)
    {
        string adminEmail = "admin@admin.com";

        // Fetch the admin user from the database
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

        // Fetch the mechanic report based on the mechanicReportsId
        var mechanicReport = await _context.MechanicReports
            .Include(mr => mr.Car)
            .Include(mr => mr.Car.Owner)
            .Include(mr => mr.Mechanic)
            .FirstOrDefaultAsync(mr => mr.Id == reportId);

        if (mechanicReport != null)
        {
            // Get the customer, mechanic, and car information
            var customerName = $"{mechanicReport.Car.Owner.FullName}";
            var mechanicName = $"{mechanicReport.Mechanic.FullName}";
            var carInfo = $"{mechanicReport.Car.Make} {mechanicReport.Car.Model}";

            // Get the current time in the user's local time zone
            var localNotificationCreatedDateTime = DateTime.Now.ToLocalTime();

            // Create the notification message for the admin
            var adminNotificationMessage = $" (Assigned Mechanic: {mechanicName}) has made report edits  for the appointment booked by {customerName} for {carInfo} .";

            Notification adminNotification = null;
            // Create a new notification for the admin
            if (admin != null)
            {
                adminNotification = new Notification
                {
                    UserId = admin.Id, // Notification sent to the admin
                    Message = adminNotificationMessage,
                    DateCreated = localNotificationCreatedDateTime,
                    IsRead = false
                };
            }

            // Create the notification message for the customer
            var customerNotificationMessage = $"The mechanic report for {carInfo} has been edited for your appointment for {carInfo}.";

            // Create a new notification for the customer
            var customerNotification = new Notification
            {
                UserId = mechanicReport.Car.Owner.Id,
                Message = customerNotificationMessage,
                DateCreated = localNotificationCreatedDateTime,
                IsRead = false
            };

            // Add the notifications to the database and save them
            await _context.Notifications.AddAsync(adminNotification);
            await _context.Notifications.AddAsync(customerNotification);
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogError($"Mechanic report with ID {reportId} not found.");
        }
    }

    public async Task SendNotificationToMechanicOnAddedFault(int faultId)
    {
        var fault = await _context.Faults
            .Include(f => f.Car)
            .ThenInclude(c => c.CarMechanicAssignments)
            .ThenInclude(cma => cma.Mechanic)
            .FirstOrDefaultAsync(f => f.Id == faultId);

        if (fault != null && fault.Car != null && fault.Car.CarMechanicAssignments.Any())
        {
            var latestMechanicAssignment = fault.Car.CarMechanicAssignments
                .OrderByDescending(cma => cma.AssignedDate)
                .FirstOrDefault();

            var assignedMechanic = latestMechanicAssignment?.Mechanic;

            if (assignedMechanic != null)
            {
                var carInfo = $"{fault.Car.Make} {fault.Car.Model}";
                var notificationMessage = $"A new fault has been reported for {carInfo}: \"{fault.Description}\".";

                var notification = new Notification
                {
                    UserId = assignedMechanic.Id,
                    Message = notificationMessage,
                    DateCreated = DateTime.Now.ToLocalTime(),
                    IsRead = false
                };

                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogError($"No assigned mechanic found for the car related to fault ID {faultId}.");
            }
        }
        else
        {
            _logger.LogError($"Fault with ID {faultId} not found, or no assigned mechanic.");
        }
    }


}
