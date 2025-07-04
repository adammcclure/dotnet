﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Tools.Properties;

namespace Microsoft.EntityFrameworkCore.Tools.Commands;

internal partial class MigrationsHasPendingModelChangesCommand
{
    protected override int Execute(string[] args)
    {
        using var executor = CreateExecutor(args);
        if (new SemanticVersionComparer().Compare(executor.EFCoreVersion, "8.0.0") < 0)
        {
            throw new CommandException(Resources.VersionRequired("8.0.0"));
        }

        executor.HasPendingModelChanges(Context!.Value());

        return base.Execute(args);
    }
}
