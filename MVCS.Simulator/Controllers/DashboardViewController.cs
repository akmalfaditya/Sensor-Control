using Microsoft.AspNetCore.Mvc;

namespace MVCS.Simulator.Controllers;

[Route("Dashboard")]
public class DashboardViewController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
