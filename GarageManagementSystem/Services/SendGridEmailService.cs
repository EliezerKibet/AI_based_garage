using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using GarageManagementSystem.Models;

namespace GarageManagementSystem.Services
{
    public class SendGridEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SendGridEmailService> _logger;

        public SendGridEmailService(IConfiguration configuration, ILogger<SendGridEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPasswordResetLinkAsync(string email, string callbackUrl)
        {
            var subject = "Reset Your Password";
            var plainTextContent = $"Please reset your password by clicking this link: {callbackUrl}";
            var htmlContent = $@"
                <h1>Reset Your Password</h1>
                <p>Please reset your password by clicking the link below:</p>
                <p><a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Reset Password</a></p>
                <p>If you did not request a password reset, please ignore this email.</p>";

            await SendEmailAsync(email, subject, plainTextContent, htmlContent);
        }

        public async Task SendEmailConfirmationLinkAsync(string email, string callbackUrl)
        {
            var subject = "Confirm Your Email";
            var plainTextContent = $"Please confirm your email by clicking this link: {callbackUrl}";
            var htmlContent = $@"
                <h1>Confirm Your Email</h1>
                <p>Please confirm your email by clicking the link below:</p>
                <p><a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Confirm Email</a></p>";

            await SendEmailAsync(email, subject, plainTextContent, htmlContent);
        }

        public async Task SendAppointmentApprovalEmailAsync(Users customer, Appointment appointment)
        {
            _logger.LogInformation("SendAppointmentApprovalEmailAsync called for customer: {CustomerEmail}", customer.Email);

            var subject = "Appointment Approved - Garage Management System";
            var appointmentTimeFormatted = appointment.AppointmentTime.ToString(@"hh\:mm");

            var plainTextContent = $@"
Dear {customer.FullName},

Great news! Your appointment has been APPROVED and confirmed.

Appointment Details:
- Date: {appointment.AppointmentDate:dddd, MMMM dd, yyyy}
- Time: {appointmentTimeFormatted}
- Vehicle: {appointment.Car?.Make} {appointment.Car?.Model} ({appointment.Car?.LicenseNumber})
- Mechanic: {appointment.MechanicName ?? "Will be assigned soon"}
- Status: {appointment.Status}
{(string.IsNullOrEmpty(appointment.Notes) ? "" : $"- Service Notes: {appointment.Notes}")}

We look forward to serving you!

Best regards,
The Garage Management Team";

            var htmlContent = GetAppointmentEmailHtml(
                customer.FullName,
                "🎉 Appointment Approved!",
                "#28a745",
                $"Great news! Your appointment has been <span class='status-approved'>APPROVED</span> and confirmed by our team.",
                appointment,
                appointmentTimeFormatted,
                GetApprovalInstructions()
            );

            await SendEmailAsync(customer.Email, subject, plainTextContent, htmlContent);
        }

        public async Task SendAppointmentCancellationEmailAsync(Users customer, Appointment appointment, string cancellationReason)
        {
            _logger.LogInformation("SendAppointmentCancellationEmailAsync called for customer: {CustomerEmail}", customer.Email);

            var subject = "Appointment Cancelled - Garage Management System";
            var appointmentTimeFormatted = appointment.AppointmentTime.ToString(@"hh\:mm");

            var plainTextContent = $@"
Dear {customer.FullName},

We regret to inform you that your appointment has been CANCELLED.

Original Appointment Details:
- Date: {appointment.AppointmentDate:dddd, MMMM dd, yyyy}
- Time: {appointmentTimeFormatted}
- Vehicle: {appointment.Car?.Make} {appointment.Car?.Model} ({appointment.Car?.LicenseNumber})
- Mechanic: {appointment.MechanicName ?? "Assigned mechanic"}

Reason for Cancellation: {cancellationReason ?? "Not specified"}

We sincerely apologize for any inconvenience this may cause. Please contact us to reschedule your appointment.

Contact Information:
- Email: elieserkibet@gmail.com
- Phone: +1 (502) 549-1179
- Office Hours: Monday - Friday, 8:00 AM - 6:00 PM

Best regards,
The Garage Management Team";

            var htmlContent = GetAppointmentEmailHtml(
                customer.FullName,
                "❌ Appointment Cancelled",
                "#dc3545",
                $"We regret to inform you that your appointment has been <span class='status-cancelled'>CANCELLED</span>.",
                appointment,
                appointmentTimeFormatted,
                GetCancellationInstructions(cancellationReason)
            );

            await SendEmailAsync(customer.Email, subject, plainTextContent, htmlContent);
        }

        public async Task SendAppointmentRescheduleEmailAsync(Users customer, Appointment appointment, DateTime oldDate, TimeSpan oldTime)
        {
            _logger.LogInformation("SendAppointmentRescheduleEmailAsync called for customer: {CustomerEmail}", customer.Email);

            var subject = "Appointment Rescheduled - Garage Management System";
            var appointmentTimeFormatted = appointment.AppointmentTime.ToString(@"hh\:mm");
            var oldTimeFormatted = oldTime.ToString(@"hh\:mm");

            var plainTextContent = $@"
            Dear {customer.FullName},

            Your appointment has been RESCHEDULED to a new date and time.

            Previous Appointment:
            - Date: {oldDate:dddd, MMMM dd, yyyy}
            - Time: {oldTimeFormatted}

            NEW Appointment Details:
            - Date: {appointment.AppointmentDate:dddd, MMMM dd, yyyy}
            - Time: {appointmentTimeFormatted}
            - Vehicle: {appointment.Car?.Make} {appointment.Car?.Model} ({appointment.Car?.LicenseNumber})
            - Mechanic: {appointment.MechanicName ?? "Will be assigned soon"}
            - Status: {appointment.Status}
            {(string.IsNullOrEmpty(appointment.Notes) ? "" : $"- Service Notes: {appointment.Notes}")}

            We apologize for any inconvenience this change may cause.

            Contact Information:
            - Email: elieserkibet@gmail.com
            - Phone: +1 (502) 549-1179
            - Office Hours: Monday - Friday, 8:00 AM - 6:00 PM

            Best regards,
            The Garage Management Team";

            var htmlContent = GetRescheduleEmailHtml(
                customer.FullName,
                appointment,
                appointmentTimeFormatted,
                oldDate,
                oldTimeFormatted
            );

            await SendEmailAsync(customer.Email, subject, plainTextContent, htmlContent);
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogInformation("SendEmailAsync (3 params) called for: {Email}", email);
            var plainTextContent = System.Text.RegularExpressions.Regex.Replace(htmlMessage, "<.*?>", string.Empty);
            await SendEmailAsync(email, subject, plainTextContent, htmlMessage);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string plainTextContent, string htmlContent)
        {
            _logger.LogInformation("SendEmailAsync (4 params) called - To: {Email}, Subject: {Subject}", toEmail, subject);

            try
            {
                var apiKey = _configuration["SendGrid:ApiKey"];
                var fromEmail = _configuration["SendGrid:FromEmail"];
                var fromName = _configuration["SendGrid:FromName"];

                _logger.LogInformation("SendGrid Config - ApiKey exists: {HasApiKey}, FromEmail: {FromEmail}, FromName: {FromName}",
                    !string.IsNullOrEmpty(apiKey), fromEmail, fromName);

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new System.Exception("SendGrid API key is not configured");
                }

                if (string.IsNullOrEmpty(fromEmail))
                {
                    throw new System.Exception("SendGrid FromEmail is not configured");
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(fromEmail, fromName);
                var to = new EmailAddress(toEmail);
                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

                _logger.LogInformation("Sending email via SendGrid...");
                var response = await client.SendEmailAsync(msg);

                _logger.LogInformation("SendGrid response - StatusCode: {StatusCode}", response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Accepted ||
                    response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.LogInformation("✅ Email sent successfully to {Email}", toEmail);
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    _logger.LogError("❌ SendGrid error - StatusCode: {StatusCode}, Response: {Response}",
                        response.StatusCode, responseBody);
                    throw new System.Exception($"SendGrid error: {response.StatusCode}, {responseBody}");
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "❌ Exception in SendEmailAsync: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        private string GetAppointmentEmailHtml(string customerName, string headerTitle, string headerColor,
            string mainMessage, Appointment appointment, string appointmentTimeFormatted, string instructions)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; margin: 0; padding: 0; }}
                        .container {{ max-width: 600px; margin: 0 auto; background-color: white; }}
                        .header {{ background-color: {headerColor}; color: white; padding: 30px 20px; text-align: center; }}
                        .header h1 {{ margin: 0; font-size: 28px; }}
                        .content {{ padding: 30px 20px; }}
                        .appointment-details {{ 
                            background-color: #f8f9fa; 
                            padding: 20px; 
                            border-left: 5px solid {headerColor}; 
                            margin: 25px 0; 
                            border-radius: 5px;
                        }}
                        .appointment-details h3 {{ color: #1b6ec2; margin-top: 0; }}
                        .status-approved {{ color: #28a745; font-weight: bold; font-size: 18px; }}
                        .status-cancelled {{ color: #dc3545; font-weight: bold; font-size: 18px; }}
                        .status-rescheduled {{ color: #ffc107; font-weight: bold; font-size: 18px; }}
                        .footer {{ 
                            background-color: #f8f9fa; 
                            text-align: center; 
                            padding: 20px; 
                            font-size: 14px; 
                            color: #666; 
                            border-top: 1px solid #e9ecef;
                        }}
                        .contact-info {{ background-color: #e3f2fd; padding: 15px; border-radius: 5px; margin: 20px 0; }}
                        .instructions {{ background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0; }}
                        ul {{ padding-left: 20px; }}
                        li {{ margin-bottom: 8px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>{headerTitle}</h1>
                        </div>
                        
                        <div class='content'>
                            <p><strong>Dear {customerName},</strong></p>
                            
                            <p>{mainMessage}</p>
                            
                            <div class='appointment-details'>
                                <h3>📅 Appointment Details</h3>
                                <p><strong>📆 Date:</strong> {appointment.AppointmentDate:dddd, MMMM dd, yyyy}</p>
                                <p><strong>🕐 Time:</strong> {appointmentTimeFormatted}</p>
                                <p><strong>🚗 Vehicle:</strong> {appointment.Car?.Make} {appointment.Car?.Model} ({appointment.Car?.LicenseNumber})</p>
                                <p><strong>🔧 Mechanic:</strong> {appointment.MechanicName ?? "Will be assigned soon"}</p>
                                {(string.IsNullOrEmpty(appointment.Notes) ? "" : $"<p><strong>📝 Service Notes:</strong> {appointment.Notes}</p>")}
                                <p><strong>📊 Status:</strong> {appointment.Status}</p>
                            </div>
                            
                            {instructions}
                            
                            <div class='contact-info'>
                                <h3>📞 Contact Information</h3>
                                <p>If you have any questions or need assistance, please contact us:</p>
                                <ul>
                                    <li><strong>📧 Email:</strong> elieserkibet@gmail.com</li>
                                    <li><strong>📱 Phone:</strong> +1 (502) 549-1179</li>
                                    <li><strong>🕒 Office Hours:</strong> Monday - Friday, 8:00 AM - 6:00 PM</li>
                                </ul>
                            </div>
                            
                            <p>Thank you for choosing our garage services!</p>
                            
                            <p>Best regards,<br>
                            <strong>The Garage Management Team</strong></p>
                        </div>
                        
                        <div class='footer'>
                            <p>This is an automated notification. Please do not reply to this email.</p>
                            <p>© 2024 Garage Management System. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GetRescheduleEmailHtml(string customerName, Appointment appointment,
            string newTimeFormatted, DateTime oldDate, string oldTimeFormatted)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; margin: 0; padding: 0; }}
                        .container {{ max-width: 600px; margin: 0 auto; background-color: white; }}
                        .header {{ background-color: #ffc107; color: white; padding: 30px 20px; text-align: center; }}
                        .header h1 {{ margin: 0; font-size: 28px; }}
                        .content {{ padding: 30px 20px; }}
                        .appointment-details {{ 
                            background-color: #f8f9fa; 
                            padding: 20px; 
                            border-left: 5px solid #ffc107; 
                            margin: 25px 0; 
                            border-radius: 5px;
                        }}
                        .old-appointment {{ 
                            background-color: #f8d7da; 
                            padding: 15px; 
                            border-left: 5px solid #dc3545; 
                            margin: 15px 0; 
                            border-radius: 5px;
                        }}
                        .new-appointment {{ 
                            background-color: #d1edff; 
                            padding: 15px; 
                            border-left: 5px solid #0066cc; 
                            margin: 15px 0; 
                            border-radius: 5px;
                        }}
                        .appointment-details h3 {{ color: #1b6ec2; margin-top: 0; }}
                        .status-rescheduled {{ color: #ffc107; font-weight: bold; font-size: 18px; }}
                        .footer {{ 
                            background-color: #f8f9fa; 
                            text-align: center; 
                            padding: 20px; 
                            font-size: 14px; 
                            color: #666; 
                            border-top: 1px solid #e9ecef;
                        }}
                        .contact-info {{ background-color: #e3f2fd; padding: 15px; border-radius: 5px; margin: 20px 0; }}
                        ul {{ padding-left: 20px; }}
                        li {{ margin-bottom: 8px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>📅 Appointment Rescheduled</h1>
                        </div>
                        
                        <div class='content'>
                            <p><strong>Dear {customerName},</strong></p>
                            
                            <p>Your appointment has been <span class='status-rescheduled'>RESCHEDULED</span> to a new date and time.</p>
                            
                            <div class='old-appointment'>
                                <h4>❌ Previous Appointment</h4>
                                <p><strong>Date:</strong> {oldDate:dddd, MMMM dd, yyyy}</p>
                                <p><strong>Time:</strong> {oldTimeFormatted}</p>
                            </div>
                            
                            <div class='new-appointment'>
                                <h4>✅ NEW Appointment</h4>
                                <p><strong>Date:</strong> {appointment.AppointmentDate:dddd, MMMM dd, yyyy}</p>
                                <p><strong>Time:</strong> {newTimeFormatted}</p>
                                <p><strong>Vehicle:</strong> {appointment.Car?.Make} {appointment.Car?.Model} ({appointment.Car?.LicenseNumber})</p>
                                <p><strong>Mechanic:</strong> {appointment.MechanicName ?? "Will be assigned soon"}</p>
                                <p><strong>Status:</strong> {appointment.Status}</p>
                                {(string.IsNullOrEmpty(appointment.Notes) ? "" : $"<p><strong>Service Notes:</strong> {appointment.Notes}</p>")}
                            </div>
                            
                            
                            <div class='contact-info'>
                                <h3>📞 Questions About the Change?</h3>
                                <p>If you need to discuss this reschedule or make further changes:</p>
                                <ul>
                                    <li><strong>📧 Email:</strong> elieserkibet@gmail.com</li>
                                    <li><strong>📱 Phone:</strong> +1 (502) 549-1179</li>
                                    <li><strong>🕒 Office Hours:</strong> Monday - Friday, 8:00 AM - 6:00 PM</li>
                                </ul>
                            </div>
                            
                            <p>We apologize for any inconvenience this change may cause and appreciate your understanding.</p>
                            
                            <p>Best regards,<br>
                            <strong>The Garage Management Team</strong></p>
                        </div>
                        
                        <div class='footer'>
                            <p>This is an automated notification. Please do not reply to this email.</p>
                            <p>© 2024 Garage Management System. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GetApprovalInstructions()
        {
            return @"
                <div class='instructions'>
                    <h3>📋 Before Your Appointment</h3>
                    <ul>
                        <li><strong>Arrive 10 minutes early</strong> for check-in</li>
                        <li>Bring your <strong>vehicle registration</strong> and <strong>driver's license</strong></li>
                        <li>Remove all <strong>personal items</strong> from your vehicle</li>
                        <li>Prepare a list of any <strong>specific concerns</strong> about your vehicle</li>
                        <li>Have your <strong>service history</strong> available if relevant</li>
                    </ul>
                </div>
                
                <h3>🔧 What to Expect</h3>
                <ul>
                    <li>Our certified mechanic will perform a thorough inspection</li>
                    <li>A detailed service report will be provided upon completion</li>
                </ul>";
        }

        private string GetCancellationInstructions(string cancellationReason)
        {
            return $@"
                <div class='instructions'>
                    <h3>❗ Cancellation Details</h3>
                    <p><strong>Reason:</strong> {cancellationReason ?? "Not specified"}</p>
                    <p>We sincerely apologize for any inconvenience this cancellation may cause.</p>
                </div>
                
                <h3>📞 Next Steps</h3>
                <ul>
                    <li><strong>Reschedule:</strong> Contact us to book a new appointment</li>
                    <li><strong>Questions:</strong> Call or email us for clarification</li>
                    <li><strong>Emergency:</strong> If urgent, mention this when contacting us</li>
                    <li><strong>Refund:</strong> Any prepayments will be processed within 3-5 business days</li>
                </ul>";
        }
    }
}