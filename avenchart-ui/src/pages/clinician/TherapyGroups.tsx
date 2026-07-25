import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { createTherapyGroup, getTherapyGroups, type TherapyGroup } from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

export default function TherapyGroups() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [groups, setGroups] = useState<TherapyGroup[]>([]); const [name, setName] = useState(''); const [capacity, setCapacity] = useState(12)
  const load = () => getTherapyGroups(session.sessionId).then((data) => setGroups(data.groups)).catch(() => {})
  useEffect(() => { load() }, [])
  async function create() { if (!name.trim()) return; await createTherapyGroup(session.sessionId, { name, capacity }); setName(''); load() }
  return <div className="clinician-page"><div className="clinician-page-header"><h1 className="clinician-page-title">Therapy groups</h1><p className="clinician-page-subtitle">Create local group programs before assigning members or sessions.</p></div><section className="cl-card"><div className="cl-inline-form"><label className="cl-admin-field"><span>Group name</span><input className="ne-input" value={name} onChange={(event) => setName(event.target.value)} /></label><label className="cl-admin-field"><span>Capacity</span><input className="ne-input" type="number" min="1" max="200" value={capacity} onChange={(event) => setCapacity(Number(event.target.value))} /></label><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="button" onClick={create}>Create group</button></div></div></section><section className="cl-card"><table className="cl-table"><thead><tr><th>Group</th><th>Status</th><th>Capacity</th></tr></thead><tbody>{groups.map((group) => <tr key={group.id}><td>{group.name}</td><td>{group.status}</td><td>{group.capacity}</td></tr>)}</tbody></table>{groups.length === 0 && <p className="cl-empty-text">No therapy groups are defined.</p>}</section></div>
}
