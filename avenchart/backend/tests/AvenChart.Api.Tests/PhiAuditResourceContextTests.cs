// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests;

public sealed class PhiAuditResourceContextTests
{
    [Fact]
    public void DirectResourceContextRetainsOnlyBoundedIdentifiers()
    {
        var context = new DefaultHttpContext();

        PhiAuditResourceContext.Set(context, "Patient", "MOD-PAT-0004");

        Assert.Equal(new PhiAuditResource("Patient", "MOD-PAT-0004"), PhiAuditResourceContext.Get(context));

        PhiAuditResourceContext.Set(context, "Patient", new string('x', 257));

        Assert.Equal(new PhiAuditResource("Patient", "MOD-PAT-0004"), PhiAuditResourceContext.Get(context));
    }
}
