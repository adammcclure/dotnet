// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader;

internal interface INuGetPackageDownloader
{
    Task<string> DownloadPackageAsync(PackageId packageId,
        NuGetVersion packageVersion = null,
        PackageSourceLocation packageSourceLocation = null,
        bool includePreview = false,
        bool? includeUnlisted = null,
        DirectoryPath? downloadFolder = null,
        PackageSourceMapping packageSourceMapping = null);

    Task<string> GetPackageUrl(PackageId packageId,
        NuGetVersion packageVersion = null,
        PackageSourceLocation packageSourceLocation = null,
        bool includePreview = false);

    Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder);

    Task<NuGetVersion> GetLatestPackageVersion(PackageId packageId,
         PackageSourceLocation packageSourceLocation = null,
         bool includePreview = false);

    Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(PackageId packageId,
         int numberOfResults,
         PackageSourceLocation packageSourceLocation = null,
         bool includePreview = false);

    Task<NuGetVersion> GetBestPackageVersionAsync(PackageId packageId,
        VersionRange versionRange,
         PackageSourceLocation packageSourceLocation = null);

    Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(PackageId packageId,
        VersionRange versionRange,
        PackageSourceLocation packageSourceLocation = null);
} 
