using Sawa3ed.Api.Observability;
using Sawa3ed.Api.Security;

namespace Sawa3ed.Api.Configuration;

internal static class ApiPipeline
{
    public static WebApplication UseApi(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context =>
            await Results.Problem(statusCode: context.HttpContext.Response.StatusCode).ExecuteAsync(context.HttpContext));
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseRouting();
        app.UseCors();
        app.UseMiddleware<AdmissionMiddleware>();
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(o => { o.SwaggerEndpoint("/swagger/v1/swagger.json", "Sawa3ed v1"); o.EnablePersistAuthorization(); });
        }
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapHealthEndpoints();
        app.MapControllers();
        return app;
    }
}
