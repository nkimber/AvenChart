// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;

namespace AvenChart.Api.Infrastructure;

public interface IIntegrationTransport
{
    Task<IntegrationTransportResult> DeliverAsync(
        IntegrationOutboxMessage message,
        CancellationToken cancellationToken);
}
