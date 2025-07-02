using System;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Microsoft.Extensions.Configuration;

public class PhoneCallService
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _twilioPhoneNumber;

    public PhoneCallService(IConfiguration configuration)
    {
        _accountSid = configuration["Twilio:AccountSid"];
        _authToken = configuration["Twilio:AuthToken"];
        _twilioPhoneNumber = configuration["Twilio:PhoneNumber"];
    }

    public void MakeCall(string customerPhone, string message)
    {
        TwilioClient.Init(_accountSid, _authToken);

        var call = CallResource.Create(
            new PhoneNumber(customerPhone),  // Customer's phone number
            from: new PhoneNumber(_twilioPhoneNumber),  // Your Twilio number
            twiml: new Twiml($"<Response><Say>{message}</Say></Response>")
        );

        Console.WriteLine($"Call initiated: {call.Sid}");
    }

    public void SendSms(string customerPhone, string message)
    {
        TwilioClient.Init(_accountSid, _authToken);

        var sms = MessageResource.Create(
            body: message,
            from: new PhoneNumber(_twilioPhoneNumber),
            to: new PhoneNumber(customerPhone)
        );

        Console.WriteLine($"SMS sent: {sms.Sid}");
    }
}
