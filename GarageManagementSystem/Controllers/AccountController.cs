using Azure.Identity;
using GarageManagementSystem.Models;
using GarageManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using GarageManagementSystem.Controllers;
using Org.BouncyCastle.Bcpg;
using GarageManagementSystem.Services;
namespace GarageManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> signInManager;
        private readonly UserManager<Users> userManager;
        private readonly NotificationService _notificationService;
        private readonly IEmailService emailService;

        public AccountController(SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            NotificationService notificationService,
            IEmailService emailservice)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            _notificationService = notificationService;
            this.emailService = emailservice;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }

                var result = await signInManager.PasswordSignInAsync(
                    user.UserName,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false 
                );

                if (result.Succeeded)
                {
                    await userManager.ResetAccessFailedCountAsync(user);
                    var roles = await userManager.GetRolesAsync(user);
                    if (roles.Contains("Admin"))
                        return RedirectToAction("Dashboard", "Admin");
                    else if (roles.Contains("Mechanic"))
                        return RedirectToAction("Dashboard", "Mechanic");
                    else if (roles.Contains("Customer"))
                        return RedirectToAction("CustomerDashboard", "Customer");
                    return RedirectToAction("Index", "Home");
                }
                else if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Account locked due to multiple failed login attempts. Please try again later.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
            }
            return View(model);
        }




        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            var roles = new List<string>() { "Customer", "Mechanic" };
            ViewBag.Roles = new SelectList(roles);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // 🔹 Set UserName explicitly instead of using Email
                Users users = new Users
                {
                    FullName = model.Name,
                    Email = model.Email,
                    UserName = model.Name.Replace(" ", "").ToLower(), // Example: "John Doe" → "johndoe"
                    PhoneNumber = model.PhoneNumber,
                };
                var result = await userManager.CreateAsync(users, model.Password);
                if (result.Succeeded)
                {
                    // Add user to role
                    await userManager.AddToRoleAsync(users, model.Role);
                    // Send existing notifications
                    await _notificationService.SendAdminNotificationOnUserRegistration(users.Id);
                    await _notificationService.SendUserNotificationOnRegistration(users.Id);
                    // Generate email confirmation token
                    var token = await userManager.GenerateEmailConfirmationTokenAsync(users);
                    // Create confirmation link (avoid Url.Action to prevent double-encoding)
                    var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
                    var encodedToken = System.Net.WebUtility.UrlEncode(token);
                    var confirmationUrl = $"{baseUrl}/Account/ConfirmEmail?userId={users.Id}&token={encodedToken}";
                    // Send confirmation email
                    await emailService.SendEmailConfirmationLinkAsync(users.Email, confirmationUrl);
                    // Redirect to registration confirmation page
                    return RedirectToAction("RegisterConfirmation");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }

                    // IMPORTANT: Repopulate the roles dropdown before returning the view
                    var roles = new List<string>() { "Customer", "Mechanic" };
                    ViewBag.Roles = new SelectList(roles);

                    return View(model);
                }
            }

            // IMPORTANT: Repopulate the roles dropdown when ModelState is invalid
            var rolesList = new List<string>() { "Customer", "Mechanic" };
            ViewBag.Roles = new SelectList(rolesList);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Error", "Home", new { message = "Invalid email confirmation link" });
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Error", "Home", new { message = "User not found" });
            }

            var result = await userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                return View("ConfirmEmailSuccess");
            }

            return RedirectToAction("Error", "Home", new { message = "Email confirmation failed" });
        }

        // GET: /Account/RegisterConfirmation
        [HttpGet]
        public IActionResult RegisterConfirmation()
        {
            return View();
        }

        // GET: /Account/ResendEmailConfirmation
        [HttpGet]
        public IActionResult ResendEmailConfirmation()
        {
            return View();
        }

        // POST: /Account/ResendEmailConfirmation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailConfirmation(ResendEmailConfirmationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user doesn't exist
                return RedirectToAction("RegisterConfirmation");
            }

            if (await userManager.IsEmailConfirmedAsync(user))
            {
                // User's email is already confirmed
                return RedirectToAction("Login");
            }

            // Generate email confirmation token
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

            // Create confirmation link
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var encodedToken = System.Net.WebUtility.UrlEncode(token);
            var confirmationUrl = $"{baseUrl}/Account/ConfirmEmail?userId={user.Id}&token={encodedToken}";

            // Send confirmation email
            await emailService.SendEmailConfirmationLinkAsync(user.Email, confirmationUrl);

            return RedirectToAction("RegisterConfirmation");
        }

        public async Task<IActionResult> EditUser(int id)
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }


        [HttpPost]
        [ValidateAntiForgeryToken] // Protects against CSRF attacks
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }


        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        public async Task SendPasswordResetLinkAsync(string email, string callbackUrl)
        {
            var subject = "Reset Your Password";
            var htmlContent = $@"
        <h1>Reset Your Password</h1>
        <p>Please reset your password by clicking the link below:</p>
        <p><a href='{callbackUrl}'>Reset Password</a></p>
        <p>If you did not request a password reset, please ignore this email.</p>
        <hr>
        <p>Debug - Raw URL: {callbackUrl}</p>
    ";

            // Rest of your email sending code...
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await userManager.IsEmailConfirmedAsync(user)))
            {
                return RedirectToAction("ForgotPasswordConfirmation");
            }

            // Generate the token
            var token = await userManager.GeneratePasswordResetTokenAsync(user);

            // IMPORTANT: Don't use Url.Action here as it's causing double-encoding
            // Instead, build the URL manually:
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var encodedToken = System.Net.WebUtility.UrlEncode(token);
            var resetUrl = $"{baseUrl}/Account/ResetPassword?userId={user.Id}&token={encodedToken}";

            // Send email with the callback url
            await emailService.SendPasswordResetLinkAsync(model.Email, resetUrl);

            return RedirectToAction("ForgotPasswordConfirmation");
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Error", "Home", new { message = "Invalid password reset link" });
            }

            // Don't URL decode the token again - ASP.NET Core already decoded it when binding to the action parameter

            var model = new ResetPasswordViewModel
            {
                UserId = userId,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        [Route("/Account/debug-token")] // Note the leading slash to make it an absolute path
        public async Task<IActionResult> DebugToken(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Content("User not found with email: " + email);
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = System.Net.WebUtility.UrlEncode(token);

            var debugInfo = new
            {
                UserId = user.Id,
                Email = user.Email,
                EmailConfirmed = await userManager.IsEmailConfirmedAsync(user),
                RawToken = token,
                TokenLength = token.Length,
                EncodedToken = encodedToken,
                EncodedTokenLength = encodedToken.Length,
                ResetUrl = Url.Action(
                    action: "ResetPassword",
                    controller: "Account",
                    values: new { userId = user.Id, token = encodedToken },
                    protocol: Request.Scheme)
            };

            return Json(debugInfo);
        }

        // GET: /Account/ResetPasswordConfirmation
        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [Route("/email-test")]  // Absolute path at the root
        public async Task<IActionResult> EmailTest()
        {
            try
            {
                string testEmail = "elieserkibet@gmail.com";
                string callbackUrl = "https://localhost:7064/Account/ResetPassword?userId=test-user-id&token=test-token";
                await emailService.SendPasswordResetLinkAsync(testEmail, callbackUrl);
                return Content("Email sent successfully! Please check your inbox.");
            }
            catch (Exception ex)
            {
                return Content($"Error sending email: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
        }
    }
}
