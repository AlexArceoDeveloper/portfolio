using AiControlCenter.Models;
using AiControlCenter.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiControlCenter.Controllers;

public sealed class HomeController(IWorkflowPlanner planner) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new ControlCenterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(ControlCenterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.Plan = planner.Build(model.Input);
        return View(model);
    }

    [Route("Home/Error")]
    public IActionResult Error() => Problem("The workflow plan could not be created.");
}
