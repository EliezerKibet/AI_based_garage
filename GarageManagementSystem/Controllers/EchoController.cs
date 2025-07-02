using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageManagementSystem.Controllers
{
    [Route("api/echo")]
    [ApiController]
    [AllowAnonymous]
    public class EchoController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { message = "Echo service is working" });
        }

        [HttpPost]
        public IActionResult Post([FromBody] object data)
        {
            return Ok(new
            {
                message = "Received your data",
                data = data
            });
        }
    }
}