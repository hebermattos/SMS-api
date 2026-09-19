using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Sms.Api.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace Sms.Infrastructure.Tests;

public sealed class SwaggerConfigurationTests
{
    [Fact]
    public void SwaggerDocumentation_RegistersApiAndJwtBearerSecurity()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerDocumentation();
        using var app = builder.Build();

        app.UseSwaggerDocumentation();
        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.Equal("SMS API", document.Info.Title);
        Assert.Equal("v1", document.Info.Version);
        Assert.True(document.Components.SecuritySchemes.TryGetValue("Bearer", out var securityScheme));
        Assert.NotNull(securityScheme);
        Assert.Equal(SecuritySchemeType.Http, securityScheme.Type);
        Assert.Equal("bearer", securityScheme.Scheme);
        Assert.Single(document.SecurityRequirements);
    }
}
