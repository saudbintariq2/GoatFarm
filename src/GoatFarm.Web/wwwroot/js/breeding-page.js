/* Breeding tab — layout and behaviour aligned with index_new.html */
window.BreedingPage = (() => {
  const gr = () => window.GoatRecords;
  const $ = id => document.getElementById(id);
  const today = () => new Date().toISOString().slice(0, 10);
  const normalizeTag = v => String(v ?? '').replace(/[\x00-\x1F\x7F]/g, '').trim();
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  let currentView = 'today';
  const buildBreedingUrl = () => {
    const p = new URLSearchParams();
    if (currentView && currentView !== 'today') p.set('view', currentView);
    const sel = $('se-batch');
    if (sel?.value) p.set('batch', sel.value);
    const q = p.toString();
    return '/Breeding' + (q ? '?' + q : '');
  };
  const reload = (msg, undo) => {
    sessionStorage.setItem('goatToast', msg);
    if (undo) sessionStorage.setItem('goatToastUndo', JSON.stringify(undo));
    else sessionStorage.removeItem('goatToastUndo');
    location.href = buildBreedingUrl();
  };

  const breedingCan = (action) => {
    const p = window.FarmPermissions?.breeding ?? { view: true, add: true, edit: true, delete: true };
    if (action !== 'view' && !p.view) return false;
    return !!p[action];
  };

  function setBreedingView(view) {
    currentView = view || 'today';
    document.querySelectorAll('.breed-subnav-btn').forEach(b => {
      b.classList.toggle('on', b.dataset.breedView === currentView);
    });
    document.querySelectorAll('.breed-view').forEach(sec => {
      const on = sec.dataset.breedView === currentView;
      sec.classList.toggle('on', on);
      if (on) sec.removeAttribute('hidden');
      else sec.setAttribute('hidden', '');
    });
    const url = new URL(location.href);
    if (currentView === 'today') url.searchParams.delete('view');
    else url.searchParams.set('view', currentView);
    history.replaceState(null, '', url.pathname + url.search);
    if (view === 'does') {
      const p = new URLSearchParams(location.search);
      if (p.get('scan')) openScanPanel(false);
      else loadScanSheet().catch(() => {});
      const first = document.querySelector('#brRows tr[data-br-id]');
      if (first) loadDoeProfile(+first.dataset.brId);
    }
    if (view === 'bucks') renderGroupCards().catch(() => {});
    if (view === 'kidding') { /* birth form opens from Record kidding */ }
  }

  function filterDueToKid() {
    const f = document.querySelector('.breed-kd-chip.breed-on[data-kd-filter]')?.dataset.kdFilter || 'all';
    const rows = [...document.querySelectorAll('#kdTable tr[data-kd-row]')];
    rows.forEach(tr => {
      let show = true;
      if (f === 'soon') show = tr.dataset.kdSoon === '1';
      else if (f === 'dry') show = tr.dataset.kdDry === '1';
      tr.style.display = show ? '' : 'none';
    });
  }

  function filterKidsGround() {
    const f = document.querySelector('.breed-kd-chip.breed-on[data-kk-filter]')?.dataset.kkFilter || 'all';
    const rows = [...document.querySelectorAll('#kkTable tr[data-kk-row]')];
    rows.forEach(tr => {
      let show = true;
      if (f === 'milk') show = tr.dataset.kkMilk === '1';
      else if (f === 'week') show = tr.dataset.kkWeek === '1';
      else if (f === 'weaned') show = tr.dataset.kkWeaned === '1';
      else if (f === 'weak') show = tr.dataset.kkWeak === '1';
      tr.style.display = show ? '' : 'none';
    });
  }

  const kkSelected = new Set();

  function syncKkWeanBtn() {
    const btn = $('kkWeanEarly');
    if (!btn) return;
    btn.disabled = kkSelected.size === 0;
    btn.textContent = kkSelected.size
      ? `Wean ${kkSelected.size} now (early)`
      : 'Wean early';
  }

  function wireKidsTable() {
    document.querySelectorAll('.breed-kk-sel').forEach(cb => {
      cb.addEventListener('change', () => {
        const id = +cb.dataset.kkSel;
        if (cb.checked) kkSelected.add(id);
        else kkSelected.delete(id);
        syncKkWeanBtn();
      });
    });
    $('kkWeanEarly')?.addEventListener('click', async () => {
      if (!kkSelected.size) return;
      try {
        await gr().api('/Breeding/BulkWeanKids', {
          method: 'POST',
          body: JSON.stringify({ goatIds: [...kkSelected], weanDate: today() })
        });
        reload(`Weaned ${kkSelected.size} kid(s) — add wean weights`);
      } catch { /* shown */ }
    });
    document.querySelectorAll('.breed-seg[data-kk-field]').forEach(seg => {
      seg.querySelectorAll('.breed-seg-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
          const id = +seg.dataset.kkId;
          const field = seg.dataset.kkField;
          const val = btn.dataset.val;
          try {
            await gr().api('/Breeding/UpdateKidField', {
              method: 'POST',
              body: JSON.stringify({ goatId: id, field, value: val })
            });
            seg.querySelectorAll('.breed-seg-btn').forEach(b => {
              b.classList.toggle('breed-on', b === btn);
            });
          } catch { /* shown */ }
        });
      });
    });
    document.querySelectorAll('[data-kk-weankg]').forEach(inp => {
      inp.addEventListener('change', async () => {
        const kg = +inp.value;
        if (!(kg > 0)) return;
        const id = +inp.dataset.kkWeankg;
        try {
          await gr().api('/Breeding/RecordKidWeanWeight', {
            method: 'POST',
            body: JSON.stringify({ goatId: id, kg })
          });
          inp.classList.remove('breed-need');
          gr().showToast?.('Wean weight saved');
        } catch { /* shown */ }
      });
    });
    const saveWeanRule = async body => {
      try {
        await gr().api('/Breeding/UpdateKidWeanRule', {
          method: 'POST',
          body: JSON.stringify(body)
        });
        gr().showToast?.('Wean settings saved');
      } catch { /* shown */ }
    };
    document.querySelectorAll('[data-kk-wd]').forEach(inp => {
      inp.addEventListener('change', () => {
        const days = +inp.value;
        if (days > 0) saveWeanRule({ weanDays: days });
      });
    });
    document.querySelectorAll('[data-kk-wa]').forEach(cb => {
      cb.addEventListener('change', () => {
        saveWeanRule({ autoWean: cb.checked });
        cb.closest('.breed-wset')?.classList.toggle('breed-wset-off', !cb.checked);
      });
    });
  }

  function wireKiddingFilters() {
    document.querySelectorAll('[data-kd-filter]').forEach(btn => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('[data-kd-filter]').forEach(b => b.classList.remove('breed-on'));
        btn.classList.add('breed-on');
        filterDueToKid();
      });
    });
    document.querySelectorAll('[data-kk-filter]').forEach(btn => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('[data-kk-filter]').forEach(b => b.classList.remove('breed-on'));
        btn.classList.add('breed-on');
        filterKidsGround();
      });
    });
  }

  function wireSubNav() {
    document.querySelectorAll('.breed-subnav-btn').forEach(b => {
      b.addEventListener('click', () => setBreedingView(b.dataset.breedView));
    });
    document.querySelectorAll('[data-breed-goto]').forEach(b => {
      b.addEventListener('click', () => {
        const view = b.dataset.breedGoto;
        setBreedingView(view);
        if (b.dataset.breedScan === '1' && view === 'does') {
          const url = new URL(location.href);
          url.searchParams.set('scan', '1');
          history.replaceState(null, '', url.pathname + url.search);
        }
      });
    });
    const params = new URLSearchParams(location.search);
    const v = params.get('view');
    if (v && document.querySelector(`.breed-view[data-breed-view="${v}"]`)) setBreedingView(v);
    else setBreedingView('today');
  }

  function normalizeSeasonBatch(b) {
    if (!b || typeof b !== 'object') return null;
    const id = String(b.id ?? b.Id ?? '').trim();
    const name = String(b.name ?? b.Name ?? '').trim();
    const icon = String(b.icon ?? b.Icon ?? '⭐').trim() || '⭐';
    if (!id && !name) return null;
    return { id: id || name, name: name || id || 'Birth group', icon };
  }

  function parseSeasonBatches() {
    const box = $('bg-groups');
    if (!box?.dataset.batches) return [];
    try {
      const raw = JSON.parse(box.dataset.batches) || [];
      return raw.map(normalizeSeasonBatch).filter(Boolean);
    } catch { return []; }
  }

  function groupCardHtml(grp) {
    const pct = Math.min(100, Math.round(grp.memberCount / Math.max(1, grp.max) * 100));
    const statusChip = grp.status === 'active' ? 'chip-milk' : 'chip-dry';
    const statusLbl = grp.status === 'active' ? 'Active' : 'Closed';
    const range = !grp.start
      ? (grp.crossDeferred ? 'Buck not in yet' : '—')
      : (grp.end ? `${esc(grp.start)} → out ${esc(grp.end)}` : esc(grp.start));
    const rate = grp.conceptionPercent == null ? '—' : `${grp.conceptionPercent}%`;
    const pending = (grp.members || []).filter(m => m.outcomeKey === 'pending').length;
    const rows = (grp.members || []).map(m =>
      `<tr><td><b>${esc(m.tag)}</b></td><td><span class="breed">${esc(m.breed)}</span></td><td>${esc(grp.start || '—')}</td><td>${esc(m.outcomeText)}</td>
      <td>${m.canRemove ? `<button type="button" class="btn btn-ghost btn-sm" data-bg-rm="${esc(grp.id)}|${m.goatId}">Remove</button>` : ''}</td></tr>`
    ).join('');
    const acts = [];
    if (grp.canBuckIn) acts.push(`<button type="button" class="btn btn-green btn-sm" data-bg-buckin="${esc(grp.id)}">Put buck in today</button>`);
    if (grp.canClose) acts.push(`<button type="button" class="btn btn-ghost btn-sm" data-bg-close="${esc(grp.id)}">Take buck out — close</button>`);
    if (grp.canDelete) acts.push(`<button type="button" class="btn btn-ghost btn-sm" data-bg-del="${esc(grp.id)}" style="color:var(--red)">Delete group</button>`);
    return `<div class="v206-bg-card" data-bg-id="${esc(grp.id)}">
      <h3>${esc(grp.name)} <span class="chip ${statusChip}">${statusLbl}</span>
      <span class="breed" style="font-weight:500">Buck ${esc(grp.buck)} · ${range} · ${grp.stayDays || 42}d with does</span></h3>
      <div style="display:flex;justify-content:space-between;font-size:13px;margin-top:6px"><b>${grp.memberCount} does assigned</b><b style="color:var(--green-dark)">${grp.memberCount}/${grp.max}</b></div>
      <div class="v206-bg-bar"><i style="width:${pct}%"></i></div>
      <div class="v206-bg-kpis">
        ${pending ? `<div><b>${pending}</b>buck not in</div>` : ''}
        <div><b>${grp.waitingScan}</b>awaiting scan</div>
        <div><b>${grp.pregnant}</b>pregnant</div>
        <div><b>${grp.empty}</b>empty</div>
        <div><b>${grp.kidded}</b>kidded</div>
        <div><b>${grp.kidsAlive}</b>kids alive</div>
        <div><b>${rate}</b>conception</div>
        ${grp.gone ? `<div><b>${grp.gone}</b>left / moved</div>` : ''}
      </div>
      <details><summary>Individual does (${grp.memberCount})</summary>
      <div class="v206-bg-scroll"><table class="tbl"><thead><tr><th>Doe</th><th>Breed</th><th>Crossed</th><th>Result</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>
      </details>
      ${acts.length ? `<div style="margin-top:10px;display:flex;gap:8px;flex-wrap:wrap">${acts.join('')}</div>` : ''}
    </div>`;
  }

  function openNewGroupForBatch(batchId) {
    const sel = $('bg-batch');
  if (sel && batchId) sel.value = batchId;
    $('bgPanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    $('bg-buck')?.focus();
  }

  function openBuckCheckPanel(buckId) {
    const panel = $('buckCheckPanel');
    if (!panel) return;
    panel.hidden = false;
    if (buckId && $('bc-check-buck')) $('bc-check-buck').value = String(buckId);
    if ($('bc-check-date') && !$('bc-check-date').value) $('bc-check-date').value = today();
    panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function closeBuckCheckPanel() {
    const panel = $('buckCheckPanel');
    if (panel) panel.hidden = true;
  }

  async function saveBuckCheck(confirmProblem) {
    const grApi = gr();
    const buckId = +($('bc-check-buck')?.value);
    const checkDate = $('bc-check-date')?.value || today();
    if (!buckId) { await grApi.showModal('Choose the buck.'); return; }
    const bcsVal = $('bc-check-bcs')?.value;
    const body = {
      buckId,
      checkDate,
      weightKg: $('bc-check-kg')?.value ? +$('bc-check-kg').value : null,
      bcs: bcsVal ? +bcsVal : null,
      feet: $('bc-check-feet')?.value || 'good',
      teeth: $('bc-check-teeth')?.value || 'good',
      testicles: $('bc-check-test')?.value || 'normal',
      notes: $('bc-check-note')?.value?.trim() || null,
      confirmProblem: !!confirmProblem
    };
    const res = await fetch('/Breeding/RecordBuckCheck', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify(body)
    });
    if (res.ok) {
      reload('Buck check saved');
      return;
    }
    const err = await res.json().catch(() => ({}));
    const msg = err.error || err.title || 'Could not save buck check';
    if (res.status === 409 && err.requiresConfirmation && !confirmProblem) {
      const warn = (err.warnings || []).join('\n') || msg;
      if (await grApi.showConfirm(warn + '\n\nSave anyway?')) await saveBuckCheck(true);
      return;
    }
    await grApi.showModal(msg);
  }

  function wireBuckCheck() {
    $('bucksOpenCheck')?.addEventListener('click', () => {
      const first = $('bc-check-buck')?.value;
      openBuckCheckPanel(first ? +first : null);
    });
    $('buckCheckClose')?.addEventListener('click', closeBuckCheckPanel);
    $('bc-check-save')?.addEventListener('click', () => saveBuckCheck(false));
    document.getElementById('breedViewBucks')?.addEventListener('click', e => {
      const chk = e.target.closest?.('[data-buck-check]');
      if (chk) {
        e.preventDefault();
        openBuckCheckPanel(+chk.dataset.buckCheck);
        return;
      }
      const add = e.target.closest?.('[data-bg-add-batch]');
      if (add) openNewGroupForBatch(add.dataset.bgAddBatch);
    });
  }

  async function renderGroupCards() {
    const box = $('bg-groups');
    if (!box) return;
    const batches = parseSeasonBatches();
    let cards;
    try {
      cards = await gr().api('/Breeding/GroupCards');
    } catch {
      box.innerHTML = '<div class="empty">Could not load groups.</div>';
      return;
    }
    cards = cards || [];
    const byBatch = new Map();
    cards.forEach(c => {
      const k = c.birthBatchId ?? c.BirthBatchId ?? '';
      if (!byBatch.has(k)) byBatch.set(k, []);
      byBatch.get(k).push(c);
    });
    if (!batches.length) {
      box.innerHTML = cards.length
        ? cards.map(groupCardHtml).join('')
        : '<div class="sub muted">No season birth groups yet — add them on the Season plan tab.</div>';
      return;
    }
    let html = '';
    batches.forEach(batch => {
      const gs = (byBatch.get(batch.id) || []).slice()
        .sort((a, b) => (a.status === 'active' ? 0 : 1) - (b.status === 'active' ? 0 : 1));
      const n = gs.length;
      html += `<div class="breed-buck-season-h"><h3>${esc(batch.icon || '⭐')} ${esc(batch.name)}</h3><span class="breed-buck-season-line"></span><span class="sub muted small">${n} buck group${n === 1 ? '' : 's'}</span></div>`;
      if (!n) {
        html += `<div class="breed-buck-season-empty"><p class="muted">No bucks in this group yet.</p><button type="button" class="btn btn-green btn-sm" data-bg-add-batch="${esc(batch.id)}">＋ Add buck group</button></div>`;
      } else {
        html += gs.map(groupCardHtml).join('');
      }
    });
    const known = new Set(batches.map(b => b.id));
    const orphan = cards.filter(c => c.birthBatchId && !known.has(c.birthBatchId));
    if (orphan.length) {
      html += `<div class="breed-buck-season-h"><h3>Other</h3><span class="breed-buck-season-line"></span><span class="sub muted small">${orphan.length} buck group${orphan.length === 1 ? '' : 's'}</span></div>`;
      html += orphan.map(groupCardHtml).join('');
    }
    box.innerHTML = html;
  }

  async function onGroupAction(e) {
    const btn = e.target.closest?.('button');
    if (!btn || !btn.closest('#bg-groups')) return;
    const grApi = gr();
    if (btn.dataset.bgBuckin && !breedingCan('edit')) return;
    if (btn.dataset.bgClose && !breedingCan('edit')) return;
    if (btn.dataset.bgDel && !breedingCan('delete')) return;
    if (btn.dataset.bgRm && !breedingCan('edit')) return;
    try {
      if (btn.dataset.bgBuckin) {
        if (!await grApi.showConfirm('Put the buck in today and record a cross for every doe still waiting?')) return;
        let res;
        try {
          res = await grApi.api('/Breeding/PutBuckIn', { method: 'POST', body: JSON.stringify({ groupId: btn.dataset.bgBuckin, confirmWarnings: true }) });
        } catch (err) {
          if (err?.requiresConfirmation) {
            if (!await grApi.showConfirm((err.warnings || []).join('\n\n') + '\n\nContinue?')) return;
            res = await grApi.api('/Breeding/PutBuckIn', { method: 'POST', body: JSON.stringify({ groupId: btn.dataset.bgBuckin, confirmWarnings: true }) });
          } else throw err;
        }
        const ids = res?.crossedDoeIds || [];
        reload('Buck in recorded — does are waiting for scan', ids.length && breedingCan('delete')
          ? { kind: 'crosses', doeIds: ids }
          : null);
        return;
      }
      if (btn.dataset.bgClose) {
        const r = await grApi.showModal('Date buck was taken out', { prompt: true, defaultValue: today() });
        if (r == null) return;
        await grApi.api('/Breeding/TakeBuckOut', { method: 'POST', body: JSON.stringify({ groupId: btn.dataset.bgClose, endDate: r || today() }) });
        reload('Group closed', breedingCan('edit')
          ? { kind: 'reopen', groupId: btn.dataset.bgClose }
          : null);
        return;
      }
      if (btn.dataset.bgDel) {
        if (!await grApi.showConfirm('Delete this breeding group and clear crosses for does still awaiting scan?')) return;
        await grApi.api('/Breeding/DeleteGroup?id=' + encodeURIComponent(btn.dataset.bgDel), { method: 'DELETE' });
        reload('Group deleted');
        return;
      }
      if (btn.dataset.bgRm) {
        const [gid, goatId] = btn.dataset.bgRm.split('|');
        if (!await grApi.showConfirm('Remove this doe from the group and clear her cross?')) return;
        await grApi.api('/Breeding/RemoveGroupMember', { method: 'POST', body: JSON.stringify({ groupId: gid, goatId: +goatId }) });
        reload('Doe removed from group');
      }
    } catch { /* toast */ }
  }

  let scanSheet = null;
  let scanGroupKey = '';
  const scanPicks = new Map();

  const fmtScanDate = iso => {
    if (!iso) return '—';
    const d = new Date(iso + 'T12:00:00');
    return isNaN(d) ? iso : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
  };

  const birthBatches = () => scanSheet?.birthBatches || [];

  const defaultNextBatchId = doe => {
    const batches = birthBatches();
    if (!batches.length) return '';
    const name = (doe.birthBatchName || '').toLowerCase();
    const idx = batches.findIndex(b => (b.name || '').toLowerCase() === name);
    const next = batches[(idx < 0 ? 0 : idx + 1) % batches.length];
    return next?.id || batches[0].id;
  };

  const scanGroupsList = () => {
    const groups = [...(scanSheet?.groups || [])];
    if (scanSheet?.ungrouped?.length) {
      groups.push({ id: '_hand', name: 'Hand mated', buck: '—', buckStillWithDoes: false, does: scanSheet.ungrouped });
    }
    return groups;
  };

  const activeScanGroup = () => scanGroupsList().find(g => g.id === scanGroupKey);

  function openScanPanel(scroll) {
    const panel = $('scanPanel');
    if (!panel) return;
    panel.hidden = false;
    loadScanSheet().catch(() => {});
    if (scroll) panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function closeScanPanel() {
    $('scanPanel')?.setAttribute('hidden', '');
  }

  function picksForGroup(groupId) {
    const g = scanGroupsList().find(x => x.id === groupId);
    if (!g) return 0;
    return (g.does || []).filter(d => scanPicks.has(d.id)).length;
  }

  function setScanPick(id, patch) {
    const cur = scanPicks.get(id) || {};
    const next = { ...cur, ...patch };
    if (!next.r) scanPicks.delete(id);
    else scanPicks.set(id, next);
    return next;
  }

  async function loadScanSheet() {
    if (!$('scTable')) return;
    scanSheet = await gr().api('/Breeding/ScanSheet');
    const groups = scanGroupsList();
    const tabs = $('scGroups');
    if (!tabs) return;
    if (!groups.length) {
      tabs.replaceChildren();
      $('scTable').innerHTML = '<div class="empty" style="padding:16px">Nobody is waiting for a scan.</div>';
      updateScanUi();
      return;
    }
    if (!scanGroupKey || !groups.some(g => g.id === scanGroupKey)) scanGroupKey = groups[0].id;
    tabs.innerHTML = groups.map(g => {
      const done = picksForGroup(g.id);
      const total = g.does?.length || 0;
      const label = g.id === '_hand'
        ? 'Hand mated'
        : `${esc(g.buck || g.name)}${g.birthBatchIcon ? ' · ' + esc(g.birthBatchIcon) : ''}`;
      return `<button type="button" data-sc-group="${esc(g.id)}" class="${g.id === scanGroupKey ? 'on' : ''}" role="tab">${label}<span class="scan-tab-count">${done}/${total}</span></button>`;
    }).join('');
    tabs.querySelectorAll('[data-sc-group]').forEach(b => {
      b.addEventListener('click', () => {
        scanGroupKey = b.dataset.scGroup;
        tabs.querySelectorAll('button').forEach(x => x.classList.toggle('on', x.dataset.scGroup === scanGroupKey));
        renderScanTable();
      });
    });
    renderScanTable();
    const anyWaiting = scanGroupsList().some(g => (g.does || []).length > 0);
    const headPreg = $('scAllPregHead');
    if (headPreg) headPreg.hidden = !anyWaiting;
  }

  function activeScanDoes() {
    return activeScanGroup()?.does || [];
  }

  function renderDetailsCell(d, pick, buckStill) {
    if (!pick?.r) return '<span class="sub muted">← pick a result</span>';
    if (pick.r === 'preg') {
      const k = pick.kids ?? 2;
      const kidBtns = [1, 2, 3, 4].map(n =>
        `<button type="button" class="${k === n ? 'on' : ''}" data-sc-kpick="${d.id}" data-k="${n}">${n === 4 ? '4+' : n}</button>`
      ).join('');
      return `<span class="sub muted">Kids</span> <span class="scan-kp">${kidBtns}</span>
        <input type="number" class="scan-days-preg" data-sc-days="${d.id}" min="0" max="180" placeholder="days" title="Days pregnant (optional)" value="${esc(pick.daysPregnant || '')}">`;
    }
    if (pick.r === 'empty') {
      const nx = pick.emptyNext || '';
      const backOpt = buckStill ? `<option value="back"${nx === 'back' ? ' selected' : ''}>🐐 Back with buck</option>` : '';
      const batches = birthBatches();
      const batchOpts = batches.map(b =>
        `<option value="${esc(b.id)}"${b.id === (pick.emptyBatchId || defaultNextBatchId(d)) ? ' selected' : ''}>${esc(b.icon)} ${esc(b.name)}</option>`
      ).join('');
      return `<select class="scan-empty-next" data-sc-enext="${d.id}" required>
        <option value="" disabled${nx ? '' : ' selected'}>What next?</option>
        <option value="next"${nx === 'next' ? ' selected' : ''}>📅 Next birth group</option>
        <option value="heat"${nx === 'heat' ? ' selected' : ''}>👀 Watch for heat</option>
        <option value="cull"${nx === 'cull' ? ' selected' : ''}>🚩 Cull list</option>
        ${backOpt}
      </select>
      ${nx === 'next' && batches.length ? `<select class="scan-empty-batch" data-sc-ebatch="${d.id}">${batchOpts}</select>` : ''}`;
    }
    if (pick.r === 'unsure') {
      return `<span class="sub muted">Scan again in</span>
        <input type="number" class="scan-days-again" data-sc-again="${d.id}" min="1" max="90" value="${pick.again ?? 14}">
        <span class="sub muted">days</span>`;
    }
    return '';
  }

  function renderScanTable() {
    const box = $('scTable');
    if (!box) return;
    const find = normalizeTag($('scFind')?.value).toLowerCase();
    let does = activeScanDoes();
    if (find) does = does.filter(d => d.tag.toLowerCase().includes(find));
    if (!does.length) {
      box.innerHTML = '<div class="empty" style="padding:16px">No does waiting in this group.</div>';
      updateScanUi();
      return;
    }
    const buckStill = !!activeScanGroup()?.buckStillWithDoes;
    box.innerHTML = `<table class="tbl scan-sheet-tbl"><thead><tr>
      <th>Doe</th><th>Crossed</th><th>Result</th><th>Details</th></tr></thead><tbody>${
      does.map(d => {
        const pick = scanPicks.get(d.id);
        const hit = find && d.tag.toLowerCase().includes(find);
        const rowCls = `${pick?.r ? 'scan-r-' + pick.r : ''}${hit ? ' scan-hit' : ''}`;
        const seg = (code, icon, label) =>
          `<button type="button" class="${pick?.r === code ? 'on' : ''}" data-sc-res="${d.id}" data-r="${code}">${icon} ${label}</button>`;
        const rescan = d.awaitingRescan ? ' · rescan' : '';
        return `<tr class="${rowCls}" id="sc-row-${d.id}" data-sc-row="${d.id}">
          <td><b>${esc(d.tag)}</b><div class="sub muted">${esc(d.breed || '')} · ${esc(d.kiddingsText || (d.kiddings === 0 ? 'first time' : ''))}</div></td>
          <td class="small">${fmtScanDate(d.matedDate)}<div class="muted">${esc(d.buckTag || '')}${rescan}</div></td>
          <td><div class="scan-rseg">${seg('preg', '🤰', 'Pregnant')}${seg('empty', '⭕', 'Empty')}${seg('unsure', '❓', 'Not sure')}</div></td>
          <td class="scan-det">${renderDetailsCell(d, pick, buckStill)}</td>
        </tr>`;
      }).join('')
    }</tbody></table>`;

    const syncTabCounts = () => {
      $('scGroups')?.querySelectorAll('[data-sc-group]').forEach(tab => {
        const g = scanGroupsList().find(x => x.id === tab.dataset.scGroup);
        const count = tab.querySelector('.scan-tab-count');
        if (!g || !count) return;
        const done = (g.does || []).filter(x => scanPicks.has(x.id)).length;
        count.textContent = `${done}/${g.does?.length || 0}`;
      });
    };

    box.querySelectorAll('[data-sc-res]').forEach(b => {
      b.addEventListener('click', () => {
        const id = +b.dataset.scRes;
        const r = b.dataset.r;
        const cur = scanPicks.get(id);
        const doe = does.find(x => x.id === id);
        if (cur?.r === r) scanPicks.delete(id);
        else {
          scanPicks.set(id, {
            r,
            kids: r === 'preg' ? (cur?.kids ?? 2) : undefined,
            again: r === 'unsure' ? (cur?.again ?? 14) : undefined,
            emptyNext: r === 'empty' ? (cur?.emptyNext ?? '') : undefined,
            emptyBatchId: r === 'empty' ? (cur?.emptyBatchId ?? (doe ? defaultNextBatchId(doe) : '')) : undefined,
            daysPregnant: cur?.daysPregnant
          });
        }
        renderScanTable();
        syncTabCounts();
      });
    });
    box.querySelectorAll('[data-sc-kpick]').forEach(b => {
      b.addEventListener('click', () => { setScanPick(+b.dataset.scKpick, { kids: +b.dataset.k }); renderScanTable(); });
    });
    box.querySelectorAll('[data-sc-days]').forEach(inp => {
      inp.addEventListener('input', () => setScanPick(+inp.dataset.scDays, { daysPregnant: inp.value }));
    });
    box.querySelectorAll('[data-sc-enext]').forEach(sel => {
      sel.addEventListener('change', () => { setScanPick(+sel.dataset.scEnext, { emptyNext: sel.value }); renderScanTable(); });
    });
    box.querySelectorAll('[data-sc-ebatch]').forEach(sel => {
      sel.addEventListener('change', () => setScanPick(+sel.dataset.scEbatch, { emptyBatchId: sel.value }));
    });
    box.querySelectorAll('[data-sc-again]').forEach(inp => {
      inp.addEventListener('input', () => setScanPick(+inp.dataset.scAgain, { again: Math.max(1, +inp.value || 14) }));
    });
    updateScanUi();
  }

  function updateScanUi() {
    const list = activeScanDoes();
    const done = list.filter(d => scanPicks.has(d.id));
    const c = kind => done.filter(d => scanPicks.get(d.id)?.r === kind).length;
    const sum = $('scSum');
    if (sum) {
      sum.innerHTML = list.length
        ? `<b class="scan-sum-n">${done.length} of ${list.length}</b> recorded
          <span class="chip chip-preg">🤰 ${c('preg')} pregnant</span>
          <span class="chip scan-chip-empty">⭕ ${c('empty')} empty</span>
          <span class="chip scan-chip-unsure">❓ ${c('unsure')} not sure</span>`
        : '';
    }
    const save = $('scSave');
    const allPreg = $('scAllPreg');
    if (save) {
      save.disabled = done.length === 0;
      save.textContent = done.length ? `Save ${done.length} result${done.length > 1 ? 's' : ''}` : 'Save results';
    }
    const syncAllPreg = btn => {
      if (!btn) return;
      const show = list.length > 0;
      btn.hidden = !show;
      btn.disabled = !show || done.length >= list.length;
    };
    syncAllPreg(allPreg);
    syncAllPreg($('scAllPregHead'));
  }

  function markAllScanPregnant() {
    activeScanDoes().forEach(d => { if (!scanPicks.has(d.id)) scanPicks.set(d.id, { r: 'preg', kids: 2 }); });
    renderScanTable();
  }

  function wireScanPanel() {
    if (!$('scTable')) return;
    $('doesOpenScan')?.addEventListener('click', () => openScanPanel(true));
    $('scanClose')?.addEventListener('click', closeScanPanel);
    $('scanReload')?.addEventListener('click', () => { scanPicks.clear(); loadScanSheet().catch(() => {}); });
    $('scFind')?.addEventListener('input', () => renderScanTable());
    $('scFind')?.addEventListener('keydown', e => {
      if (e.key !== 'Enter') return;
      e.preventDefault();
      const q = normalizeTag($('scFind')?.value).toLowerCase();
      if (!q) return;
      for (const g of scanGroupsList()) {
        const hit = (g.does || []).find(d => d.tag.toLowerCase() === q || d.tag.toLowerCase().includes(q));
        if (!hit) continue;
        scanGroupKey = g.id;
        $('scFind').value = hit.tag;
        renderScanTable();
        document.getElementById('sc-row-' + hit.id)?.scrollIntoView({ block: 'center', behavior: 'smooth' });
        loadScanSheet().catch(() => {});
        return;
      }
      gr().showModal('No doe with tag <b>' + esc(q) + '</b> is waiting for a scan.');
    });
    $('scClear')?.addEventListener('click', () => { scanPicks.clear(); renderScanTable(); loadScanSheet().catch(() => {}); });
    $('scAllPreg')?.addEventListener('click', () => markAllScanPregnant());
    $('scAllPregHead')?.addEventListener('click', () => {
      openScanPanel(false);
      markAllScanPregnant();
    });
    $('scSave')?.addEventListener('click', async () => {
      const list = activeScanDoes().filter(d => scanPicks.has(d.id));
      if (!list.length) return;
      const scanDate = $('scDate')?.value || today();
      const missingNext = list.filter(d => {
        const p = scanPicks.get(d.id);
        return p?.r === 'empty' && !p.emptyNext;
      });
      if (missingNext.length) {
        await gr().showModal(
          'For each <b>empty</b> doe, choose the next step: next birth group, watch for heat, back with the buck, or cull list.'
        );
        return;
      }
      const results = list.map(d => {
        const p = scanPicks.get(d.id);
        if (p.r === 'unsure') {
          return { goatId: d.id, kidsCount: 0, notSure: true, rescanInDays: p.again ?? 14 };
        }
        if (p.r === 'empty') {
          return {
            goatId: d.id,
            kidsCount: 0,
            notSure: false,
            emptyNextStep: p.emptyNext,
            emptyBirthBatchId: p.emptyBatchId || defaultNextBatchId(d)
          };
        }
        return {
          goatId: d.id,
          kidsCount: p.kids ?? 2,
          notSure: false,
          daysPregnant: p.daysPregnant ? +p.daysPregnant : null
        };
      });
      try {
        const res = await gr().api('/Breeding/SaveScanBatch', {
          method: 'POST',
          body: JSON.stringify({ scanDate, results })
        });
        let msg = `Saved ${res.saved} scan(s)`;
        if (res.errors?.length) msg += ' — ' + res.errors.slice(0, 3).join('; ');
        scanPicks.clear();
        reload(msg);
      } catch { /* shown */ }
    });
  }

  const STAGE_OPTS = {
    plan: '<option value="">Change…</option><option value="cross">Record cross now</option><option value="replan">Change the date</option><option value="prep2">Mark ready for cross</option><option value="mistake">Remove the plan</option>',
    prep: '<option value="">Change…</option><option value="cross">Record cross</option><option value="notcross">Decided not to cross her</option><option value="mistake">Entered by mistake</option>',
    wait: '<option value="">Change…</option><option value="scan">Record scan result</option><option value="abort">Aborted / lost it</option><option value="mistake">Entered by mistake</option>',
    preg: '<option value="">Change…</option><option value="kidded">Record birth</option><option value="rescan">Change scan result</option><option value="abort">Aborted / lost it</option><option value="mistake">Entered by mistake</option>'
  };

  function wireStageSelects() {
    document.querySelectorAll('[data-bract]').forEach(sel => {
      const st = sel.dataset.brStage;
      sel.innerHTML = STAGE_OPTS[st] || STAGE_OPTS.wait;
      sel.onchange = () => onStageAction(sel);
    });
  }

  async function onStageAction(sel) {
    const v = sel.value;
    sel.value = '';
    if (!v) return;
    const id = +sel.dataset.bract;
    const stageKey = sel.dataset.brStage || '';
    const gc = window.GoatCard;
    if (!gc?.runBreedingStageAction) {
      await gr().showModal('Goat card scripts did not load — refresh the page.');
      return;
    }
    try {
      await gc.runBreedingStageAction(id, v, stageKey, {
        afterSuccess: () => reload('')
      });
    } catch { /* errors shown by goat-card */ }
  }

  let doeFlowFilter = null;
  let brKdFilter = 'all';
  let selectedDoeId = null;

  function highlightSelectedDoe(id) {
    document.querySelectorAll('#brRows tr[data-br-id], #heatRows tr[data-doe-profile]').forEach(tr => {
      const tid = +(tr.dataset.brId || tr.dataset.doeProfile || 0);
      tr.classList.toggle('breed-doe-sel', tid === id);
    });
  }

  function renderDoeProfileHtml(p) {
    const title = esc(p.tag) + (p.name ? ' · ' + esc(p.name) : '');
    const hist = (p.history || []).length
      ? '<ul class="breed-doe-hist">' + p.history.map(h =>
        `<li class="${esc(h.cssClass)}"><b>${esc(h.headline)}</b><span>${esc(h.detail)}</span></li>`).join('') + '</ul>'
      : '<p class="muted small" style="margin:0">Not crossed yet.</p>';
    const dueVal = p.dueRangeText ? esc(p.dueRangeText) : '—';
    const tagShort = esc(String(p.tag).slice(0, 13));
    const sireTag = esc(p.sireTag || '—');
    const damTag = esc(p.damTag || '—');
    const sirePed = esc(p.sirePedigree || '—');
    const damPed = esc(p.damPedigree || '—');
    return `<div class="breed-doe-profile">
      <div class="breed-doe-profile-head">
        <div class="breed-doe-avatar" aria-hidden="true">${esc(p.avatarText)}</div>
        <div>
          <h2 class="breed-doe-title">${title}</h2>
          <div class="muted small">${esc(p.breed || '')} · Female · ${esc(p.ageText)} · ${esc(p.birthBatchLabel || '—')}</div>
        </div>
        <div class="breed-doe-profile-actions">
          <span class="chip ${esc(p.flowStageCssClass)}">${esc(p.flowStageLabel)}</span>
          <button type="button" class="btn btn-green btn-sm" data-doe-status="${p.id}">Change status</button>
          <button type="button" class="btn btn-ghost btn-sm" data-goatcard="${p.id}">Goat card</button>
        </div>
      </div>
      <div class="breed-doe-stats">
        <div class="breed-doe-stat"><b class="breed-doe-stat-v">${p.kiddingsTotal}×</b>
          <div class="breed-doe-stat-l">Kidded (${p.kiddingsHere} here + ${p.kiddingsBeforePurchase} before purchase)</div></div>
        <div class="breed-doe-stat"><b class="breed-doe-stat-v">${p.kidsBorn}</b>
          <div class="breed-doe-stat-l">Kids born (${p.kidsAlive} alive)</div></div>
        <div class="breed-doe-stat"><b class="breed-doe-stat-v">${p.timesPregnant}×</b>
          <div class="breed-doe-stat-l">Times pregnant</div></div>
        <div class="breed-doe-stat"><b class="breed-doe-stat-v" style="font-size:20px">${dueVal}</b>
          <div class="breed-doe-stat-l">${esc(p.dueStatLabel)}</div></div>
      </div>
      <div class="breed-doe-mainherd">
        <span>📋 <b>Main herd shows:</b></span>
        <span class="chip ${esc(p.flowStageCssClass)}">${esc(p.flowStageLabel)}</span>
        <span class="muted">${esc(p.birthBatchLabel || '—')}</span>
        <span class="chip chip-kid">${esc(p.mainHerdKiddingText)}</span>
        <span class="muted" style="margin-left:auto">updates by itself when you change status here</span>
      </div>
      <div class="breed-doe-grid2">
        <div class="breed-doe-subpanel">
          <div class="breed-doe-subhead"><h3>Breeding history</h3><span class="muted small">every cross, never deleted</span></div>
          ${hist}
        </div>
        <div class="breed-doe-subpanel">
          <div class="breed-doe-subhead"><h3>Family</h3>
            <button type="button" class="btn btn-ghost btn-sm" style="margin-left:auto" data-goatcard="${p.id}">🌳 Full tree</button></div>
          <svg viewBox="0 0 320 190" width="100%" role="img" aria-label="Sire and dam">
            <rect x="6" y="70" width="108" height="50" rx="12" fill="#E6EFE8"/>
            <text x="60" y="91" text-anchor="middle" font-size="13" font-weight="700" fill="#1E4635">${tagShort}</text>
            <text x="60" y="108" text-anchor="middle" font-size="11" fill="#46504a">${p.kiddingsTotal}× · ${p.kidsBorn} kids</text>
            <path d="M114 95 C140 95 140 45 166 45 M114 95 C140 95 140 145 166 145" stroke="#D9D2C5" stroke-width="2" fill="none"/>
            <rect x="166" y="20" width="146" height="50" rx="12" fill="#fff" stroke="#E7E1D6"/>
            <text x="239" y="41" text-anchor="middle" font-size="12" font-weight="600" fill="#1B2420">Sire ${sireTag}</text>
            <text x="239" y="58" text-anchor="middle" font-size="11" fill="#7c847f">${sirePed}</text>
            <rect x="166" y="120" width="146" height="50" rx="12" fill="#fff" stroke="#E7E1D6"/>
            <text x="239" y="141" text-anchor="middle" font-size="12" font-weight="600" fill="#1B2420">Dam ${damTag}</text>
            <text x="239" y="158" text-anchor="middle" font-size="11" fill="#7c847f">${damPed}</text>
          </svg>
        </div>
      </div>
    </div>`;
  }

  async function loadDoeProfile(id) {
    if (!id) return;
    selectedDoeId = id;
    highlightSelectedDoe(id);
    const panel = $('doeProfilePanel');
    const mount = $('doeProfileMount');
    if (!panel || !mount) return;
    panel.hidden = false;
    mount.innerHTML = '<p class="muted small" style="margin:0">Loading…</p>';
    try {
      const p = await gr().api('/Breeding/DoeProfile?id=' + encodeURIComponent(id));
      mount.innerHTML = renderDoeProfileHtml(p);
      mount.querySelector('[data-doe-status]')?.addEventListener('click', async e => {
        e.preventDefault();
        const gc = window.GoatCard;
        if (!gc?.open) {
          await gr().showModal('Goat card did not load — refresh the page.');
          return;
        }
        await gc.open({ id: p.id });
        await gc.runAction?.('status');
      });
      setTimeout(() => panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' }), 30);
    } catch {
      mount.innerHTML = '<p class="empty">Could not load this doe.</p>';
    }
  }

  function wireDoeProfilePick() {
    document.addEventListener('click', e => {
      const root = e.target.closest('#breedViewDoes');
      if (!root) return;
      if (e.target.closest('select, [data-bract], button[data-heat]')) return;
      const pick = e.target.closest('[data-doe-profile]');
      if (!pick) return;
      const id = +pick.getAttribute('data-doe-profile');
      if (!id) return;
      e.preventDefault();
      e.stopPropagation();
      loadDoeProfile(id);
    }, true);
  }

  function syncDoeFlowUi() {
    document.querySelectorAll('.breed-fnode[data-flow]').forEach(btn => {
      const on = doeFlowFilter && btn.dataset.flow === doeFlowFilter;
      btn.classList.toggle('breed-on', !!on);
      btn.setAttribute('aria-pressed', on ? 'true' : 'false');
    });
  }

  function wireDoeFlow() {
    document.querySelectorAll('.breed-fnode[data-flow]').forEach(btn => {
      btn.addEventListener('click', () => {
        const key = btn.dataset.flow;
        doeFlowFilter = doeFlowFilter === key ? null : key;
        syncDoeFlowUi();
        filterPipeline();
      });
    });
  }

  function filterPipeline() {
    const f = $('brFilter')?.value || 'all';
    const rows = [...document.querySelectorAll('#brRows tr[data-br-stage]')];
    let n = 0;
    rows.forEach(tr => {
      const st = tr.dataset.brStage;
      const kd = +(tr.dataset.brKiddings || 0);
      let show = true;
      if (doeFlowFilter) show = tr.dataset.brFlow === doeFlowFilter;
      else if (f === 'plan') show = st === 'plan';
      else if (f === 'prep') show = st === 'prep';
      else if (f === 'wait') show = st === 'wait';
      else if (f === 'preg') show = st === 'preg';
      else if (f === 'due30') show = st === 'preg'; // rough; server already computed due
      else if (f === 'scan') show = st === 'wait';
      if (show && brKdFilter !== 'all') {
        if (brKdFilter === '5') show = kd >= 5;
        else show = kd === +brKdFilter;
      }
      if (show) n++;
      tr.style.display = show ? '' : 'none';
    });
    const pill = $('brCount');
    if (pill) pill.textContent = n + ' doe(s)';
  }

  function wireBrKdFilter() {
    document.querySelectorAll('#brKdFilter [data-br-kd]').forEach(chip => {
      chip.addEventListener('click', () => {
        brKdFilter = chip.dataset.brKd || 'all';
        document.querySelectorAll('#brKdFilter [data-br-kd]').forEach(c =>
          c.classList.toggle('breed-on', c.dataset.brKd === brKdFilter));
        filterPipeline();
      });
    });
  }

  function filterHeat() {
    const f = $('heatFilter')?.value || 'active';
    const rows = [...document.querySelectorAll('#heatRows tr[data-heat-row]')];
    let n = 0;
    rows.forEach(tr => {
      const can = tr.dataset.canCycle === '1';
      const times = +tr.dataset.times || 0;
      const inTxt = tr.dataset.in || '';
      let show = true;
      if (f === 'active') show = can;
      else if (f === 'due') show = can && (inTxt === 'now' || inTxt === 'overdue');
      else if (f === 'seen') show = times > 0;
      if (show) n++;
      tr.style.display = show ? '' : 'none';
    });
    const pill = $('heatCount');
    if (pill) pill.textContent = n + ' doe(s)';
  }

  async function lookupTag(inId, outId) {
    const val = normalizeTag($(inId)?.value);
    const out = $(outId);
    if (!out) return;
    if (!val) { out.textContent = ''; return; }
    try {
      const res = await fetch('/Breeding/LookupTag?tag=' + encodeURIComponent(val), { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
      if (!res.ok) {
        out.innerHTML = '<span style="color:#8a261c;font-weight:700">No goat with that tag</span>';
        return;
      }
      const g = await res.json();
      let html = `<span style="color:var(--green-dark);font-weight:700">✓ ${g.tag}${g.name ? ' · ' + g.name : ''} · ${g.status}${g.extra || ''}</span>`;
      if (inId === 'bc-tag' && $('bc-stage')?.value === 'cross') {
        const buck = $('bc-buck')?.value || '';
        const date = $('bc-date')?.value || today();
        const checkUrl = '/Breeding/CheckCross?tag=' + encodeURIComponent(val)
          + (buck ? '&buckTag=' + encodeURIComponent(buck) : '')
          + (date ? '&date=' + encodeURIComponent(date) : '');
        const checkRes = await fetch(checkUrl, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
        if (checkRes.ok) {
          const check = await checkRes.json();
          if (check.errors?.length) {
            html += `<div style="color:#8a261c;margin-top:4px">${check.errors.join(' ')}</div>`;
          }
          if (check.warnings?.length) {
            html += `<div style="color:var(--amber);margin-top:4px">⚠ ${check.warnings.join(' ')}</div>`;
          }
          if (check.notes?.length) {
            html += `<div style="color:var(--ink-soft);margin-top:4px;font-size:12px">${check.notes.join(' ')}</div>`;
          }
        }
      }
      out.innerHTML = html;
    } catch { out.textContent = ''; }
  }

  async function postSeason(body) {
    try {
      const res = await gr().api('/Breeding/UpdateSeason', { method: 'POST', body: JSON.stringify(body) });
      const batch = res?.newBatchId || res?.selectedBatchId || $('se-batch')?.value || '';
      const p = new URLSearchParams();
      if (currentView !== 'today') p.set('view', currentView);
      if (batch) p.set('batch', batch);
      location.href = '/Breeding' + (p.toString() ? '?' + p.toString() : '');
    } catch {
      gr().showToast?.('Could not save season settings.');
    }
  }

  let birthRequestId = null;
  let birthPastScore = null;
  let birthOpenTag = null;
  const mgSelected = new Set();

  const MOTH_PT = [
    [1, '1 — Poor'], [2, '2 — Weak'], [3, '3 — OK'], [4, '4 — Good'], [5, '5 — Excellent']
  ];
  const easePts = { Easy: 5, SlightHelp: 4, HardPull: 2, Vet: 1, Caesarean: 1, Normal: 5, Assisted: 4 };
  const healthPts = { Good: 5, Watch: 3, Sick: 1 };

  function fillPtSelect(id, val) {
    const sel = $(id);
    if (!sel) return;
    sel.innerHTML = MOTH_PT.map(([v, lab]) =>
      `<option value="${v}"${+val === v ? ' selected' : ''}>${lab}</option>`).join('');
  }

  function kbHealth() {
    return document.querySelector('#kb-health-seg .breed-seg-btn.breed-on')?.dataset.val || 'Good';
  }

  function kbKeep() {
    return document.querySelector('#kb-keep-seg .breed-seg-btn.breed-on')?.dataset.val || 'Keep';
  }

  function kbEase() {
    return $('kb-ease')?.value || 'Easy';
  }

  function kbLiveScore() {
    const moth = +$('kb-mothering')?.value || 3;
    const milk = +$('kb-milk')?.value || 3;
    const sc = (moth + milk + (easePts[kbEase()] || 3) + (healthPts[kbHealth()] || 3)) / 4;
    const full = Math.max(0, Math.min(5, Math.round(sc)));
  const stars = '★'.repeat(full) + '☆'.repeat(5 - full);
    return { sc, html: `<span class="breed-ms">${stars}</span> <b>${sc.toFixed(1)}</b>` };
  }

  function refreshBirthUi() {
    const health = kbHealth();
    $('kb-issues-wrap').hidden = health === 'Good';
    const keep = kbKeep();
    $('kb-why-wrap').hidden = keep === 'Keep';
    if ($('kb-mscore')) $('kb-mscore').innerHTML = kbLiveScore().html;
    const miss = [...$('kb-kids')?.querySelectorAll('tr') || []].filter(tr => !(+tr.querySelector('[data-k=weight]')?.value > 0)).length;
    const live = +$('kb-live')?.value || 0;
    const msg = $('kb-msg');
    const save = $('kb-save');
    if (msg) msg.textContent = live && miss ? `Add birth weight for ${miss} kid${miss > 1 ? 's' : ''}` : '';
    if (save) save.disabled = live > 0 && miss > 0;
  }

  function setKbSeg(group, val) {
    const seg = $(group);
    if (!seg) return;
    seg.querySelectorAll('.breed-seg-btn').forEach(b => {
      b.classList.toggle('breed-on', b.dataset.val === val);
    });
    if (group === 'kb-ease-seg') $('kb-ease').value = val;
    refreshBirthUi();
  }

  function renderKidRows() {
    const body = $('kb-kids');
    if (!body) return;
    const count = +$('kb-live').value || 0;
    body.replaceChildren();
    for (let i = 0; i < count; i++) {
      const tr = document.createElement('tr');
      tr.innerHTML = `<td>${i + 1}</td>
        <td><select data-k="gender"><option value="Female">Female</option><option value="Male">Male</option></select></td>
        <td><input data-k="tag" maxlength="50" autocomplete="off"></td>
        <td><input data-k="name" maxlength="100"></td>
        <td class="num-cell"><input data-k="weight" type="number" min="0" max="20" step="0.1" placeholder="kg"></td>
        <td><div class="breed-seg" data-k-row-col>
          <button type="button" class="breed-seg-btn breed-on" data-val="Yes">Yes</button>
          <button type="button" class="breed-seg-btn" data-val="No">No</button>
        </div></td>
        <td><div class="breed-seg" data-k-row-milk>
          ${['Mother', 'Powder', 'Mixed', 'Foster'].map(m =>
            `<button type="button" class="breed-seg-btn${m === 'Mother' ? ' breed-on' : ''}" data-val="${m}">${m}</button>`).join('')}
        </div></td>
        <td><div class="breed-seg" data-k-row-hl>
          ${['Good', 'Weak'].map(h =>
            `<button type="button" class="breed-seg-btn${h === 'Good' ? ' breed-on' : ''}" data-val="${h}">${h}</button>`).join('')}
        </div></td>`;
      tr.querySelectorAll('.breed-seg-btn').forEach(btn => {
        btn.addEventListener('click', () => {
          btn.parentElement.querySelectorAll('.breed-seg-btn').forEach(b => b.classList.toggle('breed-on', b === btn));
          refreshBirthUi();
        });
      });
      tr.querySelector('[data-k=weight]')?.addEventListener('input', refreshBirthUi);
      body.appendChild(tr);
    }
    refreshBirthUi();
  }

  function closeInlineBirth() {
    document.querySelectorAll('.breed-kform-row').forEach(r => r.remove());
    document.querySelectorAll('[data-kd-row]').forEach(r => {
      r.classList.remove('breed-kd-open');
      const b = r.querySelector('[data-birth-open]');
      if (b) {
        b.textContent = '🍼 Record kidding';
        b.classList.remove('breed-on');
      }
    });
    const panel = $('birthPanel');
    const slot = $('birthPanelSlot');
    if (panel && slot) {
      panel.classList.add('perm-hidden');
      panel.classList.remove('breed-kform-inline');
      slot.appendChild(panel);
      slot.hidden = true;
    }
    birthOpenTag = null;
    birthRequestId = null;
  }

  function toggleBirthInline(btn) {
    const tag = btn.dataset.birthOpen;
    if (birthOpenTag === tag) {
      closeInlineBirth();
      return;
    }
    closeInlineBirth();
    const row = btn.closest('[data-kd-row]');
    if (!row) return;
    const formRow = document.createElement('tr');
    formRow.className = 'breed-kform-row';
    const td = document.createElement('td');
    td.colSpan = 8;
    td.className = 'breed-kform-cell';
    formRow.appendChild(td);
    row.after(formRow);
    row.classList.add('breed-kd-open');
    const panel = $('birthPanel');
    panel.classList.remove('perm-hidden');
    panel.classList.add('breed-kform-inline');
    td.appendChild(panel);
    btn.textContent = 'Close';
    btn.classList.add('breed-on');
    birthOpenTag = tag;
    openBirth(tag, btn.dataset.birthKids, btn.dataset.birthKiddings, btn.dataset.birthOrdinal, btn.dataset.birthPastScore);
    formRow.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  function openBirth(tag, kids, kiddings, ordinal, pastScore) {
    birthRequestId = crypto.randomUUID ? crypto.randomUUID() : String(Date.now());
    $('kb-doe').value = tag || '';
    $('kb-date').value = today();
    $('kb-live').value = String(Math.min(Math.max(+kids || 1, 0), 6));
    $('kb-still').value = '0';
    $('kb-notes').value = '';
    fillPtSelect('kb-mothering', 3);
    fillPtSelect('kb-milk', 3);
    fillPtSelect('kb-bcs', 3);
    setKbSeg('kb-ease-seg', 'Easy');
    setKbSeg('kb-health-seg', 'Good');
    setKbSeg('kb-keep-seg', 'Keep');
    $('kb-why').value = '';
    document.querySelectorAll('#kb-issues .breed-ichip').forEach(b => b.classList.remove('breed-on'));
    const n = +kiddings || 0;
    birthPastScore = pastScore != null && pastScore !== '' ? +pastScore : null;
    const ordNum = parseInt(String(ordinal || ''), 10) || (n + 1);
    const ordSuffix = ordNum === 1 ? 'st' : ordNum === 2 ? 'nd' : ordNum === 3 ? 'rd' : 'th';
    const ordEl = $('kb-ordinal');
    if (ordEl) {
      const prior = birthPastScore != null && !Number.isNaN(birthPastScore)
        ? ` · past average ${birthPastScore.toFixed(1)}★` : '';
      ordEl.innerHTML = `<b>This is her ${ordNum}${ordSuffix} kidding</b>${n ? ` (${n}× before this farm)` : ' (first time here)'}${prior}`;
    }
    renderKidRows();
  }

  function kbIssues() {
    return [...document.querySelectorAll('#kb-issues .breed-ichip.breed-on')].map(b => b.dataset.iss);
  }

  function kidSegVal(tr, sel) {
    return tr.querySelector(`${sel} .breed-seg-btn.breed-on`)?.dataset.val;
  }

  function wireBirthForm() {
    ['kb-mothering', 'kb-milk', 'kb-bcs'].forEach(id => {
      $(id)?.addEventListener('change', refreshBirthUi);
    });
    document.querySelectorAll('[data-kb-seg]').forEach(seg => {
      seg.querySelectorAll('.breed-seg-btn').forEach(btn => {
        btn.addEventListener('click', () => {
          if (seg.id === 'kb-ease-seg') $('kb-ease').value = btn.dataset.val;
          seg.querySelectorAll('.breed-seg-btn').forEach(b => b.classList.toggle('breed-on', b === btn));
          refreshBirthUi();
        });
      });
    });
    document.querySelectorAll('#kb-issues .breed-ichip').forEach(btn => {
      btn.addEventListener('click', () => {
        btn.classList.toggle('breed-on');
      });
    });
  }

  async function patchKiddingDam(row, body) {
    const id = +row.dataset.kmId;
    try {
      const res = await gr().api('/Breeding/UpdateKiddingDam', {
        method: 'POST',
        body: JSON.stringify({ kiddingRecordId: id, ...body })
      });
      const sc = row.querySelector('.breed-km-score');
      if (sc && res?.motherScoreHtml) sc.innerHTML = res.motherScoreHtml;
      gr().showToast?.('Mother updated');
    } catch { /* shown */ }
  }

  function wireMotherCheck() {
    document.querySelectorAll('.breed-pt-sel[data-km-id]').forEach(sel => {
      sel.addEventListener('change', () => {
        const row = sel.closest('[data-km-row]');
        if (!row) return;
        patchKiddingDam(row, { [sel.dataset.kmField]: +sel.value });
      });
    });
    document.querySelectorAll('.breed-seg[data-km-field]').forEach(seg => {
      seg.querySelectorAll('.breed-seg-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
          const row = seg.closest('[data-km-row]');
          if (!row) return;
          const field = seg.dataset.kmField;
          const val = btn.dataset.val;
          seg.querySelectorAll('.breed-seg-btn').forEach(b => b.classList.toggle('breed-on', b === btn));
          const extra = {};
          if (field === 'keepDecision') {
            extra.keepReason = val === 'Keep' ? null : (val === 'Cull' ? 'Cull after weaning' : '');
          }
          patchKiddingDam(row, { [field]: val, ...extra });
        });
      });
    });
  }

  function syncMgBar() {
    const n = mgSelected.size;
    $('mgCount').textContent = `${n} selected`;
    $('mgMove').disabled = n === 0;
  }

  function filterMotherGroups() {
    const f = document.querySelector('.breed-kd-chip.breed-on[data-mg-filter]')?.dataset.mgFilter || 'all';
    document.querySelectorAll('#mgTable [data-mg-row]').forEach(tr => {
      const g = tr.dataset.mgGroup || '';
      const show = f === 'all' || (f === 'none' ? !g : g === f);
      tr.style.display = show ? '' : 'none';
    });
  }

  function wireMotherGroups() {
    document.querySelectorAll('#mgFilter [data-mg-filter]').forEach(chip => {
      chip.addEventListener('click', () => {
        document.querySelectorAll('#mgFilter [data-mg-filter]').forEach(c => c.classList.toggle('breed-on', c === chip));
        filterMotherGroups();
      });
    });
    document.querySelectorAll('.breed-mg-sel').forEach(cb => {
      cb.addEventListener('change', () => {
        const id = +cb.dataset.mgSel;
        if (cb.checked) mgSelected.add(id);
        else mgSelected.delete(id);
        syncMgBar();
      });
    });
    $('mgHead')?.addEventListener('change', () => {
      const on = !!$('mgHead').checked;
      document.querySelectorAll('.breed-mg-sel').forEach(cb => {
        const tr = cb.closest('[data-mg-row]');
        if (tr?.style.display === 'none') return;
        cb.checked = on;
        const id = +cb.dataset.mgSel;
        if (on) mgSelected.add(id);
        else mgSelected.delete(id);
      });
      syncMgBar();
    });
    $('mgClearSel')?.addEventListener('click', () => {
      mgSelected.clear();
      document.querySelectorAll('.breed-mg-sel').forEach(cb => { cb.checked = false; });
      if ($('mgHead')) $('mgHead').checked = false;
      syncMgBar();
    });
    $('mgMove')?.addEventListener('click', async () => {
      if (!mgSelected.size) return;
      const motherGroupId = $('mgMoveTo')?.value || null;
      try {
        const res = await gr().api('/Breeding/MoveMothersToGroup', {
          method: 'POST',
          body: JSON.stringify({ doeIds: [...mgSelected], motherGroupId })
        });
        reload(`${res?.count || mgSelected.size} mother(s) moved`);
      } catch { /* shown */ }
    });
    $('mgAuto')?.addEventListener('click', async () => {
      try {
        const res = await gr().api('/Breeding/AutoAssignMotherGroups', { method: 'POST', body: '{}' });
        reload(res?.count ? `${res.count} mothers grouped by last litter` : 'No ungrouped mothers to assign');
      } catch { /* shown */ }
    });
    $('mgPlus')?.addEventListener('click', () => {
      $('mgNewWrap').hidden = false;
      $('mgName')?.focus();
    });
    $('mgAdd')?.addEventListener('click', async () => {
      const name = ($('mgName')?.value || '').trim();
      if (!name) return;
      try {
        await gr().api('/Breeding/CreateMotherGroup', { method: 'POST', body: JSON.stringify({ name }) });
        reload(`Group <b>${name}</b> added`);
      } catch { /* shown */ }
    });
    document.querySelectorAll('.breed-mg-selone').forEach(sel => {
      sel.addEventListener('change', async () => {
        const id = +sel.dataset.mgDoe;
        const motherGroupId = sel.value || null;
        try {
          await gr().api('/Breeding/MoveMothersToGroup', {
            method: 'POST',
            body: JSON.stringify({ doeIds: [id], motherGroupId })
          });
          gr().showToast?.('Group updated');
        } catch { /* shown */ }
      });
    });
    filterMotherGroups();
    syncMgBar();
  }

  function bgDoeCheckboxes(visibleOnly = false) {
    return [...document.querySelectorAll('#bg-does [data-bgdoe]')].filter(cb => {
      if (!visibleOnly) return true;
      const lab = cb.closest('.breed-bg-doe');
      return lab && lab.style.display !== 'none';
    });
  }

  function updateBgCount() {
    const el = $('bg-count');
    if (!el) return;
    const list = bgDoeCheckboxes();
    const pickable = list.filter(c => !c.disabled);
    const max = Math.max(1, +($('bg-max')?.value || 15));
    const selected = list.filter(c => c.checked).length;
    const over = selected > max;
    let html = `${selected} selected · max ${max} per buck`;
    if (over) {
      html += ` — <span style="color:var(--red)">${selected - max} over your limit of ${max}</span> — allowed, but conception may drop`;
    } else if (selected > 0) {
      html += ` — <span style="color:var(--green-dark)">${Math.max(0, max - selected)} more can go with this buck</span>`;
    }
    html += ` · ${pickable.length} can pick now`;
    el.innerHTML = html;
    const meter = $('bg-meter');
    const fill = $('bg-meter-fill');
    if (meter && fill) {
      meter.hidden = false;
      const pct = Math.min(100, Math.round(selected / max * 100));
      fill.style.width = pct + '%';
      fill.style.background = over ? 'var(--red)' : pct >= 85 ? 'var(--amber)' : 'var(--green-dark)';
    }
  }

  function filterBgDoes() {
    const q = ($('bg-find')?.value || '').trim().toLowerCase();
    const readyOnly = !!$('bg-ready-only')?.checked;
    const mg = $('bg-mother-group')?.value || 'all';
    document.querySelectorAll('.breed-bg-doe').forEach(lab => {
      const tag = lab.dataset.bgTag || '';
      const ready = lab.dataset.bgReady === '1';
      const labMg = lab.dataset.bgMg || '';
      let show = (!q || tag.includes(q)) && (!readyOnly || ready);
      if (show && mg !== 'all') {
        if (mg === 'none') show = !labMg;
        else show = labMg === mg;
      }
      lab.style.display = show ? '' : 'none';
    });
  }

  function syncBuckOutDate() {
    const start = $('bg-date')?.value;
    const stay = Math.max(1, +($('bg-stay')?.value) || 42);
    if (!start || !$('bg-out')) return;
    const d = new Date(start + 'T12:00:00');
    d.setDate(d.getDate() + stay);
    $('bg-out').value = d.toISOString().slice(0, 10);
  }

  function renderBgDoes(candidates) {
    const box = $('bg-does');
    if (!box) return;
    if (!candidates?.length) {
      box.innerHTML = '<div class="empty">No does in the herd to show.</div>';
      updateBgCount();
      return;
    }
    box.innerHTML = candidates.map(g => {
      const can = g.canPick ?? g.CanPick;
      const block = g.blockReason ?? g.BlockReason ?? '';
      const w = g.latestWeightKg ?? g.LatestWeightKg;
      const ready = g.ready ?? g.Ready;
      const tag = g.tag ?? g.Tag;
      const breed = g.breed ?? g.Breed ?? '';
      const id = g.id ?? g.Id;
      const mgName = g.motherGroupName ?? g.MotherGroupName;
      const mgId = g.motherGroupId ?? g.MotherGroupId ?? '';
      const wTxt = w != null && w > 0 ? ` · ${w} kg` : '';
      const mgTxt = mgName ? ` · ${esc(mgName)}` : '';
      return `<label class="breed-bg-doe${can ? '' : ' breed-bg-doe-no'}" data-bg-ready="${ready ? '1' : '0'}" data-bg-tag="${String(tag).toLowerCase()}" data-bg-mg="${esc(mgId)}" data-bg-can="${can ? '1' : '0'}" data-bg-block="${esc(block)}">
        <input type="checkbox" data-bgdoe="${id}"${can ? '' : ' disabled'}>
        <b>${esc(tag)}</b>
        <span class="breed">${esc(breed)}${wTxt}${mgTxt}</span>
        ${ready && can ? '<span class="chip chip-kid">ready</span>' : ''}
        ${block ? `<span class="breed-bg-block">✕ ${esc(block)}</span>` : ''}
      </label>`;
    }).join('');
    bgDoeCheckboxes().forEach(c => c.addEventListener('change', updateBgCount));
    document.querySelectorAll('.breed-bg-doe-no').forEach(lab => {
      lab.addEventListener('click', ev => {
        if (ev.target.closest('input[type=checkbox]')) return;
        const why = lab.dataset.bgBlock;
        const tag = lab.querySelector('b')?.textContent || '';
        if (why) gr().showToast?.(`<b>${tag}</b> can't go in: ${why}`, 'error');
      });
    });
    filterBgDoes();
    updateBgCount();
  }

  async function refreshBgDoesForBuck() {
    const buck = normalizeTag($('bg-buck')?.value);
    try {
      const q = buck ? '?buckTag=' + encodeURIComponent(buck) : '';
      const list = await gr().api('/Breeding/GroupPicker' + q);
      renderBgDoes(list);
    } catch { /* shown */ }
  }

  function applyBreedingPermissions() {
    const canAdd = breedingCan('add');
    const canEdit = breedingCan('edit');
    const canDel = breedingCan('delete');
    if (!canAdd && !canEdit) {
      document.querySelectorAll(
        '#bgPanel input, #bgPanel select, #bgPanel button, #bgPanel textarea, .breed-se-edit input, .breed-se-edit select, .breed-se-edit button, [data-kk-wa], .breed-rep-tgt, [data-br-stage], [data-birth-open], #kb-save, #scSave, .breed-mg-selone'
      ).forEach(el => { el.disabled = true; });
      ['bg-create', 'bucksOpenCheck', 'bc-check-save', 'mgAdd', 'mgAuto', 'mgApply', 'se-np-save', 'se-del', 'se-copy', 'br-reconcile'].forEach(id => {
        const el = $(id);
        if (el) el.classList.add('perm-hidden');
      });
    } else {
      if (!canAdd) {
        ['bg-create'].forEach(id => $(id)?.classList.add('perm-hidden'));
      }
      if (!canEdit) {
        ['bc-check-save', 'mgApply', 'mgAuto', 'se-np-save', 'se-copy', 'br-reconcile'].forEach(id => $(id)?.classList.add('perm-hidden'));
        document.querySelectorAll('.breed-rep-tgt, [data-kk-wa]').forEach(el => { el.disabled = true; });
      }
      if (!canDel) {
        $('se-del')?.classList.add('perm-hidden');
      }
    }
  }

  function wireBgPanel() {
    if (!$('bg-does')) return;
  $('bg-date')?.addEventListener('change', syncBuckOutDate);
    $('bg-stay')?.addEventListener('input', syncBuckOutDate);
    syncBuckOutDate();
    $('bg-pick')?.addEventListener('click', () => {
      const n = +($('bg-n')?.value) || +($('bg-max')?.value) || 15;
      const list = bgDoeCheckboxes(true).filter(c => !c.disabled);
      bgDoeCheckboxes().forEach(c => { c.checked = false; });
      list.slice(0, n).forEach(c => { c.checked = true; });
      updateBgCount();
    });
    $('bg-max')?.addEventListener('input', updateBgCount);
    $('bg-buck')?.addEventListener('change', () => refreshBgDoesForBuck());
    bgDoeCheckboxes().forEach(c => c.addEventListener('change', updateBgCount));
    $('bg-find')?.addEventListener('input', filterBgDoes);
    $('bg-mother-group')?.addEventListener('change', filterBgDoes);
    $('bg-ready-only')?.addEventListener('change', filterBgDoes);
    $('bg-all-shown')?.addEventListener('click', () => {
      document.querySelectorAll('.breed-bg-doe').forEach(lab => {
        if (lab.style.display === 'none') return;
        const cb = lab.querySelector('[data-bgdoe]');
        if (cb) cb.checked = true;
      });
      updateBgCount();
    });
    $('bg-all-ready')?.addEventListener('click', () => {
      document.querySelectorAll('.breed-bg-doe').forEach(lab => {
        if (lab.style.display === 'none') return;
        const cb = lab.querySelector('[data-bgdoe]');
        if (lab.dataset.bgReady === '1' && cb) cb.checked = true;
      });
      updateBgCount();
    });
    $('bg-clear')?.addEventListener('click', () => {
      bgDoeCheckboxes().forEach(c => { c.checked = false; });
      updateBgCount();
    });
    $('bucksJumpCreate')?.addEventListener('click', () => openNewGroupForBatch($('bg-batch')?.value));
    updateBgCount();
    filterBgDoes();
  }

  function navigateReports({ batch, buck }) {
    const p = new URLSearchParams(location.search);
    p.set('view', 'reports');
    if (batch) p.set('batch', batch);
    if (buck && buck !== 'all') p.set('buck', buck);
    else p.delete('buck');
    location.href = '/Breeding?' + p.toString();
  }

  function wireReports() {
    $('breedRepSeason')?.addEventListener('click', e => {
      const btn = e.target.closest('[data-rep-batch]');
      if (!btn) return;
      navigateReports({ batch: btn.dataset.repBatch, buck: 'all' });
    });
    $('breedRepBuck')?.addEventListener('click', e => {
      const btn = e.target.closest('[data-rep-buck]');
      if (!btn) return;
      const batch = document.querySelector('#breedRepSeason .breed-seg-btn.on')?.dataset.repBatch
        || new URLSearchParams(location.search).get('batch')
        || $('se-batch')?.value;
      navigateReports({ batch, buck: btn.dataset.repBuck });
    });
    $('breedRepPrint')?.addEventListener('click', () => {
      document.body.classList.add('breed-printing');
      setTimeout(() => {
        window.print();
        document.body.classList.remove('breed-printing');
      }, 50);
    });
    document.querySelectorAll('.breed-rep-tgt').forEach(inp => {
      inp.addEventListener('change', async () => {
        const v = parseFloat(inp.value);
        if (!(v > 0)) return;
        const batch = $('se-batch')?.value;
        try {
          await gr().api('/Breeding/UpdateSeason', {
            method: 'POST',
            body: JSON.stringify({
              selectedBatchId: batch,
              reportTargetKey: inp.dataset.k,
              reportTargetValue: v
            })
          });
          gr().showToast?.('Target updated');
        } catch { /* shown */ }
      });
    });
  }

  function init() {
    if (!$('breedSubnav')) return;
    wireSubNav();
    wireReports();
    wireScanPanel();
    renderGroupCards().catch(() => {});
    wireStageSelects();
    wireDoeFlow();
    wireBrKdFilter();
    wireDoeProfilePick();
    wireKiddingFilters();
    wireKidsTable();
    $('brFilter')?.addEventListener('change', () => {
      doeFlowFilter = null;
      syncDoeFlowUi();
      filterPipeline();
    });
    $('heatFilter')?.addEventListener('change', filterHeat);
    filterPipeline();
    filterHeat();

    const tn = $('se-todayNote');
    if (tn) tn.textContent = 'counted from today, ' + new Date().toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });

    $('bc-stage')?.addEventListener('change', () => {
      const cross = $('bc-stage')?.value === 'cross';
      $('bc-datefield').style.display = cross ? '' : 'none';
      $('bc-buckfield').style.display = cross ? '' : 'none';
    });

    $('bc-tag')?.addEventListener('input', () => lookupTag('bc-tag', 'bc-found'));
    $('bc-buck')?.addEventListener('change', () => lookupTag('bc-tag', 'bc-found'));
    $('bc-date')?.addEventListener('change', () => lookupTag('bc-tag', 'bc-found'));

    $('addCross')?.addEventListener('click', async () => {
      const tag = normalizeTag($('bc-tag')?.value);
      if (!tag) { await gr().showModal('Scan or type a valid doe tag'); return; }
      const stage = $('bc-stage')?.value || 'prep';
      const buckTag = $('bc-buck')?.value?.trim() || null;
      if (stage === 'cross' && !buckTag) {
        await gr().showModal('Pick the buck for this cross.');
        return;
      }
      try {
        await gr().api('/Breeding/AddDoe', {
          method: 'POST',
          body: JSON.stringify({
            tag,
            stage,
            date: $('bc-date')?.value || today(),
            buckTag
          })
        });
        reload(stage === 'cross' ? 'Cross recorded' : 'Doe marked ready for cross');
      } catch { /* shown */ }
    });

    document.querySelectorAll('[data-heat]').forEach(b => {
      b.addEventListener('click', async () => {
        try {
          await gr().api('/Goat/RecordHeat', { method: 'POST', body: JSON.stringify({ goatId: +b.dataset.heat, date: today(), note: '' }) });
          reload('Heat recorded today');
        } catch { /* shown */ }
      });
    });

    const seasonFields = [
      ['se-build', 'buildUp'], ['se-check', 'buckCheckDays'], ['se-weigh', 'weighYoungDoesDays'],
      ['se-minkg', 'minYoungDoeKg'], ['se-scan', 'scanAfter'], ['se-dry', 'dryOff'],
      ['se-pens', 'kiddingPensDays'], ['se-drycross', 'dryBeforeCross'], ['se-wean', 'weanDays']
    ];
    seasonFields.forEach(([id, key]) => {
      $(id)?.addEventListener('change', e => {
        postSeason({ selectedBatchId: $('se-batch')?.value, [key]: +e.target.value || 0 }).catch(() => {});
      });
    });
    ['se-from', 'se-to'].forEach(id => {
      $(id)?.addEventListener('change', e => {
        const v = e.target.value;
        if (!v) return;
        const body = { selectedBatchId: $('se-batch')?.value };
        body[id === 'se-from' ? 'batchFrom' : 'batchTo'] = v.slice(5);
        postSeason(body).catch(() => {});
      });
    });

    $('se-batch')?.addEventListener('change', () => {
      const p = new URLSearchParams();
      if (currentView !== 'today') p.set('view', currentView);
      p.set('batch', $('se-batch').value);
      location.href = '/Breeding?' + p.toString();
    });
    $('se-rename')?.addEventListener('click', async () => {
      const n = await gr().showModal('Name this group', { prompt: true, defaultValue: $('se-batch')?.selectedOptions[0]?.text || '' });
      if (!n) return;
      await postSeason({ selectedBatchId: $('se-batch')?.value, batchName: n });
    });
    const seasonRules = () => {
      const el = document.getElementById('breed-season-rules');
      if (!el) return { build: 60, scan: 45, wean: 90, gest: 150 };
      return {
        build: +el.dataset.build || 60,
        scan: +el.dataset.scan || 45,
        wean: +el.dataset.wean || 90,
        gest: +el.dataset.gest || 150
      };
    };

    const addDaysIso = (iso, days) => {
      const d = new Date(iso + 'T12:00:00');
      d.setDate(d.getDate() + days);
      return d.toISOString().slice(0, 10);
    };

    const fmtShort = iso => {
      if (!iso) return '—';
      return new Date(iso + 'T12:00:00').toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
    };

    const calcNewBirthGroup = () => {
      const box = $('se-np-calc');
      const from = $('se-np-from')?.value;
      if (!box || !from) {
        if (box) box.innerHTML = '';
        return;
      }
      const rules = seasonRules();
      const cross = addDaysIso(from, -rules.gest);
      const feed = addDaysIso(cross, -rules.build);
      const scan = addDaysIso(cross, rules.scan);
      const wean = addDaysIso(from, rules.wean);
      const past = cross < today();
      box.innerHTML =
        `<div>Start extra feed<b>${fmtShort(feed)}</b></div>` +
        `<div>Cross the does<b>${fmtShort(cross)}</b></div>` +
        `<div>Scan<b>${fmtShort(scan)}</b></div>` +
        `<div>Wean<b>${fmtShort(wean)}</b></div>` +
        (past ? '<div class="breed-np-warn">Crossing date has passed<b>Will plan for next year</b></div>' : '');
    };

    const openNewBirthGroupPanel = () => {
      const panel = $('se-new-batch-panel');
      if (!panel) return;
      const yr = new Date().getFullYear();
      if ($('se-np-from')) $('se-np-from').value = yr + '-12-01';
      if ($('se-np-to')) $('se-np-to').value = yr + '-12-15';
      if ($('se-np-name')) $('se-np-name').value = '';
      if ($('se-np-copy') && $('se-batch')?.value) $('se-np-copy').value = $('se-batch').value;
      panel.removeAttribute('hidden');
      calcNewBirthGroup();
      panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };

    const closeNewBirthGroupPanel = () => {
      $('se-new-batch-panel')?.setAttribute('hidden', '');
    };

    const saveNewBirthGroup = async () => {
      const name = $('se-np-name')?.value?.trim();
      const from = $('se-np-from')?.value;
      const to = $('se-np-to')?.value;
      if (!from || !to) {
        await gr().showModal('Pick the birth window.');
        return;
      }
      if (to < from) {
        await gr().showModal('The window must end after it starts.');
        return;
      }
      await postSeason({
        selectedBatchId: $('se-np-copy')?.value || $('se-batch')?.value,
        copyFromBatchId: $('se-np-copy')?.value,
        newBatchName: name || 'New group',
        newBatchIcon: $('se-np-emo')?.value || '⭐',
        batchFrom: from.slice(5),
        batchTo: to.slice(5)
      });
    };

    ['se-add', 'se-add-chip'].forEach(id => $(id)?.addEventListener('click', openNewBirthGroupPanel));
    $('se-np-close')?.addEventListener('click', closeNewBirthGroupPanel);
    $('se-np-cancel')?.addEventListener('click', closeNewBirthGroupPanel);
    $('se-np-save')?.addEventListener('click', () => saveNewBirthGroup().catch(() => {}));
    ['se-np-from', 'se-np-to', 'se-np-copy'].forEach(id => {
      $(id)?.addEventListener('input', calcNewBirthGroup);
      $(id)?.addEventListener('change', calcNewBirthGroup);
    });
    $('se-copy')?.addEventListener('click', async () => {
      if (!await gr().showConfirm('Duplicate this birth group with the same birth window dates?')) return;
      await postSeason({ selectedBatchId: $('se-batch')?.value, duplicateBatch: true });
    });
    document.querySelectorAll('.breed-plan-tab').forEach(btn => {
      btn.addEventListener('click', () => {
        const id = btn.dataset.seBatch;
        if (!id || !$('se-batch')) return;
        $('se-batch').value = id;
        $('se-batch').dispatchEvent(new Event('change'));
      });
    });
    document.querySelectorAll('.breed-year-row').forEach(row => {
      row.addEventListener('click', () => {
        const id = row.dataset.seOpen;
        if (!id || !$('se-batch')) return;
        $('se-batch').value = id;
        $('se-batch').dispatchEvent(new Event('change'));
      });
    });
    $('planToBucks')?.addEventListener('click', () => setBreedingView('bucks'));
    $('se-del')?.addEventListener('click', async () => {
      const sel = $('se-batch');
      const batchCount = sel?.options.length || 0;
      if (batchCount < 2) {
        await gr().showModal('You need at least one group.');
        return;
      }
      const name = sel?.selectedOptions[0]?.text?.trim() || 'this group';
      const esc = s => String(s ?? '').replace(/[&<>"']/g, c =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
      const countMatch = ($('se-count')?.textContent || '').match(/^(\d+)\s+doe\(s\)\s+in this group/);
      const inGroup = countMatch ? +countMatch[1] : 0;
      let msg = `Remove the group <b>${esc(name)}</b>?`;
      if (inGroup) {
        msg += `<br><br>${inGroup} doe(s) are in it — they will simply have no group.`;
      }
      if (!await gr().showConfirm(msg, { html: true })) return;
      await postSeason({ selectedBatchId: sel?.value, removeBatch: true });
    });

    $('br-reconcile')?.addEventListener('click', async () => {
      try {
        const res = await gr().api('/Breeding/ReconcilePregnancy', { method: 'POST', body: '{}' });
        reload(res?.message || 'Pregnancy status synced');
      } catch { /* shown */ }
    });

    $('kb-live')?.addEventListener('change', renderKidRows);
    $('kb-cancel')?.addEventListener('click', () => closeInlineBirth());
    $('kb-save')?.addEventListener('click', async () => {
      if (!birthRequestId) return;
      const doeTag = normalizeTag($('kb-doe')?.value);
      const kids = [...$('kb-kids').querySelectorAll('tr')].map(tr => ({
        gender: tr.querySelector('[data-k=gender]').value,
        tag: tr.querySelector('[data-k=tag]').value.trim() || null,
        name: tr.querySelector('[data-k=name]').value.trim() || null,
        weightKg: tr.querySelector('[data-k=weight]').value ? +tr.querySelector('[data-k=weight]').value : null,
        colostrumGiven: kidSegVal(tr, '[data-k-row-col]') !== 'No',
        milkType: kidSegVal(tr, '[data-k-row-milk]') || 'Mother',
        kidHealth: kidSegVal(tr, '[data-k-row-hl]') || 'Good'
      }));
      try {
        await gr().api('/Breeding/RecordBirth', {
          method: 'POST',
          body: JSON.stringify({
            clientRequestId: birthRequestId,
            doeTag,
            kiddingDate: $('kb-date').value,
            ease: kbEase(),
            stillborn: +$('kb-still').value || 0,
            notes: $('kb-notes')?.value.trim() || null,
            mothering: +$('kb-mothering')?.value || 3,
            milkProduction: +$('kb-milk')?.value || 3,
            bodyCondition: +$('kb-bcs')?.value || 3,
            damHealth: kbHealth(),
            damHealthIssues: kbIssues(),
            keepDecision: kbKeep(),
            keepReason: kbKeep() === 'Keep' ? null : ($('kb-why')?.value || null),
            kids
          })
        });
        reload('Kidding recorded');
      } catch { /* shown */ }
    });

    const slot = $('birthPanelSlot');
    const panel = $('birthPanel');
    if (slot && panel) {
      slot.appendChild(panel);
      slot.hidden = true;
    }

    wireBirthForm();
    wireMotherCheck();
    wireMotherGroups();
    fillPtSelect('kb-mothering', 3);
    fillPtSelect('kb-milk', 3);
    fillPtSelect('kb-bcs', 3);

    const params = new URLSearchParams(location.search);
    if (params.get('birth')) {
      setBreedingView('kidding');
      const openBtn = document.querySelector(`[data-birth-open="${params.get('birth')}"]`);
      if (openBtn) toggleBirthInline(openBtn);
    } else if (params.get('scan')) {
      setBreedingView('does');
      openScanPanel(false);
      const tag = params.get('scan');
      if (tag && tag !== '1' && $('scFind')) $('scFind').value = tag;
      loadScanSheet().then(() => renderScanTable()).catch(() => {});
    }

    document.querySelectorAll('[data-birth-open]').forEach(b => {
      b.addEventListener('click', () => toggleBirthInline(b));
    });

    wireBgPanel();
    wireBuckCheck();
    applyBreedingPermissions();

    $('bg-groups')?.addEventListener('click', onGroupAction);

    $('bg-create')?.addEventListener('click', async () => {
      if (!breedingCan('add')) return;
      const ids = [...document.querySelectorAll('[data-bgdoe]:checked')].map(c => +c.dataset.bgdoe);
      const buck = normalizeTag($('bg-buck')?.value);
      const startDate = $('bg-date')?.value;
      const defer = !!$('bg-defer')?.checked;
      if (!buck) { await gr().showModal('Enter the buck tag'); return; }
      if (!defer && !startDate) { await gr().showModal('Pick the buck-in date'); return; }
      if (!ids.length) { await gr().showModal('Tick at least one doe'); return; }
      const body = {
        birthBatchId: $('bg-batch')?.value || null,
        buckTag: buck,
        startDate: startDate || today(),
        stayDays: +($('bg-stay')?.value) || 42,
        deferCross: defer,
        max: +$('bg-max')?.value || 15,
        doeIds: ids
      };
      try {
        const res = await gr().api('/Breeding/CreateGroup', { method: 'POST', body: JSON.stringify(body) });
        const gid = res?.groupId;
        reload(defer ? 'Group saved — put the buck in when ready' : 'Breeding group created',
          gid && defer && breedingCan('delete') ? { kind: 'deleteGroup', groupId: gid } : null);
      } catch { /* shown */ }
    });

    if (currentView === 'bucks') renderGroupCards().catch(() => {});
  }

  return { init, openBirth };
})();
