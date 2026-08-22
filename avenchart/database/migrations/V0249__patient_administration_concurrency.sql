-- Makes the contact and demographics form one versioned patient-administration aggregate.

alter table patients
  add column if not exists administration_version bigint not null default 1;
