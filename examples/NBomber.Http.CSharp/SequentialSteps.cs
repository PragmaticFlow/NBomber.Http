using NBomber.CSharp;
using NBomber.Http;
using NBomber.Plugins.Http.CSharp;

namespace Example
{
    class SequentialSteps
    {
        public static void Run()
        {
            var step1 = HttpStep.Create("step 1", (context) =>
                Http.CreateRequest("GET", "https://gitter.im"));

            var step2 = HttpStep.Create("step 2", async (context) =>
            {
                var step1Response = context.GetPreviousHttpResponse();
                var headers = step1Response.Headers;
                var body = await step1Response.Content.ReadAsStringAsync();

                return Http.CreateRequest("GET", "https://gitter.im");
            });

            var scenario = ScenarioBuilder.CreateScenario("test_gitter", step1, step2);

            NBomberRunner
                .RegisterScenarios(scenario)
                .LoadConfig("test_config.json")
                .Run();
        }
    }
}
