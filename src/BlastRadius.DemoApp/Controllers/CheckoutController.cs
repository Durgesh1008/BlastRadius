using BlastRadius.DemoApp.Models;
using BlastRadius.DemoApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlastRadius.DemoApp.Controllers;

[ApiController]
[Route("api/checkout")]
[Produces("application/json")]
public sealed class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;

    public CheckoutController(ICheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    /// <summary>
    /// Executes a simulated e-commerce checkout using the BlastRadius SDK to evaluate feature flag status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _checkoutService.ProcessCheckoutAsync(request, cancellationToken);
        return Ok(response);
    }
}
