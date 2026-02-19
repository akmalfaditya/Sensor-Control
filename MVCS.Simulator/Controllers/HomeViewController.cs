using Microsoft.AspNetCore.Mvc;

namespace MVCS.Simulator.Controllers;

[Route("")]
public class HomeViewController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
