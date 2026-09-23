using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sawa3ed.Application.Auth;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Tests;

public sealed class AuthTests
{
    [Fact]
    public async Task Registration_requires_confirmation_and_never_returns_an_otp_or_token()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        const string email = "new@example.test";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, ApiFactory.Password, "New Student"));
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        var payload = await register.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, ApiFactory.Password))).StatusCode);
        var code = await app.LatestCodeAsync(email, OtpPurpose.ConfirmEmail);
        Assert.DoesNotContain(code, payload);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new VerifyEmailRequest(email, code))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new VerifyEmailRequest(email, code))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, ApiFactory.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        ApiFactory.Authenticate(client, tokens);
        var me = await client.GetFromJsonAsync<UserResponse>("/api/v1/auth/me");
        Assert.Equal([Roles.Student], me!.Roles);
    }

    [Fact]
    public async Task Refresh_rotates_and_replay_revokes_the_session()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        var (_, original) = await app.AccountAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(original.RefreshToken))).StatusCode);
        ApiFactory.Authenticate(client, rotated);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(rotated.RefreshToken))).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.ToListAsync();
        Assert.All(stored, x => Assert.Equal(64, x.Hash.Length));
        Assert.DoesNotContain(stored, x => x.Hash == original.RefreshToken || x.Hash == rotated.RefreshToken);
    }

    [Fact]
    public async Task Anonymous_and_student_cannot_assign_roles_and_role_changes_revoke_access()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/roles")).StatusCode);
        var student = await app.AccountAsync(client);
        ApiFactory.Authenticate(client, student.Tokens);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/roles/users/{student.Id}", new RoleAssignmentRequest(Roles.Admin))).StatusCode);
        var admin = await app.AccountAsync(client, Roles.Admin);
        ApiFactory.Authenticate(client, admin.Tokens);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/roles/users/{student.Id}", new RoleAssignmentRequest(Roles.Teacher))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/v1/roles/users/{admin.Id}/Admin")).StatusCode);
        ApiFactory.Authenticate(client, student.Tokens);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Otp_attempts_are_bounded_and_purpose_is_enforced()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        const string email = "otp@example.test";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, ApiFactory.Password, "OTP User"));
        var code = await app.LatestCodeAsync(email, OtpPurpose.ConfirmEmail);
        var wrong = code == "000000" ? "111111" : "000000";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(email, code, "Different-password-123!"))).StatusCode);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new VerifyEmailRequest(email, wrong))).StatusCode);
        await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(email, OtpPurpose.ConfirmEmail));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/email/confirm", new VerifyEmailRequest(email, code))).StatusCode);
    }

    [Fact]
    public async Task Password_reset_and_logout_immediately_revoke_tokens()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        var account = await app.AccountAsync(client);
        ApiFactory.Authenticate(client, account.Tokens);
        var me = (await client.GetFromJsonAsync<UserResponse>("/api/v1/auth/me"))!;
        await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { me.Email });
        var code = await app.LatestCodeAsync(me.Email, OtpPurpose.ResetPassword);
        var reset = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(me.Email, code, "New-password-for-test-123!"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(me.Email, "New-password-for-test-123!"));
        ApiFactory.Authenticate(client, (await login.Content.ReadFromJsonAsync<TokenResponse>())!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Auth_rate_limit_returns_problem_details_and_retry_after()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
        {
            last?.Dispose();
            last = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest("nobody@example.test", OtpPurpose.ConfirmEmail));
        }
        using (last)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
            Assert.NotNull(last.Headers.RetryAfter);
            Assert.Equal("application/problem+json", last.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Swagger_defines_http_bearer_and_contains_no_business_endpoints()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var scheme = json.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.All(json.RootElement.GetProperty("paths").EnumerateObject(), path =>
            Assert.Contains(new[] { "/api/v1/auth", "/api/v1/roles", "/api/v1/files", "/api/v1/chat", "/health/" }, prefix => path.Name.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
