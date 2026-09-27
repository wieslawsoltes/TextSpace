using Uno.UI.Hosting;
namespace TextSpace.App;
internal static class Program
{
    public static async Task Main(string[] args) => await UnoPlatformHostBuilder.Create().App(() => new App()).UseWebAssembly().Build().RunAsync();
}
