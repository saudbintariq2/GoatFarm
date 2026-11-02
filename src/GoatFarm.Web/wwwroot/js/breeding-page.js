/* Breeding tab — layout and behaviour aligned with index_new.html */
window.BreedingPage = (() => {
  const gr = () => window.GoatRecords;
  const $ = id => document.getElementById(id);
  const today = () => new Date().toISOString().slice(0, 10);
  const normalizeTag = v => String(v ?? '').replace(/[\x00-\x1F\x7F]/g, '').trim();
  const batchParam = () => {
    const sel = $('se-batch');
    return sel?.value ? '?batch=' + encodeURIComponent(sel.value) : '';
  };
  const reload = msg => {
    sessionStorage.setItem('goatToast', msg);
    location.href = '/Breeding' + batchParam();
  };

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

  function filterPipeline() {
    const f = $('brFilter')?.value || 'all';
    const rows = [...document.querySelectorAll('#brRows tr[data-br-stage]')];
    let n = 0;
    rows.forEach(tr => {
      const st = tr.dataset.brStage;
      const mated = tr.dataset.brMated;
      const kids = tr.dataset.brKids;
      let show = true;
      if (f === 'plan') show = st === 'plan';
      else if (f === 'prep') show = st === 'prep';
      else if (f === 'wait') show = st === 'wait';
      else if (f === 'preg') show = st === 'preg';
      else if (f === 'due30') show = st === 'preg'; // rough; server already computed due
      else if (f === 'scan') show = st === 'wait';
      if (show) n++;
      tr.style.display = show ? '' : 'none';
    });
    const pill = $('brCount');
    if (pill) pill.textContent = n + ' doe(s)';
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
      out.innerHTML = `<span style="color:var(--green-dark);font-weight:700">✓ ${g.tag}${g.name ? ' · ' + g.name : ''} · ${g.status}${g.extra || ''}</span>`;
    } catch { out.textContent = ''; }
  }

  async function postSeason(body) {
    await gr().api('/Breeding/UpdateSeason', { method: 'POST', body: JSON.stringify(body) });
    location.href = '/Breeding?batch=' + encodeURIComponent($('se-batch')?.value || '');
  }

  let birthRequestId = null;

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
        <td class="num-cell"><input data-k="weight" type="number" min="0" max="20" step="0.1"></td>`;
      body.appendChild(tr);
    }
  }

  function openBirth(tag, kids) {
    birthRequestId = crypto.randomUUID ? crypto.randomUUID() : String(Date.now());
    $('kb-doe').value = tag || '';
    $('kb-date').value = today();
    $('kb-live').value = String(Math.min(Math.max(+kids || 1, 0), 6));
    $('kb-still').value = '0';
    renderKidRows();
    $('birthPanel')?.classList.remove('perm-hidden');
    $('birthPanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function bgDoeCheckboxes() {
    return [...document.querySelectorAll('#bg-does [data-bgdoe]')];
  }

  function updateBgCount() {
    const el = $('bg-count');
    if (!el) return;
    const list = bgDoeCheckboxes();
    const max = Math.max(1, +($('bg-max')?.value || 15));
    const selected = list.filter(c => c.checked).length;
    const over = selected > max;
    let html = `${selected} selected · max ${max} per buck`;
    if (over) {
      html += ` — <span style="color:var(--red)">only the first ${max} will be assigned; the other ${selected - max} stay available for another buck</span>`;
    }
    html += ` · ${list.length} eligible does free`;
    el.innerHTML = html;
  }

  function wireBgPanel() {
    if (!$('bg-does')) return;
    $('bg-pick')?.addEventListener('click', () => {
      const n = +($('bg-n')?.value) || +($('bg-max')?.value) || 15;
      const list = bgDoeCheckboxes();
      list.forEach(c => { c.checked = false; });
      list.slice(0, n).forEach(c => { c.checked = true; });
      updateBgCount();
    });
    $('bg-max')?.addEventListener('input', updateBgCount);
    bgDoeCheckboxes().forEach(c => c.addEventListener('change', updateBgCount));
    updateBgCount();
  }

  function init() {
    if (!$('brRows')) return;
    wireStageSelects();
    $('brFilter')?.addEventListener('change', filterPipeline);
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

    $('addCross')?.addEventListener('click', async () => {
      const tag = normalizeTag($('bc-tag')?.value);
      if (!tag) { await gr().showModal('Scan or type a valid doe tag'); return; }
      const stage = $('bc-stage')?.value || 'prep';
      try {
        await gr().api('/Breeding/AddDoe', {
          method: 'POST',
          body: JSON.stringify({
            tag,
            stage,
            date: $('bc-date')?.value || today(),
            buckTag: $('bc-buck')?.value || null
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
      ['se-build', 'buildUp'], ['se-scan', 'scanAfter'], ['se-dry', 'dryOff'],
      ['se-drycross', 'dryBeforeCross'], ['se-wean', 'weanDays'], ['se-weankg', 'weanKg']
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
      location.href = '/Breeding?batch=' + encodeURIComponent($('se-batch').value);
    });
    $('se-rename')?.addEventListener('click', async () => {
      const n = await gr().showModal('Name this group', { prompt: true, defaultValue: $('se-batch')?.selectedOptions[0]?.text || '' });
      if (!n) return;
      await postSeason({ selectedBatchId: $('se-batch')?.value, batchName: n });
    });
    $('se-add')?.addEventListener('click', async () => {
      const n = await gr().showModal('Name the new group', { prompt: true, defaultValue: 'Second group' });
      if (!n) return;
      await postSeason({ newBatchName: n });
    });
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

    $('kb-live')?.addEventListener('change', renderKidRows);
    $('kb-cancel')?.addEventListener('click', () => {
      birthRequestId = null;
      $('birthPanel')?.classList.add('perm-hidden');
    });
    $('kb-save')?.addEventListener('click', async () => {
      if (!birthRequestId) return;
      const doeTag = normalizeTag($('kb-doe')?.value);
      const kids = [...$('kb-kids').querySelectorAll('tr')].map(tr => ({
        gender: tr.querySelector('[data-k=gender]').value,
        tag: tr.querySelector('[data-k=tag]').value.trim() || null,
        name: tr.querySelector('[data-k=name]').value.trim() || null,
        weightKg: tr.querySelector('[data-k=weight]').value ? +tr.querySelector('[data-k=weight]').value : null
      }));
      try {
        await gr().api('/Breeding/RecordBirth', {
          method: 'POST',
          body: JSON.stringify({
            clientRequestId: birthRequestId,
            doeTag,
            kiddingDate: $('kb-date').value,
            ease: $('kb-ease').value,
            stillborn: +$('kb-still').value || 0,
            notes: $('kb-notes')?.value.trim() || null,
            kids
          })
        });
        reload('Birth recorded');
      } catch { /* shown */ }
    });

    const params = new URLSearchParams(location.search);
    if (params.get('birth')) openBirth(params.get('birth'), params.get('kids') || '1');

    wireBgPanel();

    $('bg-create')?.addEventListener('click', async () => {
      const ids = [...document.querySelectorAll('[data-bgdoe]:checked')].map(c => +c.dataset.bgdoe);
      if (!ids.length) { await gr().showModal('Tick at least one doe'); return; }
      try {
        await gr().api('/Breeding/CreateGroup', {
          method: 'POST',
          body: JSON.stringify({
            buckTag: $('bg-buck')?.value,
            startDate: $('bg-date')?.value || today(),
            max: +$('bg-max')?.value || 15,
            doeIds: ids
          })
        });
        reload('Breeding group created');
      } catch { /* shown */ }
    });
  }

  return { init, openBirth };
})();
