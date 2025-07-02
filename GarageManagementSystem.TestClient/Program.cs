using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Newtonsoft.Json;
using System.Diagnostics;

namespace GarageManagementSystem.TestClient
{
    class Program
    {
        // Create a single HttpClient instance to reuse
        private static readonly HttpClient client = new HttpClient(new HttpClientHandler
        {
            // For development: Allow testing with self-signed certificates
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        });

        // Configure with your API URL
        private static string baseUrl = "https://localhost:7064"; // Change to your API URL

        static async Task Main(string[] args)
        {
            SetupHttpClient();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=======================================");
            Console.WriteLine("GARAGE MANAGEMENT SYSTEM - CHATBOT TEST");
            Console.WriteLine("=======================================");
            Console.ResetColor();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nChoose an option:");
                Console.WriteLine("1. Test Original Chatbot");
                Console.WriteLine("2. Test Enhanced Chatbot");
                Console.WriteLine("3. Side-by-Side Comparison");
                Console.WriteLine("4. Test Echo Endpoint"); // Add this line
                Console.WriteLine("5. Exit");  // Change this to 5
                Console.ResetColor();

                Console.Write("\nEnter option (1-4): ");
                string option = Console.ReadLine();

                switch (option)
                {
                    case "1":
                        await TestChatbot("test-chatbot");
                        break;
                    case "2":
                        await TestChatbot("enhanced-chatbot");
                        break;
                    case "3":
                        await CompareResponses();
                        break;
                    // And in your switch statement:
                    case "4":
                        await TestEchoEndpoint();
                        break;
                    case "5":  // Update this to 5
                        return;
                    default:
                        Console.WriteLine("Invalid option, please try again.");
                        break;
                }
            }
        }

        private static void SetupHttpClient()
        {
            // Configure your base URL
            Console.Write("Enter your API URL (default: https://localhost:7177): ");
            string input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                baseUrl = input.TrimEnd('/');
            }

            Console.WriteLine($"Using API URL: {baseUrl}");

            // Set a reasonable timeout
            client.Timeout = TimeSpan.FromSeconds(60);

            // Add any required headers
            client.DefaultRequestHeaders.Add("User-Agent", "GarageSystem-ChatbotTester/1.0");
        }

        private static async Task TestChatbot(string endpoint)
        {
            string chatbotType = endpoint == "test-chatbot" ? "Original" : "Enhanced";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTesting {chatbotType} Chatbot");
            Console.WriteLine("=============================");
            Console.ResetColor();

            // Role selection
            Console.WriteLine("\nSelect user role:");
            Console.WriteLine("1. Customer");
            Console.WriteLine("2. Mechanic");
            Console.WriteLine("3. Admin");

            Console.Write("Enter role (1-3): ");
            string roleOption = Console.ReadLine();

            string role = roleOption switch
            {
                "1" => "Customer",
                "2" => "Mechanic",
                "3" => "Admin",
                _ => "Customer"
            };

            // User ID input
            Console.Write("Enter User ID (default: 1): ");
            string userIdInput = Console.ReadLine();
            int userId = 1;

            if (!string.IsNullOrWhiteSpace(userIdInput) && int.TryParse(userIdInput, out int parsedId))
            {
                userId = parsedId;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTesting as {role} (User ID: {userId})");
            Console.WriteLine("Type 'exit' to return to main menu");
            Console.ResetColor();

            // Start chat loop
            while (true)
            {
                Console.Write("\nYour question: ");
                string question = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(question))
                    continue;

                if (question.ToLower() == "exit")
                    break;

                try
                {
                    // In your TestChatbot method in the test client
                    var requestObject = new
                    {
                        Question = question,
                        Role = role,
                        UserId = userId,
                        Model = "llama-3.3-70b-versatile", // Add this line
                        MaxTokens = 150                    // Add this line if needed
                    };

                    // Create request content
                    var content = new StringContent(
                        JsonConvert.SerializeObject(requestObject),
                        Encoding.UTF8,
                        "application/json");

                    Console.WriteLine("Sending request...");

                    // Measure response time
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // Send request to API
                    var response = await client.PostAsync($"{baseUrl}/api/{endpoint}/ask", content);

                    stopwatch.Stop();
                    long responseTime = stopwatch.ElapsedMilliseconds;

                    // Process response
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var responseJson = JsonDocument.Parse(responseBody);

                        // Extract answer
                        if (responseJson.RootElement.TryGetProperty("answer", out var answerElement))
                        {
                            string answer = answerElement.GetString();
                            string source = "unknown";

                            if (responseJson.RootElement.TryGetProperty("source", out var sourceElement))
                            {
                                source = sourceElement.GetString();
                            }

                            // Display answer with formatting
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\n[Response Time: {responseTime}ms] [Source: {source}]");
                            Console.WriteLine("-----------------------------------------------------");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine(answer);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("-----------------------------------------------------");
                            Console.ResetColor();

                            // Option to see full response details
                            Console.Write("\nShow full response details? (y/n): ");
                            if (Console.ReadLine().ToLower() == "y")
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine(JsonSerializeWithIndentation(responseJson));
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Could not find 'answer' in the response");
                            Console.WriteLine(responseBody);
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error: {response.StatusCode}");
                        Console.WriteLine(await response.Content.ReadAsStringAsync());
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Exception: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        private static async Task CompareResponses()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nSide-by-Side Comparison");
            Console.WriteLine("=======================");
            Console.ResetColor();

            // Role selection
            Console.WriteLine("\nSelect user role:");
            Console.WriteLine("1. Customer");
            Console.WriteLine("2. Mechanic");
            Console.WriteLine("3. Admin");

            Console.Write("Enter role (1-3): ");
            string roleOption = Console.ReadLine();

            string role = roleOption switch
            {
                "1" => "Customer",
                "2" => "Mechanic",
                "3" => "Admin",
                _ => "Customer"
            };

            // User ID input
            Console.Write("Enter User ID (default: 1): ");
            string userIdInput = Console.ReadLine();
            int userId = 1;

            if (!string.IsNullOrWhiteSpace(userIdInput) && int.TryParse(userIdInput, out int parsedId))
            {
                userId = parsedId;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nComparing responses as {role} (User ID: {userId})");
            Console.WriteLine("Type 'exit' to return to main menu");
            Console.ResetColor();

            // Start comparison loop
            while (true)
            {
                Console.Write("\nYour question: ");
                string question = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(question))
                    continue;

                if (question.ToLower() == "exit")
                    break;

                try
                {
                    Console.WriteLine("Sending requests to both chatbots...");

                    // Create request object
                    var requestObject = new
                    {
                        Question = question,
                        Role = role,
                        UserId = userId,
                        Model = "llama-3.3-70b-versatile", // Add this line
                        MaxTokens = 150,                    // Add this line if needed
                        SkipApiCall = false
                    };

                    var jsonRequest = JsonConvert.SerializeObject(requestObject);

                    // Send requests to both endpoints
                    var originalTask = SendRequest("test-chatbot", jsonRequest);
                    var enhancedTask = SendRequest("enhanced-chatbot", jsonRequest);

                    // Wait for both responses
                    await Task.WhenAll(originalTask, enhancedTask);

                    var originalResult = originalTask.Result;
                    var enhancedResult = enhancedTask.Result;

                    // Display comparison
                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n======== SIDE-BY-SIDE COMPARISON ========");
                    Console.WriteLine($"Question: {question}");
                    Console.WriteLine($"Role: {role}, User ID: {userId}");
                    Console.WriteLine("==========================================");
                    Console.ResetColor();

                    // Display original response
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nORIGINAL CHATBOT");
                    Console.WriteLine($"Response Time: {originalResult.responseTime}ms");
                    Console.WriteLine($"Source: {originalResult.source}");
                    Console.WriteLine("------------------------------------------");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(originalResult.answer);

                    // Display enhanced response
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nENHANCED CHATBOT");
                    Console.WriteLine($"Response Time: {enhancedResult.responseTime}ms");
                    Console.WriteLine($"Source: {enhancedResult.source}");
                    if (!string.IsNullOrEmpty(enhancedResult.relevantContextCount))
                    {
                        Console.WriteLine($"Relevant Context Count: {enhancedResult.relevantContextCount}");
                    }
                    if (!string.IsNullOrEmpty(enhancedResult.topRelevanceScore))
                    {
                        Console.WriteLine($"Top Relevance Score: {enhancedResult.topRelevanceScore}");
                    }
                    Console.WriteLine("------------------------------------------");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(enhancedResult.answer);

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n==========================================");
                    Console.ResetColor();

                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Exception: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        private static async Task<(string answer, string source, long responseTime, string relevantContextCount, string topRelevanceScore)> SendRequest(string endpoint, string jsonRequest)
        {
            try
            {
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var response = await client.PostAsync($"{baseUrl}/api/{endpoint}/ask", content);

                stopwatch.Stop();
                long responseTime = stopwatch.ElapsedMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var responseJson = JsonDocument.Parse(responseBody);

                    string answer = "Error extracting answer";
                    string source = "unknown";
                    string relevantContextCount = "";
                    string topRelevanceScore = "";

                    if (responseJson.RootElement.TryGetProperty("answer", out var answerElement))
                    {
                        answer = answerElement.GetString();
                    }

                    if (responseJson.RootElement.TryGetProperty("source", out var sourceElement))
                    {
                        source = sourceElement.GetString();
                    }

                    // Extract enhanced-specific information if available
                    if (responseJson.RootElement.TryGetProperty("relevantContextCount", out var contextCountElement))
                    {
                        relevantContextCount = contextCountElement.GetInt32().ToString();
                    }

                    if (responseJson.RootElement.TryGetProperty("topRelevanceScore", out var scoreElement))
                    {
                        topRelevanceScore = scoreElement.GetSingle().ToString("F2");
                    }

                    return (answer, source, responseTime, relevantContextCount, topRelevanceScore);
                }
                else
                {
                    return ($"Error: {response.StatusCode}", "error", responseTime, "", "");
                }
            }
            catch (Exception ex)
            {
                return ($"Exception: {ex.Message}", "error", 0, "", "");
            }
        }

        private static string JsonSerializeWithIndentation(JsonDocument doc)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, options);
        }

        private static async Task TestEchoEndpoint()
        {
            Console.WriteLine("Testing basic connectivity with echo endpoint...");

            try
            {
                // Simple GET request first
                var getResponse = await client.GetAsync($"{baseUrl}/api/echo");
                Console.WriteLine($"GET response: {getResponse.StatusCode}");
                if (getResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine(await getResponse.Content.ReadAsStringAsync());
                }

                // Now test POST
                var simpleData = new { test = "Hello", number = 123 };
                var content = new StringContent(
                    JsonConvert.SerializeObject(simpleData),
                    Encoding.UTF8,
                    "application/json");

                var postResponse = await client.PostAsync($"{baseUrl}/api/echo", content);
                Console.WriteLine($"POST response: {postResponse.StatusCode}");
                if (postResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine(await postResponse.Content.ReadAsStringAsync());
                }
                else
                {
                    Console.WriteLine(await postResponse.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

}