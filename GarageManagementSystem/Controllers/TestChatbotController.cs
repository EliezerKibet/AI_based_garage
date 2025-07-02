using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RestSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GarageManagementSystem.Data;
using GarageManagementSystem.Models;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace GarageManagementSystem.Controllers
{
    [Route("api/test-chatbot")]
    [ApiController]
    [AllowAnonymous] // Make sure this is accessible without authentication for testing
    public class TestChatbotController : ControllerBase
    {
        private readonly ILogger<TestChatbotController> _logger;
        private readonly string _apiKey;
        private readonly string _apiUrl = "https://api.groq.com/openai/v1/chat/completions";
        private readonly AppDbContext _context;

        public TestChatbotController(IConfiguration config,
            ILogger<TestChatbotController> logger,
            AppDbContext context)
        {
            _logger = logger;
            _apiKey = config["TestApiKey"] ?? config["ApiKey"]; // Use test key first if available
            _context = context;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskTestChatbot([FromBody] TestChatbotRequest request)
        {
            var rawRequestBody = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            _logger.LogInformation("[TEST] Raw request body: {RawBody}", rawRequestBody);

            if (request == null)
            {
                _logger.LogWarning("[TEST] Request deserialized as null");
                return BadRequest(new { message = "Invalid request format" });
            }

            _logger.LogInformation("[TEST] Deserialized request: {Request}", JsonConvert.SerializeObject(request));

            if (string.IsNullOrWhiteSpace(request.Question))
            {
                _logger.LogWarning("[TEST] Question is null or empty");
                return BadRequest(new { message = "Question is required" });
            }

            // Log the incoming request for debugging
            _logger.LogInformation("[TEST] Received request: {Request}", JsonConvert.SerializeObject(request));

            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                _logger.LogWarning("[TEST] User submitted an empty question.");
                return BadRequest(new { message = "Please enter a valid question." });
            }

            _logger.LogInformation("[TEST] Chatbot request received: {Question}", request.Question);

            // Use the specified role or default to Customer
            string userRole = request.Role ?? "Customer";

            // Use the specified userId or default test ID
            int userId = request.UserId ?? 1;

            try
            {
                // First try to get a direct database answer
                string directAnswer = await GetDirectDatabaseAnswer(request.Question, userRole, userId);
                if (!string.IsNullOrEmpty(directAnswer))
                {
                    _logger.LogInformation("[TEST] Direct database answer found: {Answer}", directAnswer);
                    return Ok(new { answer = directAnswer, source = "database" });
                }

                // If no direct answer, get context for RAG
                string context = await RetrieveContextBasedOnQuery(request.Question, userRole, userId);

                // Create an option to skip API call for faster testing
                if (request.SkipApiCall == true)
                {
                    return Ok(new
                    {
                        answer = "[TEST MODE] This is where the AI response would be. The following context was retrieved: " + context,
                        context = context,
                        userRole = userRole,
                        userId = userId,
                        source = "test-mode"
                    });
                }

                var client = new RestClient();
                var apiRequest = new RestRequest(_apiUrl, Method.Post);
                apiRequest.AddHeader("Authorization", $"Bearer {_apiKey}");
                apiRequest.AddHeader("Content-Type", "application/json");

                string systemPrompt = GetRoleSpecificSystemPrompt(userRole, context);
                _logger.LogInformation("[TEST] Using system prompt: {SystemPrompt}", systemPrompt);

                var chatRequest = new
                {
                    model = request.Model ?? "llama-3.1-70b-versatile", // Allow model override for testing
                    max_tokens = request.MaxTokens ?? 150,
                    messages = new List<object>
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = request.Question }
                    }
                };

                apiRequest.AddJsonBody(chatRequest);

                _logger.LogInformation("[TEST] Sending request to AI API");
                var response = await client.ExecuteAsync(apiRequest);

                if (response.IsSuccessful)
                {
                    var jsonResponse = JsonConvert.DeserializeObject<ChatCompletionResponse>(response.Content);

                    if (jsonResponse?.choices != null && jsonResponse.choices.Count > 0)
                    {
                        string aiResponse = jsonResponse.choices[0].message.content;
                        _logger.LogInformation("[TEST] AI Response: {AiResponse}", aiResponse);
                        return Ok(new
                        {
                            answer = aiResponse,
                            context = context,
                            userRole = userRole,
                            userId = userId,
                            source = "ai"
                        });
                    }
                    else
                    {
                        _logger.LogError("[TEST] Invalid AI response format");
                        return StatusCode(500, new { message = "Failed to process AI response." });
                    }
                }
                else
                {
                    _logger.LogError("[TEST] AI API request failed: {StatusCode} - {Content}", response.StatusCode, response.Content);
                    return StatusCode((int)response.StatusCode, new
                    {
                        message = "Failed to get response from AI.",
                        error = response.Content
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEST] An error occurred while processing the test chatbot request");
                return StatusCode(500, new
                {
                    message = "An error occurred. Please try again.",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        private async Task<string> GetDirectDatabaseAnswer(string question, string userRole, int userId)
        {
            // Log for debugging
            _logger.LogInformation("[TEST] Checking for direct database answer. Question: {Question}, Role: {Role}, UserId: {UserId}",
                question, userRole, userId);

            question = question.ToLowerInvariant();

            // Example: We'll use a simplified version of your existing logic
            if (userRole == "Customer")
            {
                try
                {
                    // Customer's appointments
                    if (question.Contains("my appointments") || question.Contains("my upcoming appointments"))
                    {
                        int appointmentCount = await _context.Appointments
                            .Where(a => a.UserId == userId &&
                            a.AppointmentDate >= DateTime.Today &&
                            a.Status != "Cancelled")
                            .CountAsync();
                        string appointmentText = appointmentCount == 1 ? "appointment" : "appointments";
                        return appointmentCount > 0
                            ? $"[TEST] You have {appointmentCount} {appointmentText} scheduled."
                            : "[TEST] You don't have any upcoming appointments.";
                    }

                    // Customer's cars
                    else if (question.Contains("my cars") || question.Contains("my vehicles"))
                    {
                        int carCount = await _context.Cars
                            .Where(c => c.OwnerId == userId || c.UserId == userId)
                            .CountAsync();
                        string carText = carCount == 1 ? "car" : "cars";
                        return $"[TEST] You have {carCount} {carText} registered in our system.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TEST] Error in GetDirectDatabaseAnswer");
                }
            }
            else if (userRole == "Admin")
            {
                try
                {
                    // Admin car count
                    if (question.Contains("how many cars") || question.Contains("total cars"))
                    {
                        int carCount = await _context.Cars.CountAsync();
                        string carText = carCount == 1 ? "car" : "cars";
                        return $"[TEST] There are {carCount} {carText} in the system.";
                    }

                    // Admin appointment count
                    else if (question.Contains("total appointments") || question.Contains("all appointments"))
                    {
                        int totalCount = await _context.Appointments.CountAsync();
                        string appointmentText = totalCount == 1 ? "appointment" : "appointments";
                        return $"[TEST] There are {totalCount} {appointmentText} in total.";
                    }

                    // Busiest mechanic
                    else if (question.Contains("busiest mechanic"))
                    {
                        var busiest = await _context.Appointments
                            .Where(a => a.MechanicName != null)
                            .GroupBy(a => a.MechanicName)
                            .Select(g => new { MechanicName = g.Key, Count = g.Count() })
                            .OrderByDescending(g => g.Count)
                            .FirstOrDefaultAsync();

                        if (busiest != null)
                        {
                            string appointmentText = busiest.Count == 1 ? "appointment" : "appointments";
                            return $"[TEST] Mechanic {busiest.MechanicName} is the busiest with {busiest.Count} {appointmentText}.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TEST] Error in GetDirectDatabaseAnswer for Admin");
                }
            }

            return string.Empty;
        }

        private async Task<string> RetrieveContextBasedOnQuery(string question, string userRole, int userId)
        {
            // Log for debugging
            _logger.LogInformation("[TEST] Retrieving context. Question: {Question}, Role: {Role}, UserId: {UserId}",
                question, userRole, userId);

            StringBuilder contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("[TEST MODE] Context data:");

            // Simplified context retrieval for testing
            if (userRole == "Customer")
            {
                try
                {
                    // Get user's appointments
                    var appointments = await _context.Appointments
                        .Where(a => a.UserId == userId && a.AppointmentDate >= DateTime.Today)
                        .OrderBy(a => a.AppointmentDate)
                        .Take(3)
                        .ToListAsync();

                    if (appointments.Any())
                    {
                        contextBuilder.AppendLine($"[TEST] User has {appointments.Count} upcoming appointments:");
                        foreach (var appointment in appointments)
                        {
                            contextBuilder.AppendLine($"- Date: {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.AppointmentTime}");
                            contextBuilder.AppendLine($"  Status: {appointment.Status}");
                            contextBuilder.AppendLine($"  Notes: {appointment.Notes}");
                        }
                    }
                    else
                    {
                        contextBuilder.AppendLine("[TEST] User has no upcoming appointments.");
                    }

                    // Get user's cars
                    var cars = await _context.Cars
                        .Where(c => c.OwnerId == userId || c.UserId == userId)
                        .Take(3)
                        .ToListAsync();

                    if (cars.Any())
                    {
                        contextBuilder.AppendLine($"[TEST] User has {cars.Count} registered cars:");
                        foreach (var car in cars)
                        {
                            contextBuilder.AppendLine($"- {car.Make} {car.Model} ({car.Year}) - License: {car.LicenseNumber}");
                        }
                    }
                    else
                    {
                        contextBuilder.AppendLine("[TEST] User has no registered cars.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TEST] Error retrieving context for Customer");
                    contextBuilder.AppendLine("[TEST] Error retrieving context data.");
                }
            }
            else if (userRole == "Admin")
            {
                try
                {
                    // System statistics for admin
                    int totalCars = await _context.Cars.CountAsync();
                    int totalAppointments = await _context.Appointments.CountAsync();
                    int pendingAppointments = await _context.Appointments
                        .Where(a => a.Status.ToLower() == "pending")
                        .CountAsync();

                    contextBuilder.AppendLine("[TEST] System Statistics:");
                    contextBuilder.AppendLine($"- Total Cars: {totalCars}");
                    contextBuilder.AppendLine($"- Total Appointments: {totalAppointments}");
                    contextBuilder.AppendLine($"- Pending Appointments: {pendingAppointments}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TEST] Error retrieving context for Admin");
                    contextBuilder.AppendLine("[TEST] Error retrieving admin statistics.");
                }
            }
            else if (userRole == "Mechanic")
            {
                try
                {
                    var mechanic = await _context.Users.FindAsync(userId);
                    string mechanicName = mechanic?.FullName ?? "Unknown Mechanic";

                    // Mechanic's assignments
                    var mechanicAppointments = await _context.Appointments
                        .Where(a => a.MechanicName != null && a.MechanicName.Contains(mechanicName))
                        .OrderBy(a => a.AppointmentDate)
                        .Take(3)
                        .ToListAsync();

                    if (mechanicAppointments.Any())
                    {
                        contextBuilder.AppendLine("[TEST] Mechanic assignments:");
                        foreach (var appointment in mechanicAppointments)
                        {
                            contextBuilder.AppendLine($"- Date: {appointment.AppointmentDate:yyyy-MM-dd} - Service: {appointment.Notes}");
                        }
                    }
                    else
                    {
                        contextBuilder.AppendLine("[TEST] Mechanic has no assigned appointments.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TEST] Error retrieving context for Mechanic");
                    contextBuilder.AppendLine("[TEST] Error retrieving mechanic data.");
                }
            }

            return contextBuilder.ToString();
        }

        private string GetRoleSpecificSystemPrompt(string userRole, string context)
        {
            string basePrompt = $@"[TEST MODE] You are an AI assistant for a garage management system. 
The user is a {userRole}.

Here is the relevant context from the database:
{context}

Based on this context, provide a helpful and concise response.
Always begin your response with '[TEST MODE]' to indicate this is a test environment.";

            switch (userRole)
            {
                case "Admin":
                    return basePrompt + "\nFor admins: Provide administrative insights with actionable information. Be direct and business-focused.";
                case "Customer":
                    return basePrompt + "\nFor customers: Focus on their specific vehicles, appointment status, and service recommendations. Be friendly and helpful.";
                case "Mechanic":
                    return basePrompt + "\nFor mechanics: Focus on their work schedule, assigned vehicles, and technical details. Use appropriate automotive terminology.";
                default:
                    return basePrompt + "\nProvide general information about garage services in a friendly, helpful manner.";
            }
        }
    }

    // Request model with additional test parameters
    public class TestChatbotRequest
    {
        public string Question { get; set; }
        public string Role { get; set; }
        public int? UserId { get; set; }
        public bool? SkipApiCall { get; set; }
        public string Model { get; set; }
        public int? MaxTokens { get; set; }
    }

    // Models to correctly parse AI API response (if not already defined elsewhere)
    public class ChatCompletionResponse
    {
        public List<Choice> choices { get; set; }
    }

    public class Choice
    {
        public Message message { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}