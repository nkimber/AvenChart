-- An intentional separate registration after duplicate matching needs durable,
-- patient-linked review evidence rather than a client-only acknowledgement.
create table if not exists patient_registration_duplicate_reviews (
    id bigserial primary key,
    registered_patient_id text not null references patients(canonical_id) on delete restrict,
    candidate_patient_id text not null references patients(canonical_id) on delete restrict,
    match_score integer not null check (match_score between 1 and 100),
    match_reasons jsonb not null,
    review_reason text not null check (char_length(btrim(review_reason)) between 10 and 500),
    reviewed_by text not null,
    reviewed_at timestamp not null default current_timestamp,
    check (registered_patient_id <> candidate_patient_id)
);

create index if not exists idx_patient_registration_duplicate_reviews_registered
    on patient_registration_duplicate_reviews (registered_patient_id, reviewed_at desc, id desc);

create index if not exists idx_patient_registration_duplicate_reviews_candidate
    on patient_registration_duplicate_reviews (candidate_patient_id, reviewed_at desc, id desc);
