﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class JsonRpcTcpServerToSingleClient : ICommunicationProtocol
{
    public JsonRpcTcpServerToSingleClient(string clientHostName, int clientPort)
    {
        ClientHostName = clientHostName;
        ClientPort = clientPort;
    }

    public string ClientHostName { get; }

    public int ClientPort { get; }

    public string Name => nameof(JsonRpcTcpServerToSingleClient);

    public string Version => AppVersion.DefaultSemVer;

    public string Description => "JsonRpc server to client handshake, implementation based on the test platform protocol specification.";
}