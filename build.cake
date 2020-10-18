var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var solution = File("./NBomber.Http.sln");
var project = File("./src/NBomber.Http/NBomber.Http.fsproj");
var version = XmlPeek(project, "//Version");

Task("Clean")
    .Does(() =>
{
    CleanDirectories("./src/**/obj");
    CleanDirectories("./src/**/bin");
    CleanDirectories("./examples/**/obj");
    CleanDirectories("./examples/**/bin");
    CleanDirectories("./tests/**/obj");
    CleanDirectories("./tests/**/bin");
    CleanDirectories("./examples/**/obj");
    CleanDirectories("./examples/**/bin");
    CleanDirectories("./artifacts/");
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreRestore(solution);
});

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
{
    Information("NBomber.Http Version: {0}", version);

    DotNetCoreBuild(solution, new DotNetCoreBuildSettings()
    {
        Configuration = configuration,
        ArgumentCustomization = args => args.Append("--no-restore"),
    });

    DotNetCoreBuild(project, new DotNetCoreBuildSettings()
    {
        Configuration = configuration,
        ArgumentCustomization = args => args.Append("--no-restore")
                                            .Append($"/property:Version={version}"),
    });
});

Task("Test")
    .Does(() =>
{
    var projects = GetFiles("./tests/**/*.fsproj");
    foreach(var project in projects)
    {
        Information("Testing project " + project);

        DotNetCoreTest(project.ToString(),
            new DotNetCoreTestSettings()
            {
                Configuration = configuration,
                NoBuild = true,
                ArgumentCustomization = args => args.Append("--no-restore"),
            });
    }
});

Task("Pack")
    .IsDependentOn("Build")
    .Does(() =>
{
    var settings = new DotNetCorePackSettings
    {
        OutputDirectory = "./artifacts/",
        NoBuild = true,
        IncludeSource = true,
        IncludeSymbols = true,
        Configuration = configuration,
        ArgumentCustomization = args => args.Append("-p:SymbolPackageFormat=snupkg")
    };

    DotNetCorePack(project, settings);
});

Task("Default")
    .IsDependentOn("Build");

RunTarget(target);
