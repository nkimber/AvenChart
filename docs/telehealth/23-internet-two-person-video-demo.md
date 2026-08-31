# Internet two-person video and audio demonstration

This runbook covers the deployed **synthetic-only, non-production** AvenChart
demonstration in which two people on separate networks act as a patient and a
physician. It must not be used for real symptoms, personal information,
clinical care, or emergencies.

## Transport design and boundaries

- The browser requests camera and microphone access only after a participant
  presses the call button in a secure HTTPS context.
- The existing AvenChart waiting-room grant is still required. Only after that
  authorization succeeds does the API create an anonymous Azure Communication
  Services (ACS) identity and return a short-lived, non-cacheable `voip`
  credential to that participant. Retries for that exact grant reuse the same
  credential window instead of creating more ACS identities.
- The patient and physician join the same ACS group call derived from the
  existing opaque synthetic connection-session identifier. The identifier and
  credential are not placed in browser storage, logs, URLs, or AvenChart audit
  payloads.
- ACS manages the calling signaling and encrypted audio/video transport.
  AvenChart authorizes the synthetic participants but does not receive, record,
  transcribe, store, or relay browser media.
- The API keeps only a process-local expiry marker for each anonymous ACS
  identity it creates, then deletes that identity five minutes after its
  one-hour credential expires. A host restart discards those markers, so this
  is hygiene for a short-lived demonstration rather than lifecycle management
  suitable for a production service.
- The AvenChart authorization service stays at one Container Apps replica for
  this POC. A restart ends the synthetic workflow and participants must obtain
  new grants. The ACS call itself is not an AvenChart clinical encounter.

This makes an ordinary two-person internet demonstration practical without
trying to operate a custom TURN service. It does **not** establish HIPAA
compliance, a BAA conclusion, production availability, clinical safety,
identity proofing, emergency support, or suitability for real patient care.

## Operator enablement

`infra/azure/operations/modules/platform.bicep` declares the synthetic ACS
resource and stores its connection string only in Key Vault. The application
template reads the Key Vault reference through the existing managed identity.
Do not put that connection string or an issued VoIP token in source control,
browser settings, an environment file, a URL, a support ticket, or logs.

Deploy the application template with the explicit opt-in parameters below. The
internet calling POC is disabled by default.

```powershell
az deployment group create `
  --resource-group <resource-group> `
  --name avenchart-internet-calling `
  --template-file .\infra\azure\operations\application.bicep `
  --parameters @<application-parameters-file> `
  telehealthInternetCallingPocEnabled=true `
  telehealthBrandedHost=<public-https-host> `
  customDomainName=<public-https-host> `
  customDomainCertificateId=<managed-certificate-resource-id>
```

Use the public HTTPS host with a valid certificate. Browser camera and
microphone APIs will not work from an insecure page. The template sets the
synthetic-only options, explicit host, supported states, Key Vault reference,
and one-replica restriction together. Confirm the readiness response reports
`Telehealth.enabled: true` before inviting participants.

Participants on a managed corporate network may need their network team to
allow the Azure Communication Services media endpoints and ports in
[Microsoft's network requirements](https://learn.microsoft.com/azure/communication-services/concepts/voice-video-calling/network-requirements).
Do not weaken a participant's firewall or browser security controls for this
demonstration.

The Container Apps application template owns its ingress configuration. Always
pass both custom-domain parameters for a deployment that uses a custom domain;
otherwise an ARM update can remove the certificate binding. The certificate ID
is available from `az containerapp env certificate list`.

## Demonstrate the call

1. Have the patient use a separate browser profile or device and sign in with
   the designated synthetic patient account. Open `/portal/telehealth`. When
   the request reaches **OperationalReview**, select **Join physician demo
   queue**. This patient-owned, one-click handoff remains available only after
   the existing synthetic eligibility, readiness, and coverage gates have
   passed; it is logged as a NON_PRODUCTION demonstration event, not a care
   acceptance or payment guarantee.
2. Have the physician sign in separately with the designated synthetic
   physician account. Open `/clinician/telehealth/physician`, start the
   synthetic shift, refresh if needed, and reserve the ready synthetic request.
3. Each person runs the device check, selects **Enter synthetic waiting room**
   or **Enter physician waiting room**, and selects the desired camera,
   microphone, and speaker in **Synthetic internet video-call POC**.
4. The physician selects **Start synthetic internet call**. The patient then
   selects **Join synthetic internet call**. Both users allow the browser
   camera and microphone prompt.
5. Confirm both camera previews, both remote video tiles, and two-way audio.
   The connected status identifies ACS as the media carrier; that is expected.

Use a headset and do not put both participants in the same room without echo
control. Do not demonstrate emergency or clinical-workflow claims. If either
participant receives a calling error, end both calls, abandon the synthetic
connection in the physician workflow, re-enter both waiting rooms, and retry.

## Post-demo checks and cleanup

End the synthetic internet call on both devices. Use the physician
connection-abandon control to return the synthetic request to the queue, then
end the synthetic shift when it is idle. Review only operational health and
aggregate service errors; do not collect browser console logs that could
contain a token or service diagnostic.

If the deployment is no longer needed, set
`telehealthInternetCallingPocEnabled=false` in the next application revision.
Rotate the ACS access key and its Key Vault secret if the connection string is
ever suspected to have been exposed.
