using Microsoft.AspNetCore.Mvc;

namespace WebAppSimulator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingPongController : ControllerBase
{
    [HttpGet]
    public IActionResult GetMessage() => Ok("Get");

    [HttpPost]
    public IActionResult PostMessage() => Ok("Post");

    [HttpPut]
    public IActionResult PutMessage() => Ok("Put");

    [HttpPatch]
    public IActionResult PatchMessage() => Ok("Patch");

    [HttpDelete]
    public IActionResult DeleteMessage() => Ok("Delete");
}
