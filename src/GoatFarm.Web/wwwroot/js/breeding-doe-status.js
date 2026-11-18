/* Breeding › Does — V16 Change status modal */
window.BreedingDoeStatus = (() => {
  const gr = () => window.GoatRecords;
  const $ = id => document.getElementById(id);
  const today = () => new Date().toISOString().slice(0, 10);
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  const STATUS_OPTS = [
    ['getting_ready', '🌱', 'Getting ready', 'Plan her for a birth group'],
    ['in_heat', '🔥', 'In heat', 'Heat seen today'],
    ['crossed', '❤️', 'Crossed', 'With a buck'],
    ['waiting_scan', '⏳', 'Waiting scan', 'Buck taken out'],
    ['pregnant', '🤰', 'Pregnant', 'After ultrasound scan'],
    ['empty', '⭕', 'Empty', 'Scan not pregnant'],
    ['dry', '🌙', 'Dry', 'Milking stopped'],
    ['kidded', '🍼', 'Kidded', 'Record kidding'],
    ['lost_kids', '💔', 'Lost kids', 'Aborted / lost pregnancy'],
    ['cull_list', '🚩', 'Cull list', 'Plan to remove'],
    ['mistake', '↩️', 'Entered by mistake', 'Undo last cross']
  ];

  const CULL_REASONS = ['Empty twice', 'Poor mother', 'Hard births', 'Old age', 'Health', 'Other'];

  let state = null;

  const breedingCanEdit = () => {
    const p = window.FarmPermissions?.breeding ?? { view: true, edit: true };
    return !!p.edit && !!p.view;
  };

  const labelFor = key => STATUS_OPTS.find(s => s[0] === key)?.[2] || key;
  const pill = (key, label) => `<span class="chip">${esc(label || labelFor(key))}</span>`;

  function closeModal() {
    $('doeStatusModal')?.classList.remove('show');
    state = null;
  }

  function openModal() {
    $('doeStatusModal')?.classList.add('show');
  }

  function optHtml(options, selected) {
    return options.map(([v, l]) =>
      `<option value="${esc(v)}"${v === selected ? ' selected' : ''}>${esc(l)}</option>`).join('');
  }

  function batchOpts(batches, selected) {
    return (batches || []).map(b =>
      `<option value="${esc(b.id)}"${b.id === selected ? ' selected' : ''}>${esc(b.label)}</option>`).join('');
  }

  function buckOpts(bucks, selected) {
    const list = (bucks || []).map(t =>
      `<option value="${esc(t)}"${t === selected ? ' selected' : ''}>${esc(t)}</option>`).join('');
    return list || '<option value="">— no bucks —</option>';
  }

  function isOddMove(flow, target) {
    if (target === 'dry') return flow !== 'pregnant' && flow !== 'dry';
    if (target === 'kidded') return flow !== 'pregnant' && flow !== 'dry';
    return false;
  }

  async function open(goatId, preselect, bulkIds) {
    if (!breedingCanEdit()) {
      await gr()?.showModal?.('You do not have permission to change breeding status.');
      return;
    }
    if (bulkIds?.length) {
      state = { mode: 'bulk', ids: bulkIds, target: preselect || null, kids: 2, ctx: null };
      $('doeStTitle').textContent = `Change status — ${bulkIds.length} doe(s)`;
      drawStatus();
      openModal();
      return;
    }
  const id = +goatId;
    if (!id) return;
    try {
      const ctx = await gr().api('/Breeding/DoeStatusContext?id=' + encodeURIComponent(id));
      state = { mode: 'single', id, target: preselect || null, kids: ctx.kidsCount && ctx.kidsCount < 4 ? ctx.kidsCount : 2, ctx };
      $('doeStTitle').textContent = 'Change status — ' + ctx.tag;
      drawStatus();
      openModal();
    } catch {
      await gr()?.showModal?.('Could not load this doe.');
    }
  }

  function drawStatus() {
    if (!state) return;
    const cur = state.mode === 'bulk' ? null : state.ctx?.flowStageKey;
    const curLabel = state.mode === 'bulk' ? '—' : (state.ctx?.flowStageLabel || '');
    const target = state.target;

    if (state.mode === 'single') {
      const toLabel = target === 'mistake' ? 'Undo cross' : labelFor(target);
      $('doeStFrom').innerHTML = `Now: ${pill(cur, curLabel)}${
        target ? ` <span class="breed-st-arrow">→</span> ${target === 'mistake' ? '<span class="chip chip-dry">Undo cross</span>' : pill(target, toLabel)}` : ''
      }`;
    } else {
      $('doeStFrom').innerHTML = `<span class="muted small">${state.ids.length} does selected${target ? ' → ' + esc(labelFor(target)) : ''}</span>`;
    }

    const pick = $('doeStPick');
    const choices = STATUS_OPTS.filter(s => state.mode === 'bulk' ? s[0] !== 'mistake' && s[0] !== 'kidded' : s[0] !== cur);
    pick.innerHTML = choices.map(s =>
      `<button type="button" class="breed-st-opt${target === s[0] ? ' breed-st-opt-on' : ''}" data-st-target="${s[0]}">` +
      `<span class="breed-st-opt-ico" aria-hidden="true">${s[1]}</span><b>${esc(s[2])}</b><span>${esc(s[3])}</span></button>`
    ).join('');
    pick.querySelectorAll('[data-st-target]').forEach(b => {
      b.onclick = () => { state.target = b.dataset.stTarget; drawStatus(); };
    });

    const fields = $('doeStFields');
    let html = '';
    const t = today();
    const ctx = state.ctx || {};
    const flow = cur || '';

    if (state.mode === 'bulk') {
      if (target === 'dry') {
        html = `<div class="breed-st-field-row"><label>Dried off on</label><input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
      } else if (target) {
        html = `<div class="note">The same status will be applied to every selected doe that can accept it.</div>`;
      }
    } else switch (target) {
      case 'getting_ready':
        html = `<div class="breed-st-field-row">Plan her for <select id="doeStBatch">${batchOpts(ctx.birthBatches, ctx.birthBatchId)}</select></div>`;
        break;
      case 'in_heat':
        html = `<div class="breed-st-field-row">Seen in heat on <input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
        if (flow === 'crossed' || flow === 'waiting_scan') {
          html += `<label class="breed-st-check"><input type="checkbox" id="doeStCrossNotTaken"> Came back on heat — cross not taken?</label>`;
        }
        html += `<div class="breed-st-field-row">Cross her now with <select id="doeStBuck"><option value="">— not yet —</option>${buckOpts(ctx.buckTags, '')}</select></div>`;
        break;
      case 'crossed':
        html = `<div class="breed-st-field-row">Crossed with <select id="doeStBuck">${buckOpts(ctx.buckTags, ctx.buckTag || '')}</select> on <input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
        html += `<div class="breed-st-field-row">For birth group <select id="doeStBatch">${batchOpts(ctx.birthBatches, ctx.birthBatchId)}</select></div>`;
        break;
      case 'waiting_scan':
        html = `<div class="breed-st-field-row">Buck taken out on <input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
        break;
      case 'pregnant':
        html = `<div class="breed-st-field-row">Scanned on <input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
        html += `<div class="breed-st-lbl">Kids seen</div><div class="breed-st-numpick" id="doeStKidsPick">${
          [1, 2, 3, 4].map(n => `<button type="button" data-kids="${n}" class="${state.kids === n ? 'on' : ''}">${n === 4 ? '4+' : n}</button>`).join('')
        }</div>`;
        html += `<div class="breed-st-field-row">About <input type="number" class="breed-st-num" id="doeStDaysPreg" min="0" max="180" placeholder="—"> days pregnant <span class="muted small">(optional)</span></div>`;
        html += `<p class="muted small" style="margin:8px 0 0">Pregnant only after an ultrasound scan.</p>`;
        break;
      case 'empty':
        html = `<div class="breed-st-field-row">Scanned on <input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
        html += `<div class="breed-st-field-row">Next <select id="doeStEmptyNext">${optHtml([
          ['next', '📅 Next birth group'],
          ['heat', '👀 Watch for heat'],
          ['back', '🐐 Back with the buck'],
          ['cull', '🚩 Cull list']
        ], 'next')}</select> <select id="doeStBatch">${batchOpts(ctx.birthBatches, ctx.birthBatchId)}</select></div>`;
        break;
      case 'dry':
        html = `<div class="breed-st-field-row">Dried off on <input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
        if (flow !== 'pregnant') html += `<div class="note">She is not marked pregnant — are you sure?</div>`;
        break;
      case 'kidded':
        html = `<p class="muted small" style="margin:0">Opens the full <b>Record kidding</b> form on the Kidding tab.</p>`;
        if (flow !== 'pregnant' && flow !== 'dry') html += `<div class="note">She is not marked pregnant — are you sure?</div>`;
        break;
      case 'lost_kids':
        html = `<div class="breed-st-field-row">Lost the kids on <input type="date" class="breed-st-date" id="doeStDate" value="${t}" max="${t}"></div>`;
        html += `<div class="breed-st-field-row">Then plan her for <select id="doeStBatch">${batchOpts(ctx.birthBatches, ctx.birthBatchId)}</select></div>`;
        break;
      case 'cull_list':
        html = `<div class="breed-st-field-row">Reason <select id="doeStCull">${optHtml(CULL_REASONS.map(r => [r, r]), 'Other')}</select></div>`;
        break;
      case 'mistake':
        html = ctx.canUndoMistake
          ? `<p style="margin:0">This removes her last cross and puts her back to <b>Getting ready</b>.</p>`
          : `<div class="note">She has no cross to undo, or she already kidded from it.</div>`;
        break;
      default:
        break;
    }

    fields.innerHTML = html ? `<div class="breed-st-fields-inner">${html}</div>` : '';
    fields.querySelectorAll('#doeStKidsPick button').forEach(b => {
      b.onclick = () => { state.kids = +b.dataset.kids; drawStatus(); };
    });

    const save = $('doeStSave');
    let disabled = !target;
    if (target === 'mistake' && !ctx.canUndoMistake) disabled = true;
    if (state.mode === 'bulk' && !target) disabled = true;
    save.disabled = disabled;
  }

  function val(id) {
    const el = $(id);
    return el ? el.value : null;
  }

  function buildPayload(confirmWarnings) {
    const target = state.target;
    const date = val('doeStDate') || today();
    const body = {
      targetStatus: target,
      date,
      confirmWarnings: !!confirmWarnings
    };
    if (state.mode === 'single') body.goatId = state.id;
    if (target === 'getting_ready' || target === 'lost_kids' || target === 'empty') {
      body.birthBatchId = val('doeStBatch') || null;
    }
    if (target === 'crossed' || (target === 'in_heat' && val('doeStBuck'))) {
      body.buckTag = val('doeStBuck') || null;
    }
    if (target === 'in_heat') {
      body.crossNotTaken = !!$('doeStCrossNotTaken')?.checked;
      body.crossNowFromHeat = !!(val('doeStBuck'));
    }
    if (target === 'pregnant') {
      body.kidsCount = state.kids || 2;
      const dp = val('doeStDaysPreg');
      body.daysPregnant = dp ? +dp : null;
    }
    if (target === 'empty') body.emptyNextStep = val('doeStEmptyNext') || 'next';
    if (target === 'cull_list') body.cullReason = val('doeStCull') || 'Other';
    return body;
  }

  async function save(confirmWarnings) {
    if (!state?.target || !breedingCanEdit()) return;
    if (state.mode === 'single' && state.target === 'crossed' && !val('doeStBuck')) {
      await gr()?.showModal?.('Choose the buck.');
      return;
    }
    if (state.mode === 'bulk') {
      const body = {
        goatIds: state.ids,
        targetStatus: state.target,
        date: val('doeStDate') || today(),
        confirmWarnings: !!confirmWarnings
      };
      try {
        const res = await gr().api('/Breeding/ChangeDoeStatusBulk', { method: 'POST', body: JSON.stringify(body) });
        closeModal();
        const msg = res.message || `Updated ${res.saved} doe(s)`;
        if (window.BreedingPage?.reload) window.BreedingPage.reload(msg);
        else location.reload();
      } catch (e) {
        const err = e?.error || e?.message;
        if (err && String(err).toLowerCase().includes('confirm')) {
          if (await gr().showConfirm(err + '\n\nSave anyway?')) await save(true);
        }
      }
      return;
    }

    if (state.target === 'kidded') {
      closeModal();
      const tag = state.ctx?.tag;
      if (window.BreedingPage?.openKiddingForDoe) {
        window.BreedingPage.openKiddingForDoe(state.id, tag);
        return;
      }
      const url = new URL(location.href);
      url.searchParams.set('view', 'kidding');
      if (tag) url.searchParams.set('birth', tag);
      location.href = url.pathname + url.search;
      return;
    }

    const body = buildPayload(confirmWarnings);
    try {
      const res = await gr().api('/Breeding/ChangeDoeStatus', { method: 'POST', body: JSON.stringify(body) });
      closeModal();
      const msg = (res.message || 'Saved') + ' · Main herd updated';
      if (window.BreedingPage?.reload) window.BreedingPage.reload(msg);
      else location.reload();
    } catch (e) {
      const err = e?.error || e?.message || 'Could not save';
      if (!confirmWarnings && (String(err).toLowerCase().includes('confirm') || isOddMove(state.ctx?.flowStageKey, state.target))) {
        if (await gr().showConfirm(err + '\n\nSave anyway?')) await save(true);
        return;
      }
      await gr()?.showModal?.(err);
    }
  }

  function fillRowSelects() {
    document.querySelectorAll('[data-br-chg-status]').forEach(sel => {
      const flow = sel.dataset.brFlow || '';
      const opts = STATUS_OPTS.filter(s => s[0] !== flow).map(s =>
        `<option value="${s[0]}">${s[1]} ${esc(s[2])}</option>`).join('');
      sel.innerHTML = `<option value="">Change status…</option>${opts}`;
    });
  }

  function wire() {
    $('doeStSave')?.addEventListener('click', () => save(false));
    document.querySelectorAll('[data-doe-st-close]').forEach(b => b.addEventListener('click', closeModal));
    $('doeStatusModal')?.addEventListener('click', e => {
      if (e.target.id === 'doeStatusModal') closeModal();
    });
    document.addEventListener('keydown', e => {
      if (e.key === 'Escape' && $('doeStatusModal')?.classList.contains('show')) closeModal();
    });

    document.addEventListener('click', e => {
      const btn = e.target.closest?.('[data-doe-status]');
      if (btn) {
        e.preventDefault();
        e.stopPropagation();
        open(+btn.dataset.doeStatus);
        return;
      }
    });

    document.addEventListener('change', e => {
      const sel = e.target.closest?.('[data-br-chg-status]');
      if (!sel) return;
      const v = sel.value;
      sel.selectedIndex = 0;
      if (!v) return;
      e.stopPropagation();
      open(+sel.dataset.brChgStatus, v);
    }, true);

    fillRowSelects();
  }

  wire();

  return { open, close: closeModal, refreshRowSelects: fillRowSelects };
})();