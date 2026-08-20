// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class LabOrderCatalogEntity
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public int? LabId { get; set; }
    public string? Code { get; set; }
    public required string Name { get; set; }
    public required string ItemType { get; set; }
    public string? ProcedureTypeName { get; set; }
    public string? Description { get; set; }
    public string? Specimen { get; set; }
    public string? StandardCode { get; set; }
    public int Sequence { get; set; }
    public bool Active { get; set; }
}
