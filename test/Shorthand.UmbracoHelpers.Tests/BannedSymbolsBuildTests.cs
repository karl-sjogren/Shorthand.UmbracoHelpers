using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace Shorthand.UmbracoHelpers.Tests;

public class BannedSymbolsBuildTests {
#if UMBRACO_17
    private const string _umbracoMajor = "17";
#elif UMBRACO_18
    private const string _umbracoMajor = "18";
#else
    private const string _umbracoMajor = "19";
#endif

    [Fact]
    public async Task ImportedTargetsEnforceBansUnlessDisabledAsync() {
        // Arrange
        var project = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "BuildFixtures", "Direct", "Direct.csproj"));

        // Act
        var enabled = await BuildAsync(project, disabled: false);
        var disabled = await BuildAsync(project, disabled: true);
        var reenabled = await BuildAsync(project, disabled: false);

        // Assert
        AssertBans(enabled);
        disabled.ExitCode.ShouldBe(0, disabled.Output);
        disabled.Output.ShouldNotContain("RS0030");
        AssertBans(reenabled);
    }

    [Fact]
    public async Task PackageContainsTransitiveBuildAssetsAsync() {
        // Arrange
        var project = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
            "Shorthand.UmbracoHelpers", "Shorthand.UmbracoHelpers.csproj"));
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"ShorthandUmbracoHelpersPackTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try {
            // Act
            var packageVersion = $"{_umbracoMajor}.0.0-buildtest.{Guid.NewGuid():N}";
            var result = await RunDotnetAsync(
                "pack", project, "--configuration", "Debug", "--no-restore", "--nologo", "-v:q",
                $"-p:UmbracoMajorVersion={_umbracoMajor}", $"-p:PackageVersion={packageVersion}", "-o", outputDirectory);

            // Assert
            result.ExitCode.ShouldBe(0, result.Output);

            using var stream = File.OpenRead(Path.Combine(outputDirectory, $"Shorthand.UmbracoHelpers.{packageVersion}.nupkg"));
            using var archive = new ZipArchive(stream);
            archive.GetEntry("buildTransitive/Shorthand.UmbracoHelpers.targets").ShouldNotBeNull();
            archive.GetEntry("buildTransitive/BannedSymbols.Shorthand.UmbracoHelpers.txt").ShouldNotBeNull();
            var nuspec = archive.GetEntry("Shorthand.UmbracoHelpers.nuspec").ShouldNotBeNull();
            await using var nuspecStream = await nuspec.OpenAsync(TestCancellationToken);
            var document = await XDocument.LoadAsync(nuspecStream, LoadOptions.None, TestCancellationToken);
            XNamespace ns = document.Root!.Name.Namespace;
            var metadata = document.Root.Element(ns + "metadata").ShouldNotBeNull();
            metadata.Element(ns + "id")!.Value.ShouldBe("Shorthand.UmbracoHelpers");
            metadata.Element(ns + "version")!.Value.ShouldBe(packageVersion);
            var targetFramework = _umbracoMajor == "19" ? "net11.0" : "net10.0";
            var group = metadata.Element(ns + "dependencies")!.Elements(ns + "group")
                .Single(item => (string?)item.Attribute("targetFramework") == targetFramework);
            var dependency = group.Elements(ns + "dependency")
                .Single(item => (string?)item.Attribute("id") == "Umbraco.Cms.Web.Common");
            var expectedRange = _umbracoMajor switch {
                "17" => "[17.0.0, 18.0.0)",
                "18" => "[18.0.0, 19.0.0)",
                _ => "[19.0.0-rc1, 19.0.0)",
            };
            ((string?)dependency.Attribute("version")).ShouldBe(expectedRange);

            var matchingVersion = _umbracoMajor == "19" ? "19.0.0-rc1" : $"{_umbracoMajor}.0.0";
            var matching = await RestoreConsumerAsync(outputDirectory, packageVersion, matchingVersion, targetFramework);
            matching.ExitCode.ShouldBe(0, matching.Output);
            var otherVersion = _umbracoMajor == "17" ? "18.0.0" : "17.0.0";
            var mismatched = await RestoreConsumerAsync(outputDirectory, packageVersion, otherVersion, targetFramework);
            mismatched.ExitCode.ShouldNotBe(0, mismatched.Output);
            (mismatched.Output.Contains("NU1605", StringComparison.Ordinal) ||
                mismatched.Output.Contains("NU1608", StringComparison.Ordinal) ||
                mismatched.Output.Contains("NU1107", StringComparison.Ordinal)).ShouldBeTrue(mismatched.Output);
        } finally {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void AssertBans((int ExitCode, string Output) result) {
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("RS0030");
        result.Output.ShouldContain("FriendlyImageCropperTemplateExtensions");
        result.Output.ShouldContain("FriendlyPublishedContentExtensions");
        result.Output.ShouldContain("FriendlyPublishedElementExtensions");
        result.Output.ShouldContain("FriendlyUrlHelperExtensions");
    }

    private static async Task<(int ExitCode, string Output)> RestoreConsumerAsync(
        string packageDirectory, string packageVersion, string umbracoVersion, string targetFramework) {
        var directory = Path.Combine(packageDirectory, $"consumer-{umbracoVersion}");
        Directory.CreateDirectory(directory);
        var project = Path.Combine(directory, "Consumer.csproj");
        await File.WriteAllTextAsync(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Shorthand.UmbracoHelpers" Version="{packageVersion}" />
                <PackageReference Include="Umbraco.Cms.Web.Common" Version="{umbracoVersion}" />
              </ItemGroup>
            </Project>
            """, TestCancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "Consumer.cs"),
            "using Shorthand.UmbracoHelpers; public class Consumer { public IFriendlyPublishedContentHelper Helper { get; set; } }",
            TestCancellationToken);
        var config = Path.Combine(directory, "NuGet.Config");
        await File.WriteAllTextAsync(config, $"""
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{System.Security.SecurityElement.Escape(packageDirectory)}" />
                <add key="nuget" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """, TestCancellationToken);
        var restore = await RunDotnetAsync(
            "restore", project, "--nologo", "-v:q",
            "--configfile", config);
        if(restore.ExitCode != 0) {
            return restore;
        }

        return await RunDotnetAsync("build", project, "--no-restore", "--nologo", "-v:q");
    }

    private static async Task<(int ExitCode, string Output)> BuildAsync(string project, bool disabled) {
        return await RunDotnetAsync(
            "build", project, "--nologo", "-v:q",
            $"-p:UmbracoMajorVersion={_umbracoMajor}",
            $"-p:DisableShorthandUmbracoHelpersBannedSymbols={disabled.ToString().ToLowerInvariant()}");
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(params string[] arguments) {
        var startInfo = new ProcessStartInfo("dotnet") {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach(var argument in arguments) {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        var output = process.StandardOutput.ReadToEndAsync(TestCancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestCancellationToken);

        await process.WaitForExitAsync(TestCancellationToken);
        return (process.ExitCode, (await output) + (await error));
    }
}
