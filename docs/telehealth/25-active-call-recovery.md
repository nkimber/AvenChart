# Synthetic active-call recovery

Authorized by the program owner on 16 October 2026: implement the synthetic demo launcher, active-call recovery, and their acceptance tests. This is implementation authority for those bounded demo capabilities; it does not authorize production care or deployment.

## Behavior

An established patient and the owning physician can recheck devices and prepare a reconnection after refreshing an active consultation. The server issues a fresh five-minute participant grant for the original room. It rechecks practice, facility, patient lifecycle, request state, physician ownership, and the busy shift. Recovery stops at wrap-up, closure, or four hours after consultation start. It creates no appointment, encounter, consultation, prescription, claim, or clinical attestation.

The original waiting-room session remains `Ended`, preserving the existing consultation-start evidence. Its reservation and waiting-room grants are not revived. Concurrent local signaling requests share one renewal, and expired or revoked grants cannot be replayed as current grants. Credentials remain in component memory.

The internet panel requests a current active-call grant when rejoining. The local browser transport supports either join order, negotiation identifiers, a patient request for a fresh offer, and relay reconciliation after an API restart. Both transports still require an explicit user action to capture media after a page reload.

## Verification

The focused browser/component tests cover denied permission, ambiguous retries, serialized grant renewal, local ICE ordering, call controls, and preservation of saved/unsaved physician work. The real API acceptance script creates its fixture using the existing normal intake, readiness, queue, reservation, and start endpoints:

```powershell
./avenchart/scripts/Test-TelehealthActiveCallRecovery.ps1 -ApiBaseUrl http://127.0.0.1:8089 -UseDisposableSyntheticEnvironment
```

Use a freshly initialized, isolated synthetic environment. The script refuses remote hosts, never resets/deletes records, and leaves its owned test visit in unfinished wrap-up. It checks unchanged room and consultation identities, command replay, another physician's denial, invalid request denial, old grant rejection, successful active signaling, and denial after wrap-up.

Actual executed results are recorded with each implementation checkpoint. Browser and hardware acceptance must be reported separately from API and unit results.

### 16 October 2026 implementation checkpoint

- Backend build: passed, no warnings or errors. Existing filtered backend telehealth suite: 758 passed.
- Frontend snapshot: TypeScript/Vite production build and bundle budget passed (246,539 / 256,000 initial bytes); all 184 telehealth component tests across 28 files passed. Windows worker startup repeatedly timed out, so the completed run used an isolated Node 24 Linux container with one worker and the command's 90-second per-test cap. Repository timeouts and assertions were not weakened.
- Real API/PostgreSQL acceptance script: passed on loopback port 8090 against a newly created, gold-seeded disposable database. The isolated second-physician account was provisioned with `fixtures/telehealth-recovery-second-physician.sql`. Earlier paused fixtures correctly failed the expired financial-evidence start gate; that gate was not relaxed.
- The real-API office browser scenario now includes reconnection after consultation start and restoration of the saved note. Execution of that extended browser scenario is pending at this checkpoint.
- No ACS call, physical camera/audio test, shared Azure mutation, deployment, or production acceptance was performed by this checkpoint.

## Remaining POC boundaries

The synthetic startup restriction and existing clinical gates remain enforced. ACS identities and cleanup remain process-local POC infrastructure. An already-issued ACS token is not a room-scoped production authorization, and application grant revocation must not be described as immediate provider-side revocation. This increment does not implement ACS Rooms, provider-side removal, production failover, real patient identity proofing, clinical consent, or real patient care. Physical camera/audio and real-network verification remain manual acceptance work.
