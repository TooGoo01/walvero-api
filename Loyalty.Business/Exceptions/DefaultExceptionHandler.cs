using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loyalty.Business.Exceptions
{
    public class DefaultExceptionHandler
    {
        public static async Task HandleExceptionAsync(HttpContext context)
        {
            var telemetryClient = context.RequestServices.GetRequiredService<TelemetryClient>();
            var exception = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;

            if (exception == null)
            {
                return;
            }

            telemetryClient.TrackException(exception);

            var exceptionMatchers = context.RequestServices.GetServices<Func<Exception, ProblemDetails?>>();
            var problem = exceptionMatchers
                .Select(x => x(exception))
                .Where(x => x is not null)
                .FirstOrDefault(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Type = "Internal server error has occurred"
                });

            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();

            context.Response.ContentType = MediaTypeNames.Application.Json;
            context.Response.StatusCode = problem!.Status.GetValueOrDefault((int)HttpStatusCode.InternalServerError);

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }

    }
}
