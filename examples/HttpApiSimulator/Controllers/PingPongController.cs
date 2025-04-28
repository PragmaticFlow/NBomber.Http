using Microsoft.AspNetCore.Mvc;

namespace WebAppSimulator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingPongController : ControllerBase
{
    [HttpGet]
    public IActionResult GetMessage()
    {
        return Ok("Hello");
    }
}
