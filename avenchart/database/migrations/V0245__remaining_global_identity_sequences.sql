create sequence if not exists patient_documents_id_seq;
select setval(
    'patient_documents_id_seq',
    greatest(coalesce((select max(id) from patient_documents), 0), 8999999),
    true);
alter table patient_documents alter column id set default nextval('patient_documents_id_seq');
alter sequence patient_documents_id_seq owned by patient_documents.id;

create sequence if not exists payment_sessions_id_seq;
select setval(
    'payment_sessions_id_seq',
    greatest(coalesce((select max(id) from payment_sessions), 0), 1200000),
    true);
alter table payment_sessions alter column id set default nextval('payment_sessions_id_seq');
alter sequence payment_sessions_id_seq owned by payment_sessions.id;

create sequence if not exists encounters_id_seq;
select setval(
    'encounters_id_seq',
    greatest(coalesce((select max(greatest(id, encounter)) from encounters), 0), 1),
    coalesce((select max(greatest(id, encounter)) from encounters), 0) > 0);
alter table encounters alter column id set default nextval('encounters_id_seq');
alter sequence encounters_id_seq owned by encounters.id;

create sequence if not exists encounter_signatures_id_seq;
select setval(
    'encounter_signatures_id_seq',
    greatest(coalesce((select max(id) from encounter_signatures), 0), 1),
    coalesce((select max(id) from encounter_signatures), 0) > 0);
alter table encounter_signatures alter column id set default nextval('encounter_signatures_id_seq');
alter sequence encounter_signatures_id_seq owned by encounter_signatures.id;

create sequence if not exists portal_mailbox_messages_id_seq;
select setval(
    'portal_mailbox_messages_id_seq',
    greatest(coalesce((select max(id) from portal_mailbox_messages), 0), 9390000),
    true);
alter table portal_mailbox_messages alter column id set default nextval('portal_mailbox_messages_id_seq');
alter sequence portal_mailbox_messages_id_seq owned by portal_mailbox_messages.id;

create sequence if not exists patients_legacy_pid_seq;
select setval(
    'patients_legacy_pid_seq',
    greatest(coalesce((select max(legacy_pid) from patients), 0), 100000),
    true);
alter table patients alter column legacy_pid set default nextval('patients_legacy_pid_seq');
alter sequence patients_legacy_pid_seq owned by patients.legacy_pid;

create sequence if not exists lab_orders_id_seq;
select setval(
    'lab_orders_id_seq',
    greatest(coalesce((select max(id) from lab_orders), 0), 1),
    coalesce((select max(id) from lab_orders), 0) > 0);
alter table lab_orders alter column id set default nextval('lab_orders_id_seq');
alter sequence lab_orders_id_seq owned by lab_orders.id;

create sequence if not exists lab_reports_id_seq;
select setval(
    'lab_reports_id_seq',
    greatest(coalesce((select max(id) from lab_reports), 0), 1),
    coalesce((select max(id) from lab_reports), 0) > 0);
alter table lab_reports alter column id set default nextval('lab_reports_id_seq');
alter sequence lab_reports_id_seq owned by lab_reports.id;

create sequence if not exists lab_results_id_seq;
select setval(
    'lab_results_id_seq',
    greatest(coalesce((select max(id) from lab_results), 0), 1),
    coalesce((select max(id) from lab_results), 0) > 0);
alter table lab_results alter column id set default nextval('lab_results_id_seq');
alter sequence lab_results_id_seq owned by lab_results.id;

create sequence if not exists lab_specimens_id_seq;
select setval(
    'lab_specimens_id_seq',
    greatest(coalesce((select max(id) from lab_specimens), 0), 1),
    coalesce((select max(id) from lab_specimens), 0) > 0);
alter table lab_specimens alter column id set default nextval('lab_specimens_id_seq');
alter sequence lab_specimens_id_seq owned by lab_specimens.id;
