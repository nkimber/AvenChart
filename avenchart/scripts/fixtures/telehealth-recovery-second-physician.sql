-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later
-- Disposable synthetic acceptance fixture only; never a product migration.
insert into auth_accounts(username,display_name,role,staff_id,password_salt,password_hash)
select 'synthetic-recovery-physician','Synthetic recovery physician',source.role,staff.id,source.password_salt,source.password_hash
from auth_accounts source
cross join lateral (select id from staff where active and facility_id=10 and id<>101 order by id limit 1) staff
where source.username='gold-provider-01' and source.staff_id=101
on conflict(username) do nothing;
insert into auth_principal_facility_grants(username,facility_id,is_default,active,granted_by,updated_by)
select 'synthetic-recovery-physician',facility_id,is_default,active,'synthetic-recovery-test','synthetic-recovery-test'
from auth_principal_facility_grants where username='gold-provider-01' on conflict do nothing;
insert into auth_principal_purpose_of_use_grants(username,purpose_of_use,active,granted_by,updated_by)
select 'synthetic-recovery-physician',purpose_of_use,active,'synthetic-recovery-test','synthetic-recovery-test'
from auth_principal_purpose_of_use_grants where username='gold-provider-01' on conflict do nothing;
