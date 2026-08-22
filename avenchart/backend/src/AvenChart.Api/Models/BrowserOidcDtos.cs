// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

/// <summary>
/// Public, credential-free description of the browser SSO boundary. Provider
/// secrets, signing keys, and raw tokens are deliberately absent.
/// </summary>
public sealed record BrowserOidcConfigurationResponse(
    string Mode,
    bool BrowserSignInEnabled,
    string? FailureReason,
    IReadOnlyList<string> Audiences,
    string StartPath,
    string CallbackPath,
    string ClientId,
    string Scopes);
