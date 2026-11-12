using ACA.Application.Interfaces;
using ACA.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ACA.Application;

public static class DependencyInjection
{
    /// <summary>Registers all application services (scoped, EF Core per request).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IMemberSearchService, MemberSearchService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IReactionService, ReactionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
