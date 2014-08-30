using Microsoft.AspNet.Builder;
using Microsoft.Framework.DependencyInjection;

namespace VoteR
{
    public class Startup
    {

        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void Configure(IBuilder app)
        {
            app.UseServices(services =>
            {
                services
                    .AddSignalR()
                    .SetupOptions(options =>
                    {
                        options.Hubs.EnableDetailedErrors = true;
                    });
            });

            app.UseFileServer();

            app.UseSignalR<RawConnection>("/raw-connection");
            app.UseSignalR();
        }

    }
}
