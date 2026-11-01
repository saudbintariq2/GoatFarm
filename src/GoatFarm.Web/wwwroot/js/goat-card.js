/* Goat card modal — parity with index_new.html showGoatCard / ask / pedigreeSVG */
(() => {
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const today = () => new Date().toISOString().slice(0, 10);
  const uuid = () => (crypto.randomUUID ? crypto.randomUUID() : String(Date.now()));

  const BCS_OPTIONS = [
    '1 — very thin',
    '2 — thin',
    '3 — good',
    '4 — fat',
    '5 — very fat'
  ];
  const bcsOptionForScore = score => {
    const n = Math.min(5, Math.max(1, +score || 3));
    return BCS_OPTIONS[n - 1];
  };
  const parseBcsScore = val => {
    const m = /^(\d)/.exec(String(val ?? '').trim());
    return m ? +m[1] : NaN;
  };

  const SCAN_RESULT_OPTIONS = [
    '1 kid (single)',
    'Twins (2)',
    'Triplets (3)',
    '4+ kids',
    'Not pregnant (empty)'
  ];
  const SCAN_KIDS_BY_RESULT = {
    '1 kid (single)': 1,
    'Twins (2)': 2,
    'Triplets (3)': 3,
    '4+ kids': 4,
    'Not pregnant (empty)': 0
  };
  const GESTATION_DAYS = 150;
  const kidsScanLabel = n => {
    if (n === 1) return '1 kid (single)';
    if (n === 2) return 'Twins';
    if (n === 3) return 'Triplets';
    return n + ' kids';
  };
  const addDaysIso = (iso, days) => {
    const d = new Date(String(iso).slice(0, 10) + 'T12:00:00');
    d.setDate(d.getDate() + days);
    return d.toISOString().slice(0, 10);
  };

  const recordsUi = () => {
    if (typeof GoatRecords !== 'undefined') return GoatRecords;
    return typeof window !== 'undefined' ? window.GoatRecords : null;
  };
  const toast = (m, t) => {
    const ui = recordsUi();
    if (ui?.showToast) ui.showToast(m, t);
    else alert(m);
  };
  const modal = (m, o) => recordsUi()?.showModal?.(m, o) ?? Promise.resolve(confirm(m));
  const confirmDlg = m => recordsUi()?.showConfirm?.(m) ?? Promise.resolve(confirm(m));

  const FarmPerms = {
    can(tab, action) {
      const p = window.FarmPermissions?.[tab] ?? { view: true, add: true, edit: true, delete: true };
      if (action !== 'view' && !p.view) return false;
      return !!p[action];
    }
  };

  function goatToUpdatePayload(vm, patch) {
    return {
      tag: patch.tag ?? vm.tag,
      name: vm.name ?? null,
      comment: vm.comment ?? null,
      breed: vm.breed,
      gender: vm.gender,
      status: vm.status,
      source: vm.source,
      purchasePrice: vm.purchasePrice ?? 0,
      eventDate: vm.eventDateDisplay || vm.eventDate,
      dateOfBirth: vm.dateOfBirthDisplay || vm.dateOfBirth || null,
      dateOfBirthIsEstimated: !!vm.dateOfBirthIsEstimated,
      healthState: vm.healthState || 'Healthy',
      owner: vm.owner ?? null,
      sireTag: patch.sireTag !== undefined ? patch.sireTag : (vm.sireTag ?? null),
      damTag: patch.damTag !== undefined ? patch.damTag : (vm.damTag ?? null)
    };
  }

  async function api(url, options = {}) {
    const res = await fetch(url, {
      headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...(options.headers || {}) },
      credentials: 'same-origin',
      ...options
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      toast(err.error || 'Request failed', 'error');
      throw new Error(err.error || 'fail');
    }
    if (res.status === 204) return null;
    return res.json().catch(() => ({}));
  }

  function ask(title, fields, options) {
    return new Promise(resolve => {
      const m = document.getElementById('askModal');
      const body = document.getElementById('askBody');
      if (!m || !body) { resolve(null); return; }
      document.getElementById('askTitle').textContent = title;
      body.innerHTML = fields.map(f => {
        const rowAttr = f.rowId ? ` data-ask-row="${esc(f.rowId)}"` : '';
        if (f.type === 'select') {
          const opts = (f.options || []).map(o => {
            const v = typeof o === 'object' ? o.value : o;
            const l = typeof o === 'object' ? o.label : o;
            return `<option value="${esc(v)}"${String(v) === String(f.value ?? '') ? ' selected' : ''}>${esc(l)}</option>`;
          }).join('');
          return `<div class="field"${rowAttr}><label>${esc(f.label)}</label><select data-ask="${esc(f.key)}">${opts}</select></div>`;
        }
        return `<div class="field"${rowAttr}><label>${esc(f.label)}</label><input type="${esc(f.type || 'text')}" data-ask="${esc(f.key)}" value="${esc(f.value ?? '')}" placeholder="${esc(f.placeholder ?? '')}"></div>`;
      }).join('');
      m.classList.add('show');
      m.setAttribute('aria-hidden', 'false');
      document.body.classList.add('goat-card-submodal-open');
      const first = m.querySelector('[data-ask]');
      if (first) setTimeout(() => first.focus(), 50);
      const close = val => {
        m.classList.remove('show');
        m.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('goat-card-submodal-open');
        document.getElementById('askOk').onclick = null;
        document.getElementById('askCancel').onclick = null;
        m.onclick = null;
        resolve(val);
      };
      document.getElementById('askOk').onclick = () => {
        const out = {};
        m.querySelectorAll('[data-ask]').forEach(i => { out[i.dataset.ask] = i.value; });
        close(out);
      };
      document.getElementById('askCancel').onclick = () => close(null);
      m.querySelector('.modal-box')?.addEventListener('click', e => e.stopPropagation(), { once: true });
      m.onclick = e => { if (e.target === m) close(null); };
      if (options?.wire) options.wire(m);
    });
  }

  const PAY_CREDIT_LABEL = 'On credit (buyer pays later)';

  function wireSellPaymentAccountRow(modalEl) {
    const pay = modalEl.querySelector('[data-ask="pay"]');
    const acctRow = modalEl.querySelector('[data-ask-row="acct"]');
    const sync = () => {
      if (acctRow) acctRow.style.display = pay?.value === PAY_CREDIT_LABEL ? 'none' : '';
    };
    pay?.addEventListener('change', sync);
    sync();
  }

  let cardState = { id: null, gens: 3, profile: null, birthRequestId: null };

  function setGoatCardOpen(open) {
    document.body.classList.toggle('goat-card-open', !!open);
  }

  function isGoatCardOpen() {
    return document.getElementById('goatModal')?.classList.contains('show') ?? false;
  }

  function isSubModalOpen() {
    return document.getElementById('askModal')?.classList.contains('show')
      || document.getElementById('appModal')?.classList.contains('open');
  }

  function nodesById(profile) {
    const map = {};
    (profile.pedigreeNodes || []).forEach(n => { map[n.id] = n; });
    return map;
  }

  function pedigreeSvg(subjectId, profile, gens) {
    gens = Math.max(2, Math.min(5, gens || 3));
    const byId = nodesById(profile);
    const subject = byId[subjectId];
    if (!subject) return '';
    const rows = Math.pow(2, gens - 1);
    const BW = 136, BH = 40, GAPX = 26, GAPY = 8;
    const W = gens * BW + (gens - 1) * GAPX + 8;
    const H = rows * (BH + GAPY) + 8;
    const cellY = (col, idx) => {
      const span = rows / Math.pow(2, col);
      return 4 + (idx * span + span / 2) * (BH + GAPY) - BH / 2;
    };
    const colX = c => 4 + c * (BW + GAPX);
    const short = t => { t = String(t || ''); return t.length > 15 ? t.slice(0, 14) + '…' : t; };

    const level = [[subject]];
    for (let c = 1; c < gens; c++) {
      const prev = level[c - 1];
      const cur = [];
      prev.forEach(a => {
        cur.push(a && a.sireId && byId[a.sireId] ? byId[a.sireId] : null);
        cur.push(a && a.damId && byId[a.damId] ? byId[a.damId] : null);
      });
      level.push(cur);
    }

    let out = '';
    level.forEach((col, c) => col.forEach((a, i) => {
      const x = colX(c), y = cellY(c, i);
      const isMale = c === 0 ? subject.gender === 'Male' : (i % 2 === 0);
      if (c > 0) {
        const py = cellY(c - 1, Math.floor(i / 2));
        const px = colX(c - 1) + BW;
        out += `<path d="M${px} ${py + BH / 2} H${px + GAPX / 2} V${y + BH / 2} H${x}" fill="none" stroke="#C9D2C5" stroke-width="1.5"/>`;
      }
      if (!a) {
        out += `<g><rect x="${x}" y="${y}" width="${BW}" height="${BH}" rx="9" fill="#FAFBF8" stroke="#DDD5C3" stroke-dasharray="5 4"/>
          <text x="${x + BW / 2}" y="${y + 18}" text-anchor="middle" font-size="10" fill="#A79F8E">${c === 0 ? '' : (isMale ? 'sire' : 'dam')}</text>
          <text x="${x + BW / 2}" y="${y + 31}" text-anchor="middle" font-size="11" fill="#B9B1A0">not recorded</text></g>`;
        return;
      }
      const me0 = c === 0;
      const fill = me0 ? '#4A5A40' : '#FFFFFF';
      const stroke = me0 ? '#3F4E36' : (a.gender === 'Female' ? '#D9A6BD' : '#9CC3D5');
      const ink = me0 ? '#FFFFFF' : '#2C2A26';
      const sub = me0 ? '#DDEBDD' : '#7A756B';
      const pedOpen = me0 ? '<g>' : `<g data-goatcard="${a.id}" style="cursor:pointer">`;
      out += `${pedOpen}
        <rect x="${x}" y="${y}" width="${BW}" height="${BH}" rx="9" fill="${fill}" stroke="${stroke}" stroke-width="${me0 ? 2 : 1.4}"/>
        <text x="${x + 8}" y="${y + 17}" font-size="12.5" font-weight="700" fill="${ink}">${esc(short(a.tag))} ${a.gender === 'Female' ? '♀' : '♂'}</text>
        <text x="${x + 8}" y="${y + 31}" font-size="10" fill="${sub}">${esc(short(a.name || a.breed || ''))}${a.isArchived ? ' · archived' : ''}</text></g>`;
    }));
    return `<svg viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" style="max-width:none;font-family:inherit" xmlns="http://www.w3.org/2000/svg">${out}</svg>`;
  }

  function renderBody(profile) {
    const g = profile.goat;
    const gens = cardState.gens;
    const src = g.source === 'Born' ? 'born on farm' : 'bought';
    let html = `<div class="breed">${esc(g.breed)} · ${esc(g.gender)} · ${esc(g.ageLabel)} old · ${src}`;
    if (g.owner) html += ` · owner ${esc(g.owner)}`;
    html += '</div>';
    if (g.kidStageLine) {
      html += `<div class="name v207-stage" style="color:${esc(g.kidStageColor || 'var(--ink-soft)')};font-weight:700">${esc(g.kidStageLine)}</div>`;
    }
    html += `<div style="display:flex;align-items:center;gap:10px;margin-top:8px;flex-wrap:wrap">
      <div style="font-weight:700">Family tree</div>
      <div class="filterbar" style="gap:4px;display:flex">
        ${[3, 4, 5].map(n => `<button type="button" class="btn btn-ghost btn-sm" data-gens="${n}" style="${gens === n ? 'background:var(--green);color:#fff;border-color:var(--green)' : ''}">${n} generations</button>`).join('')}
      </div></div>
      <div style="background:#FCFDFB;border:1px solid var(--line);border-radius:12px;padding:12px 8px;overflow-x:auto" id="gmPedigreeWrap">${pedigreeSvg(profile.subjectGoatId, profile, gens)}</div>
      <div class="breed" style="font-size:12px">Tap any box to open that goat. Dashed boxes are ancestors not yet recorded.
      ${g.isArchived ? '' : `<a href="#" id="editParents" style="color:var(--green-dark);font-weight:700;margin-left:6px">Edit parents</a>`}</div>`;

    if (profile.siblings?.length) {
      html += `<div style="font-weight:700;margin-top:6px">Brothers &amp; sisters (${profile.siblings.length})</div>
        <div style="display:flex;gap:6px;flex-wrap:wrap">${profile.siblings.map(k =>
        `<span style="background:var(--green-tint);border-radius:8px;padding:4px 9px;font-size:13px">
          <a href="#" data-goatcard="${k.id}" style="color:var(--green-dark);font-weight:700;text-decoration:none">${esc(k.tag)}</a>
          <span class="breed">${k.gender === 'Male' ? '♂' : '♀'} ${esc(k.birthDateDisplay)}${k.twin ? ' · twin' : ''}</span></span>`).join('')}</div>`;
    }

    if (g.gender === 'Female' && g.statusKey !== 'kid') {
      const kd = profile.kiddings || [];
      const live = kd.reduce((a, k) => a + (k.liveKids || 0), 0);
      html += `<div style="font-weight:700;margin-top:6px">Breeding record</div>
        <div class="breed">${kd.length} birth(s) · ${live} kids born alive · ${profile.emptyScanCount || 0} empty scan(s) · ${profile.abortionCount || 0} abortion(s)</div>`;
      if (kd.length) {
        html += `<table class="tbl" style="margin-top:6px"><thead><tr><th>Date</th><th>Sire</th><th class="num-cell">Alive</th><th class="num-cell">Still</th></tr></thead><tbody>
          ${kd.map(k => `<tr><td>${esc(k.dateDisplay)}</td><td>${k.sireGoatId ? `<a href="#" data-goatcard="${k.sireGoatId}">${esc(k.sireTag || '—')}</a>` : esc(k.sireTag || '—')}</td>
          <td class="num-cell">${k.liveKids}</td><td class="num-cell">${k.stillborn || 0}</td></tr>`).join('')}</tbody></table>`;
      }
    } else if (g.gender === 'Male') {
      html += `<div style="font-weight:700;margin-top:6px">As a sire</div><div class="breed">${profile.siredKidsCount || 0} kid(s) sired</div>`;
    }

    const w = profile.weights || [];
    html += `<div style="font-weight:700;margin-top:6px">Weights</div>`;
    html += w.length
      ? `<div class="breed">${w.map(x => `${esc(x.dateDisplay)}: <b>${x.kg} kg</b>`).join(' · ')}</div>`
      : '<div class="breed">None recorded.</div>';
    if (g.comment) html += `<div style="font-weight:700;margin-top:6px">Note</div><div class="breed">${esc(g.comment)}</div>`;
    return html;
  }

  function btn(label, act, kind) {
    const need = {
      weigh: ['herd', 'edit'], bcs: ['herd', 'edit'], status: ['herd', 'edit'], wean: ['herd', 'edit'],
      youngclass: ['herd', 'edit'], younggraduate: ['herd', 'edit'], tagone: ['herd', 'edit'], tagkids: ['herd', 'edit'],
      treat: ['vaccines', 'add'], recover: ['vaccines', 'edit'], vacc: ['vaccines', 'add'],
      heat: ['breeding', 'add'], plancross: ['breeding', 'add'], prep: ['breeding', 'add'],
      cross: ['breeding', 'add'], scan: ['breeding', 'edit'], kid: ['breeding', 'add'], returned: ['breeding', 'edit'],
      unprep: ['breeding', 'edit'],
      dryoff: ['herd', 'edit'], sell: ['finance', 'add'], death: ['herd', 'delete']
    }[act];
    if (need && !FarmPerms.can(need[0], need[1])) return '';
    return `<button type="button" class="btn ${kind || 'btn-ghost'}" data-act="${act}">${label}</button>`;
  }

  function renderActions(profile) {
    const g = profile.goat;
    const box = document.getElementById('gmActions');
    if (!box) return;
    if (!FarmPerms.can('herd', 'view')) {
      box.innerHTML = '<span class="breed">You are not allowed to see this goat.</span>';
      return;
    }
    if (g.isArchived) {
      box.innerHTML = `<span class="breed">Record closed — ${esc(g.archivedReason || 'archived')}${g.archivedDateDisplay ? ' on ' + esc(g.archivedDateDisplay) : ''}.</span>`;
      return;
    }
    const sick = g.healthState && g.healthState !== 'Healthy';
    const doe = g.gender === 'Female' && g.statusKey !== 'kid';
    let first = '';
    if (g.isTemporaryTag) first += btn('🏷 Give ear tag', 'tagone', 'btn-green');
    if (profile.kidsToTag?.length) first += btn(`🏷 Tag her kids (${profile.kidsToTag.length})`, 'tagkids', 'btn-green');
    let breed = '';
    if (doe) {
      if (g.matedDate && g.kidsCount) breed = btn('🍼 Record birth', 'kid', 'btn-green');
      else if (g.matedDate) breed = btn('🔍 Scan result', 'scan', 'btn-green') + btn('🍼 Record birth', 'kid');
      else if (g.prepCrossDate) {
        breed = btn('↩ Not ready for cross', 'unprep') + btn('♥ Record cross', 'cross', 'btn-green') + btn('🍼 Record birth', 'kid');
      }
      else breed = btn('Ready for cross', 'prep') + btn('♥ Record cross', 'cross') + btn('🍼 Record birth', 'kid');
    }
    let extra = '';
    if (g.statusKey === 'kid' && !g.weanDate) extra += btn('🍼 Wean', 'wean', 'btn-green');
    if (g.nextStepAction === 'Classify') extra += btn('🏷 Classify · 7 months', 'youngclass', 'btn-green');
    if (g.statusKey === 'kid' && g.weanDate && g.youngClass && g.youngClass !== 'None') {
      extra += btn('→ Move to adult herd', 'younggraduate', 'btn-green');
    }
    if (doe && !g.matedDate) {
      extra += btn('♡ Seen in heat', 'heat') +
        btn('📅 ' + (g.nextCrossDate ? 'Change next cross' : 'Plan next cross'), 'plancross');
    }
    if (doe && g.matedDate && !g.kidsCount) extra += btn('↺ Returned to heat', 'returned');
    if (g.nextStepAction === 'DryOff') extra += btn('🌙 Dry her off', 'dryoff', 'btn-green');
    box.innerHTML = first + btn('⚖ Weigh', 'weigh') + btn('◐ Condition', 'bcs') +
      (sick ? btn('✓ Recovered', 'recover', 'btn-green') : btn('⚕ Treat', 'treat')) +
      btn('💉 Vaccinate', 'vacc') + extra + breed + btn('Status', 'status') + btn('₨ Sell', 'sell') + btn('✕ Death', 'death');
  }

  async function refreshCard() {
    if (!cardState.id) return;
    const profile = await api('/Goat/Card?id=' + cardState.id);
    cardState.profile = profile;
    renderGoatCard(profile);
  }

  function renderGoatCard(profile) {
    const g = profile.goat;
    cardState.id = profile.subjectGoatId;
    cardState.profile = profile;
    document.getElementById('gmTitle').innerHTML = `<span>${esc(g.tag)}${g.name ? ' · ' + esc(g.name) : ''}
      <span class="chip ${esc(g.statusCssClass)}" style="margin-left:8px">${esc(g.statusDisplay)}</span></span>`;
    document.getElementById('gmBody').innerHTML = renderBody(profile);
    renderActions(profile);
    document.getElementById('gmBody').scrollTop = 0;
    wireCardBody(profile);
  }

  function wireCardBody(profile) {
    document.querySelectorAll('#goatModal [data-gens]').forEach(b => {
      b.onclick = () => { cardState.gens = +b.dataset.gens; renderGoatCard(profile); };
    });
    document.getElementById('editParents')?.addEventListener('click', e => {
      e.preventDefault();
      runAction('editparents');
    });
    document.getElementById('gmActions')?.querySelectorAll('[data-act]').forEach(b => {
      b.onclick = e => {
        e.preventDefault();
        e.stopPropagation();
        runAction(b.dataset.act);
      };
    });
  }

  async function openGoatCard({ id, tag }) {
    const modalEl = document.getElementById('goatModal');
    if (!modalEl) return;
    const sameGoat = (id != null && cardState.id === id)
      || (tag && cardState.profile?.goat?.tag?.toLowerCase() === String(tag).toLowerCase());
    if (modalEl.classList.contains('show') && sameGoat) return;
    try {
      const url = id ? '/Goat/Card?id=' + encodeURIComponent(id) : '/Goat/Card?tag=' + encodeURIComponent(tag);
      const profile = await api(url);
      renderGoatCard(profile);
      modalEl.classList.add('show');
      setGoatCardOpen(true);
    } catch { /* toast */ }
  }

  const WEAN_ASK_FIELDS = [
    { key: 'date', label: 'Date weaned', type: 'date', value: () => today() },
    { key: 'weight', label: 'Weaning weight, kg (optional)', type: 'number' }
  ];

  function weanAskFields() {
    return WEAN_ASK_FIELDS.map(f => ({
      ...f,
      value: typeof f.value === 'function' ? f.value() : f.value
    }));
  }

  /** @returns {Promise<boolean>} */
  async function performWean({ id, tag }, { afterSuccess, title } = {}) {
    const r = await ask(title || ('Wean — ' + tag), weanAskFields());
    if (!r) return false;
    const when = r.date || today();
    try {
      await api('/Goat/BulkMove', {
        method: 'POST',
        body: JSON.stringify({ goatIds: [id], moveTarget: 'k:wean', weanDate: when })
      });
      if (+r.weight > 0) {
        await api('/Goat/RecordWeight', {
          method: 'POST',
          body: JSON.stringify({ tag, kg: +r.weight, date: when })
        });
      }
    } catch { return false; }
    if (afterSuccess) await afterSuccess();
    return true;
  }

  async function promptWeanFromHerd({ id, tag }) {
    return performWean({ id, tag });
  }

  async function promptWeanBulk({ goatIds }) {
    const n = goatIds?.length || 0;
    if (!n) return false;
    const r = await ask('Wean ' + n + ' kid(s)', [
      { key: 'date', label: 'Date weaned', type: 'date', value: today() }
    ]);
    if (!r) return false;
    const when = r.date || today();
    try {
      await api('/Goat/BulkMove', {
        method: 'POST',
        body: JSON.stringify({ goatIds, moveTarget: 'k:wean', weanDate: when })
      });
    } catch { return false; }
    return true;
  }

  /** @returns {Promise<boolean>} true when a scan was saved */
  async function performScanForGoat(g, { afterSuccess } = {}) {
    const mated = g.matedDate || g.matedDateDisplay;
    if (!mated) {
      toast(g.tag + ' has no mating recorded. Record her cross first.', 'error');
      return false;
    }
    if (g.kidsCount > 0) {
      toast(g.tag + ' already has a confirmed scan. Use Record birth when she kids.', 'error');
      return false;
    }
    const r = await ask('Ultrasound result — ' + g.tag, [
      {
        key: 'result',
        label: 'What did the scan show?',
        type: 'select',
        options: SCAN_RESULT_OPTIONS,
        value: SCAN_RESULT_OPTIONS[0]
      },
      { key: 'date', label: 'Scan date', type: 'date', value: today() }
    ]);
    if (!r) return false;
    const kidsCount = SCAN_KIDS_BY_RESULT[r.result];
    if (kidsCount === undefined) return false;
    const when = r.date || today();
    try {
      await api('/Breeding/RecordUltrasound', {
        method: 'POST',
        body: JSON.stringify({ tag: g.tag, kidsCount, date: when })
      });
    } catch { return false; }
    if (kidsCount === 0) {
      toast(g.tag + ' — not pregnant, moved back to Dry');
    } else {
      toast(g.tag + ' — ' + kidsScanLabel(kidsCount) + ', due ~' + addDaysIso(mated, GESTATION_DAYS));
    }
    if (afterSuccess) await afterSuccess();
    return true;
  }

  async function runScanFromHerd({ id, tag }) {
    let g;
    try {
      if (id) {
        const profile = await api('/Goat/Card?id=' + encodeURIComponent(id));
        g = profile.goat;
      } else if (tag) {
        g = await api('/Goat/GetByTag?tag=' + encodeURIComponent(tag));
      } else return false;
    } catch { return false; }
    return performScanForGoat(g);
  }

  async function runAction(act) {
    const profile = cardState.profile;
    if (!profile) return;
    const g = profile.goat;
    const id = profile.subjectGoatId;

    const runRefresh = async () => { await refreshCard(); };

    if (act === 'weigh') {
      const r = await ask('Weigh — ' + g.tag, [
        { key: 'kg', label: 'Weight (kg)', type: 'number', placeholder: 'e.g. 32.5' },
        { key: 'date', label: 'Date', type: 'date', value: today() }
      ]);
      if (!r || !(+r.kg > 0)) return;
      await api('/Goat/RecordWeight', { method: 'POST', body: JSON.stringify({ tag: g.tag, kg: +r.kg, date: r.date || today() }) });
      toast('Weight recorded');
      return runRefresh();
    }
    if (act === 'bcs') {
      const r = await ask('Body condition — ' + g.tag, [
        {
          key: 'score',
          label: 'Score (1 thin → 5 fat). Aim for 3 at crossing.',
          type: 'select',
          options: BCS_OPTIONS,
          value: bcsOptionForScore(profile.latestBcsScore || 3)
        },
        { key: 'date', label: 'Date', type: 'date', value: today() }
      ]);
      if (!r) return;
      const score = parseBcsScore(r.score);
      if (!(score >= 1 && score <= 5)) return;
      await api('/Goat/RecordBcs', { method: 'POST', body: JSON.stringify({ goatId: id, score, date: r.date || today() }) });
      toast('Condition score saved');
      return runRefresh();
    }
    if (act === 'treat') {
      const [healthList, form] = await Promise.all([
        api('/Lookup/Get?key=' + encodeURIComponent('Lookup.HealthStates')).catch(() => []),
        api('/Goat/SaleFormData').catch(() => ({ accounts: [] }))
      ]);
      let states = (healthList || []).filter(h => h && h !== 'Healthy');
      if (!states.length) states = ['Under treatment', 'Quarantine', 'Injured', 'Weak / off feed'];
      const accts = form.accounts || [];
      const acctOpts = accts.map(a => ({ value: String(a.id), label: a.name }));
      const defaultState = states.find(s => s === 'Under treatment') || states[0];
      const r = await ask('Mark sick / treat — ' + g.tag, [
        { key: 'state', label: 'Health state', type: 'select', options: states, value: defaultState },
        { key: 'problem', label: 'Problem', type: 'text', placeholder: 'e.g. scours, fever' },
        { key: 'med', label: 'Medicine given', type: 'text', placeholder: 'optional' },
        { key: 'cost', label: 'Cost (Rs)', type: 'number', placeholder: '0' },
        {
          key: 'acct',
          label: 'Cost paid from (if any)',
          type: 'select',
          options: acctOpts.length ? acctOpts : [{ value: '', label: 'Add accounts on Finance first' }],
          value: acctOpts[0]?.value || ''
        },
        { key: 'wd', label: 'Milk withdrawal (days)', type: 'number', placeholder: '0' },
        { key: 'date', label: 'Date', type: 'date', value: today() }
      ]);
      if (!r) return;
      const cost = +r.cost || 0;
      const accountId = r.acct ? +r.acct : null;
      if (cost > 0 && !(accountId > 0)) {
        toast('Choose the Cash/Bank account the medicine was paid from.', 'error');
        return;
      }
      await api('/Vaccine/SaveTreatment', {
        method: 'POST',
        body: JSON.stringify({
          goatId: id,
          startDate: r.date || today(),
          diagnosis: (r.problem || '').trim() || 'Treatment',
          medicine: (r.med || '').trim() || '—',
          cost,
          withdrawalDays: +r.wd || null,
          healthStateLabel: r.state,
          accountId: cost > 0 ? accountId : null
        })
      });
      toast('Treatment recorded');
      return runRefresh();
    }
    if (act === 'recover') {
      if (!profile.activeTreatmentId) { await modal('No active treatment on file.'); return; }
      if (!await confirmDlg(g.tag + ' has recovered?')) return;
      await api('/Goat/RecoverTreatment?id=' + profile.activeTreatmentId, { method: 'POST' });
      toast('Recovered');
      return runRefresh();
    }
    if (act === 'vacc') {
      const data = await api('/Vaccine/GetData');
      const vaccines = data?.page?.schedule || [];
      if (!vaccines.length) { await modal('No vaccines set up yet. Add them on Health & vaccines first.'); return; }
      const r = await ask('Vaccinate — ' + g.tag, [
        { key: 'v', label: 'Vaccine given', type: 'select', options: vaccines.map(v => v.name), value: vaccines[0]?.name },
        { key: 'date', label: 'Date', type: 'date', value: today() }
      ]);
      if (!r) return;
      const v = vaccines.find(x => x.name === r.v);
      if (!v) return;
      await api('/Vaccine/GiveToGroup', {
        method: 'POST',
        body: JSON.stringify({ vaccineId: v.id, target: 'tag:' + g.tag, date: r.date || today() })
      });
      toast('Vaccination recorded');
      return runRefresh();
    }
    if (act === 'wean') {
      const ok = await performWean({ id, tag: g.tag }, { afterSuccess: runRefresh });
      if (ok) toast('Kid weaned');
      return;
    }
    if (act === 'youngclass') {
      const r = await ask('Classify — ' + g.tag, [
        { key: 'cls', label: 'Class', type: 'select', options: g.gender === 'Female' ? ['Doeling', 'Meat / For Sale'] : ['Buckling', 'Meat / For Sale'] }
      ]);
      if (!r) return;
      const map = { 'Doeling': 'k:class-doeling', 'Buckling': 'k:class-buckling', 'Meat / For Sale': 'k:class-meat' };
      const target = map[r.cls];
      if (!target) return;
      await api('/Goat/BulkMove', { method: 'POST', body: JSON.stringify({ goatIds: [id], moveTarget: target }) });
      toast('Classified');
      return runRefresh();
    }
    if (act === 'younggraduate') {
      await api('/Goat/BulkMove', { method: 'POST', body: JSON.stringify({ goatIds: [id], moveTarget: 'k:graduate' }) });
      toast('Moved to adult herd');
      return runRefresh();
    }
    if (act === 'dryoff') {
      const r = await ask('Dry her off — ' + g.tag, [{ key: 'date', label: 'Date', type: 'date', value: today() }]);
      if (!r) return;
      await api('/Goat/BulkMove', { method: 'POST', body: JSON.stringify({ goatIds: [id], moveTarget: 'br:dryoff' }) });
      toast('Dry off recorded');
      return runRefresh();
    }
    if (act === 'prep') {
      if (g.matedDate) {
        toast(g.tag + ' is already crossed (' + g.matedDate + '). Record her scan result instead.', 'error');
        return;
      }
      try {
        await api('/Goat/BulkMove', { method: 'POST', body: JSON.stringify({ goatIds: [id], moveTarget: 'br:prep', prepCrossDate: today() }) });
      } catch { return; }
      toast('Ready for cross');
      return runRefresh();
    }
    if (act === 'unprep') {
      if (!g.prepCrossDate || g.matedDate) return;
      if (!await confirmDlg('Take ' + g.tag + ' off the ready-to-cross list? She stays in the herd.')) return;
      try {
        await api('/Breeding/RemovePrep?id=' + id, { method: 'DELETE' });
      } catch { return; }
      toast('Ready for cross cleared');
      return runRefresh();
    }
    if (act === 'plancross') {
      const r = await ask('Plan next cross — ' + g.tag, [
        { key: 'date', label: 'Cross her again on', type: 'date', value: g.nextCrossDate || today() },
        { key: 'clear', label: 'Keep this plan?', type: 'select', options: ['Yes, save it', 'No, remove the plan'] }
      ]);
      if (!r) return;
      if (String(r.clear).startsWith('No')) {
        await api('/Goat/BulkMove', { method: 'POST', body: JSON.stringify({ goatIds: [id], moveTarget: 'br:plan', nextCrossDate: null }) });
      } else {
        await api('/Goat/BulkMove', { method: 'POST', body: JSON.stringify({ goatIds: [id], moveTarget: 'br:plan', nextCrossDate: r.date }) });
      }
      toast('Cross plan updated');
      return runRefresh();
    }
    if (act === 'cross') {
      const bucks = await api('/Goat/ParentOptions');
      const opts = ['— none —'].concat(bucks.sires || []);
      const r = await ask('Record cross — ' + g.tag, [
        { key: 'date', label: 'Mating date', type: 'date', value: today() },
        { key: 'buck', label: 'Buck', type: 'select', options: opts }
      ]);
      if (!r?.date) return;
      const buck = r.buck && r.buck !== '— none —' ? r.buck : null;
      await api('/Goat/BulkMove', {
        method: 'POST',
        body: JSON.stringify({ goatIds: [id], moveTarget: 'br:cross', matedDate: r.date, buckTag: buck })
      });
      toast('Cross recorded');
      return runRefresh();
    }
    if (act === 'scan') {
      await performScanForGoat(g, { afterSuccess: runRefresh });
      return;
    }
    if (act === 'kid') {
      cardState.birthRequestId = uuid();
      const bucks = await api('/Goat/ParentOptions');
      const sOpts = (bucks.sires || []);
      const r = await ask('Record birth — ' + g.tag + (g.kidsCount ? ' (scan said ' + g.kidsCount + ')' : ''), [
        { key: 'date', label: 'Date she gave birth', type: 'date', value: today() },
        { key: 'sire', label: 'Sire (buck)', type: 'select', options: ['— none —'].concat(sOpts), value: g.buckTag || '— none —' },
        { key: 'fem', label: 'Female kids alive', type: 'number', placeholder: '0' },
        { key: 'male', label: 'Male kids alive', type: 'number', placeholder: '0' },
        { key: 'still', label: 'Stillborn', type: 'number', placeholder: '0' },
        { key: 'ease', label: 'Birth ease', type: 'select', options: ['Normal', 'Assisted', 'Difficult / vet needed'] },
        { key: 'note', label: 'Note', type: 'text' }
      ]);
      if (!r?.date) return;
      const fem = Math.max(0, Math.floor(+r.fem || 0));
      const male = Math.max(0, Math.floor(+r.male || 0));
      const still = Math.max(0, Math.floor(+r.still || 0));
      const kids = [];
      for (let i = 0; i < fem; i++) kids.push({ gender: 'Female' });
      for (let i = 0; i < male; i++) kids.push({ gender: 'Male' });
      const easeMap = { Normal: 'Normal', Assisted: 'Assisted', 'Difficult / vet needed': 'Difficult' };
      await api('/Breeding/RecordBirth', {
        method: 'POST',
        body: JSON.stringify({
          clientRequestId: cardState.birthRequestId,
          doeTag: g.tag,
          kiddingDate: r.date,
          ease: easeMap[r.ease] || 'Normal',
          stillborn: still,
          notes: r.note || null,
          kids
        })
      });
      toast('Birth recorded');
      return runRefresh();
    }
    if (act === 'heat') {
      const r = await ask('Seen in heat — ' + g.tag, [
        { key: 'date', label: 'Date', type: 'date', value: today() },
        { key: 'note', label: 'Note', type: 'text' }
      ]);
      if (!r) return;
      await api('/Goat/RecordHeat', { method: 'POST', body: JSON.stringify({ goatId: id, date: r.date || today(), note: r.note }) });
      toast('Heat recorded');
      return runRefresh();
    }
    if (act === 'returned') {
      const r = await ask('Returned to heat — ' + g.tag, [{ key: 'date', label: 'Date', type: 'date', value: today() }]);
      if (!r?.date) return;
      await api('/Breeding/RecordReturnedToHeat', { method: 'POST', body: JSON.stringify({ tag: g.tag, date: r.date }) });
      toast('Cross cleared — returned to heat');
      return runRefresh();
    }
    if (act === 'death') {
      const r = await ask('Record death — ' + g.tag, [
        { key: 'date', label: 'Date of death', type: 'date', value: today() },
        { key: 'reason', label: 'Reason', type: 'text' }
      ]);
      if (!r?.date) return;
      if (!await confirmDlg('Record ' + g.tag + ' as dead on ' + r.date + '?')) return;
      await api('/Goat/RecordDeath', { method: 'POST', body: JSON.stringify({ tag: g.tag, date: r.date, reason: r.reason }) });
      toast('Death recorded');
      document.getElementById('goatModal')?.classList.remove('show');
      setGoatCardOpen(false);
      return;
    }
    if (act === 'sell') {
      const form = await api('/Goat/SaleFormData');
      const accounts = form.accounts || [];
      const acctNames = accounts.map(a => a.name);
      const r = await ask('Sell — ' + g.tag, [
        { key: 'price', label: 'Sale price (Rs)', type: 'number', placeholder: 'e.g. 35000' },
        { key: 'buyer', label: 'Buyer', type: 'text', placeholder: 'name or market' },
        { key: 'weight', label: 'Weight at sale, kg (optional)', type: 'number' },
        { key: 'date', label: 'Date sold', type: 'date', value: today() },
        { key: 'pay', label: 'Payment', type: 'select', value: 'Paid now', options: ['Paid now', PAY_CREDIT_LABEL] },
        { key: 'acct', rowId: 'acct', label: 'Money received into', type: 'select', options: acctNames, value: acctNames[0] || '' }
      ], { wire: wireSellPaymentAccountRow });
      if (!r) return;
      const price = +r.price || 0;
      if (!(price > 0)) {
        toast('Enter the sale price.', 'error');
        return;
      }
      const isCredit = r.pay === PAY_CREDIT_LABEL;
      let accountId = null;
      let customerId = null;
      if (isCredit) {
        const buyerTrim = (r.buyer || '').trim();
        if (!buyerTrim) {
          toast('Enter a buyer name for credit sales.', 'error');
          return;
        }
        const match = (form.customers || []).find(c => c.name.toLowerCase() === buyerTrim.toLowerCase());
        if (match) customerId = match.id;
      } else {
        if (!accounts.length) {
          toast('Add a cash or bank account on Finance → Bank & Cash before recording a paid sale.', 'error');
          return;
        }
        const acct = accounts.find(a => a.name === r.acct);
        if (!acct?.id) {
          toast('Choose the Cash/Bank account that received the money.', 'error');
          return;
        }
        accountId = acct.id;
      }
      const priceLabel = Math.round(price).toLocaleString('en-US');
      if (!await confirmDlg(
        'Sell ' + g.tag + ' for Rs ' + priceLabel + '?\nShe leaves the herd, the sale goes automatically to Accounting revenue and Sales, and her history is kept.'
      )) return;
      const weightKg = +r.weight > 0 ? +r.weight : null;
      const result = await api('/Goat/RecordSale', {
        method: 'POST',
        body: JSON.stringify({
          tag: g.tag,
          date: r.date || today(),
          price,
          buyer: (r.buyer || '').trim() || null,
          weightKg,
          isCredit,
          accountId,
          customerId
        })
      });
      if (result && result.success === false) {
        toast(result.message || 'Could not record sale', 'error');
        return;
      }
      toast(result?.message || 'Sale recorded');
      document.getElementById('goatModal')?.classList.remove('show');
      setGoatCardOpen(false);
      return;
    }
    if (act === 'status') {
      const statuses = ['Kid', 'Milking', 'Dry', 'Pregnant', 'Buck', 'Meat'];
      const r = await ask('Change status — ' + g.tag, [
        { key: 'st', label: 'New status', type: 'select', options: statuses },
        { key: 'date', label: 'Date', type: 'date', value: today() }
      ]);
      if (!r) return;
      const key = 'st:' + String(r.st).toLowerCase();
      await api('/Goat/BulkMove', { method: 'POST', body: JSON.stringify({ goatIds: [id], moveTarget: key }) });
      toast('Status updated');
      return runRefresh();
    }
    if (act === 'tagone' || act === 'tagkids') {
      const list = act === 'tagkids' ? (profile.kidsToTag || []) : [{ id, tag: g.tag }];
      for (const kid of list) {
        let saved = false;
        while (saved === false) {
          const r = await ask('Tag kid ' + kid.tag, [
            { key: 'tag', label: 'Ear tag / RFID', type: 'text' },
            { key: 'sex', label: 'Sex', type: 'select', options: ['Female', 'Male'] },
            { key: 'weight', label: 'Weight kg (optional)', type: 'number' }
          ]);
          if (!r) break;
          const t = (r.tag || '').trim();
          if (!t) { await modal('Enter a tag.'); continue; }
          try {
            const existing = await fetch('/Goat/GetByTag?tag=' + encodeURIComponent(kid.tag), { credentials: 'same-origin' }).then(x => x.json());
            await api('/Goat/Update?id=' + kid.id, {
              method: 'PUT',
              body: JSON.stringify(goatToUpdatePayload(existing, { tag: t, gender: r.sex === 'Male' ? 'Male' : 'Female' }))
            });
            if (+r.weight > 0) await api('/Goat/RecordWeight', { method: 'POST', body: JSON.stringify({ tag: t, kg: +r.weight, date: today() }) });
            saved = true;
          } catch { /* retry */ }
        }
      }
      toast('Tag updated');
      return runRefresh();
    }
    if (act === 'editparents') {
      const bucks = await api('/Goat/ParentOptions');
      const opts = ['— none —'].concat(bucks.sires || []);
      const r = await ask('Parents of ' + g.tag, [
        { key: 'sire', label: 'Sire', type: 'select', options: opts, value: g.sireTag || '— none —' },
        { key: 'dam', label: 'Dam tag', type: 'text', value: g.damTag || '' }
      ]);
      if (!r) return;
      const sire = r.sire && r.sire !== '— none —' ? r.sire : '';
      const full = await fetch('/Goat/GetByTag?tag=' + encodeURIComponent(g.tag), { credentials: 'same-origin' }).then(x => x.json());
      await api('/Goat/Update?id=' + id, {
        method: 'PUT',
        body: JSON.stringify(goatToUpdatePayload(full, { sireTag: sire || null, damTag: (r.dam || '').trim() || null }))
      });
      toast('Parents updated');
      return runRefresh();
    }
  }

  function initGoatCard() {
    document.getElementById('gmClose')?.addEventListener('click', () => {
      document.getElementById('goatModal')?.classList.remove('show');
      setGoatCardOpen(false);
    });
    document.getElementById('goatModal')?.addEventListener('click', e => {
      if (e.target.id === 'goatModal') {
        e.target.classList.remove('show');
        setGoatCardOpen(false);
      }
    });

    document.addEventListener('click', e => {
      if (isSubModalOpen()) return;
      const ped = e.target.closest('#goatModal [data-goatcard]');
      if (ped) {
        e.preventDefault();
        e.stopPropagation();
        openGoatCard({ id: +ped.getAttribute('data-goatcard') });
        return;
      }
      const a = e.target.closest('[data-goatcard]');
      if (!a || a.closest('#goatModal')) return;
      e.preventDefault();
      e.stopPropagation();
      const cid = a.getAttribute('data-goatcard');
      if (/^\d+$/.test(cid)) openGoatCard({ id: +cid });
      else openGoatCard({ tag: cid });
    });

    const params = new URLSearchParams(location.search);
    const cardTag = params.get('card');
    if (cardTag) openGoatCard({ tag: cardTag });
  }

  window.GoatCard = {
    open: openGoatCard,
    refresh: refreshCard,
    runAction,
    runScanFromHerd,
    promptWeanFromHerd,
    promptWeanBulk,
    init: initGoatCard,
    isOpen: isGoatCardOpen,
    currentId: () => cardState.id
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initGoatCard);
  else initGoatCard();
})();
