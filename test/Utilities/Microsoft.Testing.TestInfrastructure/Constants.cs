﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Testing.TestInfrastructure;

public static class Constants
{
#if DEBUG
    public const string BuildConfiguration = "Debug";
#else
    public const string BuildConfiguration = "Release";
#endif

    public static readonly string ExecutableExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? ".exe"
        : string.Empty;
}