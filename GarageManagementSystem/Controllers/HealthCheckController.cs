using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace GarageManagementSystem.Controllers
{
    [Route("api/healthcheck")]
    [ApiController]
    [AllowAnonymous]
    public class HealthCheckController : ControllerBase
    {
        [HttpGet]
        public IActionResult Check()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                message = "API is running correctly"
            });
        }
    }
}