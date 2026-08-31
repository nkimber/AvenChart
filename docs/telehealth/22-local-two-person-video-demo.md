# Local two-person video and audio demonstration

This runbook prepares a repeatable **local-only, synthetic-data** demonstration in which a patient and physician enter the same AvenChart telehealth workflow and establish browser-to-browser WebRTC video and audio.

It is deliberately not a production telehealth deployment. The signaling relay is in-process and transient; the browsers use host ICE candidates only; AvenChart does not receive media. There is no TURN/STUN service, recording, transcription, media storage, clinical completion, emergency support, or external video vendor.

## Preconditions

- Docker Desktop is running.
- `.env.staging` is configured from `staging.env.example`.
- The synthetic staging volume is new or otherwise has no active physician work and no queued telehealth requests. The preparation command refuses to change existing queue work.
- Use two separate browser profiles on the same workstation (for example, a normal window and a private window), so the patient and physician have independent sessions.
- Both profiles must allow camera and microphone. A headset is recommended to avoid audio feedback. If two physical media devices are available, select the appropriate device in each browser's privacy settings before joining.

The staging site binds only to loopback. It is intentionally not a two-device-over-the-internet demonstration.

## Start and prepare

From the repository root:

```powershell
docker compose --env-file .env.staging -f docker-compose.staging.yml up --build --wait
.\avenchart\scripts\Reset-AvenChartTelehealthPocStaging.ps1 -ResetStagingData
.\avenchart\scripts\Prepare-TelehealthPocDemo.ps1
```

The reset command explicitly discards and reconstructs the isolated synthetic staging database from the repository's gold fixture; it never targets the development or production database. The preparation command then calls the normal patient, operational-review, physician-shift, and reserve-next HTTP endpoints. It does not write directly to the database and it stops with the request reserved—before either participant receives a media grant. The local-only recovery endpoint lets the physician's browser load that active reservation after it signs in.

## Demonstrate the call

1. In the patient browser, sign in at `http://127.0.0.1:8088/portal/login` as `mod-pat-0012@example.test` with password `PortalPass207!`. Open `/portal/telehealth`, run **Check camera and microphone**, then select **Enter synthetic waiting room**.
2. In the physician browser, sign in at `http://127.0.0.1:8088/login` as `gold-provider-01` with password `pass`. Open `/clinician/telehealth/physician`; the active synthetic shift and reservation should be present. Run **Check camera and microphone**, then select **Enter physician waiting room**.
3. In the physician browser select **Start browser media POC**. In the patient browser select **Join browser media POC**.
4. Before starting or joining, use **Choose your local devices** to select the local camera and microphone. Where the browser supports it, choose the speaker as well; otherwise use the browser's audio settings. These choices apply only to the local browser and remain outside AvenChart.
5. Confirm that each browser shows its own camera preview and the other participant's video. Speak from each side and confirm that the remote audio track is heard.

The doctor starts the offer, so the last two actions must occur in that order. If a browser denies access, the local browser context is not secure, or the reservation has expired, abandon the synthetic connection in the physician UI and run the preparation command again.

## Clean up

Select **End browser media POC** in both browsers. In the physician browser use the connection-abandon control to release the synthetic request back to the staging queue; end the idle synthetic shift when the control permits it. To stop the environment:

```powershell
docker compose --env-file .env.staging -f docker-compose.staging.yml down
```

Use `down --volumes` only when intentionally discarding the entire synthetic staging database.

## Boundaries demonstrated

- A patient and physician receive distinct, short-lived grants for one synthetic request.
- Camera and microphone tracks remain in the two browsers; only bounded WebRTC offer/answer/candidate messages pass through the transient local relay.
- The call is not recorded or transcribed, and it cannot create a prescription, claim, external message, or real clinical outcome.
- The physician cannot start the synthetic consultation lifecycle until the browser peer connection reports itself connected.
