// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useRef, useState } from 'react'
import {
  authorizeRequest,
  authorizeApplicantPracticeReview,
  claimApplicantPracticeReview,
  executeApplicantSyntheticPromotion,
  getApplicantPracticeReviewPacket,
  listApplicantPracticeReviewInbox,
  listApplicantIdentityReview,
  listApplicantPromotionAuthorization,
  listApplicantSyntheticPromotion,
  listOperationalReview,
  recordApplicantIdentityReview,
  recordApplicantPromotionAuthorization,
  type TelehealthApplicantIdentityReviewItem,
  type TelehealthApplicantPracticeReviewInboxItem,
  type TelehealthApplicantPracticeReviewPacket,
  type TelehealthApplicantPromotionAuthorizationDecisionType,
  type TelehealthApplicantPromotionAuthorizationItem,
  type TelehealthApplicantSyntheticPromotionItem,
  type TelehealthQueueItem,
} from './api.ts'
import { isRequestCancellation } from '../../api/transport.ts'
import './telehealth.css'

type ReviewDraft = { reason: string; confirmed: boolean; retryKey: string | null }
type PromotionDraft = {
  decision: TelehealthApplicantPromotionAuthorizationDecisionType
  reason: string
  noneAssuranceAcknowledged: boolean
  syntheticDataConfirmed: boolean
  retryKey: string | null
}
type SyntheticPromotionDraft = {
  reason: string
  canonicalPatientCreationAcknowledged: boolean
  noPortalNoCareAcknowledged: boolean
  retryKey: string | null
}
type PracticeReviewClaimDraft = {
  noDecisionAcknowledged: boolean
  noPatientContactAcknowledged: boolean
  noRequestOrCareQueueAcknowledged: boolean
  retryKey: string | null
}
type PracticeReviewAuthorizationDraft = {
  noClinicalEligibilityAcknowledged: boolean
  noCoverageGuaranteeAcknowledged: boolean
  noRequestOrQueueAcknowledged: boolean
  retryKey: string | null
}

export default function AdminTelehealthQueue() {
  const [items, setItems] = useState<TelehealthQueueItem[]>([])
  const [applicants, setApplicants] = useState<TelehealthApplicantIdentityReviewItem[]>([])
  const [promotionApplicants, setPromotionApplicants] = useState<TelehealthApplicantPromotionAuthorizationItem[]>([])
  const [syntheticPromotionApplicants, setSyntheticPromotionApplicants] = useState<TelehealthApplicantSyntheticPromotionItem[]>([])
  const [practiceReviewItems, setPracticeReviewItems] = useState<TelehealthApplicantPracticeReviewInboxItem[]>([])
  const [limitations, setLimitations] = useState<string[]>([])
  const [promotionLimitations, setPromotionLimitations] = useState<string[]>([])
  const [syntheticPromotionLimitations, setSyntheticPromotionLimitations] = useState<string[]>([])
  const [practiceReviewLimitations, setPracticeReviewLimitations] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [identityLoading, setIdentityLoading] = useState(true)
  const [promotionLoading, setPromotionLoading] = useState(true)
  const [syntheticPromotionLoading, setSyntheticPromotionLoading] = useState(true)
  const [practiceReviewLoading, setPracticeReviewLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [identityError, setIdentityError] = useState<string | null>(null)
  const [promotionError, setPromotionError] = useState<string | null>(null)
  const [syntheticPromotionError, setSyntheticPromotionError] = useState<string | null>(null)
  const [practiceReviewError, setPracticeReviewError] = useState<string | null>(null)
  const [practiceReviewWorkingId, setPracticeReviewWorkingId] = useState<string | null>(null)
  const [practiceReviewPacketLoadingId, setPracticeReviewPacketLoadingId] = useState<string | null>(null)
  const [practiceReviewPacket, setPracticeReviewPacket] = useState<TelehealthApplicantPracticeReviewPacket | null>(null)
  const [practiceReviewPacketError, setPracticeReviewPacketError] = useState<string | null>(null)
  const [practiceReviewAuthorizationWorkingId, setPracticeReviewAuthorizationWorkingId] = useState<string | null>(null)
  const [practiceReviewAuthorizationError, setPracticeReviewAuthorizationError] = useState<string | null>(null)
  const [workingId, setWorkingId] = useState<string | null>(null)
  const [identityWorkingId, setIdentityWorkingId] = useState<string | null>(null)
  const [promotionWorkingId, setPromotionWorkingId] = useState<string | null>(null)
  const [syntheticPromotionWorkingId, setSyntheticPromotionWorkingId] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, ReviewDraft>>({})
  const [promotionDrafts, setPromotionDrafts] = useState<Record<string, PromotionDraft>>({})
  const [syntheticPromotionDrafts, setSyntheticPromotionDrafts] = useState<Record<string, SyntheticPromotionDraft>>({})
  const [practiceReviewClaimDrafts, setPracticeReviewClaimDrafts] = useState<Record<string, PracticeReviewClaimDraft>>({})
  const [practiceReviewAuthorizationDrafts, setPracticeReviewAuthorizationDrafts] = useState<Record<string, PracticeReviewAuthorizationDraft>>({})
  const generation = useRef(0)
  const identityGeneration = useRef(0)
  const promotionGeneration = useRef(0)
  const syntheticPromotionGeneration = useRef(0)
  const practiceReviewGeneration = useRef(0)

  const refresh = useCallback(async (signal?: AbortSignal) => {
    const current = ++generation.current
    setLoading(true)
    setError(null)
    try {
      const result = await listOperationalReview(signal)
      if (current !== generation.current) return
      setItems(result)
    } catch (caught) {
      if (isRequestCancellation(caught) || current !== generation.current) return
      setItems([])
      setWorkingId(null)
      setError(caught instanceof Error ? caught.message : 'The operational review queue could not be loaded.')
    } finally {
      if (current === generation.current) setLoading(false)
    }
  }, [])

  const refreshPromotion = useCallback(async (signal?: AbortSignal) => {
    const current = ++promotionGeneration.current
    setPromotionLoading(true)
    setPromotionError(null)
    try {
      const result = await listApplicantPromotionAuthorization(signal)
      if (current !== promotionGeneration.current) return
      setPromotionApplicants(result.applicants)
      setPromotionLimitations(result.limitations)
    } catch (caught) {
      if (isRequestCancellation(caught) || current !== promotionGeneration.current) return
      setPromotionApplicants([])
      setPromotionWorkingId(null)
      setPromotionError(caught instanceof Error ? caught.message : 'The synthetic promotion-authorization queue could not be loaded.')
    } finally {
      if (current === promotionGeneration.current) setPromotionLoading(false)
    }
  }, [])

  const refreshSyntheticPromotion = useCallback(async (signal?: AbortSignal) => {
    const current = ++syntheticPromotionGeneration.current
    setSyntheticPromotionLoading(true)
    setSyntheticPromotionError(null)
    try {
      const result = await listApplicantSyntheticPromotion(signal)
      if (current !== syntheticPromotionGeneration.current) return
      setSyntheticPromotionApplicants(result.applicants)
      setSyntheticPromotionLimitations(result.limitations)
    } catch (caught) {
      if (isRequestCancellation(caught) || current !== syntheticPromotionGeneration.current) return
      setSyntheticPromotionApplicants([])
      setSyntheticPromotionWorkingId(null)
      setSyntheticPromotionError(caught instanceof Error ? caught.message : 'The atomic synthetic-promotion queue could not be loaded.')
    } finally {
      if (current === syntheticPromotionGeneration.current) setSyntheticPromotionLoading(false)
    }
  }, [])

  const refreshIdentity = useCallback(async (signal?: AbortSignal) => {
    const current = ++identityGeneration.current
    setIdentityLoading(true)
    setIdentityError(null)
    try {
      const result = await listApplicantIdentityReview(signal)
      if (current !== identityGeneration.current) return
      setApplicants(result.applicants)
      setLimitations(result.limitations)
    } catch (caught) {
      if (isRequestCancellation(caught) || current !== identityGeneration.current) return
      setApplicants([])
      setIdentityWorkingId(null)
      setIdentityError(caught instanceof Error ? caught.message : 'The applicant identity-review queue could not be loaded.')
    } finally {
      if (current === identityGeneration.current) setIdentityLoading(false)
    }
  }, [])

  const refreshPracticeReview = useCallback(async (signal?: AbortSignal) => {
    const current = ++practiceReviewGeneration.current
    setPracticeReviewLoading(true)
    setPracticeReviewError(null)
    try {
      const result = await listApplicantPracticeReviewInbox(signal)
      if (current !== practiceReviewGeneration.current) return
      setPracticeReviewItems(result.items)
      setPracticeReviewLimitations(result.limitations)
      setPracticeReviewPacket((packet) => packet && result.items.some((item) =>
        item.practiceReviewCaseId === packet.practiceReviewCaseId && item.assignedToCurrentUser)
        ? packet
        : null)
    } catch (caught) {
      if (isRequestCancellation(caught) || current !== practiceReviewGeneration.current) return
      setPracticeReviewItems([])
      setPracticeReviewError(caught instanceof Error ? caught.message : 'The pending practice-review inbox could not be loaded.')
    } finally {
      if (current === practiceReviewGeneration.current) setPracticeReviewLoading(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void refresh(controller.signal)
    void refreshIdentity(controller.signal)
    void refreshPromotion(controller.signal)
    void refreshSyntheticPromotion(controller.signal)
    void refreshPracticeReview(controller.signal)
    const timer = window.setInterval(() => {
      void refresh(controller.signal)
      void refreshIdentity(controller.signal)
      void refreshPromotion(controller.signal)
      void refreshSyntheticPromotion(controller.signal)
      void refreshPracticeReview(controller.signal)
    }, 10_000)
    return () => { controller.abort(); window.clearInterval(timer) }
  }, [refresh, refreshIdentity, refreshPracticeReview, refreshPromotion, refreshSyntheticPromotion])

  async function authorize(item: TelehealthQueueItem) {
    setWorkingId(item.requestId)
    setError(null)
    try {
      await authorizeRequest(item.requestId, item.version)
      await refresh()
    } catch (caught) {
      setItems([])
      setError(caught instanceof Error ? caught.message : 'Authorization failed. Refresh before retrying.')
    } finally {
      setWorkingId(null)
    }
  }

  function updatePracticeReviewClaimDraft(caseId: string, change: Partial<PracticeReviewClaimDraft>) {
    setPracticeReviewClaimDrafts((current) => {
      const existing = current[caseId] ?? {
        noDecisionAcknowledged: false,
        noPatientContactAcknowledged: false,
        noRequestOrCareQueueAcknowledged: false,
        retryKey: null,
      }
      return { ...current, [caseId]: { ...existing, ...change } }
    })
  }

  async function claimPracticeReview(item: TelehealthApplicantPracticeReviewInboxItem) {
    const draft = practiceReviewClaimDrafts[item.practiceReviewCaseId] ?? {
      noDecisionAcknowledged: false,
      noPatientContactAcknowledged: false,
      noRequestOrCareQueueAcknowledged: false,
      retryKey: null,
    }
    const retryKey = draft.retryKey ?? crypto.randomUUID()
    updatePracticeReviewClaimDraft(item.practiceReviewCaseId, { retryKey })
    setPracticeReviewWorkingId(item.practiceReviewCaseId)
    setPracticeReviewError(null)
    try {
      await claimApplicantPracticeReview(item.practiceReviewCaseId, {
        expectedApplicantVersion: item.applicantVersion,
        inboxPolicyVersion: 1,
        noDecisionAcknowledged: true,
        noPatientContactAcknowledged: true,
        noRequestOrCareQueueAcknowledged: true,
      }, retryKey)
      setPracticeReviewClaimDrafts((current) => {
        const next = { ...current }
        delete next[item.practiceReviewCaseId]
        return next
      })
      await refreshPracticeReview()
    } catch (caught) {
      setPracticeReviewError(caught instanceof Error ? caught.message : 'The claim result is unknown. Retry the unchanged command or refresh the inbox.')
    } finally {
      setPracticeReviewWorkingId(null)
    }
  }

  async function openPracticeReviewPacket(item: TelehealthApplicantPracticeReviewInboxItem) {
    setPracticeReviewPacketLoadingId(item.practiceReviewCaseId)
    setPracticeReviewPacketError(null)
    setPracticeReviewAuthorizationError(null)
    try {
      setPracticeReviewPacket(await getApplicantPracticeReviewPacket(item.practiceReviewCaseId))
    } catch (caught) {
      setPracticeReviewPacket(null)
      setPracticeReviewPacketError(caught instanceof Error
        ? caught.message
        : 'The review packet could not be loaded. The short claim may have expired.')
    } finally {
      setPracticeReviewPacketLoadingId(null)
    }
  }

  function updatePracticeReviewAuthorizationDraft(caseId: string, change: Partial<PracticeReviewAuthorizationDraft>) {
    setPracticeReviewAuthorizationDrafts((current) => {
      const existing = current[caseId] ?? {
        noClinicalEligibilityAcknowledged: false,
        noCoverageGuaranteeAcknowledged: false,
        noRequestOrQueueAcknowledged: false,
        retryKey: null,
      }
      return { ...current, [caseId]: { ...existing, ...change } }
    })
  }

  async function authorizePracticeReview(item: TelehealthApplicantPracticeReviewInboxItem) {
    const draft = practiceReviewAuthorizationDrafts[item.practiceReviewCaseId] ?? {
      noClinicalEligibilityAcknowledged: false,
      noCoverageGuaranteeAcknowledged: false,
      noRequestOrQueueAcknowledged: false,
      retryKey: null,
    }
    const retryKey = draft.retryKey ?? crypto.randomUUID()
    updatePracticeReviewAuthorizationDraft(item.practiceReviewCaseId, { retryKey })
    setPracticeReviewAuthorizationWorkingId(item.practiceReviewCaseId)
    setPracticeReviewAuthorizationError(null)
    try {
      await authorizeApplicantPracticeReview(item.practiceReviewCaseId, {
        expectedApplicantVersion: item.applicantVersion,
        packetPolicyVersion: 1,
        decision: 'AuthorizedForSyntheticRequestCreation',
        rationaleCode: 'OperationalPrerequisitesReviewed',
        noClinicalEligibilityAcknowledged: true,
        noCoverageGuaranteeAcknowledged: true,
        noRequestOrQueueAcknowledged: true,
      }, retryKey)
      setPracticeReviewAuthorizationDrafts((current) => {
        const next = { ...current }
        delete next[item.practiceReviewCaseId]
        return next
      })
      setPracticeReviewPacket(null)
      setPracticeReviewAuthorizationError(null)
      await refreshPracticeReview()
    } catch (caught) {
      setPracticeReviewAuthorizationError(caught instanceof Error
        ? caught.message
        : 'The authorization result is unknown. Retry the unchanged command or reload the inbox.')
    } finally {
      setPracticeReviewAuthorizationWorkingId(null)
    }
  }

  function updateDraft(applicantId: string, change: Partial<ReviewDraft>) {
    setDrafts((current) => {
      const existing = current[applicantId] ?? { reason: '', confirmed: false, retryKey: null }
      return { ...current, [applicantId]: { ...existing, ...change } }
    })
  }

  async function recordReview(applicant: TelehealthApplicantIdentityReviewItem) {
    const draft = drafts[applicant.applicantId] ?? { reason: '', confirmed: false, retryKey: null }
    const retryKey = draft.retryKey ?? crypto.randomUUID()
    updateDraft(applicant.applicantId, { retryKey })
    setIdentityWorkingId(applicant.applicantId)
    setIdentityError(null)
    try {
      await recordApplicantIdentityReview(applicant.applicantId, {
        expectedVersion: applicant.version,
        decision: applicant.allowedDecision,
        reason: draft.reason,
        syntheticDataConfirmed: true,
      }, retryKey)
      setDrafts((current) => {
        const next = { ...current }
        delete next[applicant.applicantId]
        return next
      })
      await refreshIdentity()
    } catch (caught) {
      setIdentityError(caught instanceof Error ? caught.message : 'The review result is unknown. Retry the unchanged command or refresh the queue.')
    } finally {
      setIdentityWorkingId(null)
    }
  }

  function updatePromotionDraft(applicantId: string, change: Partial<PromotionDraft>) {
    setPromotionDrafts((current) => {
      const existing = current[applicantId] ?? {
        decision: 'AuthorizedForSyntheticPromotion', reason: '',
        noneAssuranceAcknowledged: false, syntheticDataConfirmed: false, retryKey: null,
      }
      return { ...current, [applicantId]: { ...existing, ...change } }
    })
  }

  async function recordPromotionAuthorization(applicant: TelehealthApplicantPromotionAuthorizationItem) {
    const draft = promotionDrafts[applicant.applicantId] ?? {
      decision: 'AuthorizedForSyntheticPromotion' as const, reason: '',
      noneAssuranceAcknowledged: false, syntheticDataConfirmed: false, retryKey: null,
    }
    const retryKey = draft.retryKey ?? crypto.randomUUID()
    updatePromotionDraft(applicant.applicantId, { retryKey })
    setPromotionWorkingId(applicant.applicantId)
    setPromotionError(null)
    try {
      await recordApplicantPromotionAuthorization(applicant.applicantId, {
        expectedVersion: applicant.version,
        decision: draft.decision,
        reason: draft.reason,
        noneAssuranceAcknowledged: true,
        syntheticDataConfirmed: true,
      }, retryKey)
      setPromotionDrafts((current) => {
        const next = { ...current }
        delete next[applicant.applicantId]
        return next
      })
      await refreshPromotion()
    } catch (caught) {
      setPromotionError(caught instanceof Error ? caught.message : 'The authorization result is unknown. Retry the unchanged command or refresh the queue.')
    } finally {
      setPromotionWorkingId(null)
    }
  }

  function updateSyntheticPromotionDraft(applicantId: string, change: Partial<SyntheticPromotionDraft>) {
    setSyntheticPromotionDrafts((current) => {
      const existing = current[applicantId] ?? {
        reason: '', canonicalPatientCreationAcknowledged: false,
        noPortalNoCareAcknowledged: false, retryKey: null,
      }
      return { ...current, [applicantId]: { ...existing, ...change } }
    })
  }

  async function executeSyntheticPromotion(applicant: TelehealthApplicantSyntheticPromotionItem) {
    const draft = syntheticPromotionDrafts[applicant.applicantId] ?? {
      reason: '', canonicalPatientCreationAcknowledged: false,
      noPortalNoCareAcknowledged: false, retryKey: null,
    }
    const retryKey = draft.retryKey ?? crypto.randomUUID()
    updateSyntheticPromotionDraft(applicant.applicantId, { retryKey })
    setSyntheticPromotionWorkingId(applicant.applicantId)
    setSyntheticPromotionError(null)
    try {
      await executeApplicantSyntheticPromotion(applicant.applicantId, {
        expectedVersion: applicant.version,
        command: 'PromoteAuthorizedSyntheticApplicant',
        reason: draft.reason,
        canonicalPatientCreationAcknowledged: true,
        noPortalNoCareAcknowledged: true,
      }, retryKey)
      setSyntheticPromotionDrafts((current) => {
        const next = { ...current }
        delete next[applicant.applicantId]
        return next
      })
      await refreshSyntheticPromotion()
    } catch (caught) {
      setSyntheticPromotionError(caught instanceof Error ? caught.message : 'The promotion result is unknown. Retry the unchanged command or refresh the queue.')
    } finally {
      setSyntheticPromotionWorkingId(null)
    }
  }

  return (
    <main className="telehealth-page" aria-labelledby="admin-telehealth-title">
      <header className="telehealth-heading"><div><p className="telehealth-kicker">Practice operations</p><h1 id="admin-telehealth-title">Telehealth administration</h1></div><button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => { void refresh(); void refreshIdentity(); void refreshPromotion(); void refreshSyntheticPromotion(); void refreshPracticeReview() }} disabled={loading || identityLoading || promotionLoading || syntheticPromotionLoading || practiceReviewLoading}>Refresh all</button></header>
      <div className="telehealth-synthetic" role="note">Synthetic demonstration only. Staff review does not establish real identity. The bounded promotion exercise can create only a portal-disabled synthetic patient shell.</div>

      <section className="telehealth-card" aria-labelledby="practice-review-inbox-title" aria-busy={practiceReviewLoading}>
        <h2 id="practice-review-inbox-title">Pending practice review</h2>
        <p>Read-only operational awareness of submitted synthetic work items. These are not telehealth requests or patient or clinician care-queue entries.</p>
        {practiceReviewLimitations.length > 0 ? <ul>{practiceReviewLimitations.map((item) => <li key={item}>{item}</li>)}</ul> : null}
        {practiceReviewError ? <div><p className="telehealth-error" role="alert">{practiceReviewError}</p><button className="telehealth-button" type="button" onClick={() => void refreshPracticeReview()}>Reload practice-review inbox</button></div> : null}
        {practiceReviewLoading ? <p aria-live="polite">Refreshing pending practice review…</p> : null}
        {!practiceReviewLoading && practiceReviewItems.length === 0 && !practiceReviewError ? <p>No synthetic work items are pending practice review.</p> : null}
        <ul className="telehealth-queue">
          {practiceReviewItems.map((item) => {
            const claimDraft = practiceReviewClaimDrafts[item.practiceReviewCaseId] ?? {
              noDecisionAcknowledged: false,
              noPatientContactAcknowledged: false,
              noRequestOrCareQueueAcknowledged: false,
              retryKey: null,
            }
            const canClaim = claimDraft.noDecisionAcknowledged
              && claimDraft.noPatientContactAcknowledged
              && claimDraft.noRequestOrCareQueueAcknowledged
            const authorizationDraft = practiceReviewAuthorizationDrafts[item.practiceReviewCaseId] ?? {
              noClinicalEligibilityAcknowledged: false,
              noCoverageGuaranteeAcknowledged: false,
              noRequestOrQueueAcknowledged: false,
              retryKey: null,
            }
            const canAuthorize = authorizationDraft.noClinicalEligibilityAcknowledged
              && authorizationDraft.noCoverageGuaranteeAcknowledged
              && authorizationDraft.noRequestOrQueueAcknowledged
            return <li key={item.practiceReviewCaseId}>
            <div>
              <strong>{item.legalFirstName} {item.legalLastName}</strong>
              <span>Born {item.dateOfBirth} · {item.residenceStateCode} {item.postalCode}</span>
              <span>{item.maskedEmail} · {item.maskedPhone}</span>
              <span>{item.purposeDisplayLabel} · universal safety screen passed</span>
              <small>Submitted {new Date(item.submittedAt).toLocaleString()} · pending practice review · applicant version {item.applicantVersion}</small>
            </div>
            <div className="telehealth-review-form">
              <dl className="telehealth-details">
                <div><dt>Server review route</dt><dd>{item.reviewRoute}</dd></div>
                <div><dt>Review claim</dt><dd>{item.assigned ? item.assignedToCurrentUser ? `Claimed by you until ${new Date(item.assignmentExpiresAt ?? '').toLocaleTimeString()}` : 'Claimed by another authorized staff member' : 'Available'}</dd></div>
                <div><dt>Priority or practice decision</dt><dd>None</dd></div>
                <div><dt>Telehealth request or care queue</dt><dd>Not created or entered</dd></div>
              </dl>
              <details>
                <summary>Coarse submitted sections</summary>
                <ul>{item.sections.map((section) => <li key={section.sectionKey}><strong>{section.sectionKey}</strong>: {section.receiptState} · {section.outstandingRoute}</li>)}</ul>
              </details>
              {item.assigned ? <div><p role="status"><strong>{item.assignedToCurrentUser ? 'You hold this short review claim.' : 'Another authorized staff member holds this short review claim.'}</strong> It does not create priority, a decision, patient contact, a request, or care authority.</p>{item.assignedToCurrentUser ? <button className="telehealth-button" type="button" aria-expanded={practiceReviewPacket?.practiceReviewCaseId === item.practiceReviewCaseId} disabled={practiceReviewPacketLoadingId !== null} onClick={() => void openPracticeReviewPacket(item)}>{practiceReviewPacketLoadingId === item.practiceReviewCaseId ? 'Opening packet…' : 'Open operational review packet'}</button> : null}</div> : <fieldset>
                <legend>Claim this item for 120 seconds</legend>
                <label className="telehealth-check"><input type="checkbox" checked={claimDraft.noDecisionAcknowledged} onChange={(event) => updatePracticeReviewClaimDraft(item.practiceReviewCaseId, { noDecisionAcknowledged: event.target.checked, retryKey: null })} /> I understand this claim is not an accept, decline, or clinical decision.</label>
                <label className="telehealth-check"><input type="checkbox" checked={claimDraft.noPatientContactAcknowledged} onChange={(event) => updatePracticeReviewClaimDraft(item.practiceReviewCaseId, { noPatientContactAcknowledged: event.target.checked, retryKey: null })} /> I understand this claim does not contact the patient.</label>
                <label className="telehealth-check"><input type="checkbox" checked={claimDraft.noRequestOrCareQueueAcknowledged} onChange={(event) => updatePracticeReviewClaimDraft(item.practiceReviewCaseId, { noRequestOrCareQueueAcknowledged: event.target.checked, retryKey: null })} /> I understand this claim creates no telehealth request or care queue.</label>
                <button className="telehealth-button" type="button" disabled={practiceReviewWorkingId !== null || !canClaim} onClick={() => void claimPracticeReview(item)}>{practiceReviewWorkingId === item.practiceReviewCaseId ? 'Claiming…' : 'Claim for review'}</button>
              </fieldset>}
              {practiceReviewPacketError && item.assignedToCurrentUser ? <div><p className="telehealth-error" role="alert">{practiceReviewPacketError}</p><button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => void openPracticeReviewPacket(item)}>Retry packet</button></div> : null}
              {practiceReviewPacket?.practiceReviewCaseId === item.practiceReviewCaseId ? <section className="telehealth-review-form" aria-labelledby={`practice-review-packet-${item.practiceReviewCaseId}`}>
                <div className="telehealth-heading"><h3 id={`practice-review-packet-${item.practiceReviewCaseId}`}>Claimant-only operational review packet</h3><button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => { setPracticeReviewPacket(null); setPracticeReviewPacketError(null); setPracticeReviewAuthorizationError(null) }}>Close packet</button></div>
                <p><strong>Claim expires:</strong> {new Date(practiceReviewPacket.assignmentExpiresAt).toLocaleString()}. Opening this packet does not extend the claim.</p>
                <dl className="telehealth-details">
                  <div><dt>Registration receipt</dt><dd>Recorded {new Date(practiceReviewPacket.registration.confirmedAt).toLocaleString()} · identity assurance not established</dd></div>
                  <div><dt>Synthetic payer and product</dt><dd>{practiceReviewPacket.insurance.payerDisplayName} · {practiceReviewPacket.insurance.productDisplayName}</dd></div>
                  <div><dt>Masked member details</dt><dd>{practiceReviewPacket.insurance.memberIdMask}{practiceReviewPacket.insurance.groupNumberMask ? ` · group ${practiceReviewPacket.insurance.groupNumberMask}` : ''} · {practiceReviewPacket.insurance.subscriberRelationship}</dd></div>
                  <div><dt>Synthetic eligibility evidence</dt><dd>{practiceReviewPacket.insurance.eligibilityBusinessOutcome} · {practiceReviewPacket.insurance.eligibilityEvidenceCurrent ? 'current' : 'expired'}</dd></div>
                  <div><dt>Synthetic practice network evidence</dt><dd>{practiceReviewPacket.insurance.practiceNetworkBusinessOutcome} · {practiceReviewPacket.insurance.practiceNetworkEvidenceCurrent ? 'current' : 'expired'} · rendering physician not checked</dd></div>
                  <div><dt>Communication access</dt><dd>{practiceReviewPacket.communicationAccess.preferredSpokenLanguage} · interpreter requested: {practiceReviewPacket.communicationAccess.interpreterRequested ? 'yes' : 'no'} · accessibility support requested: {practiceReviewPacket.communicationAccess.accessibilitySupportRequested ? 'yes' : 'no'}</dd></div>
                  <div><dt>Client-reported device preparation</dt><dd>Browser, camera, microphone, and speaker reported available · network {practiceReviewPacket.devicePreparation.networkQuality.toLowerCase()} · technology readiness not established</dd></div>
                  <div><dt>Clinical routing only</dt><dd>{practiceReviewPacket.clinicalInformationSummaryRoute} · no clinical selections or patient chart shown</dd></div>
                </dl>
                <ul>{practiceReviewPacket.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                <fieldset>
                  <legend>Authorize a later synthetic request-creation step</legend>
                  <p><strong>Controlled rationale:</strong> Operational prerequisites reviewed. This positive-only operational authorization does not itself create the request.</p>
                  <label className="telehealth-check"><input type="checkbox" checked={authorizationDraft.noClinicalEligibilityAcknowledged} onChange={(event) => updatePracticeReviewAuthorizationDraft(item.practiceReviewCaseId, { noClinicalEligibilityAcknowledged: event.target.checked, retryKey: null })} /> I understand this is not a clinical eligibility decision.</label>
                  <label className="telehealth-check"><input type="checkbox" checked={authorizationDraft.noCoverageGuaranteeAcknowledged} onChange={(event) => updatePracticeReviewAuthorizationDraft(item.practiceReviewCaseId, { noCoverageGuaranteeAcknowledged: event.target.checked, retryKey: null })} /> I understand synthetic eligibility and practice-network evidence is not a coverage guarantee, and the rendering physician was not checked.</label>
                  <label className="telehealth-check"><input type="checkbox" checked={authorizationDraft.noRequestOrQueueAcknowledged} onChange={(event) => updatePracticeReviewAuthorizationDraft(item.practiceReviewCaseId, { noRequestOrQueueAcknowledged: event.target.checked, retryKey: null })} /> I understand this creates no request, queue, appointment, encounter, consent, or care authority.</label>
                  {practiceReviewAuthorizationError ? <p className="telehealth-error" role="alert">{practiceReviewAuthorizationError}</p> : null}
                  <button className="telehealth-button" type="button" disabled={practiceReviewAuthorizationWorkingId !== null || !canAuthorize} onClick={() => void authorizePracticeReview(item)}>{practiceReviewAuthorizationWorkingId === item.practiceReviewCaseId ? 'Authorizing…' : practiceReviewAuthorizationError ? 'Retry unchanged authorization' : 'Authorize later request creation'}</button>
                </fieldset>
                <p><strong>Boundary:</strong> this can record only a separately gated authorization. No contact, request, queue, chart, appointment, encounter, consent, or care action is available.</p>
              </section> : null}
              <p><strong>Boundary:</strong> no priority, accept, decline, contact, request, queue, appointment, encounter, prescribing, billing, claim, integration, or care action is available.</p>
            </div>
          </li>})}
        </ul>
      </section>

      <section className="telehealth-card" aria-labelledby="identity-review-title" aria-busy={identityLoading}>
        <h2 id="identity-review-title">Prospective applicant identity review</h2>
        <p>Review contact-control and duplicate-disposition evidence only. Possible matching patient information is never shown.</p>
        {limitations.length > 0 ? <ul>{limitations.map((item) => <li key={item}>{item}</li>)}</ul> : null}
        {identityError ? <div><p className="telehealth-error" role="alert">{identityError}</p><button className="telehealth-button" type="button" onClick={() => void refreshIdentity()}>Reload applicant queue</button></div> : null}
        {identityLoading ? <p aria-live="polite">Refreshing applicant queue…</p> : null}
        {!identityLoading && applicants.length === 0 && !identityError ? <p>No applicants are awaiting bounded identity review.</p> : null}
        <ul className="telehealth-queue">
          {applicants.map((applicant) => {
            const draft = drafts[applicant.applicantId] ?? { reason: '', confirmed: false, retryKey: null }
            const approving = applicant.allowedDecision === 'ApprovedForProspectiveIntake'
            return <li key={applicant.applicantId}>
              <div>
                <strong>{applicant.legalFirstName} {applicant.legalLastName}</strong>
                <span>Born {applicant.dateOfBirth} · {applicant.residenceStateCode} {applicant.postalCode}</span>
                <span>{applicant.maskedEmail} · {applicant.maskedPhone}</span>
                <small>{applicant.duplicateDisposition === 'NoCandidate' ? 'No deterministic candidate found' : 'Possible match — separate manual matching required'} · version {applicant.version}</small>
              </div>
              <div className="telehealth-review-form">
                <label htmlFor={`review-reason-${applicant.applicantId}`}>Review reason</label>
                <textarea id={`review-reason-${applicant.applicantId}`} value={draft.reason} minLength={10} maxLength={1000} onChange={(event) => updateDraft(applicant.applicantId, { reason: event.target.value, retryKey: null })} />
                <label className="telehealth-check"><input type="checkbox" checked={draft.confirmed} onChange={(event) => updateDraft(applicant.applicantId, { confirmed: event.target.checked, retryKey: null })} /> I confirm this uses synthetic data and is not identity proofing or patient creation.</label>
                <button className="telehealth-button" type="button" disabled={identityWorkingId !== null || draft.reason.trim().length < 10 || !draft.confirmed} onClick={() => void recordReview(applicant)}>{identityWorkingId === applicant.applicantId ? 'Recording…' : approving ? 'Approve for later prospective intake' : 'Require separate manual review'}</button>
              </div>
            </li>
          })}
        </ul>
      </section>

      <section className="telehealth-card" aria-labelledby="promotion-authorization-title" aria-busy={promotionLoading}>
        <h2 id="promotion-authorization-title">Synthetic promotion authorization</h2>
        <p>Review the normalized process chain and authorize or deny only a future synthetic promotion exercise. Assurance remains None and identity was not proved.</p>
        {promotionLimitations.length > 0 ? <ul>{promotionLimitations.map((item) => <li key={item}>{item}</li>)}</ul> : null}
        {promotionError ? <div><p className="telehealth-error" role="alert">{promotionError}</p><button className="telehealth-button" type="button" onClick={() => void refreshPromotion()}>Reload promotion queue</button></div> : null}
        {promotionLoading ? <p aria-live="polite">Refreshing promotion queue…</p> : null}
        {!promotionLoading && promotionApplicants.length === 0 && !promotionError ? <p>No applicants are awaiting synthetic promotion authorization.</p> : null}
        <ul className="telehealth-queue">
          {promotionApplicants.map((applicant) => {
            const draft = promotionDrafts[applicant.applicantId] ?? {
              decision: 'AuthorizedForSyntheticPromotion' as const, reason: '',
              noneAssuranceAcknowledged: false, syntheticDataConfirmed: false, retryKey: null,
            }
            const canSubmit = draft.reason.trim().length >= 10
              && draft.noneAssuranceAcknowledged && draft.syntheticDataConfirmed
            return <li key={applicant.applicantId}>
              <div>
                <strong>{applicant.legalFirstName} {applicant.legalLastName}</strong>
                <span>Born {applicant.dateOfBirth} · {applicant.residenceStateCode} {applicant.postalCode}</span>
                <span>{applicant.maskedEmail} · {applicant.maskedPhone}</span>
                <span>{applicant.payerDisplayName} · {applicant.productDisplayName}</span>
                <small>Eligibility active · practice in network and accepting new patients</small>
                <small>Process fixture passed · assurance {applicant.assuranceLevelAchieved} · identity proved: no · version {applicant.version}</small>
              </div>
              <div className="telehealth-review-form">
                <fieldset>
                  <legend>Promotion decision for {applicant.legalFirstName} {applicant.legalLastName}</legend>
                  <label className="telehealth-check"><input type="radio" name={`promotion-decision-${applicant.applicantId}`} value="AuthorizedForSyntheticPromotion" checked={draft.decision === 'AuthorizedForSyntheticPromotion'} onChange={() => updatePromotionDraft(applicant.applicantId, { decision: 'AuthorizedForSyntheticPromotion', retryKey: null })} /> Authorize a later synthetic promotion exercise</label>
                  <label className="telehealth-check"><input type="radio" name={`promotion-decision-${applicant.applicantId}`} value="DeniedForSyntheticPromotion" checked={draft.decision === 'DeniedForSyntheticPromotion'} onChange={() => updatePromotionDraft(applicant.applicantId, { decision: 'DeniedForSyntheticPromotion', retryKey: null })} /> Deny synthetic promotion</label>
                </fieldset>
                <label htmlFor={`promotion-reason-${applicant.applicantId}`}>Promotion decision reason</label>
                <textarea id={`promotion-reason-${applicant.applicantId}`} value={draft.reason} minLength={10} maxLength={1000} onChange={(event) => updatePromotionDraft(applicant.applicantId, { reason: event.target.value, retryKey: null })} />
                <label className="telehealth-check"><input type="checkbox" checked={draft.noneAssuranceAcknowledged} onChange={(event) => updatePromotionDraft(applicant.applicantId, { noneAssuranceAcknowledged: event.target.checked, retryKey: null })} /> I acknowledge assurance is None and this process did not prove identity.</label>
                <label className="telehealth-check"><input type="checkbox" checked={draft.syntheticDataConfirmed} onChange={(event) => updatePromotionDraft(applicant.applicantId, { syntheticDataConfirmed: event.target.checked, retryKey: null })} /> I confirm this uses synthetic data and creates no patient or downstream capability.</label>
                <button className="telehealth-button" type="button" disabled={promotionWorkingId !== null || !canSubmit} onClick={() => void recordPromotionAuthorization(applicant)}>{promotionWorkingId === applicant.applicantId ? 'Recording…' : 'Record promotion decision'}</button>
              </div>
            </li>
          })}
        </ul>
      </section>

      <section className="telehealth-card" aria-labelledby="synthetic-promotion-title" aria-busy={syntheticPromotionLoading}>
        <h2 id="synthetic-promotion-title">Atomic synthetic patient promotion</h2>
        <p>Execute only after explicit staff authorization. The server repeats the current duplicate check inside the patient-registration transaction: a possible match blocks creation without identifying or linking anyone.</p>
        {syntheticPromotionLimitations.length > 0 ? <ul>{syntheticPromotionLimitations.map((item) => <li key={item}>{item}</li>)}</ul> : null}
        {syntheticPromotionError ? <div><p className="telehealth-error" role="alert">{syntheticPromotionError}</p><button className="telehealth-button" type="button" onClick={() => void refreshSyntheticPromotion()}>Reload atomic promotion queue</button></div> : null}
        {syntheticPromotionLoading ? <p aria-live="polite">Refreshing atomic promotion queue…</p> : null}
        {!syntheticPromotionLoading && syntheticPromotionApplicants.length === 0 && !syntheticPromotionError ? <p>No applicants are authorized for atomic synthetic promotion.</p> : null}
        <ul className="telehealth-queue">
          {syntheticPromotionApplicants.map((applicant) => {
            const draft = syntheticPromotionDrafts[applicant.applicantId] ?? {
              reason: '', canonicalPatientCreationAcknowledged: false,
              noPortalNoCareAcknowledged: false, retryKey: null,
            }
            const canSubmit = draft.reason.trim().length >= 10
              && draft.canonicalPatientCreationAcknowledged
              && draft.noPortalNoCareAcknowledged
            return <li key={applicant.applicantId}>
              <div>
                <strong>{applicant.legalFirstName} {applicant.legalLastName}</strong>
                <span>Born {applicant.dateOfBirth} · {applicant.residenceStateCode} {applicant.postalCode}</span>
                <span>{applicant.maskedEmail} · {applicant.maskedPhone}</span>
                <small>Staff authorization recorded · assurance {applicant.assuranceLevelAchieved} · identity proved: no · version {applicant.version}</small>
              </div>
              <div className="telehealth-review-form">
                <label htmlFor={`synthetic-promotion-reason-${applicant.applicantId}`}>Atomic promotion reason</label>
                <textarea id={`synthetic-promotion-reason-${applicant.applicantId}`} value={draft.reason} minLength={10} maxLength={1000} onChange={(event) => updateSyntheticPromotionDraft(applicant.applicantId, { reason: event.target.value, retryKey: null })} />
                <label className="telehealth-check"><input type="checkbox" checked={draft.canonicalPatientCreationAcknowledged} onChange={(event) => updateSyntheticPromotionDraft(applicant.applicantId, { canonicalPatientCreationAcknowledged: event.target.checked, retryKey: null })} /> I understand that a no-match result creates one minimal canonical synthetic patient shell.</label>
                <label className="telehealth-check"><input type="checkbox" checked={draft.noPortalNoCareAcknowledged} onChange={(event) => updateSyntheticPromotionDraft(applicant.applicantId, { noPortalNoCareAcknowledged: event.target.checked, retryKey: null })} /> I understand this creates no portal, completed intake, consent, coverage, request, queue, or care capability.</label>
                <button className="telehealth-button" type="button" disabled={syntheticPromotionWorkingId !== null || !canSubmit} onClick={() => void executeSyntheticPromotion(applicant)}>{syntheticPromotionWorkingId === applicant.applicantId ? 'Promoting…' : 'Run duplicate check and promote'}</button>
              </div>
            </li>
          })}
        </ul>
      </section>

      <section className="telehealth-card" aria-busy={loading} aria-live="polite">
        <h2>Eligible requests awaiting authorization</h2>
        {error ? <div><p className="telehealth-error" role="alert">{error}</p><button className="telehealth-button" type="button" onClick={() => void refresh()}>Try again</button></div> : null}
        {loading ? <p>Refreshing queue…</p> : null}
        {!loading && items.length === 0 && !error ? <p>No requests are awaiting operational review.</p> : null}
        <ul className="telehealth-queue">
          {items.map((item) => <li key={item.requestId}><div><strong>{item.complaintCategory}</strong><span>{item.triageOutcome}</span><small>Request {item.requestId.slice(0, 8)} · version {item.version}</small></div><button className="telehealth-button" type="button" disabled={workingId !== null} onClick={() => void authorize(item)}>{workingId === item.requestId ? 'Authorizing…' : 'Authorize to clinician queue'}</button></li>)}
        </ul>
      </section>
    </main>
  )
}
