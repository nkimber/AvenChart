// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class LabProviderEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? LabDirectorId { get; set; }
    public string? Npi { get; set; }
    public required string Protocol { get; set; }
    public required string Usage { get; set; }
    public required string Direction { get; set; }
    public required string SendApplicationId { get; set; }
    public required string SendFacilityId { get; set; }
    public required string ReceiveApplicationId { get; set; }
    public required string ReceiveFacilityId { get; set; }
    public required string RemoteHost { get; set; }
    public required string Login { get; set; }
    public required string Password { get; set; }
    public required string OrdersPath { get; set; }
    public required string ResultsPath { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
}
