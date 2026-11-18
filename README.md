# Helper classes for Umbraco 17, 18 and 19

Shorthand.UmbracoHelpers provides dependency-injection-friendly wrappers around
Umbraco's friendly extension methods. Separate package major versions support
Umbraco CMS 17 and 18, with an experimental build for the Umbraco 19 release
candidate:

| Helper package | Umbraco CMS | .NET |
| -------------- | ----------- | ---- |
| 17.x           | 17.x        | 10   |
| 18.x           | 18.x        | 10   |
| 19.0.0-rc1     | 19.0.0-rc1  | 11   |

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

Select the package major matching the Umbraco installation. Once the 17.x
package is published, for Umbraco 17:

```shell
dotnet add package Shorthand.UmbracoHelpers --version 17.0.0
```

For Umbraco 18:

```shell
dotnet add package Shorthand.UmbracoHelpers
```

Without a version, the latest package may target a different Umbraco major.
Use the latest available 17.x or 18.x version as appropriate.
The minimum dependency versions establish compatibility, not a security
recommendation; use a patched Umbraco release and review its dependencies.
The 19.x preview is not automatically published; its API and dependency range
must be revalidated against the stable Umbraco 19 release.

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
The 19 preview omits `SearchChildren` and `SearchDescendants`, which are not
present in Umbraco 19.0.0-rc1.

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

All variants are maintained on `develop`. Umbraco 18 is the default when no
property is supplied. Restore separately when switching variants, then use the
same property for every command:

```shell
dotnet restore Shorthand.UmbracoHelpers.slnx -p:UmbracoMajorVersion=17
dotnet build Shorthand.UmbracoHelpers.slnx --no-restore -p:UmbracoMajorVersion=17
dotnet test Shorthand.UmbracoHelpers.slnx --no-build -p:UmbracoMajorVersion=17
dotnet pack src\Shorthand.UmbracoHelpers\Shorthand.UmbracoHelpers.csproj --no-restore -p:UmbracoMajorVersion=17
```

Replace `17` with `18` or `19` for the other variants. Don't run variants
concurrently in the same checkout: their intermediate output is shared.
The package dependency ranges are `[17.0.0, 18.0.0)`,
`[18.0.0, 19.0.0)` and, for the 19 preview, `[19.0.0-rc1, 19.0.0)`.

## Releasing

Maintain the `Version` values in `Directory.Build.props` independently.
After the release commit reaches `main`, create and push only the matching
stable version tag, for example `17.0.0` or `18.0.2`. The tag must match the
configured version and point to a commit on `main`. CI tests all variants and
publishes only the tagged package, then creates the GitHub release. Protect
release tags and configure NuGet trusted publishing for the tag-triggered
workflow. Package versions cannot be reused.

For the 19 stable release, first verify its APIs and behavior, update the
dependency range to `[19.0.0, 20.0.0)` and the package version to `19.0.0`,
and enable 19.x tags in the release workflow.

A separate support branch is not needed while the variants remain manageable
in shared source; reconsider one if their APIs diverge substantially.
