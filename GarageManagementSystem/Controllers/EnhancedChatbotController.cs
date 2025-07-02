using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using GarageManagementSystem.Data;
using GarageManagementSystem.Models;
using System.Web.Http;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;
using HttpPostAttribute = Microsoft.AspNetCore.Mvc.HttpPostAttribute;
using FromBodyAttribute = Microsoft.AspNetCore.Mvc.FromBodyAttribute;
using NuGet.ContentModel;
using HttpGetAttribute = Microsoft.AspNetCore.Mvc.HttpGetAttribute;

[Route("api/enhanced-chatbot")]
[ApiController]
[AllowAnonymous]
public class EnhancedChatbotController : ControllerBase
{
    private readonly ILogger<EnhancedChatbotController> _logger;
    private readonly string _apiKey;
    private readonly string _embeddingApiKey;
    private readonly string _chatApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    private readonly string _embeddingApiUrl = "https://api.openai.com/v1/embeddings";
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly HttpClient _httpClient;
    private readonly bool _useVectorSearch;

    public EnhancedChatbotController(
        IConfiguration config,
        ILogger<EnhancedChatbotController> logger,
        AppDbContext context,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _apiKey = config["TestApiKey"] ?? config["ApiKey"];
        _embeddingApiKey = config["EmbeddingApiKey"] ?? config["OpenAIApiKey"] ?? _apiKey;
        _context = context;
        _cache = cache;
        _httpClient = httpClientFactory.CreateClient("EmbeddingClient");
        _useVectorSearch = bool.Parse(config["UseVectorSearch"] ?? "true");
    }

    string ExtractMechanicNameFromQuestion(string question)
    {
        // This regex finds "mechanic" followed by one or more whitespace and then captures 
        // any sequence of letters (and spaces) until a punctuation or end-of-string.
        var match = Regex.Match(question, @"mechanic\s+([a-zA-Z\s]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return string.Empty;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> AskEnhancedChatbot([FromBody] EnhancedChatbotRequest request)
    {

        


        _logger.LogInformation("[ENHANCED] Chatbot request received: {Question}", request.Question);

        // Use the specified role or default to Customer
        string userRole = request.Role ?? "Customer";

        // Use the specified userId or default test ID
        int userId = request.UserId ?? 1;
        _logger.LogInformation("[ENHANCED] Starting enhanced chatbot query. Role={Role}, UserId={UserId}, Question={Question}",
    userRole, userId, request.Question);


        try
        {
            // First try to get a direct database answer (reuse existing logic)
            string directAnswer = await GetDirectDatabaseAnswer(request.Question, userRole, userId);
            if (!string.IsNullOrEmpty(directAnswer))
            {
                _logger.LogInformation("[ENHANCED] Direct database answer found: {Answer}", directAnswer);
                return Ok(new { answer = directAnswer, source = "database" });
            }

            // Fetch relevant context with semantic search
            List<string> contextItems = new List<string>();
            List<float> relevanceScores = new List<float>();

            if (_useVectorSearch && !(request.SkipVectorSearch ?? false))
            {
                (contextItems, relevanceScores) = await GetSemanticSearchResults(request.Question, userRole, userId);
                _logger.LogInformation("[ENHANCED] Retrieved {Count} semantic search results", contextItems.Count);
            }
            // 2. Add this after fetching context items
            _logger.LogInformation("[ENHANCED] Context retrieval complete. Retrieved {ContextCount} semantic search results with max relevance {MaxRelevance}",
                contextItems.Count, relevanceScores.Any() ? relevanceScores.Max() : 0);
            // Also get traditional context as fallback
            string traditionalContext = await RetrieveContextBasedOnQuery(request.Question, userRole, userId);

            // Combine contexts, putting semantic search results first
            StringBuilder combinedContext = new StringBuilder();

            _logger.LogInformation("[ENHANCED] After filtering by relevance threshold {Threshold}, {FilteredCount} context items remain",
            0.5, contextItems.Count(i => relevanceScores[contextItems.IndexOf(i)] >= 0.5));

            // Add semantic search results with their relevance scores
            if (contextItems.Any())
            {
                combinedContext.AppendLine("Most relevant information from the database:");

                for (int i = 0; i < contextItems.Count; i++)
                {
                    // Only include if the relevance score is above threshold
                    if (relevanceScores[i] >= 0.5)
                    {
                        combinedContext.AppendLine($"[Relevance: {relevanceScores[i]:F2}] {contextItems[i]}");
                    }
                }

                combinedContext.AppendLine();
            }

            bool hasRelevantContext = contextItems.Any(item => relevanceScores[contextItems.IndexOf(item)] >= 0.5);

            // If no relevant context is available, provide a clear "no data" response instead of calling AI
            if (!hasRelevantContext && !(request.SkipApiCall == true))
            {
                _logger.LogWarning("[ENHANCED] No relevant context found for question: {Question}", request.Question);

                // Create appropriate response based on the question type
                string noDataResponse;

                if (request.Question.ToLower().Contains("report"))
                {
                    noDataResponse = "I don't have specific information about reports in the system. To get accurate report data, please check the reports section in the garage management dashboard.";
                }
                else if (request.Question.ToLower().Contains("car") || request.Question.ToLower().Contains("vehicle"))
                {
                    noDataResponse = "I don't have specific information about vehicles in the context. For accurate vehicle data, please check the vehicle management section in the garage system.";
                }
                else if (request.Question.ToLower().Contains("appointment") || request.Question.ToLower().Contains("schedule"))
                {
                    noDataResponse = "I don't have specific appointment information in the context. To view your schedule, please check the appointments calendar in the garage management system.";
                }
                else
                {
                    noDataResponse = "I don't have enough relevant information in the system to answer this question specifically. You might find this information in the garage management dashboard.";
                }

                return Ok(new
                {
                    answer = noDataResponse,
                    context = "No relevant context found.",
                    userRole = userRole,
                    userId = userId,
                    source = "enhanced_ai_no_data",
                    relevantContextCount = 0,
                    topRelevanceScore = 0
                });
            }
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                _logger.LogWarning("[ENHANCED] User submitted an empty question.");
                return BadRequest(new { message = "Please enter a valid question." });
            }

            // Add traditional context
            combinedContext.AppendLine("Additional context:");
            combinedContext.AppendLine(traditionalContext);

            string finalContext = combinedContext.ToString();

            // Create an option to skip API call for faster testing
            if (request.SkipApiCall == true)
            {
                return Ok(new
                {
                    answer = "[ENHANCED] This is where the AI response would be based on semantic search. The following context was retrieved: " + finalContext,
                    context = finalContext,
                    userRole = userRole,
                    userId = userId,
                    source = "test-mode"
                });
            }

            var client = new RestClient();
            var apiRequest = new RestRequest(_chatApiUrl, Method.Post);
            apiRequest.AddHeader("Authorization", $"Bearer {_apiKey}");
            apiRequest.AddHeader("Content-Type", "application/json");

            string systemPrompt = GetAdvancedSystemPrompt(userRole, finalContext);
            _logger.LogInformation("[ENHANCED] Using system prompt: {SystemPrompt}", systemPrompt);

            var chatRequest = new
            {
                model = request.Model ?? "llama-3.1-70b-online", // Updated to supported model
                max_tokens = request.MaxTokens ?? 250,
                temperature = request.Temperature ?? 0.7f,
                messages = new List<object>
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = request.Question }
                }
            };

            apiRequest.AddJsonBody(chatRequest);

            _logger.LogInformation("[ENHANCED] Sending request to AI API");
            var response = await client.ExecuteAsync(apiRequest);

            if (response.IsSuccessful)
            {
                var jsonResponse = JsonConvert.DeserializeObject<ChatCompletionResponse>(response.Content);

                if (jsonResponse?.choices != null && jsonResponse.choices.Count > 0)
                {
                    string aiResponse = jsonResponse.choices[0].message.content;
                    _logger.LogInformation("[ENHANCED] AI Response: {AiResponse}", aiResponse);

                    // Cache the response for future similar questions
                    CacheResponse(request.Question, aiResponse, userRole, userId);

                    return Ok(new
                    {
                        answer = aiResponse,
                        context = finalContext,
                        userRole = userRole,
                        userId = userId,
                        source = "enhanced_ai",
                        relevantContextCount = contextItems.Count,
                        topRelevanceScore = relevanceScores.Any() ? relevanceScores.Max() : 0
                    });
                }
                else
                {
                    _logger.LogError("[ENHANCED] Invalid AI response format");
                    return StatusCode(500, new { message = "Failed to process AI response." });
                }
            }
            else
            {
                _logger.LogError("[ENHANCED] AI API request failed: {StatusCode} - {Content}", response.StatusCode, response.Content);
                return StatusCode((int)response.StatusCode, new
                {
                    message = "Failed to get response from AI.",
                    error = response.Content
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ENHANCED] An error occurred while processing the enhanced chatbot request");
            return StatusCode(500, new
            {
                message = "An error occurred. Please try again.",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    private async Task<(List<string>, List<float>)> GetSemanticSearchResults(string question, string userRole, int userId)
    {
        var contextItems = new List<string>();
        var relevanceScores = new List<float>();

        try
        {
            // 1. Get embedding for the question
            var questionEmbedding = await GetEmbedding(question);
            if (questionEmbedding == null || questionEmbedding.Count == 0)
            {
                _logger.LogWarning("[ENHANCED] Failed to get embedding for question");
                return (contextItems, relevanceScores);
            }

            // 2. Generate contextual data from database based on user role
            var dbContextItems = await GenerateDatabaseContextItems(userRole, userId);

            // 3. Get embeddings for each context item (could be cached for production)
            var embeddingTasks = dbContextItems.Select(item => GetEmbedding(item)).ToList();
            var embeddings = await Task.WhenAll(embeddingTasks);

            // 4. Calculate similarity scores
            for (int i = 0; i < dbContextItems.Count; i++)
            {
                if (embeddings[i] != null && embeddings[i].Count > 0)
                {
                    float similarityScore = CalculateCosineSimilarity(questionEmbedding, embeddings[i]);
                    contextItems.Add(dbContextItems[i]);
                    relevanceScores.Add(similarityScore);
                }
            }

            // 5. Sort by relevance score (descending)
            var sortedResults = contextItems.Zip(relevanceScores, (item, score) => (Item: item, Score: score))
                                           .OrderByDescending(pair => pair.Score)
                                           .ToList();

            // 6. Return top results
            contextItems = sortedResults.Select(pair => pair.Item).Take(5).ToList();
            relevanceScores = sortedResults.Select(pair => pair.Score).Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ENHANCED] Error in semantic search");
        }

        return (contextItems, relevanceScores);
    }

    private async Task<List<string>> GenerateDatabaseContextItems(string userRole, int userId)
    {
        var contextItems = new List<string>();

        try
        {
            if (userRole == "Customer")
            {
                // Get user info
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    contextItems.Add($"Customer Information: Name: {user.FullName}, Email: {user.Email}, Phone: {user.PhoneNumber}");
                }

                // Get user's cars
                var cars = await _context.Cars
                    .Where(c => c.OwnerId == userId || c.UserId == userId)
                    .ToListAsync();

                foreach (var car in cars)
                {
                    contextItems.Add($"Vehicle: {car.Make} {car.Model} ({car.Year}), License: {car.LicenseNumber}, Color: {car.Color}, Fuel: {car.FuelType}, Chassis: {car.ChassisNumber}");

                    // Get car faults
                    var faults = await _context.Faults
                        .Where(f => f.CarId == car.Id)
                        .ToListAsync();

                    foreach (var fault in faults)
                    {
                        contextItems.Add($"Fault for {car.Make} {car.Model}: {fault.Description}, Reported on {fault.DateReportedOn:yyyy-MM-dd}");
                    }
                }

                // Get appointments
                var appointments = await _context.Appointments
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Take(10)
                    .ToListAsync();

                foreach (var appointment in appointments)
                {
                    string carInfo = "Unknown car";
                    if (appointment.CarId != 0)
                    {
                        var car = cars.FirstOrDefault(c => c.Id == appointment.CarId);
                        if (car != null)
                        {
                            carInfo = $"{car.Make} {car.Model}";
                        }
                    }

                    contextItems.Add($"Appointment on {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.AppointmentTime} for {carInfo}. Status: {appointment.Status}. Notes: {appointment.Notes}. Mechanic: {appointment.MechanicName}");
                }
            }
            else if (userRole == "Mechanic")
            {
                // Get mechanic info
                var mechanic = await _context.Users.FindAsync(userId);
                if (mechanic != null)
                {
                    string mechanicName = mechanic.FullName;
                    contextItems.Add($"Mechanic Information: Name: {mechanicName}, Email: {mechanic.Email}, Phone: {mechanic.PhoneNumber}");

                    // Get assigned appointments
                    var appointments = await _context.Appointments
                        .Where(a => a.MechanicName != null && a.MechanicName.Contains(mechanicName))
                        .OrderBy(a => a.AppointmentDate)
                        .Take(10)
                        .ToListAsync();

                    foreach (var appointment in appointments)
                    {
                        var car = await _context.Cars.FindAsync(appointment.CarId);
                        string carInfo = car != null ? $"{car.Make} {car.Model} ({car.Year})" : "Unknown Car";

                        contextItems.Add($"Assignment: {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.AppointmentTime}. Car: {carInfo}. Customer notes: {appointment.Notes}. Status: {appointment.Status}");
                    }

                    // Get submitted reports
                    var reports = await _context.MechanicReports
                        .Where(r => r.MechanicId == userId)
                        .OrderByDescending(r => r.DateReported)
                        .Take(10)
                        .ToListAsync();

                    foreach (var report in reports)
                    {
                        var car = await _context.Cars.FindAsync(report.CarId);
                        string carInfo = car != null ? $"{car.Make} {car.Model}" : "Unknown Car";

                        contextItems.Add($"Report for {carInfo}: {report.ServiceDetails}. Service Fee: {report.ServiceFee:C}. Total: {report.TotalPrice:C}. Date: {report.DateReported:yyyy-MM-dd}");
                    }
                }
            }
            else if (userRole == "Admin")
            {
                // System statistics
                int totalCars = await _context.Cars.CountAsync();
                int totalCustomers = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                               ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Customer").Id))
                    .CountAsync();
                int totalMechanics = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                               ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Mechanic").Id))
                    .CountAsync();
                int totalAppointments = await _context.Appointments.CountAsync();

                contextItems.Add($"System Statistics: There are {totalCars} cars, {totalCustomers} customers, {totalMechanics} mechanics, and {totalAppointments} total appointments in the system.");

                // Add the total users count here
                int totalUsers = await _context.Users.CountAsync();
                contextItems.Add($"System Statistics: There are {totalUsers} total users in the system.");

                // Pending appointments
                var pendingAppointments = await _context.Appointments
                    .Where(a => a.Status.ToLower() == "pending")
                    .CountAsync();
                contextItems.Add($"There are {pendingAppointments} pending appointments that need attention.");

                // Today's appointments
                var todayAppointments = await _context.Appointments
                    .Where(a => a.AppointmentDate == DateTime.Today)
                    .CountAsync();
                contextItems.Add($"There are {todayAppointments} appointments scheduled for today.");

                // Get relationships between entities (cars per customer, appointments per car, etc.)
                var carsPerCustomerAvg = await _context.Users
                    .Where(u => u.Cars.Count > 0)
                    .Select(u => new { UserId = u.Id, CarCount = u.Cars.Count })
                    .AverageAsync(u => u.CarCount);
                contextItems.Add($"On average, each active customer has {carsPerCustomerAvg:F1} cars registered in the system.");

                // Most common car makes/models
                var topCarMakes = await _context.Cars
                    .GroupBy(c => c.Make)
                    .Select(g => new { Make = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(3)
                    .ToListAsync();
                if (topCarMakes.Any())
                {
                    contextItems.Add($"The most common car makes in our system are: " +
                        string.Join(", ", topCarMakes.Select(m => $"{m.Make} ({m.Count})")));
                }

                // Service metrics
                var avgServiceFee = await _context.MechanicReports.AverageAsync(r => r.ServiceFee);
                contextItems.Add($"The average service fee across all mechanic reports is {avgServiceFee:C}.");

                // Notification metrics
                var totalNotifications = await _context.Notifications.CountAsync();
                var unreadNotifications = await _context.Notifications.CountAsync(n => !n.IsRead);
                contextItems.Add($"There are {totalNotifications} total notifications in the system, with {unreadNotifications} unread.");

                // Cars with faults
                var carsWithFaults = await _context.Cars
                    .Where(c => c.Faults.Any(f => !f.ResolutionStatus))
                    .CountAsync();
                contextItems.Add($"There are {carsWithFaults} cars with unresolved faults that need attention.");

                // Recent appointments
                var recentAppointments = await _context.Appointments
                    .OrderByDescending(a => a.AppointmentDate)
                    .Take(5)
                    .ToListAsync();

                foreach (var appointment in recentAppointments)
                {
                    var car = await _context.Cars.FindAsync(appointment.CarId);
                    string carInfo = car != null ? $"{car.Make} {car.Model}" : "Unknown Car";

                    contextItems.Add($"Recent appointment: {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.AppointmentTime}. Car: {carInfo}. Status: {appointment.Status}. Mechanic: {appointment.MechanicName}");
                }

                // Busiest mechanics
                var mechanicAppointmentCounts = await _context.Appointments
                    .Where(a => a.MechanicName != null)
                    .GroupBy(a => a.MechanicName)
                    .Select(g => new { MechanicName = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(3)
                    .ToListAsync();

                foreach (var mechanic in mechanicAppointmentCounts)
                {
                    contextItems.Add($"Mechanic {mechanic.MechanicName} has {mechanic.Count} appointments assigned.");
                }

                // Add this to the Admin section of your GenerateDatabaseContextItems method
                // This explicitly adds the total users count to the context

                // Existing statistics (cars, customers, mechanics, appointments)
                contextItems.Add($"System Statistics: There are {totalCars} cars, {totalCustomers} customers, {totalMechanics} mechanics, and {totalAppointments} total appointments in the system.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ENHANCED] Error generating database context items");
        }

        return contextItems;
    }

    private async Task<List<float>> GetEmbedding(string text)
    {
        // Try to get from cache first
        string cacheKey = $"embedding_{text.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out List<float> cachedEmbedding))
        {
            return cachedEmbedding;
        }

        try
        {
            var requestBody = new
            {
                input = text,
                model = "text-embedding-ada-002" // OpenAI embedding model
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            // Set API key in header
            using var request = new HttpRequestMessage(HttpMethod.Post, _embeddingApiUrl);
            request.Headers.Add("Authorization", $"Bearer {_embeddingApiKey}");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            // 4. Add this to the GetEmbedding method
            _logger.LogInformation("[ENHANCED] Requesting embedding for text: {TextPreview}...",
                text.Length > 30 ? text.Substring(0, 30) + "..." : text);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var responseObj = JsonConvert.DeserializeObject<EmbeddingResponse>(responseString);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[ENHANCED] Embedding API failed with status {Status}: {ErrorContent}",
                    response.StatusCode, errorContent);
                if (responseObj?.data != null && responseObj.data.Count > 0)
                {
                    var embedding = responseObj.data[0].embedding;

                    // Cache the embedding
                    _cache.Set(cacheKey, embedding, TimeSpan.FromHours(24));

                    return embedding;
                }
            }
            else
            {
                _logger.LogError("[ENHANCED] Embedding API request failed: {StatusCode}", response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[ENHANCED] Error content: {Content}", errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ENHANCED] Error getting embedding");
        }

        return new List<float>();
    }

    private float CalculateCosineSimilarity(List<float> vec1, List<float> vec2)
    {
        if (vec1.Count != vec2.Count || vec1.Count == 0)
            return 0;

        float dotProduct = 0;
        float mag1 = 0;
        float mag2 = 0;

        for (int i = 0; i < vec1.Count; i++)
        {
            dotProduct += vec1[i] * vec2[i];
            mag1 += vec1[i] * vec1[i];
            mag2 += vec2[i] * vec2[i];
        }

        mag1 = (float)Math.Sqrt(mag1);
        mag2 = (float)Math.Sqrt(mag2);

        if (mag1 == 0 || mag2 == 0)
            return 0;

        return dotProduct / (mag1 * mag2);
    }

    private void CacheResponse(string question, string answer, string userRole, int userId)
    {
        // Create a cache key based on question, role and user
        string cacheKey = $"response_{userRole}_{userId}_{question.GetHashCode()}";

        // Cache for 1 hour
        _cache.Set(cacheKey, answer, TimeSpan.FromHours(1));
    }
     
    private async Task<string> GetDirectDatabaseAnswer(string question, string userRole, int userId)
    {
        // Try getting from cache first
        string cacheKey = $"direct_{userRole}_{userId}_{question.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out string cachedAnswer))
        {
            return cachedAnswer;
        }

        // Normalize question
        question = question.ToLowerInvariant();
        var normalizedQuestion = question.ToLower(); // For consistency, use this variable throughout


        if (userRole == "Admin")
        {


            // Cars queries
            if (normalizedQuestion.Contains("how many cars") || normalizedQuestion.Contains("total cars") || normalizedQuestion.Contains("count cars"))
            {
                int carCount = await _context.Cars.CountAsync();
                string carText = carCount == 1 ? "car" : "cars";
                return $"There are {carCount} {carText} in the system.";
            }

            // Customers queries
            else if (normalizedQuestion.Contains("how many customers") || normalizedQuestion.Contains("total customers") || normalizedQuestion.Contains("count customers"))
            {
                int customerCount = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Customer").Id))
                    .CountAsync();
                string customerText = customerCount == 1 ? "customer" : "customers";
                return $"There are {customerCount} {customerText} registered in the system.";
            }

            // Users queries - NEW
            else if (normalizedQuestion.Contains("how many users") ||
                     normalizedQuestion.Contains("total users") ||
                     normalizedQuestion.Contains("count users"))
            {
                int userCount = await _context.Users.CountAsync();
                string userText = userCount == 1 ? "user" : "users";
                return $"There are {userCount} {userText} in the system.";
            }

            // User statistics/breakdown - NEW
            else if (normalizedQuestion.Contains("users by role") ||
                     normalizedQuestion.Contains("user statistics") ||
                     normalizedQuestion.Contains("user breakdown"))
            {
                int adminCount = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                           ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Admin").Id))
                    .CountAsync();

                int customerCount = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                           ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Customer").Id))
                    .CountAsync();

                int mechanicCount = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                           ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Mechanic").Id))
                    .CountAsync();

                return $"User breakdown by role: {adminCount} admins, {customerCount} customers, and {mechanicCount} mechanics.";
            }

            // Notification queries - NEW
            else if (normalizedQuestion.Contains("notification") &&
                     (normalizedQuestion.Contains("count") || normalizedQuestion.Contains("how many")))
            {
                int notificationCount = await _context.Notifications.CountAsync();
                int unreadCount = await _context.Notifications.CountAsync(n => !n.IsRead);
                return $"There are {notificationCount} total notifications in the system, with {unreadCount} unread.";
            }

            // Cars with faults queries - NEW
            else if (normalizedQuestion.Contains("cars with faults") ||
                     normalizedQuestion.Contains("unresolved faults"))
            {
                int carsWithFaults = await _context.Cars
                    .Where(c => c.Faults.Any(f => !f.ResolutionStatus))
                    .CountAsync();
                return $"There are {carsWithFaults} cars with unresolved faults that need attention.";
            }

            // Common car make queries - NEW
            else if (normalizedQuestion.Contains("common car") ||
                     normalizedQuestion.Contains("popular make") ||
                     normalizedQuestion.Contains("most cars"))
            {
                var topMake = await _context.Cars
                    .GroupBy(c => c.Make)
                    .Select(g => new { Make = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .FirstOrDefaultAsync();

                if (topMake != null)
                {
                    return $"The most common car make in our system is {topMake.Make} with {topMake.Count} vehicles.";
                }
                else
                {
                    return "There is no car data available to determine the most common make.";
                }
            }

            // Mechanics queries
            else if (normalizedQuestion.Contains("how many mechanics") || normalizedQuestion.Contains("total mechanics") || normalizedQuestion.Contains("count mechanics"))
            {
                int mechanicCount = await _context.Users
                    .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == _context.Roles.FirstOrDefault(r => r.Name == "Mechanic").Id))
                    .CountAsync();
                string mechanicText = mechanicCount == 1 ? "mechanic" : "mechanics";
                return $"There are {mechanicCount} {mechanicText} registered in the system.";
            }

            // Reports queries
            else if (normalizedQuestion.Contains("how many reports") || normalizedQuestion.Contains("total reports") || normalizedQuestion.Contains("count reports"))
            {
                int reportCount = await _context.MechanicReports.CountAsync();
                string reportText = reportCount == 1 ? "report" : "reports";
                return $"There are {reportCount} {reportText} in the system.";
            }

            // Upcoming appointments queries
            else if (normalizedQuestion.Contains("upcoming appointments") || normalizedQuestion.Contains("future appointments"))
            {
                int upcomingCount = await _context.Appointments
                    .Where(a => a.AppointmentDate >= DateTime.Today &&
                    a.Status != "Cancelled")
                    .CountAsync();
                string appointmentText = upcomingCount == 1 ? "appointment" : "appointments";
                return upcomingCount == 0
                    ? "There are no upcoming appointments at the moment."
                    : $"There are {upcomingCount} {appointmentText} scheduled.";
            }

            // Total appointments queries
            else if (question.Contains("total appointments") || question.Contains("all appointments") || question.Contains("count appointments"))
            {
                int totalCount = await _context.Appointments.CountAsync();
                string appointmentText = totalCount == 1 ? "appointment" : "appointments";
                return $"There are {totalCount} {appointmentText} in total.";
            }

            // Pending appointments queries
            else if (question.Contains("pending appointments"))
            {
                int pendingCount = await _context.Appointments
                    .Where(a => a.Status.ToLower() == "pending")
                    .CountAsync();
                string appointmentText = pendingCount == 1 ? "appointment" : "appointments";
                return $"There are {pendingCount} {appointmentText} pending in the system.";
            }

            // Approved appointments queries
            else if (question.Contains("approved appointments"))
            {
                int approvedCount = await _context.Appointments
                    .Where(a => a.Status.ToLower() == "approved")
                    .CountAsync();
                string appointmentText = approvedCount == 1 ? "appointment" : "appointments";
                return $"There are {approvedCount} {appointmentText} approved in the system.";
            }

            // Rescheduled appointments queries
            else if (question.Contains("rescheduled appointments"))
            {
                int rescheduledCount = await _context.Appointments
                    .Where(a => a.Status.ToLower() == "rescheduled")
                    .CountAsync();
                string appointmentText = rescheduledCount == 1 ? "appointment" : "appointments";
                return $"There are {rescheduledCount} {appointmentText} rescheduled.";
            }

            // Mechanic-specific appointments query
            else if (question.Contains("appointments for mechanic"))
            {
                string mechanicName = ExtractMechanicNameFromQuestion(question);
                if (!string.IsNullOrEmpty(mechanicName))
                {
                    int mechanicAppointmentCount = await _context.Appointments
                        .Where(a => a.MechanicName != null && a.MechanicName.ToLower().Contains(mechanicName.ToLower()))
                        .CountAsync();
                    string appointmentText = mechanicAppointmentCount == 1 ? "appointment" : "appointments";
                    return $"Mechanic {mechanicName} has {mechanicAppointmentCount} {appointmentText} scheduled.";
                }
            }

            // Today's appointments
            else if (question.Contains("today's appointments") || question.Contains("appointments today"))
            {
                int todaysCount = await _context.Appointments
                    .Where(a => a.AppointmentDate == DateTime.Today &&
                    a.Status != "Cancelled")
                    .CountAsync();
                string appointmentText = todaysCount == 1 ? "appointment" : "appointments";
                return $"There are {todaysCount} {appointmentText} scheduled for today.";
            }

            // This week's appointments
            else if (question.Contains("this week") || question.Contains("appointments this week"))
            {
                DateTime today = DateTime.Today;
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime startOfWeek = today.AddDays(-1 * diff).Date;
                DateTime endOfWeek = startOfWeek.AddDays(7);
                int weekCount = await _context.Appointments
                    .Where(a => a.AppointmentDate >= startOfWeek && a.AppointmentDate < endOfWeek && a.Status != "Cancelled")
                    .CountAsync();
                string appointmentText = weekCount == 1 ? "appointment" : "appointments";
                return $"There are {weekCount} {appointmentText} scheduled for this week.";
            }

            // Busiest mechanic
            else if (question.Contains("busiest mechanic") || question.Contains("most appointments by mechanic"))
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
                    return $"Mechanic {busiest.MechanicName} is the busiest with {busiest.Count} {appointmentText}.";
                }
                else
                {
                    return "No appointment data available to determine the busiest mechanic.";
                }
            }
            else if (normalizedQuestion.Contains("how many users") ||
                 normalizedQuestion.Contains("total users") ||
                 normalizedQuestion.Contains("count users"))
            {
                int userCount = await _context.Users.CountAsync();
                string userText = userCount == 1 ? "user" : "users";
                return $"There are {userCount} {userText} in the system.";
            }
            else if (normalizedQuestion.Contains("my cars") ||
                normalizedQuestion.Contains("how many cars do i") ||
                normalizedQuestion.Contains("cars registered"))
            {
                int carCount = await _context.Cars
                    .Where(c => c.OwnerId == userId || c.UserId == userId)
                    .CountAsync();
                string carText = carCount == 1 ? "car" : "cars";
                return $"You have {carCount} {carText} registered in the system.";
            }
            else if (normalizedQuestion.Contains("unresolved") && normalizedQuestion.Contains("faults"))
            {
                int faultCount = await _context.Faults
                    .Where(f => !f.ResolutionStatus &&
                           _context.Cars.Any(c => c.Id == f.CarId && (c.OwnerId == userId || c.UserId == userId)))
                    .CountAsync();
                string faultText = faultCount == 1 ? "fault" : "faults";
                return $"You have {faultCount} unresolved {faultText} across all your vehicles.";
            }

        }
        else if (userRole == "Customer")
        {
            // Check for unresolved faults FIRST before checking for cars
            // This ensures the more specific query takes precedence
            if (normalizedQuestion.Contains("fault") ||
                (normalizedQuestion.Contains("car") && normalizedQuestion.Contains("problem")))
            {
                int faultCount = await _context.Faults
                    .Where(f => !f.ResolutionStatus &&
                           _context.Cars.Any(c => c.Id == f.CarId && (c.OwnerId == userId || c.UserId == userId)))
                    .CountAsync();
                string faultText = faultCount == 1 ? "fault" : "faults";
                return $"You have {faultCount} unresolved {faultText} across all your vehicles.";
            }
            // Customer-specific queries for appointments
            else if (normalizedQuestion.Contains("my appointments") || normalizedQuestion.Contains("my upcoming appointments"))
            {
               
               
                int appointmentCount = await _context.Appointments
                    .Where(a => a.UserId == userId &&
                    a.AppointmentDate >= DateTime.Today &&
                    a.Status != "Cancelled")
                    .CountAsync();
                string appointmentText = appointmentCount == 1 ? "appointment" : "appointments";
                return appointmentCount > 0
                    ? $"You have {appointmentCount} {appointmentText} scheduled."
                    : "You don't have any upcoming appointments.";
                
            }
            // Customer's cars - MOVE THIS LOWER IN THE PRIORITY
            else if (normalizedQuestion.Contains("my cars") ||
                     normalizedQuestion.Contains("my vehicles") ||
                     normalizedQuestion.Contains("how many cars do i") ||
                     normalizedQuestion.Contains("cars registered"))
            {
                int carCount = await _context.Cars
                    .Where(c => c.OwnerId == userId || c.UserId == userId)
                    .CountAsync();
                string carText = carCount == 1 ? "car" : "cars";
                return $"You have {carCount} {carText} registered in our system.";
            }

            

            // Customer's cars
            else if (normalizedQuestion.Contains("my cars") ||
                     normalizedQuestion.Contains("my vehicles") ||
                     normalizedQuestion.Contains("how many cars do i") ||
                     normalizedQuestion.Contains("cars registered"))
            {
                int carCount = await _context.Cars
                    .Where(c => c.OwnerId == userId || c.UserId == userId)
                    .CountAsync();
                string carText = carCount == 1 ? "car" : "cars";
                return $"You have {carCount} {carText} registered in our system.";
            }

            // Unresolved faults for customer
            else if (normalizedQuestion.Contains("unresolved") && normalizedQuestion.Contains("faults"))
            {
                int faultCount = await _context.Faults
                    .Where(f => !f.ResolutionStatus &&
                           _context.Cars.Any(c => c.Id == f.CarId && (c.OwnerId == userId || c.UserId == userId)))
                    .CountAsync();
                string faultText = faultCount == 1 ? "fault" : "faults";
                return $"You have {faultCount} unresolved {faultText} across all your vehicles.";
            }

            // Customer's service history
            else if (normalizedQuestion.Contains("service history") || normalizedQuestion.Contains("past appointments"))
            {
                int pastCount = await _context.Appointments
                    .Where(a => a.UserId == userId && a.AppointmentDate < DateTime.Today)
                    .CountAsync();
                string appointmentText = pastCount == 1 ? "appointment" : "appointments";
                return $"You have {pastCount} {appointmentText} in your service history.";
            }
            
            // For unresolved faults query:

            else if (MatchesPattern(normalizedQuestion,
                              "unresolved fault", "car fault", "car problem",
                              "car issue", "vehicle problem", "vehicle issue"))
            {
                int faultCount = await _context.Faults
                    .Where(f => !f.ResolutionStatus &&
                           _context.Cars.Any(c => c.Id == f.CarId && (c.OwnerId == userId || c.UserId == userId)))
                    .CountAsync();
                string faultText = faultCount == 1 ? "fault" : "faults";
                return $"You have {faultCount} unresolved {faultText} across all your vehicles.";
            }

            // For car count query:
            else if (MatchesPattern(normalizedQuestion,
                                   "my car", "my vehicle", "how many car",
                                   "car registered", "vehicle registered"))
            {
                int carCount = await _context.Cars
                    .Where(c => c.OwnerId == userId || c.UserId == userId)
                    .CountAsync();
                string carText = carCount == 1 ? "car" : "cars";
                return $"You have {carCount} {carText} registered in our system.";
            }
        }
        else if (userRole == "Mechanic") // This was nested incorrectly before - fixed now
        {
            // Mechanic's assignments - make this more robust to match different question patterns
            if (normalizedQuestion.Contains("assign") ||
                normalizedQuestion.Contains("car") &&
                (normalizedQuestion.Contains("to me") || normalizedQuestion.Contains("my")))
            {
                try
                {
                    // Log the query for debugging
                    _logger.LogInformation("[ENHANCED] Mechanic assignment query: UserId={UserId}, Question={Question}",
                        userId, normalizedQuestion);

                    // First check CarMechanicAssignments
                    int assignmentCount = await _context.CarMechanicAssignments
                        .Where(cma => cma.MechanicId == userId)
                        .CountAsync();

                    // Also check Appointments assigned to this mechanic
                    int appointmentCount = await _context.Appointments
                        .Where(a => a.MechanicId == userId && a.Status != "Cancelled")
                        .CountAsync();

                    // Log what we found
                    _logger.LogInformation("[ENHANCED] Found {AssignmentCount} car-mechanic assignments and {AppointmentCount} appointments",
                        assignmentCount, appointmentCount);

                    // Combine both counts for total assignments
                    int totalAssignments = assignmentCount + appointmentCount;

                    string vehicleText = totalAssignments == 1 ? "vehicle" : "vehicles";

                    // If any were found, return data with source details
                    if (totalAssignments > 0)
                    {
                        string detailText = "";
                        if (assignmentCount > 0 && appointmentCount > 0)
                        {
                            detailText = $" ({assignmentCount} from direct assignments and {appointmentCount} from appointments)";
                        }

                        return $"You have {totalAssignments} {vehicleText} assigned to you{detailText}.";
                    }
                    else
                    {
                        return "You don't have any vehicles assigned to you at the moment.";
                    }
                }
                catch (Exception ex)
                {
                    // Log any errors that occur
                    _logger.LogError(ex, "[ENHANCED] Error in mechanic assignment query for UserId={UserId}", userId);
                    return "";  // Fall back to AI response on error
                }
            }
            // Mechanic's assignments
            else if (normalizedQuestion.Contains("my assignments") ||
                normalizedQuestion.Contains("cars assigned to me"))
            {
                int assignmentCount = await _context.CarMechanicAssignments
                    .Where(cma => cma.MechanicId == userId)
                    .CountAsync();
                string carText = assignmentCount == 1 ? "car" : "cars";
                return $"You have {assignmentCount} {carText} assigned to you.";
            }

            // Mechanic's reports
            else if (normalizedQuestion.Contains("report") ||
             normalizedQuestion.Contains("submitted") ||
             (normalizedQuestion.Contains("how many") && normalizedQuestion.Contains("completed")))
            {
                try
                {
                    // Log the report query for debugging
                    _logger.LogInformation("[ENHANCED] Mechanic reports query: UserId={UserId}", userId);

                    // Query for mechanic's reports
                    int reportCount = await _context.MechanicReports
                        .Where(r => r.MechanicId == userId)
                        .CountAsync();

                    _logger.LogInformation("[ENHANCED] Found {ReportCount} reports for mechanic {UserId}",
                        reportCount, userId);

                    // If you want to provide a breakdown by date range
                    int recentReportCount = await _context.MechanicReports
                        .Where(r => r.MechanicId == userId && r.DateReported >= DateTime.Today.AddDays(-30))
                        .CountAsync();

                    string reportText = reportCount == 1 ? "report" : "reports";

                    if (reportCount > 0)
                    {
                        if (recentReportCount > 0)
                        {
                            return $"You have submitted {reportCount} {reportText} in total, with {recentReportCount} in the last 30 days.";
                        }
                        else
                        {
                            return $"You have submitted {reportCount} {reportText} in total.";
                        }
                    }
                    else
                    {
                        return "You haven't submitted any reports yet.";
                    }
                }
                catch (Exception ex)
                {
                    // Log any errors
                    _logger.LogError(ex, "[ENHANCED] Error in mechanic reports query for UserId={UserId}", userId);
                    return ""; // Fall back to AI on error
                }
            }

            // Today's appointments for mechanic
            else if (normalizedQuestion.Contains("today") &&
                    (normalizedQuestion.Contains("appointments") || normalizedQuestion.Contains("schedule")))
            {
                int todayCount = await _context.Appointments
                    .Where(a => a.MechanicId == userId &&
                           a.AppointmentDate == DateTime.Today &&
                           a.Status != "Cancelled")
                    .CountAsync();
                string appointmentText = todayCount == 1 ? "appointment" : "appointments";
                return $"You have {todayCount} {appointmentText} scheduled for today.";
            }
        }


        // No direct database answer found
        return string.Empty;
    }
    private bool MatchesPattern(string normalizedQuestion, params string[] patterns)
    {
        // Check if the question contains ANY of the patterns
        return patterns.Any(pattern => normalizedQuestion.Contains(pattern));
    }

    private async Task<string> RetrieveContextBasedOnQuery(string question, string userRole, int userId)
    {
        // Reuse your existing context retrieval logic here
        // This is similar to what you have in your original controller
        StringBuilder contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("[ENHANCED] Context data:");

        // Add appropriate context based on user role
        if (userRole == "Customer")
        {
            // Get user's appointments
            var appointments = await _context.Appointments
                .Where(a => a.UserId == userId && a.AppointmentDate >= DateTime.Today)
                .OrderBy(a => a.AppointmentDate)
                .Take(3)
                .ToListAsync();

            if (appointments.Any())
            {
                contextBuilder.AppendLine($"User has {appointments.Count} upcoming appointments:");
                foreach (var appointment in appointments)
                {
                    contextBuilder.AppendLine($"- Date: {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.AppointmentTime}");
                    contextBuilder.AppendLine($"  Status: {appointment.Status}");
                    contextBuilder.AppendLine($"  Notes: {appointment.Notes}");
                }
            }
            else
            {
                contextBuilder.AppendLine("User has no upcoming appointments.");
            }

            // ... additional user context
        }

        // Add similar logic for other roles

        return contextBuilder.ToString();
    }

    // Modify your GetAdvancedSystemPrompt method to include these strict guidelines
    // Add this to all role-specific prompts:

    private string GetAdvancedSystemPrompt(string userRole, string context)
    {
        string basePrompt = $@"[ENHANCED] You are an AI assistant for a garage management system with semantic search capabilities. 
The user is a {userRole}.

Here is the relevant context from the database, sorted by relevance to the user's question:
{context}

VERY IMPORTANT INSTRUCTIONS:
1. Only provide information that is explicitly mentioned in the context above.
2. NEVER invent specific details like vehicle IDs, dates, or maintenance details that aren't in the context.
3. If specific information is not available in the context, clearly state 'I don't have enough information in the system to answer this specifically.'
4. Do not make up numbers, statistics, or specific data points.
5. If asked about something not in the context, suggest where the user might find this information instead of inventing an answer.

Based on the context available, provide a helpful, accurate, and detailed response.";

        switch (userRole)
        {
            case "Admin":
                return basePrompt + @"
For admins: 
- Provide administrative insights with actionable information
- Present statistics in a clear way
- If the info is not in the context, acknowledge what you know and what's missing
- When relevant, suggest next actions an admin might take
- Be direct and business-focused";

            case "Customer":
                return basePrompt + @"
For customers:
- Focus on their specific vehicles, appointment status, and service recommendations
- Be friendly, welcoming, and helpful
- Avoid technical jargon unless necessary
- When making service suggestions, be clear about why they're relevant
- If the customer is asking about future appointments, be precise about dates and times
- For car issues, be empathetic and solution-oriented";

            case "Mechanic":
                return basePrompt + @"
For mechanics:
- Focus on their work schedule, assigned vehicles, and technical details
- Use appropriate automotive terminology
- Be direct and practical
- If discussing a repair or diagnostic procedure, be specific
- For work assignment questions, be clear about priorities
- When discussing customer vehicles, maintain professionalism";

            default:
                return basePrompt + @"
Provide general information about garage services in a friendly, helpful manner.
If you're unsure about any specifics, acknowledge what you know and what you don't.";
        }
    }

    // 4. Add a simple test endpoint to check if the controller is accessible:
    [HttpGet("test")]
    public IActionResult TestEndpoint()
    {
        return Ok(new { message = "Enhanced chatbot API is working" });
    }
}

// Enhanced request model
public class EnhancedChatbotRequest
{
    public string Question { get; set; }
    public string Role { get; set; } // Optional role override
    public int? UserId { get; set; } // Optional user ID override
    public bool? SkipApiCall { get; set; } // Skip AI call for testing
    public bool? SkipVectorSearch { get; set; } // Skip vector search
    public string Model { get; set; } // Optional model override
    public int? MaxTokens { get; set; } // Response length
    public float? Temperature { get; set; } // Response creativity
}

// Model for OpenAI embeddings response
public class EmbeddingResponse
{
    public List<EmbeddingData> data { get; set; }
}

public class EmbeddingData
{
    public List<float> embedding { get; set; }
}