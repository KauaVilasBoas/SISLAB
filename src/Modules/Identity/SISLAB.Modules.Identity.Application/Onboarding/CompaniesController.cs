using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Onboarding;

/// <summary>
/// HTTP boundary for company self-service onboarding (card [E12] #75a). The controller only dispatches the
/// signup CQRS command through the SISLAB <see cref="IMediator"/> and maps the result to the uniform
/// <see cref="ApiResult"/> envelope; it never touches Lumen, repositories or the DbContext directly.
///
/// <para><b>Anonymous by design:</b> signup is the one write endpoint that cannot be permission-gated — the
/// caller has no account and no active company yet, so <c>[AllowAnonymous]</c> is correct here (and the
/// architecture rule that requires <c>[RequirePermission]</c> on writes explicitly exempts anonymous actions).
/// Because it runs before any session cookie exists, it is also exempt from CSRF at the Host, mirroring the
/// public Lumen auth endpoints.</para>
/// </summary>
[Route("api/companies")]
public sealed class CompaniesController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Registers a new laboratory: creates the company (tenant) and its initial coordinator, links the
    /// coordinator as the founding member and grants company-scoped access. Returns the new company and
    /// coordinator ids. Anonymous. A duplicate company name or coordinator e-mail returns 409.
    /// </summary>
    [HttpPost("signup", Name = "SignupCompany")]
    [ActionName("SignupCompany")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResult<SignupCompanyResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Signup([FromBody] SignupCompanyRequest body, CancellationToken ct)
    {
        SignupCompanyResult result = await _mediator.SendAsync(
            new SignupCompanyCommand(
                body.CompanyName,
                body.TaxId,
                body.CoordinatorEmail,
                body.CoordinatorUsername,
                body.CoordinatorPassword),
            ct);

        return Ok(new ApiResult<SignupCompanyResult>(true, "Company registered.", result));
    }
}

/// <summary>
/// Request body for company self-service signup. All fields are required except <see cref="TaxId"/>.
/// </summary>
/// <param name="CompanyName">Name of the laboratory/company to create.</param>
/// <param name="TaxId">Optional tax identifier.</param>
/// <param name="CoordinatorEmail">Coordinator e-mail (login).</param>
/// <param name="CoordinatorUsername">Coordinator display/user name.</param>
/// <param name="CoordinatorPassword">Coordinator password (must satisfy Lumen's password policy).</param>
public sealed record SignupCompanyRequest(
    string CompanyName,
    string? TaxId,
    string CoordinatorEmail,
    string CoordinatorUsername,
    string CoordinatorPassword);
