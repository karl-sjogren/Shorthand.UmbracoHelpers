# Helper classes for Umbraco 18

Shorthand.UmbracoHelpers provides dependency-injection-friendly wrappers around
Umbraco's friendly extension methods. It targets .NET 10 and Umbraco CMS 18.

## What is included

The `Shorthand.UmbracoHelpers` package contains four helper interfaces and
their implementations:

- `IFriendlyPublishedContentHelper` for published content operations such as
  property values, content-tree traversal, URLs, and template checks.
- `IFriendlyPublishedElementHelper` for published element names, values,
  visibility, and related operations.
- `IFriendlyImageCropperTemplateHelper` for generating image crop URLs.
- `IFriendlyUrlHelper` for generating surface-controller action URLs.

The project also includes a Banned API Analyzer configuration that flags
direct calls to the corresponding Umbraco extension classes, steering code
toward these helper interfaces.

## Why use the helpers?

Umbraco's friendly APIs are static extension methods. Injecting the helper
interfaces makes the dependency explicit and lets application code substitute
a fake or mock in unit tests, instead of relying on static calls. The analyzer
helps keep new code consistent and makes these dependencies easier to replace
and test.

## Installation and use

Add the package to an Umbraco 18 project:

```shell
dotnet add package Shorthand.UmbracoHelpers
```

The included Umbraco composer registers all four helpers automatically. You
can also register them explicitly:

```csharp
builder.Services.AddFriendlyHelpers();
```

Inject the helper you need into a service or controller:

```csharp
using Umbraco.Cms.Core.Models.PublishedContent;
using Shorthand.UmbracoHelpers;

public class ArticleService(IFriendlyPublishedContentHelper contentHelper)
{
    public string? GetTitle(IPublishedContent article) =>
        contentHelper.Value<string>(article, "title");
}
```

The helper methods correspond to Umbraco's friendly extension methods; their
interfaces document the underlying Umbraco API with `inheritdoc` references.

## BannedApiAnalyzer

The repository uses `Microsoft.CodeAnalysis.BannedApiAnalyzers` to report
banned API use as diagnostic `RS0030`. The project-level
`src/BannedSymbols.txt` bans a few general APIs, while the package's transitive
MSBuild targets add Umbraco's friendly extension classes to the banned list
for projects that consume the package.

See the BannedApiAnalyzers package on NuGet:
<https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers>

To enable the analyzer in your application, reference the analyzer package.
The helper package supplies its Umbraco-specific banned-symbol list and build
targets:

```xml
<ItemGroup>
  <PackageReference
    Include="Microsoft.CodeAnalysis.BannedApiAnalyzers"
    Version="5.6.0"
    PrivateAssets="all" />
</ItemGroup>
```

When the analyzer is active, these Umbraco extension classes produce
`RS0030`:

- `FriendlyPublishedContentExtensions`
- `FriendlyPublishedElementExtensions`
- `FriendlyImageCropperTemplateExtensions`
- `FriendlyUrlHelperExtensions`

Inject and use the matching `I...Helper` interface instead.

The package's Umbraco-specific bans can be disabled for a build by setting
the MSBuild property `DisableShorthandUmbracoHelpersBannedSymbols` to `true`:

```shell
dotnet build -p:DisableShorthandUmbracoHelpersBannedSymbols=true
```

## Development

Build and run the test suite from the repository root:

```shell
dotnet build Shorthand.UmbracoHelpers.slnx
dotnet test Shorthand.UmbracoHelpers.slnx
```
