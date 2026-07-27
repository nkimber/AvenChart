import { useEffect, useState, type FormEvent } from 'react'
import { useOutletContext } from 'react-router-dom'
import { Check, Pencil, Plus, RotateCcw, Trash2, X } from 'lucide-react'
import {
  acceptAdministrationPortalProfileReview,
  createAdministrationFacility,
  createAdministrationUser,
  createCodingCatalog,
  deleteAdministrationFacility,
  deleteAdministrationUser,
  getAdministrationDirectory,
  getApiClients,
  getConfigurationCatalog,
  getCodingCatalogs,
  getCodingCatalogHistory,
  getClinicalAlertRuleHistory,
  getClinicalAlertRules,
  getFormLayout,
  getFormLayoutHistory,
  getFormLayouts,
  getFormOptionList,
  getFormOptionListHistory,
  getFormOptionLists,
  getModuleCatalog,
  getPhiAccessAudit,
  getPracticeSettings,
  getPracticeSettingHistory,
  grantAdministrationAccessMembership,
  grantAdministrationAccessPermission,
  revokeAdministrationAccessMembership,
  revokeAdministrationAccessPermission,
  revertAdministrationPortalProfileReview,
  updateAdministrationFacility,
  updateAdministrationUser,
  updateCodingCatalog,
  updatePracticeSetting,
  rollbackPracticeSetting,
  rollbackCodingCatalog,
  rollbackFormOptionList,
  rollbackFormLayout,
  rollbackClinicalAlertRule,
  saveFormLayout,
  saveFormLayoutField,
  saveFormLayoutGroup,
  saveFormOptionList,
  saveFormOptionValue,
  saveClinicalAlertRule,
  saveApiClient,
  type AdministrationDirectoryResponse,
  type ApiClientRegistryItem,
  type AdministrationFacilityItem,
  type AdministrationFacilityMutationInput,
  type AdministrationAccessGroupPermissionItem,
  type AdministrationAccessUserMembershipItem,
  type PhiAccessAuditResponse,
  type PracticeSettingHistory,
  type CodingCatalogHistory,
  type ClinicalAlertRuleHistory,
  type FormOptionListHistory,
  type FormLayoutHistory,
  type AdministrationPortalProfileReviewRequest,
  type AdministrationUserItem,
  type AdministrationUserMutationInput,
  type ConfigurationCatalogItem,
  type CodingCatalogItem,
  type CodingCatalogMutationInput,
  type ClinicalAlertRuleItem,
  type FormLayoutDetail,
  type FormLayoutItem,
  type FormOptionListDetail,
  type FormOptionListItem,
  type ModuleCatalogItem,
  type PracticeSettingItem,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

type FacilityForm = {
  code: string
  name: string
  phone: string
  street: string
  city: string
  state: string
  postalCode: string
  color: string
  active: boolean
}

function emptyFacilityForm(): FacilityForm {
  return { code: '', name: '', phone: '', street: '', city: '', state: '', postalCode: '', color: '#0f6e56', active: true }
}

function facilityToForm(facility: AdministrationFacilityItem): FacilityForm {
  return {
    code: facility.code, name: facility.name, phone: facility.phone ?? '', street: facility.street ?? '', city: facility.city ?? '',
    state: facility.state ?? '', postalCode: facility.postalCode ?? '', color: facility.color ?? '#0f6e56', active: facility.active,
  }
}

function normalizeFacilityForm(form: FacilityForm): AdministrationFacilityMutationInput {
  const optional = (value: string) => value.trim() || null
  return {
    code: form.code.trim(), name: form.name.trim(), phone: optional(form.phone), street: optional(form.street), city: optional(form.city),
    state: optional(form.state), postalCode: optional(form.postalCode), color: optional(form.color), active: form.active,
  }
}

type UserForm = {
  username: string
  firstName: string
  lastName: string
  role: string
  calendar: boolean
  facilityId: string
  email: string
  npi: string
  active: boolean
}

function emptyUserForm(): UserForm {
  return { username: '', firstName: '', lastName: '', role: 'provider', calendar: true, facilityId: '', email: '', npi: '', active: true }
}

function userToForm(user: AdministrationUserItem): UserForm {
  return {
    username: user.username, firstName: user.firstName, lastName: user.lastName, role: user.role,
    calendar: user.calendar, facilityId: user.facilityId?.toString() ?? '', email: user.email ?? '', npi: user.npi ?? '', active: user.active,
  }
}

function normalizeUserForm(form: UserForm): AdministrationUserMutationInput {
  const optional = (value: string) => value.trim() || null
  const facilityId = Number(form.facilityId)
  return {
    username: form.username.trim(), firstName: form.firstName.trim(), lastName: form.lastName.trim(), role: form.role.trim(),
    calendar: form.calendar, facilityId: Number.isInteger(facilityId) && facilityId > 0 ? facilityId : null,
    email: optional(form.email), npi: optional(form.npi), active: form.active,
  }
}

type AccessMembershipForm = { userValue: string; groupValue: string }
type AccessPermissionForm = { groupValue: string; permissionKey: string; returnValue: 'addonly' | 'view' | 'write' | 'wsome' }
type CodingCatalogForm = CodingCatalogMutationInput & { key: string }
type ApiClientForm = Omit<ApiClientRegistryItem, 'key'> & { key: string }

function emptyMembershipForm(): AccessMembershipForm { return { userValue: '', groupValue: '' } }
function emptyPermissionForm(): AccessPermissionForm { return { groupValue: '', permissionKey: '', returnValue: 'view' } }
function emptyCodingCatalogForm(): CodingCatalogForm { return { key: '', displayName: '', sequence: 40, active: true, claimEnabled: false, feeEnabled: false, modifierLength: 0 } }
function emptyApiClientForm(): ApiClientForm { return { key: '', displayName: '', redirectUri: '', scopes: '', active: true } }
function apiClientToForm(client: ApiClientRegistryItem): ApiClientForm { return { ...client } }

export default function AdminDirectory() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [state, setState] = useState<AsyncState<AdministrationDirectoryResponse>>({ status: 'loading' })
  const [tab, setTab] = useState<'users' | 'facilities' | 'access' | 'reviews' | 'audit' | 'configuration' | 'layouts' | 'rules' | 'modules' | 'apiClients'>('users')
  const [configuration, setConfiguration] = useState<ConfigurationCatalogItem[]>([])
  const [practiceSettings, setPracticeSettings] = useState<PracticeSettingItem[]>([])
  const [practiceSettingHistory, setPracticeSettingHistory] = useState<PracticeSettingHistory | null>(null)
  const [codingCatalogs, setCodingCatalogs] = useState<CodingCatalogItem[]>([])
  const [codingCatalogHistory, setCodingCatalogHistory] = useState<CodingCatalogHistory | null>(null)
  const [codingCatalogForm, setCodingCatalogForm] = useState<CodingCatalogForm>(() => emptyCodingCatalogForm())
  const [savingCodingCatalog, setSavingCodingCatalog] = useState(false)
  const [layouts, setLayouts] = useState<FormLayoutItem[]>([])
  const [layoutDetail, setLayoutDetail] = useState<FormLayoutDetail | null>(null)
  const [layoutHistory, setLayoutHistory] = useState<FormLayoutHistory | null>(null)
  const [layoutKey, setLayoutKey] = useState('')
  const [savingLayout, setSavingLayout] = useState(false)
  const [groupDraft, setGroupDraft] = useState({ key: '', title: '', sequence: 10 })
  const [fieldDraft, setFieldDraft] = useState({ key: '', groupKey: '', label: '', fieldType: 'text', sequence: 10, listId: '' })
  const [formOptionLists, setFormOptionLists] = useState<FormOptionListItem[]>([])
  const [formOptionListDetail, setFormOptionListDetail] = useState<FormOptionListDetail | null>(null)
  const [formOptionListHistory, setFormOptionListHistory] = useState<FormOptionListHistory | null>(null)
  const [formOptionListKey, setFormOptionListKey] = useState('')
  const [savingFormOptionList, setSavingFormOptionList] = useState(false)
  const [formOptionDraft, setFormOptionDraft] = useState({ key: '', title: '', sequence: 10, value: '', isDefault: false, active: true })
  const [alertRules, setAlertRules] = useState<ClinicalAlertRuleItem[]>([])
  const [alertRuleHistory, setAlertRuleHistory] = useState<ClinicalAlertRuleHistory | null>(null)
  const [modules, setModules] = useState<ModuleCatalogItem[]>([])
  const [apiClients, setApiClients] = useState<ApiClientRegistryItem[]>([])
  const [apiClientForm, setApiClientForm] = useState<ApiClientForm>(() => emptyApiClientForm())
  const [editingApiClientKey, setEditingApiClientKey] = useState<string | null>(null)
  const [savingApiClient, setSavingApiClient] = useState(false)
  const [auditState, setAuditState] = useState<AsyncState<PhiAccessAuditResponse>>({ status: 'loading' })
  const [facilityForm, setFacilityForm] = useState<FacilityForm>(() => emptyFacilityForm())
  const [editingFacilityId, setEditingFacilityId] = useState<number | 'new' | null>(null)
  const [savingFacility, setSavingFacility] = useState(false)
  const [deletingFacilityId, setDeletingFacilityId] = useState<number | null>(null)
  const [userForm, setUserForm] = useState<UserForm>(() => emptyUserForm())
  const [editingUserId, setEditingUserId] = useState<number | 'new' | null>(null)
  const [savingUser, setSavingUser] = useState(false)
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null)
  const [membershipForm, setMembershipForm] = useState<AccessMembershipForm>(() => emptyMembershipForm())
  const [permissionForm, setPermissionForm] = useState<AccessPermissionForm>(() => emptyPermissionForm())
  const [savingMembership, setSavingMembership] = useState(false)
  const [savingPermission, setSavingPermission] = useState(false)
  const [removingMembership, setRemovingMembership] = useState<string | null>(null)
  const [removingPermission, setRemovingPermission] = useState<string | null>(null)
  const [reviewActionId, setReviewActionId] = useState<string | null>(null)

  useEffect(() => {
    getAdministrationDirectory(session.sessionId)
      .then((data) => setState({ status: 'ready', data }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (tab !== 'audit') return
    setAuditState({ status: 'loading' })
    getPhiAccessAudit(session.sessionId)
      .then((data) => setAuditState({ status: 'ready', data }))
      .catch((err) => setAuditState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load PHI access audit.' }))
  }, [session.sessionId, tab])
  async function openPracticeSettingHistory(key: string) { try { setPracticeSettingHistory(await getPracticeSettingHistory(session.sessionId, key)) } catch { showToast('Could not load setting history.', 'error') } }
  async function rollbackPracticeSettingRevision(revisionId: number) { if (!practiceSettingHistory || !window.confirm(`Restore ${practiceSettingHistory.setting.label} to the selected historical value?`)) return; try { const result = await rollbackPracticeSetting(session.sessionId, practiceSettingHistory.setting.key, revisionId); setPracticeSettingHistory(result); setPracticeSettings((await getPracticeSettings(session.sessionId)).settings); showToast('Practice setting restored.', 'success') } catch { showToast('Could not restore the practice setting.', 'error') } }
  async function openCodingCatalogHistory(key: string) { try { setCodingCatalogHistory(await getCodingCatalogHistory(session.sessionId, key)) } catch { showToast('Could not load catalog history.', 'error') } }
  async function rollbackCodingCatalogRevision(revisionId: number) { if (!codingCatalogHistory || !window.confirm(`Restore ${codingCatalogHistory.catalog.key} to the selected historical definition?`)) return; try { const result = await rollbackCodingCatalog(session.sessionId, codingCatalogHistory.catalog.key, revisionId); setCodingCatalogHistory(result); setCodingCatalogs((await getCodingCatalogs(session.sessionId)).catalogs); showToast('Coding catalog restored.', 'success') } catch { showToast('Could not restore the coding catalog.', 'error') } }
  async function openClinicalAlertRuleHistory(key: string) { try { setAlertRuleHistory(await getClinicalAlertRuleHistory(session.sessionId, key)) } catch { showToast('Could not load alert-rule history.', 'error') } }
  async function rollbackClinicalAlertRuleRevision(revisionId: number) { if (!alertRuleHistory || !window.confirm(`Restore ${alertRuleHistory.rule.title} to the selected historical definition?`)) return; try { const result = await rollbackClinicalAlertRule(session.sessionId, alertRuleHistory.rule.key, revisionId); setAlertRuleHistory(result); setAlertRules((await getClinicalAlertRules(session.sessionId)).rules); showToast('Alert rule restored.', 'success') } catch { showToast('Could not restore the alert rule.', 'error') } }
  useEffect(() => { if (tab === 'layouts') { getFormLayouts(session.sessionId).then((result) => setLayouts(result.layouts)).catch(() => showToast('Could not load form layouts.', 'error')); getFormOptionLists(session.sessionId).then((result) => setFormOptionLists(result.lists)).catch(() => showToast('Could not load form option lists.', 'error')) } }, [session.sessionId, tab])
  useEffect(() => { if (tab === 'rules') getClinicalAlertRules(session.sessionId).then((result) => setAlertRules(result.rules)).catch(() => showToast('Could not load alert rules.', 'error')) }, [session.sessionId, tab])
  useEffect(() => { if (tab === 'modules') getModuleCatalog(session.sessionId).then((result) => setModules(result.modules)).catch(() => showToast('Could not load modules.', 'error')) }, [session.sessionId, tab])
  useEffect(() => { if (tab === 'apiClients') getApiClients(session.sessionId).then((result) => setApiClients(result.clients)).catch(() => showToast('Could not load API clients.', 'error')) }, [session.sessionId, tab])

  async function openLayout(key: string) { try { setLayoutDetail(await getFormLayout(session.sessionId, key)); setLayoutKey(key) } catch { showToast('Could not load layout detail.', 'error') } }
  async function openLayoutHistory(key: string) { try { setLayoutHistory(await getFormLayoutHistory(session.sessionId, key)) } catch { showToast('Could not load layout history.', 'error') } }
  async function rollbackLayoutRevision(revisionId: number) { if (!layoutHistory || !window.confirm(`Restore ${layoutHistory.detail.layout.title}, its groups, and its fields to this revision?`)) return; try { const result = await rollbackFormLayout(session.sessionId, layoutHistory.detail.layout.key, revisionId); setLayoutHistory(result); setLayoutDetail(result.detail); setLayouts((await getFormLayouts(session.sessionId)).layouts); showToast('Layout restored.', 'success') } catch { showToast('Could not restore the layout.', 'error') } }
  async function saveLayout(event: FormEvent) { event.preventDefault(); setSavingLayout(true); try { const detail = await saveFormLayout(session.sessionId, layoutKey, { title: layoutDetail?.layout.title ?? layoutKey, mapping: layoutDetail?.layout.mapping ?? 'Core', sequence: layoutDetail?.layout.sequence ?? ((layouts.at(-1)?.sequence ?? 0) + 10), active: layoutDetail?.layout.active ?? true }); setLayoutDetail(detail); setLayouts(await getFormLayouts(session.sessionId).then((result) => result.layouts)); showToast('Layout saved.', 'success') } catch { showToast('Could not save layout.', 'error') } finally { setSavingLayout(false) } }
  async function saveGroup(event: FormEvent) { event.preventDefault(); if (!layoutDetail) return; try { setLayoutDetail(await saveFormLayoutGroup(session.sessionId, layoutDetail.layout.key, groupDraft.key, { title: groupDraft.title, sequence: groupDraft.sequence, active: true })); setGroupDraft({ key: '', title: '', sequence: groupDraft.sequence + 10 }); showToast('Group saved.', 'success') } catch { showToast('Could not save group.', 'error') } }
  async function saveField(event: FormEvent) { event.preventDefault(); if (!layoutDetail) return; try { setLayoutDetail(await saveFormLayoutField(session.sessionId, layoutDetail.layout.key, fieldDraft.key, { groupKey: fieldDraft.groupKey, label: fieldDraft.label, fieldType: fieldDraft.fieldType, sequence: fieldDraft.sequence, required: false, active: true, maxLength: 255, listId: fieldDraft.listId, defaultValue: '' })); setFieldDraft({ key: '', groupKey: '', label: '', fieldType: 'text', sequence: fieldDraft.sequence + 10, listId: '' }); showToast('Field saved.', 'success') } catch { showToast('Could not save field.', 'error') } }
  async function openFormOptionList(key: string) { try { setFormOptionListDetail(await getFormOptionList(session.sessionId, key)); setFormOptionListKey(key) } catch { showToast('Could not load form option list.', 'error') } }
  async function openFormOptionListHistory(key: string) { try { setFormOptionListHistory(await getFormOptionListHistory(session.sessionId, key)) } catch { showToast('Could not load option-list history.', 'error') } }
  async function rollbackFormOptionListRevision(revisionId: number) { if (!formOptionListHistory || !window.confirm(`Restore ${formOptionListHistory.detail.list.title} and all of its historical options?`)) return; try { const result = await rollbackFormOptionList(session.sessionId, formOptionListHistory.detail.list.key, revisionId); setFormOptionListHistory(result); setFormOptionLists((await getFormOptionLists(session.sessionId)).lists); if (formOptionListDetail?.list.key === result.detail.list.key) setFormOptionListDetail(result.detail); showToast('Option list restored.', 'success') } catch { showToast('Could not restore the option list.', 'error') } }
  async function saveFormOptionListDefinition(event: FormEvent) { event.preventDefault(); if (!formOptionListKey.trim()) return; setSavingFormOptionList(true); try { const detail = await saveFormOptionList(session.sessionId, formOptionListKey, { title: formOptionListDetail?.list.title ?? formOptionListKey, active: formOptionListDetail?.list.active ?? true }); setFormOptionListDetail(detail); setFormOptionLists((await getFormOptionLists(session.sessionId)).lists); showToast('Form option list saved.', 'success') } catch { showToast('Could not save form option list.', 'error') } finally { setSavingFormOptionList(false) } }
  async function saveFormOption(event: FormEvent) { event.preventDefault(); if (!formOptionListDetail || !formOptionDraft.key.trim()) return; try { const detail = await saveFormOptionValue(session.sessionId, formOptionListDetail.list.key, formOptionDraft.key, { title: formOptionDraft.title, sequence: formOptionDraft.sequence, isDefault: formOptionDraft.isDefault, active: formOptionDraft.active, value: formOptionDraft.value }); setFormOptionListDetail(detail); setFormOptionLists((await getFormOptionLists(session.sessionId)).lists); setFormOptionDraft({ key: '', title: '', sequence: formOptionDraft.sequence + 10, value: '', isDefault: false, active: true }); showToast('Form option saved.', 'success') } catch { showToast('Could not save form option.', 'error') } }
  useEffect(() => {
    if (tab !== 'configuration') return
    getConfigurationCatalog(session.sessionId).then((result) => setConfiguration(result.settings)).catch(() => showToast('Could not load configuration catalog.', 'error'))
    getPracticeSettings(session.sessionId).then((result) => setPracticeSettings(result.settings)).catch(() => showToast('Could not load practice settings.', 'error'))
    getCodingCatalogs(session.sessionId).then((result) => setCodingCatalogs(result.catalogs)).catch(() => showToast('Could not load coding catalogs.', 'error'))
  }, [session.sessionId, tab])

  async function saveCodingCatalog(catalog: CodingCatalogItem, patch: Partial<CodingCatalogMutationInput>) {
    setSavingCodingCatalog(true)
    try {
      const result = await updateCodingCatalog(session.sessionId, catalog.key, { ...catalog, ...patch })
      setCodingCatalogs(result.catalogs)
      showToast(`${catalog.key} catalog saved.`, 'success')
    } catch {
      showToast(`Could not save ${catalog.key}.`, 'error')
    } finally {
      setSavingCodingCatalog(false)
    }
  }

  async function createCatalog(event: FormEvent) {
    event.preventDefault()
    setSavingCodingCatalog(true)
    try {
      const { key, ...input } = codingCatalogForm
      const result = await createCodingCatalog(session.sessionId, key, input)
      setCodingCatalogs(result.catalogs)
      setCodingCatalogForm(emptyCodingCatalogForm())
      showToast(`${key.trim().toUpperCase()} catalog created.`, 'success')
    } catch {
      showToast('Could not create coding catalog.', 'error')
    } finally {
      setSavingCodingCatalog(false)
    }
  }

  function beginApiClientCreate() { setApiClientForm(emptyApiClientForm()); setEditingApiClientKey('new') }
  function beginApiClientEdit(client: ApiClientRegistryItem) { setApiClientForm(apiClientToForm(client)); setEditingApiClientKey(client.key) }
  function cancelApiClientEdit() { setApiClientForm(emptyApiClientForm()); setEditingApiClientKey(null) }
  async function submitApiClient(event: FormEvent) {
    event.preventDefault()
    if (!apiClientForm.key.trim() || savingApiClient) return
    setSavingApiClient(true)
    try {
      const { key, ...input } = apiClientForm
      const result = await saveApiClient(session.sessionId, key, input)
      setApiClients(result.clients)
      cancelApiClientEdit()
      showToast(`${key.trim().toUpperCase()} API client saved.`, 'success')
    } catch {
      showToast('Could not save API client. Use an HTTPS redirect URI and at least one scope.', 'error')
    } finally {
      setSavingApiClient(false)
    }
  }

  function beginFacilityCreate() {
    setFacilityForm(emptyFacilityForm())
    setEditingFacilityId('new')
  }

  function beginFacilityEdit(facility: AdministrationFacilityItem) {
    setFacilityForm(facilityToForm(facility))
    setEditingFacilityId(facility.id)
  }

  function cancelFacilityEdit() {
    setEditingFacilityId(null)
    setFacilityForm(emptyFacilityForm())
  }

  async function saveFacility(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (editingFacilityId === null || savingFacility) return
    setSavingFacility(true)
    try {
      const body = normalizeFacilityForm(facilityForm)
      const mutation = editingFacilityId === 'new'
        ? await createAdministrationFacility(session.sessionId, body)
        : await updateAdministrationFacility(session.sessionId, editingFacilityId, body)
      setState({ status: 'ready', data: mutation.detail })
      showToast(editingFacilityId === 'new' ? 'Facility created.' : 'Facility updated.', 'success')
      cancelFacilityEdit()
    } catch {
      showToast('Could not save this facility. Check the required fields and try again.', 'error')
    } finally {
      setSavingFacility(false)
    }
  }

  async function removeFacility(facility: AdministrationFacilityItem) {
    if (deletingFacilityId !== null || !window.confirm(`Delete ${facility.name}? This cannot be undone.`)) return
    setDeletingFacilityId(facility.id)
    try {
      await deleteAdministrationFacility(session.sessionId, facility.id)
      const refreshed = await getAdministrationDirectory(session.sessionId)
      setState({ status: 'ready', data: refreshed })
      if (editingFacilityId === facility.id) cancelFacilityEdit()
      showToast('Facility deleted.', 'success')
    } catch {
      showToast('Could not delete this facility. It may still be in use.', 'error')
    } finally {
      setDeletingFacilityId(null)
    }
  }

  function beginUserCreate() {
    setUserForm(emptyUserForm())
    setEditingUserId('new')
  }

  function beginUserEdit(user: AdministrationUserItem) {
    setUserForm(userToForm(user))
    setEditingUserId(user.id)
  }

  function cancelUserEdit() {
    setEditingUserId(null)
    setUserForm(emptyUserForm())
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (editingUserId === null || savingUser) return
    setSavingUser(true)
    try {
      const body = normalizeUserForm(userForm)
      const mutation = editingUserId === 'new'
        ? await createAdministrationUser(session.sessionId, body)
        : await updateAdministrationUser(session.sessionId, editingUserId, body)
      setState({ status: 'ready', data: mutation.detail })
      showToast(editingUserId === 'new' ? 'Staff user created.' : 'Staff user updated.', 'success')
      cancelUserEdit()
    } catch {
      showToast('Could not save this staff user. Check the required fields and try again.', 'error')
    } finally {
      setSavingUser(false)
    }
  }

  async function removeUser(user: AdministrationUserItem) {
    if (user.id === session.staffId) {
      showToast('You cannot delete the signed-in staff user.', 'error')
      return
    }
    if (deletingUserId !== null || !window.confirm(`Delete ${user.displayName}? This also removes their access-group memberships.`)) return
    setDeletingUserId(user.id)
    try {
      await deleteAdministrationUser(session.sessionId, user.id)
      const refreshed = await getAdministrationDirectory(session.sessionId)
      setState({ status: 'ready', data: refreshed })
      if (editingUserId === user.id) cancelUserEdit()
      showToast('Staff user deleted.', 'success')
    } catch {
      showToast('Could not delete this staff user. It may still be in use.', 'error')
    } finally {
      setDeletingUserId(null)
    }
  }

  async function saveMembership(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (savingMembership || !membershipForm.userValue || !membershipForm.groupValue) return
    setSavingMembership(true)
    try {
      const mutation = await grantAdministrationAccessMembership(session.sessionId, membershipForm)
      setState({ status: 'ready', data: mutation.detail })
      setMembershipForm(emptyMembershipForm())
      showToast('Access-group membership saved.', 'success')
    } catch {
      showToast('Could not save this access-group membership.', 'error')
    } finally {
      setSavingMembership(false)
    }
  }

  async function removeMembership(membership: AdministrationAccessUserMembershipItem) {
    const key = `${membership.userValue}:${membership.groupValue}`
    if (membership.staffId === session.staffId) {
      showToast('You cannot remove the signed-in staff user from an access group.', 'error')
      return
    }
    if (removingMembership !== null || !window.confirm(`Remove ${membership.userName} from ${membership.groupName}?`)) return
    setRemovingMembership(key)
    try {
      const mutation = await revokeAdministrationAccessMembership(session.sessionId, membership.userValue, membership.groupValue)
      setState({ status: 'ready', data: mutation.detail })
      showToast('Access-group membership removed.', 'success')
    } catch {
      showToast('Could not remove this access-group membership.', 'error')
    } finally {
      setRemovingMembership(null)
    }
  }

  async function savePermission(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (savingPermission || !permissionForm.groupValue || !permissionForm.permissionKey) return
    const [sectionValue, permissionValue] = permissionForm.permissionKey.split(':', 2)
    if (!sectionValue || !permissionValue) return
    setSavingPermission(true)
    try {
      const mutation = await grantAdministrationAccessPermission(session.sessionId, {
        groupValue: permissionForm.groupValue,
        sectionValue,
        permissionValue,
        returnValue: permissionForm.returnValue,
      })
      setState({ status: 'ready', data: mutation.detail })
      setPermissionForm(emptyPermissionForm())
      showToast('Access permission saved.', 'success')
    } catch {
      showToast('Could not save this access permission.', 'error')
    } finally {
      setSavingPermission(false)
    }
  }

  async function removePermission(permission: AdministrationAccessGroupPermissionItem) {
    const key = `${permission.groupValue}:${permission.sectionValue}:${permission.permissionValue}`
    if (removingPermission !== null || !window.confirm(`Remove ${permission.permissionName} from ${permission.groupValue}?`)) return
    setRemovingPermission(key)
    try {
      const mutation = await revokeAdministrationAccessPermission(session.sessionId, permission.groupValue, permission.sectionValue, permission.permissionValue)
      setState({ status: 'ready', data: mutation.detail })
      showToast('Access permission removed.', 'success')
    } catch {
      showToast('Could not remove this access permission.', 'error')
    } finally {
      setRemovingPermission(null)
    }
  }

  async function resolveProfileReview(request: AdministrationPortalProfileReviewRequest, action: 'accept' | 'revert') {
    if (reviewActionId !== null) return
    const verb = action === 'accept' ? 'commit these changes to the chart' : 'revert these requested changes'
    if (!window.confirm(`Confirm: ${verb} for ${request.patientName}?`)) return
    setReviewActionId(request.id)
    try {
      const mutation = action === 'accept'
        ? await acceptAdministrationPortalProfileReview(session.sessionId, request.id)
        : await revertAdministrationPortalProfileReview(session.sessionId, request.id)
      setState({ status: 'ready', data: mutation.detail })
      showToast(action === 'accept' ? 'Portal profile changes committed.' : 'Portal profile changes reverted.', 'success')
    } catch {
      showToast('Could not resolve this profile review. It may already have changed.', 'error')
    } finally {
      setReviewActionId(null)
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Administration</h1>
        {state.status === 'ready' && (
          <p className="clinician-page-subtitle">
            {state.data.counts.users} users · {state.data.counts.facilities} facilities · {state.data.counts.accessGroups} access groups
          </p>
        )}
      </div>

      {state.status === 'loading' && (
        <div className="cl-card">
          <div className="skeleton-list">{[0,1,2,3].map((i)=><div key={i} className="skeleton-row" style={{height:56}} />)}</div>
        </div>
      )}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && (() => {
        const { data } = state
        return (
          <>
            <div className="cl-tab-bar">
              {([
                { id: 'users', label: `Users (${data.counts.users})` },
                { id: 'facilities', label: `Facilities (${data.counts.facilities})` },
                { id: 'access', label: `Access control (${data.counts.accessGroups})` },
                { id: 'reviews', label: `Profile reviews (${data.counts.waitingProfileReviews})` },
                { id: 'audit', label: 'PHI access audit' },
                { id: 'configuration', label: 'Configuration' },
                { id: 'layouts', label: 'Forms & layouts' },
                { id: 'rules', label: 'Rules & alerts' },
                { id: 'modules', label: 'Modules' },
                { id: 'apiClients', label: 'API clients' },
              ] as const).map((t) => (
                <button
                  key={t.id}
                  className={`cl-tab-btn${tab === t.id ? ' cl-tab-btn-active' : ''}`}
                  type="button"
                  onClick={() => setTab(t.id)}
                >
                  {t.label}
                </button>
              ))}
            </div>

            {tab === 'users' && (
              <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
                <div className="cl-admin-facility-header">
                  <div>
                    <h2 className="cl-card-title">Staff directory</h2>
                    <p className="clinician-page-subtitle">Local staff identity, facility assignment, calendar availability, and directory attributes.</p>
                  </div>
                  {editingUserId === null && (
                    <button className="cl-btn-primary" type="button" onClick={beginUserCreate}>
                      <Plus size={15} /> Add staff user
                    </button>
                  )}
                </div>

                {editingUserId !== null && (
                  <form className="cl-admin-facility-form" onSubmit={saveUser}>
                    <div className="cl-admin-form-heading">
                      <div>
                        <p className="cl-form-section-label">{editingUserId === 'new' ? 'New staff user' : 'Edit staff user'}</p>
                        <p className="cl-admin-form-copy">Password and production identity management remain outside this local directory workflow.</p>
                      </div>
                      <button className="cl-icon-button" type="button" onClick={cancelUserEdit} aria-label="Cancel staff user edit" title="Cancel"><X size={16} /></button>
                    </div>
                    <div className="cl-admin-form-grid">
                      <label className="cl-admin-field"><span>First name <em>*</em></span><input className="ne-input" value={userForm.firstName} onChange={(event) => setUserForm((form) => ({ ...form, firstName: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Last name <em>*</em></span><input className="ne-input" value={userForm.lastName} onChange={(event) => setUserForm((form) => ({ ...form, lastName: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Username <em>*</em></span><input className="ne-input" autoCapitalize="none" value={userForm.username} onChange={(event) => setUserForm((form) => ({ ...form, username: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Role <em>*</em></span><input className="ne-input" value={userForm.role} onChange={(event) => setUserForm((form) => ({ ...form, role: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Facility</span><select className="ne-input" value={userForm.facilityId} onChange={(event) => setUserForm((form) => ({ ...form, facilityId: event.target.value }))}><option value="">No facility assigned</option>{data.facilities.map((facility) => <option key={facility.id} value={facility.id}>{facility.name}{facility.active ? '' : ' (inactive)'}</option>)}</select></label>
                      <label className="cl-admin-field"><span>Email</span><input className="ne-input" type="email" value={userForm.email} onChange={(event) => setUserForm((form) => ({ ...form, email: event.target.value }))} /></label>
                      <label className="cl-admin-field"><span>NPI</span><input className="ne-input" value={userForm.npi} onChange={(event) => setUserForm((form) => ({ ...form, npi: event.target.value }))} /></label>
                      <label className="cl-admin-active-toggle"><input type="checkbox" checked={userForm.calendar} onChange={(event) => setUserForm((form) => ({ ...form, calendar: event.target.checked }))} /><span>Appears on calendar</span></label>
                      <label className="cl-admin-active-toggle"><input type="checkbox" checked={userForm.active} onChange={(event) => setUserForm((form) => ({ ...form, active: event.target.checked }))} /><span>Staff user is active</span></label>
                    </div>
                    <div className="cl-inline-form-actions">
                      <button className="cl-btn-primary" type="submit" disabled={savingUser}>{savingUser ? 'Saving...' : editingUserId === 'new' ? 'Create staff user' : 'Save changes'}</button>
                      <button className="cl-btn-secondary" type="button" onClick={cancelUserEdit} disabled={savingUser}>Cancel</button>
                    </div>
                  </form>
                )}
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Username</th>
                      <th>Role</th>
                      <th>Facility</th>
                      <th>NPI</th>
                      <th>Active</th>
                      <th><span className="sr-only">Actions</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.users.map((u) => (
                      <tr key={u.id}>
                        <td>
                          {u.displayName}
                          {u.email && <p className="cl-table-sub">{u.email}</p>}
                        </td>
                        <td className="cl-td-muted">{u.username}</td>
                        <td>{u.role}</td>
                        <td className="cl-td-muted">{u.facilityName ?? '—'}</td>
                        <td className="cl-td-muted">{u.npi ?? '—'}</td>
                        <td>
                          <span className={`cl-badge ${u.active ? 'cl-badge-green' : 'cl-badge-muted'}`}>
                            {u.active ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="cl-admin-row-actions">
                          <button className="cl-icon-button" type="button" onClick={() => beginUserEdit(u)} aria-label={`Edit ${u.displayName}`} title="Edit staff user"><Pencil size={15} /></button>
                          <button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removeUser(u)} disabled={deletingUserId === u.id || u.id === session.staffId} aria-label={`Delete ${u.displayName}`} title={u.id === session.staffId ? 'You cannot delete the signed-in staff user' : 'Delete staff user'}><Trash2 size={15} /></button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            )}

            {tab === 'facilities' && (
              <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
                <div className="cl-admin-facility-header">
                  <div>
                    <h2 className="cl-card-title">Facility directory</h2>
                    <p className="clinician-page-subtitle">Locations used for scheduling, care teams, and staff assignments.</p>
                  </div>
                  {editingFacilityId === null && (
                    <button className="cl-btn-primary" type="button" onClick={beginFacilityCreate}>
                      <Plus size={15} /> Add facility
                    </button>
                  )}
                </div>

                {editingFacilityId !== null && (
                  <form className="cl-admin-facility-form" onSubmit={saveFacility}>
                    <div className="cl-admin-form-heading">
                      <div>
                        <p className="cl-form-section-label">{editingFacilityId === 'new' ? 'New facility' : 'Edit facility'}</p>
                        <p className="cl-admin-form-copy">Required fields are marked. Changes apply to the shared facility directory.</p>
                      </div>
                      <button className="cl-icon-button" type="button" onClick={cancelFacilityEdit} aria-label="Cancel facility edit" title="Cancel">
                        <X size={16} />
                      </button>
                    </div>
                    <div className="cl-admin-form-grid">
                      <label className="cl-admin-field"><span>Facility name <em>*</em></span><input className="ne-input" value={facilityForm.name} onChange={(event) => setFacilityForm((form) => ({ ...form, name: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Code <em>*</em></span><input className="ne-input" value={facilityForm.code} onChange={(event) => setFacilityForm((form) => ({ ...form, code: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Phone</span><input className="ne-input" type="tel" value={facilityForm.phone} onChange={(event) => setFacilityForm((form) => ({ ...form, phone: event.target.value }))} /></label>
                      <label className="cl-admin-field"><span>Street</span><input className="ne-input" value={facilityForm.street} onChange={(event) => setFacilityForm((form) => ({ ...form, street: event.target.value }))} /></label>
                      <label className="cl-admin-field"><span>City</span><input className="ne-input" value={facilityForm.city} onChange={(event) => setFacilityForm((form) => ({ ...form, city: event.target.value }))} /></label>
                      <label className="cl-admin-field cl-admin-field-state"><span>State</span><input className="ne-input" value={facilityForm.state} onChange={(event) => setFacilityForm((form) => ({ ...form, state: event.target.value }))} /></label>
                      <label className="cl-admin-field cl-admin-field-postal"><span>Postal code</span><input className="ne-input" value={facilityForm.postalCode} onChange={(event) => setFacilityForm((form) => ({ ...form, postalCode: event.target.value }))} /></label>
                      <label className="cl-admin-field cl-admin-field-color"><span>Directory color</span><input className="cl-color-input" type="color" value={facilityForm.color} onChange={(event) => setFacilityForm((form) => ({ ...form, color: event.target.value }))} /></label>
                      <label className="cl-admin-active-toggle"><input type="checkbox" checked={facilityForm.active} onChange={(event) => setFacilityForm((form) => ({ ...form, active: event.target.checked }))} /><span>Facility is active</span></label>
                    </div>
                    <div className="cl-inline-form-actions">
                      <button className="cl-btn-primary" type="submit" disabled={savingFacility}>{savingFacility ? 'Saving...' : editingFacilityId === 'new' ? 'Create facility' : 'Save changes'}</button>
                      <button className="cl-btn-secondary" type="button" onClick={cancelFacilityEdit} disabled={savingFacility}>Cancel</button>
                    </div>
                  </form>
                )}
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Code</th>
                      <th>Address</th>
                      <th>Phone</th>
                      <th>Active</th>
                      <th><span className="sr-only">Actions</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.facilities.map((f) => (
                      <tr key={f.id}>
                        <td>
                          <span className="cl-facility-color" style={{ background: f.color ?? '#ccc' }} />
                          {f.name}
                        </td>
                        <td className="cl-td-muted">{f.code}</td>
                        <td className="cl-td-muted">
                          {[f.street, f.city, f.state, f.postalCode].filter(Boolean).join(', ') || '—'}
                        </td>
                        <td className="cl-td-muted">{f.phone ?? '—'}</td>
                        <td>
                          <span className={`cl-badge ${f.active ? 'cl-badge-green' : 'cl-badge-muted'}`}>
                            {f.active ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="cl-admin-row-actions">
                          <button className="cl-icon-button" type="button" onClick={() => beginFacilityEdit(f)} aria-label={`Edit ${f.name}`} title="Edit facility"><Pencil size={15} /></button>
                          <button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removeFacility(f)} disabled={deletingFacilityId === f.id} aria-label={`Delete ${f.name}`} title="Delete facility"><Trash2 size={15} /></button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            )}

            {tab === 'reviews' && (
              <section className="cl-card">
                <div className="cl-card-header">
                  <div>
                    <h2 className="cl-card-title">Portal profile reviews</h2>
                    <p className="clinician-page-subtitle">Review requested patient portal profile changes before they reach the chart.</p>
                  </div>
                  <span className="cl-badge cl-badge-muted">{data.portalActivity.waitingAuditCount} audits waiting</span>
                </div>
                {data.portalActivity.profileReviewRequests.length === 0 ? (
                  <p className="cl-empty-text">No portal profile changes are waiting for review.</p>
                ) : (
                  <div className="cl-review-list">
                    {data.portalActivity.profileReviewRequests.map((request) => {
                      const working = reviewActionId === request.id
                      const demographic = request.requestedDemographics
                      const contact = [demographic.email, demographic.phoneHome, demographic.phoneCell].filter(Boolean).join(' · ')
                      const address = [demographic.street, demographic.city, demographic.state, demographic.postalCode].filter(Boolean).join(', ')
                      return <article key={request.id} className="cl-review-card"><div className="cl-review-top"><div><p className="cl-form-section-label">{request.narrative}</p><h3>{request.patientName}</h3><p>{request.pubpid} · PID {request.legacyPid} · Requested {request.requestedAt}</p></div><span className="cl-badge cl-badge-muted">{request.status}</span></div><div className="cl-review-facts"><span><strong>Contact:</strong> {contact || 'Not supplied'}</span><span><strong>Address:</strong> {address || 'Not supplied'}</span><span><strong>Pending:</strong> {request.pendingAction}</span></div><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="button" disabled={working} onClick={() => resolveProfileReview(request, 'accept')}><Check size={15} /> {working ? 'Working...' : 'Commit to chart'}</button><button className="cl-btn-secondary" type="button" disabled={working} onClick={() => resolveProfileReview(request, 'revert')}><RotateCcw size={15} /> Revert edits</button></div></article>
                    })}
                  </div>
                )}
              </section>
            )}

            {tab === 'audit' && (
              <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
                <div className="cl-admin-facility-header">
                  <div>
                    <h2 className="cl-card-title">Recent PHI access decisions</h2>
                    <p className="clinician-page-subtitle">Read-only access-control evidence from the protected audit endpoint. This view does not replace production retention or export policy.</p>
                  </div>
                  {auditState.status === 'ready' && <span className="cl-badge cl-badge-muted">{auditState.data.authorizedEvents} allowed · {auditState.data.deniedEvents} denied</span>}
                </div>
                {auditState.status === 'loading' && <div className="skeleton-list">{[0, 1, 2].map((item) => <div key={item} className="skeleton-row" style={{ height: 52 }} />)}</div>}
                {auditState.status === 'error' && <div className="error-banner">{auditState.message}</div>}
                {auditState.status === 'ready' && auditState.data.events.length === 0 && <p className="cl-empty-text" style={{ padding: '0 20px 20px' }}>No recent PHI access decisions are available.</p>}
                {auditState.status === 'ready' && auditState.data.events.length > 0 && <table className="cl-table"><thead><tr><th>When</th><th>User</th><th>Request</th><th>Permission</th><th>Decision</th></tr></thead><tbody>{auditState.data.events.map((entry) => <tr key={entry.auditId}><td className="cl-td-muted">{entry.occurredAt}</td><td>{entry.username}</td><td className="cl-td-muted">{entry.httpMethod} {entry.requestPath}</td><td className="cl-td-muted">{entry.requiredPermission}</td><td><span className={`cl-badge ${entry.authorized ? 'cl-badge-green' : 'cl-badge-red'}`}>{entry.authorized ? `Allowed (${entry.responseStatus})` : `Denied (${entry.responseStatus})`}</span></td></tr>)}</tbody></table>}
              </section>
            )}

            {tab === 'configuration' && (
              <section className="cl-card">
                <h2 className="cl-card-title">Practice settings</h2>
                <p className="clinician-page-subtitle">Non-secret legacy-style globals save only when changed and retain an authenticated audit event.</p>
                {practiceSettings.map((item) => <div className="form-row" key={item.key}><div className="field" style={{ flex: 1 }}><label className="label">{item.label}</label><input className="input" defaultValue={item.value} onBlur={async (event) => { if (event.target.value === item.value) return; try { const result = await updatePracticeSetting(session.sessionId, item.key, event.target.value); setPracticeSettings(result.settings); showToast(`${item.label} saved.`, 'success') } catch { event.target.value = item.value; showToast(`Could not save ${item.label}.`, 'error') } }} /></div><p className="cl-empty-text">{item.updatedBy}</p><button className="cl-btn-secondary" type="button" onClick={() => void openPracticeSettingHistory(item.key)}>History</button></div>)}
                {practiceSettingHistory ? <div className="cl-card" style={{ marginTop: 12 }}><h3 className="cl-card-title">{practiceSettingHistory.setting.label} revisions</h3><table className="cl-table"><thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Value</th><th /></tr></thead><tbody>{practiceSettingHistory.revisions.map((revision) => <tr key={revision.revisionId}><td>{new Date(revision.occurredAt).toLocaleString()}</td><td>{revision.username}</td><td>{revision.action}</td><td>{revision.value}</td><td><button className="cl-btn-secondary" type="button" disabled={revision.value === practiceSettingHistory.setting.value} onClick={() => void rollbackPracticeSettingRevision(revision.revisionId)}>Restore</button></td></tr>)}</tbody></table></div> : null}

                <h2 className="cl-card-title">Coding catalogs</h2>
                <p className="clinician-page-subtitle">Legacy code types are ordered, active or inactive, and carry claim, fee, and modifier capabilities. Inactivation preserves the catalog key and its historical references.</p>
                <form className="cl-access-form" onSubmit={createCatalog}>
                  <label className="cl-admin-field"><span>Code system key</span><input className="ne-input" value={codingCatalogForm.key} onChange={(event) => setCodingCatalogForm((form) => ({ ...form, key: event.target.value.toUpperCase() }))} maxLength={32} placeholder="LOINC" required /></label>
                  <label className="cl-admin-field"><span>Display name</span><input className="ne-input" value={codingCatalogForm.displayName} onChange={(event) => setCodingCatalogForm((form) => ({ ...form, displayName: event.target.value }))} maxLength={120} placeholder="LOINC" required /></label>
                  <label className="cl-admin-field"><span>Order</span><input className="ne-input" type="number" min="0" value={codingCatalogForm.sequence} onChange={(event) => setCodingCatalogForm((form) => ({ ...form, sequence: Number(event.target.value) }))} required /></label>
                  <label className="cl-admin-field"><span>Modifier length</span><input className="ne-input" type="number" min="0" max="12" value={codingCatalogForm.modifierLength} onChange={(event) => setCodingCatalogForm((form) => ({ ...form, modifierLength: Number(event.target.value) }))} required /></label>
                  <label className="cl-admin-field"><span><input type="checkbox" checked={codingCatalogForm.active} onChange={(event) => setCodingCatalogForm((form) => ({ ...form, active: event.target.checked }))} /> Active</span></label>
                  <label className="cl-admin-field"><span><input type="checkbox" checked={codingCatalogForm.claimEnabled} onChange={(event) => setCodingCatalogForm((form) => ({ ...form, claimEnabled: event.target.checked }))} /> Claims</span></label>
                  <label className="cl-admin-field"><span><input type="checkbox" checked={codingCatalogForm.feeEnabled} onChange={(event) => setCodingCatalogForm((form) => ({ ...form, feeEnabled: event.target.checked }))} /> Fees</span></label>
                  <button className="cl-btn-primary" type="submit" disabled={savingCodingCatalog || !codingCatalogForm.key.trim() || !codingCatalogForm.displayName.trim()}>{savingCodingCatalog ? 'Saving...' : 'Add catalog'}</button>
                </form>
                <table className="cl-table"><thead><tr><th>Catalog</th><th>Order</th><th>Modifier length</th><th>Capabilities</th><th>Active</th></tr></thead><tbody>{codingCatalogs.map((catalog) => <tr key={catalog.key}><td><strong>{catalog.key}</strong><input className="ne-input" defaultValue={catalog.displayName} aria-label={`${catalog.key} display name`} onBlur={(event) => { if (event.target.value !== catalog.displayName) void saveCodingCatalog(catalog, { displayName: event.target.value }) }} /></td><td><input className="ne-input" type="number" min="0" defaultValue={catalog.sequence} aria-label={`${catalog.key} order`} onBlur={(event) => { const sequence = Number(event.target.value); if (Number.isInteger(sequence) && sequence !== catalog.sequence) void saveCodingCatalog(catalog, { sequence }) }} /></td><td><input className="ne-input" type="number" min="0" max="12" defaultValue={catalog.modifierLength} aria-label={`${catalog.key} modifier length`} onBlur={(event) => { const modifierLength = Number(event.target.value); if (Number.isInteger(modifierLength) && modifierLength !== catalog.modifierLength) void saveCodingCatalog(catalog, { modifierLength }) }} /></td><td><label><input type="checkbox" checked={catalog.claimEnabled} disabled={savingCodingCatalog} onChange={(event) => void saveCodingCatalog(catalog, { claimEnabled: event.target.checked })} /> Claims</label><br /><label><input type="checkbox" checked={catalog.feeEnabled} disabled={savingCodingCatalog} onChange={(event) => void saveCodingCatalog(catalog, { feeEnabled: event.target.checked })} /> Fees</label></td><td><label><input type="checkbox" checked={catalog.active} disabled={savingCodingCatalog} onChange={(event) => void saveCodingCatalog(catalog, { active: event.target.checked })} /> {catalog.active ? 'Active' : 'Inactive'}</label></td></tr>)}</tbody></table>
                <div className="cl-actions" style={{ marginTop: 12 }}>{codingCatalogs.map((catalog) => <button className="cl-btn-secondary" type="button" key={catalog.key} onClick={() => void openCodingCatalogHistory(catalog.key)}>History: {catalog.key}</button>)}</div>
                {codingCatalogHistory ? <div className="cl-card" style={{ marginTop: 12 }}><h3 className="cl-card-title">{codingCatalogHistory.catalog.key} revisions</h3><table className="cl-table"><thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Definition</th><th /></tr></thead><tbody>{codingCatalogHistory.revisions.map((revision) => <tr key={revision.revisionId}><td>{new Date(revision.occurredAt).toLocaleString()}</td><td>{revision.username}</td><td>{revision.action}</td><td>{revision.displayName} · order {revision.sequence} · modifier {revision.modifierLength} · {revision.active ? 'active' : 'inactive'}</td><td><button className="cl-btn-secondary" type="button" disabled={revision.displayName === codingCatalogHistory.catalog.displayName && revision.sequence === codingCatalogHistory.catalog.sequence && revision.active === codingCatalogHistory.catalog.active && revision.claimEnabled === codingCatalogHistory.catalog.claimEnabled && revision.feeEnabled === codingCatalogHistory.catalog.feeEnabled && revision.modifierLength === codingCatalogHistory.catalog.modifierLength} onClick={() => void rollbackCodingCatalogRevision(revision.revisionId)}>Restore</button></td></tr>)}</tbody></table></div> : null}

                <h2 className="cl-card-title">Configuration catalog</h2>
                <table className="cl-table"><thead><tr><th>Family</th><th>Classification</th><th>Authority</th><th>Mutation state</th></tr></thead><tbody>{configuration.map((item) => <tr key={item.key}><td><strong>{item.family}</strong><p className="cl-table-sub">{item.validation}</p></td><td>{item.classification}</td><td>{item.authority}</td><td>{item.mutationState}</td></tr>)}</tbody></table>
              </section>
            )}

            {tab === 'layouts' && (
              <section className="cl-card">
                <h2 className="cl-card-title">Forms and layouts</h2>
                <p className="clinician-page-subtitle">Manage the metadata that organizes legacy-style forms. This registry does not alter patient records or database columns.</p>
                <div className="cl-access-grid">
                  <section className="cl-access-panel"><h3 className="cl-access-title">Layout registry</h3><ul className="cl-access-list">{layouts.map((layout) => <li className="cl-access-row" key={layout.key}><div><p>{layout.title}</p><span>{layout.key} · {layout.mapping} · {layout.active ? 'Active' : 'Inactive'}</span></div><button className="cl-btn-secondary" type="button" onClick={() => void openLayout(layout.key)}>Edit</button><button className="cl-btn-secondary" type="button" onClick={() => void openLayoutHistory(layout.key)}>History</button></li>)}{layouts.length === 0 && <li className="cl-empty-text">No layouts configured.</li>}</ul>{layoutHistory ? <div className="cl-card" style={{ marginTop: 12 }}><h3 className="cl-card-title">{layoutHistory.detail.layout.title} revisions</h3><table className="cl-table"><thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Shape</th><th /></tr></thead><tbody>{layoutHistory.revisions.map((revision) => <tr key={revision.revisionId}><td>{new Date(revision.occurredAt).toLocaleString()}</td><td>{revision.username}</td><td>{revision.action}</td><td>{revision.groupCount} groups · {revision.fieldCount} fields</td><td><button className="cl-btn-secondary" type="button" disabled={revision.title === layoutHistory.detail.layout.title && revision.mapping === layoutHistory.detail.layout.mapping && revision.sequence === layoutHistory.detail.layout.sequence && revision.active === layoutHistory.detail.layout.active && revision.groupCount === layoutHistory.detail.groups.length && revision.fieldCount === layoutHistory.detail.fields.length} onClick={() => void rollbackLayoutRevision(revision.revisionId)}>Restore</button></td></tr>)}</tbody></table></div> : null}</section>
                  <section className="cl-access-panel"><h3 className="cl-access-title">Layout editor</h3><form className="cl-access-form" onSubmit={saveLayout}><label className="cl-admin-field"><span>Layout key</span><input className="ne-input" value={layoutKey} onChange={(event) => { setLayoutKey(event.target.value.toUpperCase()); setLayoutDetail(null) }} placeholder="LBFINTAKE" required /></label><label className="cl-admin-field"><span>Title</span><input className="ne-input" value={layoutDetail?.layout.title ?? ''} onChange={(event) => setLayoutDetail((detail) => detail ? { ...detail, layout: { ...detail.layout, title: event.target.value } } : { layout: { key: layoutKey, title: event.target.value, mapping: 'Core', sequence: 10, active: true }, groups: [], fields: [] })} required /></label><button className="cl-btn-primary" type="submit" disabled={savingLayout || !layoutKey.trim()}>Save layout</button></form>{layoutDetail && <div><p className="cl-admin-form-copy">{layoutDetail.groups.length} groups · {layoutDetail.fields.length} fields</p><form className="cl-access-form" onSubmit={saveGroup}><input className="ne-input" placeholder="Group key" value={groupDraft.key} onChange={(e) => setGroupDraft({ ...groupDraft, key: e.target.value.toUpperCase() })} required /><input className="ne-input" placeholder="Group title" value={groupDraft.title} onChange={(e) => setGroupDraft({ ...groupDraft, title: e.target.value })} required /><button className="cl-btn-secondary">Add group</button></form><form className="cl-access-form" onSubmit={saveField}><input className="ne-input" placeholder="Field key" value={fieldDraft.key} onChange={(e) => setFieldDraft({ ...fieldDraft, key: e.target.value.toUpperCase() })} required /><select className="ne-input" value={fieldDraft.groupKey} onChange={(e) => setFieldDraft({ ...fieldDraft, groupKey: e.target.value })} required><option value="">Group</option>{layoutDetail.groups.map((g) => <option key={g.key} value={g.key}>{g.title}</option>)}</select><input className="ne-input" placeholder="Field label" value={fieldDraft.label} onChange={(e) => setFieldDraft({ ...fieldDraft, label: e.target.value })} required /><button className="cl-btn-secondary">Add field</button></form><ul className="cl-access-list">{layoutDetail.groups.map((group) => <li className="cl-access-row" key={group.key}><div><p>{group.title}</p><span>{group.key} · {layoutDetail.fields.filter((field) => field.groupKey === group.key).length} fields</span></div></li>)}</ul></div>}</section>
                </div>
                <div className="cl-access-grid" style={{ marginTop: 20 }}>
                  <section className="cl-access-panel"><h3 className="cl-access-title">Option-list registry</h3><p className="cl-admin-form-copy">Legacy forms use reusable list IDs with ordered active values.</p><ul className="cl-access-list">{formOptionLists.map((list) => <li className="cl-access-row" key={list.key}><div><p>{list.title}</p><span>{list.key} Â· {list.optionCount} options Â· {list.active ? 'Active' : 'Inactive'}</span></div><button className="cl-btn-secondary" type="button" onClick={() => void openFormOptionList(list.key)}>Edit</button><button className="cl-btn-secondary" type="button" onClick={() => void openFormOptionListHistory(list.key)}>History</button></li>)}{formOptionLists.length === 0 && <li className="cl-empty-text">No form option lists configured.</li>}</ul>{formOptionListHistory ? <div className="cl-card" style={{ marginTop: 12 }}><h3 className="cl-card-title">{formOptionListHistory.detail.list.title} revisions</h3><table className="cl-table"><thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Options</th><th /></tr></thead><tbody>{formOptionListHistory.revisions.map((revision) => <tr key={revision.revisionId}><td>{new Date(revision.occurredAt).toLocaleString()}</td><td>{revision.username}</td><td>{revision.action}</td><td>{revision.optionCount}</td><td><button className="cl-btn-secondary" type="button" disabled={revision.title === formOptionListHistory.detail.list.title && revision.active === formOptionListHistory.detail.list.active && revision.optionCount === formOptionListHistory.detail.options.length} onClick={() => void rollbackFormOptionListRevision(revision.revisionId)}>Restore</button></td></tr>)}</tbody></table></div> : null}</section>
                  <section className="cl-access-panel">
                    <h3 className="cl-access-title">Option-list editor</h3>
                    <form className="cl-access-form" onSubmit={saveFormOptionListDefinition}>
                      <label className="cl-admin-field"><span>List key</span><input className="ne-input" value={formOptionListKey} onChange={(event) => { setFormOptionListKey(event.target.value.toLowerCase()); setFormOptionListDetail(null) }} placeholder="state" required /></label>
                      <label className="cl-admin-field"><span>Title</span><input className="ne-input" value={formOptionListDetail?.list.title ?? ''} onChange={(event) => setFormOptionListDetail((detail) => detail ? { ...detail, list: { ...detail.list, title: event.target.value } } : { list: { key: formOptionListKey, title: event.target.value, active: true, optionCount: 0 }, options: [] })} required /></label>
                      <button className="cl-btn-primary" type="submit" disabled={savingFormOptionList || !formOptionListKey.trim()}>Save list</button>
                    </form>
                    {formOptionListDetail && <div>
                      <p className="cl-admin-form-copy">Reuse an option key to update it. Inactivation preserves historical form data.</p>
                      <form className="cl-access-form" onSubmit={saveFormOption}>
                        <input className="ne-input" placeholder="Option key" value={formOptionDraft.key} onChange={(event) => setFormOptionDraft((draft) => ({ ...draft, key: event.target.value }))} required />
                        <input className="ne-input" placeholder="Label" value={formOptionDraft.title} onChange={(event) => setFormOptionDraft((draft) => ({ ...draft, title: event.target.value }))} required />
                        <input className="ne-input" placeholder="Stored value" value={formOptionDraft.value} onChange={(event) => setFormOptionDraft((draft) => ({ ...draft, value: event.target.value }))} />
                        <input className="ne-input" type="number" min="0" value={formOptionDraft.sequence} onChange={(event) => setFormOptionDraft((draft) => ({ ...draft, sequence: Number(event.target.value) }))} required />
                        <label className="cl-admin-field"><span><input type="checkbox" checked={formOptionDraft.isDefault} onChange={(event) => setFormOptionDraft((draft) => ({ ...draft, isDefault: event.target.checked }))} /> Default</span></label>
                        <label className="cl-admin-field"><span><input type="checkbox" checked={formOptionDraft.active} onChange={(event) => setFormOptionDraft((draft) => ({ ...draft, active: event.target.checked }))} /> Active</span></label>
                        <button className="cl-btn-secondary" type="submit">Add or update option</button>
                      </form>
                      <table className="cl-table"><thead><tr><th>Option</th><th>Value</th><th>Order</th><th>Default</th><th>State</th></tr></thead><tbody>{formOptionListDetail.options.map((option) => <tr key={option.key}><td>{option.title}<p className="cl-table-sub">{option.key}</p></td><td>{option.value}</td><td>{option.sequence}</td><td>{option.isDefault ? 'Default' : '—'}</td><td>{option.active ? 'Active' : 'Inactive'}</td></tr>)}</tbody></table>
                    </div>}
                  </section>
                </div>
              </section>
            )}

            {tab === 'rules' && <section className="cl-card"><h2 className="cl-card-title">Rules and alerts</h2><p className="clinician-page-subtitle">Local rule definitions control which clinical context produces an in-app banner or reminder. No external notification is sent from this catalog.</p><table className="cl-table"><thead><tr><th>Rule</th><th>Trigger</th><th>Target</th><th>Severity</th><th>Active</th><th /></tr></thead><tbody>{alertRules.map((rule) => <tr key={rule.key}><td><strong>{rule.title}</strong><p className="cl-table-sub">{rule.message}</p></td><td>{rule.triggerType}</td><td>{rule.targetType}</td><td>{rule.severity}</td><td><label><input type="checkbox" checked={rule.active} onChange={(event) => void saveClinicalAlertRule(session.sessionId, rule.key, { ...rule, active: event.target.checked }).then((result) => setAlertRules(result.rules)).catch(() => showToast('Could not save alert rule.', 'error'))} /> {rule.active ? 'Active' : 'Inactive'}</label></td><td><button className="cl-btn-secondary" type="button" onClick={() => void openClinicalAlertRuleHistory(rule.key)}>History</button></td></tr>)}</tbody></table>{alertRuleHistory ? <div className="cl-card" style={{ marginTop: 12 }}><h3 className="cl-card-title">{alertRuleHistory.rule.title} revisions</h3><table className="cl-table"><thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Definition</th><th /></tr></thead><tbody>{alertRuleHistory.revisions.map((revision) => <tr key={revision.revisionId}><td>{new Date(revision.occurredAt).toLocaleString()}</td><td>{revision.username}</td><td>{revision.action}</td><td>{revision.triggerType} · {revision.targetType} · {revision.severity} · {revision.active ? 'Active' : 'Inactive'}</td><td><button className="cl-btn-secondary" type="button" disabled={revision.title === alertRuleHistory.rule.title && revision.triggerType === alertRuleHistory.rule.triggerType && revision.targetType === alertRuleHistory.rule.targetType && revision.severity === alertRuleHistory.rule.severity && revision.message === alertRuleHistory.rule.message && revision.sequence === alertRuleHistory.rule.sequence && revision.active === alertRuleHistory.rule.active} onClick={() => void rollbackClinicalAlertRuleRevision(revision.revisionId)}>Restore</button></td></tr>)}</tbody></table></div> : null}</section>}

            {tab === 'modules' && <section className="cl-card"><h2 className="cl-card-title">Module inventory</h2><p className="clinician-page-subtitle">Local modules can be enabled only through their own lifecycle. Decision-required and partner-gated modules remain visible until their accountable owners authorize them.</p><table className="cl-table"><thead><tr><th>Module</th><th>Category</th><th>Status</th><th>Scope</th></tr></thead><tbody>{modules.map((module) => <tr key={module.key}><td><strong>{module.displayName}</strong><p className="cl-table-sub">{module.key}</p></td><td>{module.category}</td><td><span className="cl-badge cl-badge-muted">{module.status}</span></td><td>{module.description}</td></tr>)}</tbody></table></section>}

            {tab === 'apiClients' && <section className="cl-card"><div className="cl-admin-facility-header"><div><h2 className="cl-card-title">API clients</h2><p className="clinician-page-subtitle">Redirect URIs and scopes are managed here. Client credentials and secrets remain deployment-managed and are never returned by this application.</p></div>{editingApiClientKey === null && <button className="cl-btn-primary" type="button" onClick={beginApiClientCreate}><Plus size={15} /> Add API client</button>}</div>{editingApiClientKey !== null && <form className="cl-admin-facility-form" onSubmit={submitApiClient}><div className="cl-admin-form-heading"><div><p className="cl-form-section-label">{editingApiClientKey === 'new' ? 'New API client' : 'Edit API client'}</p><p className="cl-admin-form-copy">This registry stores identity and routing metadata only; it never stores or returns client secrets.</p></div><button className="cl-icon-button" type="button" onClick={cancelApiClientEdit} aria-label="Cancel API client edit" title="Cancel"><X size={16} /></button></div><div className="cl-admin-form-grid"><label className="cl-admin-field"><span>Client key <em>*</em></span><input className="ne-input" autoCapitalize="characters" value={apiClientForm.key} disabled={editingApiClientKey !== 'new'} onChange={(event) => setApiClientForm((form) => ({ ...form, key: event.target.value.toUpperCase() }))} maxLength={64} required /></label><label className="cl-admin-field"><span>Display name <em>*</em></span><input className="ne-input" value={apiClientForm.displayName} onChange={(event) => setApiClientForm((form) => ({ ...form, displayName: event.target.value }))} maxLength={120} required /></label><label className="cl-admin-field"><span>HTTPS redirect URI <em>*</em></span><input className="ne-input" type="url" value={apiClientForm.redirectUri} onChange={(event) => setApiClientForm((form) => ({ ...form, redirectUri: event.target.value }))} placeholder="https://client.example/callback" required /></label><label className="cl-admin-field"><span>Scopes <em>*</em></span><input className="ne-input" value={apiClientForm.scopes} onChange={(event) => setApiClientForm((form) => ({ ...form, scopes: event.target.value }))} placeholder="patient.read patient.write" maxLength={500} required /></label><label className="cl-admin-active-toggle"><input type="checkbox" checked={apiClientForm.active} onChange={(event) => setApiClientForm((form) => ({ ...form, active: event.target.checked }))} /><span>API client is active</span></label></div><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={savingApiClient}>{savingApiClient ? 'Saving...' : editingApiClientKey === 'new' ? 'Create API client' : 'Save changes'}</button><button className="cl-btn-secondary" type="button" onClick={cancelApiClientEdit} disabled={savingApiClient}>Cancel</button></div></form>}<table className="cl-table"><thead><tr><th>Client</th><th>Redirect URI</th><th>Scopes</th><th>State</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{apiClients.map((client) => <tr key={client.key}><td><strong>{client.displayName}</strong><p className="cl-table-sub">{client.key}</p></td><td>{client.redirectUri}</td><td>{client.scopes}</td><td>{client.active ? 'Active' : 'Inactive'}</td><td className="cl-admin-row-actions"><button className="cl-icon-button" type="button" onClick={() => beginApiClientEdit(client)} aria-label={`Edit ${client.displayName}`} title="Edit API client"><Pencil size={15} /></button></td></tr>)}</tbody></table></section>}

            {tab === 'access' && (
              <section className="cl-card">
                <div className="cl-card-header">
                  <h2 className="cl-card-title">Access groups</h2>
                  <p className="clinician-page-subtitle">
                    {data.counts.accessGroupPermissions} permissions · {data.counts.accessUserMemberships} user memberships
                  </p>
                </div>
                {data.accessControl.groups.length === 0 ? (
                  <p className="cl-empty-text">No access groups configured.</p>
                ) : (
                  <ul className="cl-clinical-list">
                    {data.accessControl.groups.map((g) => (
                      <li key={g.id} className="cl-clinical-row">
                        <div>
                          <p className="cl-clinical-title">{g.name}</p>
                          <p className="cl-clinical-meta">{g.value} · {g.permissionCount} permissions</p>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
                <div className="cl-access-grid">
                  <section className="cl-access-panel">
                    <div>
                      <h3 className="cl-access-title">User memberships</h3>
                      <p className="cl-admin-form-copy">Assign a staff user to an existing access group.</p>
                    </div>
                    <form className="cl-access-form" onSubmit={saveMembership}>
                      <label className="cl-admin-field"><span>Staff user</span><select className="ne-input" value={membershipForm.userValue} onChange={(event) => setMembershipForm((form) => ({ ...form, userValue: event.target.value }))} required><option value="">Select staff user</option>{data.users.map((user) => <option key={user.id} value={user.username}>{user.displayName} ({user.username})</option>)}</select></label>
                      <label className="cl-admin-field"><span>Access group</span><select className="ne-input" value={membershipForm.groupValue} onChange={(event) => setMembershipForm((form) => ({ ...form, groupValue: event.target.value }))} required><option value="">Select group</option>{data.accessControl.groups.map((group) => <option key={group.id} value={group.value}>{group.name}</option>)}</select></label>
                      <button className="cl-btn-primary" type="submit" disabled={savingMembership || !membershipForm.userValue || !membershipForm.groupValue}>{savingMembership ? 'Saving...' : 'Add membership'}</button>
                    </form>
                    <ul className="cl-access-list">
                      {data.accessControl.userMemberships.map((membership) => {
                        const key = `${membership.userValue}:${membership.groupValue}`
                        const isCurrentUser = membership.staffId === session.staffId
                        return <li key={key} className="cl-access-row"><div><p>{membership.userName}</p><span>{membership.groupName}</span></div><button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removeMembership(membership)} disabled={removingMembership === key || isCurrentUser} aria-label={`Remove ${membership.userName} from ${membership.groupName}`} title={isCurrentUser ? 'You cannot remove the signed-in user' : 'Remove membership'}><Trash2 size={15} /></button></li>
                      })}
                      {data.accessControl.userMemberships.length === 0 && <li className="cl-empty-text">No access-group memberships configured.</li>}
                    </ul>
                  </section>

                  <section className="cl-access-panel">
                    <div>
                      <h3 className="cl-access-title">Group permissions</h3>
                      <p className="cl-admin-form-copy">Grant or replace one existing permission on an access group.</p>
                    </div>
                    <form className="cl-access-form" onSubmit={savePermission}>
                      <label className="cl-admin-field"><span>Access group</span><select className="ne-input" value={permissionForm.groupValue} onChange={(event) => setPermissionForm((form) => ({ ...form, groupValue: event.target.value }))} required><option value="">Select group</option>{data.accessControl.groups.map((group) => <option key={group.id} value={group.value}>{group.name}</option>)}</select></label>
                      <label className="cl-admin-field"><span>Permission</span><select className="ne-input" value={permissionForm.permissionKey} onChange={(event) => setPermissionForm((form) => ({ ...form, permissionKey: event.target.value }))} required><option value="">Select permission</option>{data.accessControl.permissions.map((permission) => <option key={`${permission.sectionValue}:${permission.value}`} value={`${permission.sectionValue}:${permission.value}`}>{permission.name} ({permission.sectionValue})</option>)}</select></label>
                      <label className="cl-admin-field"><span>Access level</span><select className="ne-input" value={permissionForm.returnValue} onChange={(event) => setPermissionForm((form) => ({ ...form, returnValue: event.target.value as AccessPermissionForm['returnValue'] }))}><option value="view">View</option><option value="addonly">Add only</option><option value="write">Write</option><option value="wsome">Write some</option></select></label>
                      <button className="cl-btn-primary" type="submit" disabled={savingPermission || !permissionForm.groupValue || !permissionForm.permissionKey}>{savingPermission ? 'Saving...' : 'Save permission'}</button>
                    </form>
                    <ul className="cl-access-list">
                      {data.accessControl.groupPermissions.map((permission) => {
                        const key = `${permission.groupValue}:${permission.sectionValue}:${permission.permissionValue}`
                        return <li key={key} className="cl-access-row"><div><p>{permission.permissionName}</p><span>{permission.groupValue} · {permission.returnValue}</span></div><button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removePermission(permission)} disabled={removingPermission === key} aria-label={`Remove ${permission.permissionName} from ${permission.groupValue}`} title="Remove permission"><Trash2 size={15} /></button></li>
                      })}
                      {data.accessControl.groupPermissions.length === 0 && <li className="cl-empty-text">No access permissions configured.</li>}
                    </ul>
                  </section>
                </div>
              </section>
            )}
          </>
        )
      })()}
    </div>
  )
}
