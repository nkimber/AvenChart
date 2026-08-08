# Synthetic demo data

`avenchart-shared-synthetic-v1` is the deterministic public demonstration dataset used by AvenChart's local PostgreSQL runtime and verification scripts.

All identities and clinical records are synthetic. The dataset must never be combined with or replaced by protected health information in this repository.

The canonical JSON is versioned directly in this repository. The AvenChart PostgreSQL adapter is generated when `avenchart/scripts/Seed-AvenChartGoldDataset.ps1` runs.

Legacy-system schema adapters and their generation rules are deliberately outside the public distribution.
