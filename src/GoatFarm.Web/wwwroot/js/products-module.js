(() => {
  const page = (() => {
    const el = document.getElementById('products-page-json');
    if (!el) return {};
    try { return JSON.parse(el.textContent || '{}'); } catch { return {}; }
  })();

  const N = v => { const n = Number(v); return Number.isFinite(n) ? n : 0; };
  const fmtN = (n, d) => {
    const v = N(n);
    const dd = d == null ? (Math.abs(v - Math.round(v)) < 0.005 ? 0 : 1) : d;
    return v.toLocaleString('en-US', { minimumFractionDigits: dd, maximumFractionDigits: dd });
  };
  const fmtQ = (q, unit) => `${fmtN(q)} ${unit || ''}`.trim();
  const rs = n => 'Rs ' + Math.round(N(n)).toLocaleString('en-US');
  const money = v => rs(v);
  const pct = (a, b) => b > 0.0005 ? fmtN(a / b * 100, 1) + '%' : '—';
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const today = () => new Date().toISOString().slice(0, 10);
  const milk = page.milk || {};
  const meat = page.meat || {};
  let customSessions = [...(page.customMilkingSessions || [])];
  const reload = () => location.reload();
  const post = (url, body) => fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  async function postJson(url, body) {
    const res = await post(url, body);
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.message || data.title || 'Save failed');
    return data;
  }

  const revEl = document.getElementById('productRevenueMonth');
  if (revEl) revEl.textContent = rs(page.monthSalesTotal);
  const tankEl = document.getElementById('productStockCount');
  if (tankEl) tankEl.textContent = `${Math.round(page.milkInTanksLiters || 0).toLocaleString('en-US')} L`;

  function renderMilkPane() {
    const raw = N(milk.rawTankLiters);
    const past = N(milk.pastTankLiters);
    const todayL = N(milk.milkedTodayLiters);
    const monthL = N(milk.milkedMonthLiters);
    const soldL = N(milk.bulkSoldMonthLiters);
    const pzN = N(milk.pasteurizeRunsMonth);
    const pzIn = N(milk.pasteurizeInMonth);
    const pzOut = N(milk.pasteurizeOutMonth);

    const k = document.getElementById('sl-milk-kpis');
    if (k) {
      k.innerHTML = `<div class="sl-kpi"><span>Raw milk in tanks</span><b>${fmtQ(raw, 'L')}</b><small>chilled, not pasteurized</small></div>
        <div class="sl-kpi"><span>Pasteurized in tanks</span><b>${fmtQ(past, 'L')}</b><small>chilled</small></div>
        <div class="sl-kpi"><span>Milked today</span><b>${fmtQ(todayL, 'L')}</b><small>${fmtQ(monthL, 'L')} this month</small></div>
        <div class="sl-kpi"><span>Bulk milk sold</span><b>${fmtQ(soldL, 'L')}</b><small>this month</small></div>`;
    }

    const rawT = (page.warehouses || []).filter(w => w.kind === 'raw_tank');
    const pastT = (page.warehouses || []).filter(w => w.kind === 'past_tank');
    const card = (w, item) => {
      const q = N(w.litersOrKg);
      const cap = N(w.capacity);
      const fill = cap > 0 ? Math.min(100, Math.max(0, q / cap * 100)) : 0;
      const isRaw = item === 'raw';
      return `<div class="sl-tank ${isRaw ? 'raw' : 'past'}"><div class="sl-tank-top"><b>${esc(w.name)}</b>${w.targetTempC != null ? `<span class="sl-temp">${esc(w.targetTempC)}°C</span>` : ''}</div>
        <div class="sl-tank-q">${fmtN(q)} <small>L</small></div>
        ${cap > 0 ? `<div class="sl-bar" role="img" aria-label="${fmtN(fill, 0)}% full"><i style="width:${fill}%"></i></div><small>${fmtN(fill, 0)}% of ${fmtN(cap)} L</small>` : ''}
        <div class="sl-tank-act">${isRaw ? `<button type="button" class="btn btn-sm btn-green" data-sl-sub="pasteurize" data-sl-src="${w.id}">Pasteurize</button>` : ''}<button type="button" class="btn btn-sm btn-ghost" data-sl-sub="make" data-sl-src="${w.id}">Bottle</button><button type="button" class="btn btn-sm btn-ghost" data-sl-sub="move" data-sl-src="${w.id}">Move</button><button type="button" class="btn btn-sm btn-ghost" data-sl-sell="${isRaw ? 'raw' : 'past'}" data-sl-src="${w.id}">Sell</button></div></div>`;
    };

    const dairy = milk.dairyProducts || [];
    const f = document.getElementById('sl-milk-flow');
    if (f) {
      f.innerHTML = `
        <div class="sl-stage"><div class="sl-stage-h"><span>1</span>Milking</div><div class="sl-stage-b"><div class="sl-big">${fmtN(todayL)} <small>L today</small></div><small>${fmtN(monthL)} L this month</small><button type="button" class="btn btn-sm btn-green" data-sl-sub="milking">+ Record milking</button></div></div>
        <div class="sl-flow-arrow" aria-hidden="true">➜</div>
        <div class="sl-stage"><div class="sl-stage-h"><span>2</span>Raw chilled tank${rawT.length > 1 ? 's' : ''}</div><div class="sl-stage-b">${rawT.map(w => card(w, 'raw')).join('') || '<small>No raw milk tank yet.</small>'}</div></div>
        <div class="sl-flow-arrow" aria-hidden="true">➜</div>
        <div class="sl-stage"><div class="sl-stage-h"><span>3</span>Pasteurizer</div><div class="sl-stage-b"><div class="sl-big">${pzN} <small>run${pzN === 1 ? '' : 's'} this month</small></div><small>${fmtN(pzIn)} L in → ${fmtN(pzOut)} L out</small>${pzIn > 0 ? `<small>loss ${fmtN(pzIn - pzOut)} L (${pct(pzIn - pzOut, pzIn)})</small>` : ''}<button type="button" class="btn btn-sm btn-green" data-sl-sub="pasteurize">Pasteurize</button></div></div>
        <div class="sl-flow-arrow" aria-hidden="true">➜</div>
        <div class="sl-stage"><div class="sl-stage-h"><span>4</span>Pasteurized chilled tank${pastT.length > 1 ? 's' : ''}</div><div class="sl-stage-b">${pastT.map(w => card(w, 'past')).join('') || '<small>No pasteurized tank yet.</small>'}</div></div>
        <div class="sl-flow-arrow" aria-hidden="true">➜</div>
        <div class="sl-stage"><div class="sl-stage-h"><span>5</span>Bottles &amp; products</div><div class="sl-stage-b">${dairy.length ? '<ul class="sl-list">' + dairy.slice(0, 6).map(d => `<li><span>${esc(d.name)}</span><b>${fmtN(d.qty)} ${esc(d.unit)}</b></li>`).join('') + '</ul>' : '<small>Nothing bottled yet.</small>'}<button type="button" class="btn btn-sm btn-green" data-sl-sub="make">Bottle / make</button></div></div>`;
    }
  }

  function renderMeatPane() {
    const wholeN = N(meat.wholeCount);
    const wholeKg = N(meat.wholeKg);
    const halfN = N(meat.halfCount);
    const halfKg = N(meat.halfKg);
    const cutsKg = N(meat.cutsKg);
    const cutsLines = N(meat.cutsLines);
    const slM = N(meat.slaughteredMonth);
    const slKg = N(meat.slaughteredCarcassKgMonth);

    const k = document.getElementById('sl-meat-kpis');
    if (k) {
      k.innerHTML = `<div class="sl-kpi"><span>Whole carcasses</span><b>${fmtN(wholeN, 0)}</b><small>${fmtQ(wholeKg, 'kg')}</small></div>
        <div class="sl-kpi"><span>Half carcasses</span><b>${fmtN(halfN, 0)}</b><small>${fmtQ(halfKg, 'kg')}</small></div>
        <div class="sl-kpi"><span>Cuts &amp; pieces</span><b>${fmtQ(cutsKg, 'kg')}</b><small>${cutsLines} line${cutsLines === 1 ? '' : 's'} in stock</small></div>
        <div class="sl-kpi"><span>Slaughtered this month</span><b>${slM}</b><small>${fmtQ(slKg, 'kg')} carcass</small></div>`;
    }

    const f = document.getElementById('sl-meat-flow');
    if (f) {
      f.innerHTML = `<div class="sl-stage"><div class="sl-stage-h"><span>1</span>Live goat</div><div class="sl-stage-b"><small>From the Herd. Slaughter removes the goat from the live herd.</small><button type="button" class="btn btn-sm btn-green" data-sl-open="slaughter">+ Slaughter / carcass in</button></div></div>
        <div class="sl-flow-arrow" aria-hidden="true">➜</div>
        <div class="sl-stage"><div class="sl-stage-h"><span>2</span>Carcass stock</div><div class="sl-stage-b"><div class="sl-big">${fmtN(wholeN, 0)} <small>whole</small> · ${fmtN(halfN, 0)} <small>half</small></div><small>${fmtQ(wholeKg + halfKg, 'kg')} in chiller/freezer</small><div class="sl-tank-act"><button type="button" class="btn btn-sm btn-ghost" data-sl-open="split">Split into halves</button><button type="button" class="btn btn-sm btn-ghost" data-sl-sell="whole">Sell carcass</button></div></div></div>
        <div class="sl-flow-arrow" aria-hidden="true">➜</div>
        <div class="sl-stage"><div class="sl-stage-h"><span>3</span>Cutting</div><div class="sl-stage-b"><small>Weigh every cut. Bone, trim and waste are recorded so the kilos always balance.</small><button type="button" class="btn btn-sm btn-green" data-sl-open="cut">Cut into pieces</button></div></div>
        <div class="sl-flow-arrow" aria-hidden="true">➜</div>
        <div class="sl-stage"><div class="sl-stage-h"><span>4</span>Cuts in stock</div><div class="sl-stage-b"><small>No cuts in stock.</small><button type="button" class="btn btn-sm btn-ghost" data-sl-sell="cut">Sell cuts</button></div></div>`;
    }
  }

  function activateProductTab(tab) {
    if (!tab) tab = 'milk';
    document.querySelectorAll('#productTabs .prod-tab').forEach(t => {
      t.classList.toggle('active', t.dataset.ptab === tab);
    });
    document.querySelectorAll('#view-milk .prod-pane').forEach(p => {
      p.classList.toggle('active', p.dataset.ppane === tab);
    });
    if (tab === 'milk') {
      renderMilkPane();
      if (typeof renderMilking === 'function') renderMilking();
    } else if (tab === 'meat') renderMeatPane();
  }

  /* Main product tabs */
  document.querySelectorAll('#productTabs .prod-tab').forEach(btn => {
    btn.addEventListener('click', () => activateProductTab(btn.dataset.ptab));
  });

  document.getElementById('view-milk')?.addEventListener('click', e => {
    const subBtn = e.target.closest('[data-sl-sub]');
    if (subBtn?.dataset.slSub) {
      const sub = subBtn.dataset.slSub;
      const src = subBtn.dataset.slSrc;
      setMilkSub(sub);
      document.querySelector('.prod-tab[data-ptab="milk"]')?.click();
      if (sub === 'pasteurize') renderPasteurize(src, null);
      if (sub === 'move') renderMove(src || null);
      if (sub === 'make') renderMake(src || null);
      return;
    }
    const spoilBtn = e.target.closest('[data-sl-spoil]');
    if (spoilBtn?.dataset.slSpoil) {
      setMilkSub('spoil');
      document.querySelector('.prod-tab[data-ptab="milk"]')?.click();
      renderSpoil(spoilBtn.dataset.slSpoil);
      return;
    }
    if (e.target.closest('[data-sl-sell]')) {
      document.querySelector('.prod-tab[data-ptab="sales"]')?.click();
      return;
    }
    const open = e.target.closest('[data-sl-open]');
    if (open?.dataset.slOpen) {
      document.querySelector('.prod-tab[data-ptab="meat"]')?.click();
      setMeatForm(open.dataset.slOpen);
    }
  });

  /* Milk sub-navigation */
  const milkSubs = ['milking', 'pasteurize', 'move', 'make', 'spoil', 'tanks', 'records'];
  function setMilkSub(name) {
    if (!milkSubs.includes(name)) name = 'milking';
    document.querySelectorAll('#view-milk .sl-subnav [data-sl-sub]').forEach(b => {
      const on = b.dataset.slSub === name;
      b.classList.toggle('active', on);
      b.setAttribute('aria-selected', on ? 'true' : 'false');
    });
    document.querySelectorAll('#view-milk [data-sl-sub-pane]').forEach(p => { p.hidden = p.dataset.slSubPane !== name; });
    if (name === 'milking') renderMilking();
    if (name === 'pasteurize') renderPasteurize();
    if (name === 'move') renderMove();
    if (name === 'make') renderMake();
    if (name === 'spoil') renderSpoil();
    if (name === 'tanks') renderTankAdmin();
    if (name === 'records') renderMilkRecords();
  }
  document.querySelectorAll('.sl-subnav [data-sl-sub]').forEach(btn => {
    btn.addEventListener('click', () => setMilkSub(btn.dataset.slSub));
  });
  document.querySelectorAll('.sl-subnav [data-sl-sell]').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelector('.prod-tab[data-ptab="sales"]')?.click();
    });
  });
  function lsGet(key, def) {
    try { const v = localStorage.getItem('gr232:' + key); return v == null ? def : v; } catch { return def; }
  }
  function lsSet(key, val) {
    try { localStorage.setItem('gr232:' + key, val); } catch { /* ignore */ }
  }

  async function persistCustomSessions() {
    await post('/Products/SaveMilkingSessionNames', customSessions);
  }

  function sessionNames() {
    const base = ['Morning', 'Evening'];
    const used = (page.recentSessions || page.sessions || []).map(s => s.session).filter(Boolean);
    return [...new Set([...base, ...customSessions, ...used].filter(x => x && !x.startsWith('__')))].sort((a, b) => {
      const ai = base.indexOf(a), bi = base.indexOf(b);
      if (ai >= 0 && bi >= 0) return ai - bi;
      if (ai >= 0) return -1;
      if (bi >= 0) return 1;
      return a.localeCompare(b);
    });
  }

  function tankKind(w) {
    return (w.kind || w.location || '').toLowerCase();
  }
  function isRawTank(w) { return tankKind(w) === 'raw_tank'; }
  function isPastTank(w) { return tankKind(w) === 'past_tank'; }
  function isDairyStore(w) { return tankKind(w) === 'dairy_store'; }
  function isActiveWh(w) { return w && w.isActive !== false; }

  function warehousesOfKind(kind, includeInactive) {
    return (page.warehouses || []).filter(w => tankKind(w) === kind && (includeInactive || isActiveWh(w)));
  }

  function tankMetaGet(id) {
    try {
      const v = localStorage.getItem('gr232:tankMeta:' + id);
      return v ? JSON.parse(v) : {};
    } catch { return {}; }
  }

  function tankMetaSet(id, meta) {
    try { localStorage.setItem('gr232:tankMeta:' + id, JSON.stringify(meta)); } catch { /* ignore */ }
  }

  function defaultRawTankId() {
    const saved = lsGet('defaultRawTank', '');
    if (saved && (page.warehouses || []).some(w => String(w.id) === saved && isActiveWh(w))) return saved;
    const first = warehousesOfKind('raw_tank', false)[0];
    return first ? String(first.id) : '';
  }

  const TANK_EXTRA = {
    raw_tank: '<option value="__add_raw_tank">＋ Add new raw tank…</option>',
    past_tank: '<option value="__add_past_tank">＋ Add new pasteurized tank…</option>',
    dairy_store: '<option value="__add_dairy_store">＋ Add new cold store…</option>'
  };
  const MANAGE_TANK_OPT = '<option value="__manage">✎ Rename / manage tanks…</option>';

  function tankOptsForKind(kind) {
    const filter = kind === 'past_tank' ? isPastTank : isRawTank;
    const tanks = (page.warehouses || []).filter(w => filter(w) && isActiveWh(w));
    const opts = tanks.map(w =>
      `<option value="${w.id}">${esc(w.name)} — ${fmtQ(w.litersOrKg || 0, 'L')}</option>`
    ).join('');
    const emptyLabel = kind === 'past_tank' ? 'No pasteurized tank yet — add one below' : 'No raw tank yet — add one below';
    const head = tanks.length ? '' : `<option value="" disabled selected>${emptyLabel}</option>`;
    return head + opts + (TANK_EXTRA[kind] || '') + MANAGE_TANK_OPT;
  }

  function tankOptsRaw() { return tankOptsForKind('raw_tank'); }

  function tankOptsDairyStore() {
    const tanks = (page.warehouses || []).filter(isDairyStore);
    const opts = tanks.map(w =>
      `<option value="${w.id}">${esc(w.name)}</option>`
    ).join('');
    const head = tanks.length ? '' : '<option value="" disabled selected>No cold store yet — add one below</option>';
    return head + opts + TANK_EXTRA.dairy_store + MANAGE_TANK_OPT;
  }

  function isBulkMilkSku(sku) {
    const s = (sku || '').toUpperCase();
    return s === 'MILK-RAW' || s === 'MILK-PAST';
  }

  function dairyCatalogProducts() {
    return (page.catalog || []).filter(c => {
      const sku = (c.sku || '').toUpperCase();
      return !isBulkMilkSku(sku) && sku !== 'MEAT';
    });
  }

  function catalogById(id) {
    return (page.catalog || []).find(c => c.id === +id);
  }

  function productLabel(p) {
    if (!p) return 'Unknown product';
    return p.name || 'Product';
  }

  function packSizeL(p) {
    const src = String((p && (p.pack || p.name)) || '').toLowerCase();
    const m = src.match(/([\d.]+)\s*(ml|l|litre|liter|ltr|g|gm|kg)\b/);
    if (!m) return 0;
    const n = N(m[1]);
    const u = m[2];
    return u === 'ml' || u === 'g' || u === 'gm' ? n / 1000 : n;
  }

  function dairyStockQty(productId) {
    const p = catalogById(productId);
    if (!p) return 0;
    const row = (milk.dairyProducts || []).find(d =>
      d.name === p.name || (d.name && p.name && d.name.toLowerCase() === p.name.toLowerCase())
    );
    return row ? N(row.qty) : 0;
  }

  function allMilkTankOpts(extra) {
    const group = (label, filterFn) => {
      const list = (page.warehouses || []).filter(w => filterFn(w) && isActiveWh(w));
      if (!list.length) return '';
      return `<optgroup label="${esc(label)}">${list.map(w =>
        `<option value="${w.id}">${esc(w.name)} — ${fmtQ(w.litersOrKg || 0, 'L')}</option>`
      ).join('')}</optgroup>`;
    };
    let html = group('Raw milk tanks', isRawTank) + group('Pasteurized milk tanks', isPastTank);
    if (extra !== false) {
      html += `<optgroup label="—">${TANK_EXTRA.raw_tank}${TANK_EXTRA.past_tank}${MANAGE_TANK_OPT}</optgroup>`;
    }
    return html;
  }

  function kindOfWhId(id) {
    const w = warehouseById(id);
    if (!w) return '';
    const k = tankKind(w);
    if (k === 'past_tank' || k === 'raw_tank') return k;
    return k;
  }

  function setSelectHtml(id, html, preferred) {
    const el = document.getElementById(id);
    if (!el) return;
    const prev = preferred != null ? String(preferred) : el.value;
    el.innerHTML = html;
    const ids = [...el.options].map(o => o.value).filter(v => v && !v.startsWith('__'));
    if (prev && ids.includes(prev)) el.value = prev;
    else if (ids.length) el.value = ids[0];
  }

  function warehouseById(id) {
    return (page.warehouses || []).find(w => w.id === +id);
  }

  function whName(id) {
    const w = warehouseById(id);
    return w ? w.name : '—';
  }

  function catalogId(sku) {
    const c = (page.catalog || []).find(x => (x.sku || '').toUpperCase() === sku.toUpperCase());
    return c ? c.id : null;
  }

  function balLineHtml(whId, deltaLiters) {
    const w = warehouseById(whId);
    if (!w) return '';
    const now = N(w.litersOrKg);
    const after = now + (deltaLiters || 0);
    const cap = N(w.capacity);
    const bad = after < -0.001 || (cap > 0 && after > cap + 0.001);
    return `<span class="sl-bal-now">${fmtQ(now, 'L')} now</span>${deltaLiters ? `<span class="sl-bal-arrow">→</span><b class="${bad ? 'sl-bad' : ''}">${fmtQ(after, 'L')} after</b>` : ''}${cap > 0 ? `<small>${after > cap ? 'over capacity · ' : ''}capacity ${fmtN(cap)} L</small>` : ''}`;
  }

  function accountOptionsHtml() {
    const accts = page.moneyAccounts || [];
    if (!accts.length) {
      return '<option value="">Add cash/bank accounts on Finance → Bank & Cash</option>';
    }
    return accts.map(a => `<option value="${a.id}">${esc(a.name)} · ${money(a.balance)}</option>`).join('');
  }

  function fillPaidFromSelects() {
    const html = accountOptionsHtml();
    ['sl-pz-acct', 'sl-mk-acct', 'sl-sg-acct'].forEach(id => {
      const el = document.getElementById(id);
      if (!el) return;
      const prev = el.value;
      el.innerHTML = html;
      if (prev && [...el.options].some(o => o.value === prev)) el.value = prev;
    });
  }

  function renderPasteurize(preferredSrc, preferredDst) {
    setSelectHtml('sl-pz-src', tankOptsForKind('raw_tank'), preferredSrc);
    setSelectHtml('sl-pz-dst', tankOptsForKind('past_tank'), preferredDst);
    fillPaidFromSelects();
    pzPreview();
  }

  function renderMove(preferredFrom) {
    setSelectHtml('sl-mvm-from', allMilkTankOpts(false), preferredFrom);
    const from = document.getElementById('sl-mvm-from')?.value;
    const kind = kindOfWhId(from);
    const sameKind = kind === 'past_tank' ? isPastTank : isRawTank;
    const toTanks = (page.warehouses || []).filter(w => sameKind(w) && String(w.id) !== String(from));
    const toHtml = toTanks.map(w =>
      `<option value="${w.id}">${esc(w.name)} — ${fmtQ(w.litersOrKg || 0, 'L')}</option>`
    ).join('') + (TANK_EXTRA[kind] || '') + MANAGE_TANK_OPT;
    setSelectHtml('sl-mvm-to', toHtml);
    mvPreview();
  }

  function mvPreview() {
    const from = document.getElementById('sl-mvm-from')?.value;
    const to = document.getElementById('sl-mvm-to')?.value;
    const q = +document.getElementById('sl-mvm-qty')?.value || 0;
    const fromBal = document.getElementById('sl-mvm-from-bal');
    if (fromBal) {
      fromBal.innerHTML = from && !from.startsWith('__') ? balLineHtml(from, q > 0 ? -q : 0) : '';
    }
    const toBal = document.getElementById('sl-mvm-to-bal');
    if (toBal) {
      toBal.innerHTML = to && !to.startsWith('__')
        ? balLineHtml(to, q > 0 ? q : 0)
        : '<small>Add a second tank of the same type to move milk.</small>';
    }
    const kind = kindOfWhId(from);
    const sideClass = kind === 'past_tank' ? 'past' : 'raw';
    ['sl-mv-from-box', 'sl-mv-to-box'].forEach(id => {
      const box = document.getElementById(id);
      if (!box) return;
      box.classList.toggle('raw', sideClass === 'raw');
      box.classList.toggle('past', sideClass === 'past');
    });
  }

  function renderMake(preferredSrc) {
    setSelectHtml('sl-mk-src', allMilkTankOpts(), preferredSrc);
    const prods = dairyCatalogProducts();
    const prodHtml = (prods.length
      ? prods.map(p => `<option value="${p.id}">${esc(productLabel(p))}</option>`).join('')
      : '<option value="" disabled selected>Add a bottled product in Product Setup first</option>')
      + '<option value="__add">＋ Add new product…</option>'
      + '<option value="__rename">✎ Rename / change product…</option>';
    setSelectHtml('sl-mk-prod', prodHtml);
    setSelectHtml('sl-mk-dst', tankOptsDairyStore());
    fillPaidFromSelects();
    renderDairyCards();
    mkPreview();
  }

  function renderDairyCards() {
    const el = document.getElementById('sl-dairy-cards');
    if (!el) return;
    const rows = milk.dairyProducts || [];
    if (!rows.length) {
      el.innerHTML = '<p class="sl-empty">Nothing bottled yet. Use the form above: choose the tank, the litres used, the product and how many you filled — the milk leaves the tank and the bottles appear here, ready to sell.</p>';
      return;
    }
    el.innerHTML = rows.map(d => {
      const spoilKey = d.productId && d.warehouseId ? spoilOptionKeyForProduct(d) : '';
      return `<div class="sl-prod-card"><div class="sl-prod-top"><b>${esc(d.name)}</b>${d.pack ? `<span>${esc(d.pack)}</span>` : ''}</div><div class="sl-prod-q">${fmtN(d.qty)} <small>${esc(d.unit)}</small></div><small>${esc(whName(d.warehouseId))}</small>${spoilKey ? `<div class="sl-tank-act"><button type="button" class="btn btn-sm btn-ghost" data-sl-spoil="${esc(spoilKey)}">Spoiled</button></div>` : ''}</div>`;
    }).join('');
  }

  function mkPreview() {
    const src = document.getElementById('sl-mk-src')?.value;
    const inL = +document.getElementById('sl-mk-in')?.value || 0;
    const out = +document.getElementById('sl-mk-out')?.value || 0;
    const pid = document.getElementById('sl-mk-prod')?.value;
    const p = pid && !pid.startsWith('__') ? catalogById(pid) : null;
    const srcBal = document.getElementById('sl-mk-src-bal');
    if (srcBal) {
      srcBal.innerHTML = src && !src.startsWith('__') ? balLineHtml(src, inL > 0 ? -inL : 0) : '';
    }
    const extra = (+document.getElementById('sl-mk-pack')?.value || 0)
      + (+document.getElementById('sl-mk-ingr')?.value || 0)
      + (+document.getElementById('sl-mk-proc')?.value || 0);
    const unitEl = document.getElementById('sl-mk-unit');
    if (unitEl) unitEl.textContent = p ? (p.unit || 'units') : 'units';
    const heat = document.getElementById('sl-mk-heat');
    if (heat && src && !src.startsWith('__')) {
      const pastSrc = kindOfWhId(src) === 'past_tank';
      [...heat.options].forEach(o => {
        o.disabled = pastSrc ? o.value !== 'pasteurized-source' : o.value === 'pasteurized-source';
      });
      if (pastSrc) heat.value = 'pasteurized-source';
      else if (heat.value === 'pasteurized-source') heat.value = 'heat-in-process';
    }
    const size = packSizeL(p);
    const expect = document.getElementById('sl-mk-expect');
    if (expect) {
      if (p && size > 0 && inL > 0) {
        expect.innerHTML = `<span>${fmtN(inL)} L fills about <b>${fmtN(Math.floor(inL / size + 1e-9), 0)} × ${esc(p.name)}</b></span>${out > 0 ? `<small>in stock after: ${fmtN(dairyStockQty(pid) + out)} ${esc(p.unit || '')}</small>` : ''}`;
      } else if (p) {
        expect.innerHTML = `<small>${fmtN(dairyStockQty(pid))} ${esc(p.unit || '')} in stock now</small>`;
      } else {
        expect.innerHTML = '';
      }
    }
    const prev = document.getElementById('sl-mk-preview');
    if (!prev) return;
    let h = '';
    if (inL > 0 && out > 0 && p) {
      const unitCost = inL > 0 ? extra / out : 0;
      if (unitCost > 0) h += `<span>Extra cost: <b>${money(unitCost)} per ${esc(p.unit || 'unit')}</b></span>`;
      if (size > 0) {
        const loss = inL - out * size;
        h += `<span>Milk left over / lost: <b class="${loss < -0.001 ? 'sl-bad' : ''}">${fmtQ(loss, 'L')}</b></span>`;
      }
    }
    const av = src && !src.startsWith('__') ? N(warehouseById(src)?.litersOrKg) : 0;
    if (inL > av + 0.001) h += '<span class="sl-bad">More milk than the tank holds</span>';
    prev.innerHTML = h;
  }

  function renderTankAdmin() {
    const box = document.getElementById('sl-tank-admin');
    if (!box) return;
    const defRaw = defaultRawTankId();
    const groups = [
      ['raw_tank', 'Raw milk tanks (chilled)', true],
      ['past_tank', 'Pasteurized milk tanks (chilled)', true],
      ['dairy_store', 'Cold stores for bottles & products', false]
    ];
    box.innerHTML = groups.map(([kind, label, liters]) => {
      const ws = warehousesOfKind(kind, true);
      const cards = ws.map(w => {
        const meta = tankMetaGet(w.id);
        const capVal = meta.capacity != null ? meta.capacity : (w.capacity != null ? w.capacity : '');
        const tempVal = meta.targetTempC != null ? meta.targetTempC : (w.targetTempC != null ? w.targetTempC : '');
        const siteVal = meta.site != null ? meta.site : 'Farm';
        const off = w.isActive === false;
        const isDef = kind === 'raw_tank' && String(w.id) === defRaw;
        const q = N(w.litersOrKg);
        const qtyLine = liters ? `${fmtQ(q, 'L')} in tank` : `${fmtN(q)} units stored`;
        const capLabel = liters ? 'Capacity (L)' : 'Capacity (units)';
        return `<div class="sl-admin-card${off ? ' off' : ''}" data-loc="${w.id}">
          <div class="sl-admin-top"><b>${esc(w.name)}</b>${isDef ? '<span class="chip chip-milk">milking goes here</span>' : ''}${off ? '<span class="chip chip-exp">off</span>' : ''}</div>
          <small>${qtyLine}</small>
          <div class="sl-grid sl-grid-3">
            <div class="field sl-span-3"><label>Name</label><input class="sl-tk-name" value="${esc(w.name)}"></div>
            <div class="field"><label>${capLabel}</label><input class="sl-tk-cap" type="number" min="0" step="1" value="${capVal === '' ? '' : esc(String(capVal))}"></div>
            <div class="field"><label>Target °C</label><input class="sl-tk-temp" type="number" step="0.1" value="${tempVal === '' ? '' : esc(String(tempVal))}"></div>
            <div class="field"><label>Site</label><input class="sl-tk-city" value="${esc(siteVal)}"></div>
          </div>
          <div class="sl-tank-act">
            <button type="button" class="btn btn-sm btn-green" data-sl-tksave="${w.id}">Save</button>
            ${kind === 'raw_tank' && !isDef && !off ? `<button type="button" class="btn btn-sm btn-ghost" data-sl-defraw="${w.id}">Use for milking</button>` : ''}
            <button type="button" class="btn btn-sm btn-ghost" data-sl-togloc="${w.id}">${off ? 'Switch on' : 'Switch off'}</button>
          </div>
        </div>`;
      }).join('');
      return `<h3 class="sl-admin-h">${esc(label)}</h3><div class="sl-admin-grid">${cards || '<p class="sl-empty">None yet.</p>'}</div>`;
    }).join('');
  }

  async function saveTankAdminCard(locId) {
    const card = document.querySelector(`.sl-admin-card[data-loc="${CSS.escape(String(locId))}"]`);
    if (!card) return;
    const name = card.querySelector('.sl-tk-name')?.value?.trim();
    if (!name) { alert('Enter a name.'); return; }
    const w = warehouseById(locId);
    if (!w) return;
    if ((page.warehouses || []).some(x => x.id !== +locId && String(x.name).toLowerCase() === name.toLowerCase())) {
      alert('Another tank or store already has this name.');
      return;
    }
    const cap = card.querySelector('.sl-tk-cap')?.value;
    const tmp = card.querySelector('.sl-tk-temp')?.value;
    const site = card.querySelector('.sl-tk-city')?.value?.trim() || 'Farm';
    tankMetaSet(locId, {
      capacity: cap === '' || cap == null ? null : +cap,
      targetTempC: tmp === '' || tmp == null ? null : +tmp,
      site
    });
    await post('/Products/SaveWarehouse', {
      id: +locId,
      name,
      location: w.kind || w.location || tankKind(w),
      isActive: w.isActive !== false
    });
    reload();
  }

  async function toggleTankActive(locId) {
    const w = warehouseById(locId);
    if (!w) return;
    const turningOff = w.isActive !== false;
    if (turningOff && N(w.litersOrKg) > 0.001) {
      if (!confirm('This location still holds stock. Switch it off anyway? (It stays in history but won’t appear in dropdowns.)')) return;
    }
    await post('/Products/SaveWarehouse', {
      id: +locId,
      name: w.name,
      location: w.kind || w.location || tankKind(w),
      isActive: !turningOff
    });
    reload();
  }

  async function quickAddTankKind(kind) {
    const labels = { raw_tank: 'raw milk tank', past_tank: 'pasteurized milk tank', dairy_store: 'cold store' };
    const prefixes = { raw_tank: 'Raw Milk Tank ', past_tank: 'Pasteurized Milk Tank ', dairy_store: 'Cold Store ' };
    const n = warehousesOfKind(kind, true).length + 1;
    const label = labels[kind] || 'store';
    const name = (prompt(`Name of the new ${label}:`, (prefixes[kind] || 'Store ') + n) || '').trim();
    if (!name) { renderTankAdmin(); return; }
    if ((page.warehouses || []).some(x => String(x.name).toLowerCase() === name.toLowerCase())) {
      alert('That name is already used.');
      renderTankAdmin();
      return;
    }
    await post('/Products/SaveWarehouse', { name, location: kind });
    reload();
  }

  function milkTanksForSpoil() {
    return (page.warehouses || []).filter(w => isRawTank(w) || isPastTank(w));
  }

  function spoilOptionKeyForTank(w) {
    const item = kindOfWhId(w.id) === 'past_tank' ? 'milk_past' : 'milk_raw';
    return `${item}|${w.id}|`;
  }

  function spoilOptionKeyForProduct(d) {
    return `p|${d.productId}|${d.warehouseId}|`;
  }

  function parseSpoilKey(key) {
    const parts = String(key || '').split('|');
    if (parts[0] === 'p' && parts[1] && parts[2]) {
      return { type: 'product', productId: +parts[1], warehouseId: +parts[2] };
    }
    if ((parts[0] === 'milk_raw' || parts[0] === 'milk_past') && parts[1]) {
      return {
        type: 'tank',
        milkKind: parts[0],
        warehouseId: +parts[1],
        productId: catalogId(parts[0] === 'milk_past' ? 'MILK-PAST' : 'MILK-RAW')
      };
    }
    return null;
  }

  function spoilSelectionMeta(key) {
    const k = parseSpoilKey(key);
    if (!k) return null;
    if (k.type === 'tank') {
      const w = warehouseById(k.warehouseId);
      return {
        productId: k.productId,
        warehouseId: k.warehouseId,
        unit: 'L',
        placeName: w?.name || 'Tank',
        now: N(w?.litersOrKg)
      };
    }
    const p = catalogById(k.productId);
    const row = (milk.dairyProducts || []).find(d =>
      d.productId === k.productId && d.warehouseId === k.warehouseId
    );
    return {
      productId: k.productId,
      warehouseId: k.warehouseId,
      unit: p?.unit || row?.unit || 'unit',
      placeName: whName(k.warehouseId),
      now: N(row?.qty)
    };
  }

  function renderSpoil(preferredKey) {
    const tanks = milkTanksForSpoil().map(w => {
      const item = kindOfWhId(w.id) === 'past_tank' ? 'milk_past' : 'milk_raw';
      const short = item === 'milk_past' ? 'Pasteurized milk' : 'Raw milk';
      return `<option value="${spoilOptionKeyForTank(w)}">${esc(short)} · ${esc(w.name)} — ${fmtQ(w.litersOrKg || 0, 'L')}</option>`;
    }).join('');
    const prods = (milk.dairyProducts || [])
      .filter(d => d.productId && d.warehouseId && N(d.qty) > 0.0001)
      .map(d => `<option value="${spoilOptionKeyForProduct(d)}">${esc(d.name)} · ${esc(whName(d.warehouseId))} — ${fmtQ(d.qty, d.unit || '')}</option>`)
      .join('');
    let html = `<optgroup label="Milk in tanks">${tanks || '<option value="" disabled>No milk tanks set up</option>'}</optgroup>`;
    if (prods) html += `<optgroup label="Bottles &amp; products">${prods}</optgroup>`;
    setSelectHtml('sl-sp2-item', html, preferredKey);
    spoilPreview();
  }

  function spoilPreview() {
    const key = document.getElementById('sl-sp2-item')?.value;
    const meta = spoilSelectionMeta(key);
    const q = +document.getElementById('sl-sp2-qty')?.value || 0;
    const dir = document.getElementById('sl-sp2-dir')?.value || 'out';
    const unitEl = document.getElementById('sl-sp2-unit');
    if (unitEl) unitEl.textContent = meta ? meta.unit : 'L';
    const prev = document.getElementById('sl-sp2-preview');
    if (!prev || !meta) {
      if (prev) prev.innerHTML = '';
      return;
    }
    const after = meta.now + (dir === 'out' ? -q : q);
    prev.innerHTML = `<span>${esc(meta.placeName)}: <b>${fmtQ(meta.now, meta.unit)}</b> now${q > 0 ? ` → <b class="${after < -0.001 ? 'sl-bad' : ''}">${fmtQ(after, meta.unit)}</b> after` : ''}</span>`;
  }

  function pzPreview() {
    const src = document.getElementById('sl-pz-src')?.value;
    const dst = document.getElementById('sl-pz-dst')?.value;
    const inL = +document.getElementById('sl-pz-in')?.value || 0;
    const outL = +document.getElementById('sl-pz-out')?.value || 0;
    const sb = document.getElementById('sl-pz-src-bal');
    if (sb) sb.innerHTML = src && !src.startsWith('__') ? balLineHtml(src, inL > 0 ? -inL : 0) : '';
    const db = document.getElementById('sl-pz-dst-bal');
    if (db) db.innerHTML = dst && !dst.startsWith('__') ? balLineHtml(dst, outL > 0 ? outL : 0) : '';
    const e = document.getElementById('sl-pz-preview');
    if (!e) return;
    const av = src && !src.startsWith('__') ? N(warehouseById(src)?.litersOrKg) : 0;
    let h = '';
    if (inL > 0 && outL > 0) {
      const loss = inL - outL;
      h += `<span>Loss: <b class="${loss < 0 ? 'sl-bad' : ''}">${fmtQ(loss, 'L')} (${pct(loss, inL)})</b></span>`;
    }
    if (inL > av + 0.001) h += '<span class="sl-bad">More than the tank holds</span>';
    if (outL > inL + 0.001) h += '<span class="sl-bad">Output cannot be more than milk taken</span>';
    e.innerHTML = h;
  }

  async function quickAddPastTank() {
    const n = (page.warehouses || []).filter(isPastTank).length + 1;
    const name = (prompt('Name of the new pasteurized milk tank:', `Pasteurized Milk Tank ${n}`) || '').trim();
    if (!name) { renderPasteurize(); return; }
    await post('/Products/SaveWarehouse', { name, location: 'past_tank' });
    reload();
  }

  function findGoatByTag(tag) {
    const q = String(tag || '').trim().toLowerCase();
    if (!q) return null;
    return (page.herdGoats || []).find(g => String(g.tag).toLowerCase() === q) || null;
  }

  function milkingGoatsList() {
    return (page.herdGoats || [])
      .filter(g => (g.statusKey || '').toLowerCase() === 'milking')
      .sort((a, b) => String(a.breed || '').localeCompare(String(b.breed || '')) ||
        String(a.tag).localeCompare(String(b.tag), undefined, { numeric: true }));
  }

  function goatRowHtml(g) {
    return `<td><b>${esc(g.tag)}</b>${g.name ? ` <small>${esc(g.name)}</small>` : ''}</td><td>${esc(g.breed || '—')}</td><td class="num-cell"><input class="sl-mg-gl" type="number" min="0" step="0.1" inputmode="decimal"></td><td class="num-cell"><input class="sl-mg-gr" type="number" min="0" step="0.1" inputmode="decimal" placeholder="0"></td><td><button type="button" class="btn btn-ghost btn-sm sl-mg-del" aria-label="Remove goat">×</button></td>`;
  }

  function addGoatRow(g) {
    const body = document.getElementById('sl-mg-goat-rows');
    if (!body || !g) return;
    const key = String(g.id);
    if (body.querySelector(`tr[data-goat="${CSS.escape(key)}"]`)) return;
    const tr = document.createElement('tr');
    tr.dataset.goat = key;
    tr.innerHTML = goatRowHtml(g);
    body.appendChild(tr);
  }

  function refreshGoatDatalist() {
    const dl = document.getElementById('sl-mg-goat-tags');
    if (!dl) return;
    dl.innerHTML = (page.herdGoats || []).map(g =>
      `<option value="${esc(g.tag)}">${esc(`${g.breed || ''} · ${g.status || ''}`.trim())}</option>`
    ).join('');
  }

  function ensureMilkingGoatRows() {
    const gr = document.getElementById('sl-mg-goat-rows');
    if (!gr || gr.children.length) return;
    milkingGoatsList().forEach(addGoatRow);
  }

  function readMilkingRows() {
    if (mgMode === 'goat') {
      return [...document.querySelectorAll('#sl-mg-goat-rows tr')].map(tr => {
        const g = (page.herdGoats || []).find(x => String(x.id) === tr.dataset.goat) || {};
        return {
          goatId: g.id,
          tag: g.tag,
          breed: g.breed || 'Unknown',
          total: +tr.querySelector('.sl-mg-gl')?.value || 0,
          rejected: +tr.querySelector('.sl-mg-gr')?.value || 0
        };
      }).filter(r => r.total > 0 || r.rejected > 0);
    }
    if (mgMode === 'breed') {
      return [...document.querySelectorAll('#sl-mg-breed-rows tr')].map(tr => ({
        breed: tr.querySelector('.sl-mg-b')?.value,
        total: +tr.querySelector('.sl-mg-t')?.value || 0,
        rejected: +tr.querySelector('.sl-mg-r')?.value || 0,
        goats: +tr.querySelector('.sl-mg-g')?.value || 0
      })).filter(r => r.total > 0 || r.rejected > 0 || r.goats > 0);
    }
    const liters = +document.getElementById('mp-total')?.value || 0;
    const rejected = +document.getElementById('mp-reject')?.value || 0;
    return liters > 0 || rejected > 0 ? [{ total: liters, rejected }] : [];
  }

  function renderMilking() {
    const ses = document.getElementById('mp-session');
    if (ses) {
      const cur = ses.value || (new Date().getHours() < 14 ? 'Morning' : 'Evening');
      const names = sessionNames();
      ses.innerHTML = names.map(x => `<option value="${esc(x)}">${esc(x)}</option>`).join('') +
        '<option value="__add">＋ Add new milking time…</option><option value="__rename">✎ Rename a milking time…</option>';
      const pick = names.includes(cur) ? cur : (names[0] || 'Morning');
      ses.value = pick;
    }
    const wh = document.getElementById('mp-warehouse');
    if (wh) {
      const prev = wh.value;
      wh.innerHTML = tankOptsRaw();
      const ids = [...wh.options].map(o => o.value).filter(v => v && !v.startsWith('__'));
      const def = defaultRawTankId();
      if (prev && ids.includes(prev)) wh.value = prev;
      else if (def && ids.includes(def)) wh.value = def;
      else if (ids.length) wh.value = ids[0];
    }

    document.querySelectorAll('#view-milk [data-sl-mode]').forEach(b => {
      const on = b.dataset.slMode === mgMode;
      b.classList.toggle('active', on);
      b.setAttribute('aria-checked', on ? 'true' : 'false');
    });
    document.querySelectorAll('#view-milk [data-sl-mode-pane]').forEach(p => {
      p.hidden = p.dataset.slModePane !== mgMode;
    });

    const br = document.getElementById('sl-mg-breed-rows');
    if (br && !br.children.length) {
      const breeds = [...new Set(milkingGoatsList().map(g => g.breed).filter(Boolean))];
      (breeds.length ? breeds : ['Beetal', 'Makhee Cheeni']).forEach(b => addBreedRow(b));
    }
    ensureMilkingGoatRows();
    refreshGoatDatalist();

    const day = document.getElementById('mp-date')?.value || today();
    const lab = document.getElementById('sl-mg-day-label');
    if (lab) lab.textContent = day === today() ? `today (${day})` : day;
    const list = (page.recentSessions || page.sessions || []).filter(s => s.dateDisplay === day);
    const tot = document.getElementById('sl-mg-day-total');
    if (tot) {
      const usable = list.reduce((a, s) => a + Math.max(0, (s.liters || 0) - (s.rejectedLiters || 0)), 0);
      tot.innerHTML = list.length
        ? `<b>${fmtQ(usable, 'L')}</b> usable from ${list.length} milking${list.length === 1 ? '' : 's'}`
        : '';
    }
    const rowsEl = document.getElementById('sl-mg-day-rows');
    if (rowsEl) {
      rowsEl.innerHTML = list.length ? list.map(s => {
        const usable = Math.max(0, (s.liters || 0) - (s.rejectedLiters || 0));
        return `<tr><td><b>${esc(s.session)}</b></td><td>${esc(s.breed || 'Total')}</td><td class="num-cell">—</td><td class="num-cell">${fmtQ(s.liters, 'L')}</td><td class="num-cell">${fmtQ(s.rejectedLiters, 'L')}</td><td class="num-cell"><b>${fmtQ(usable, 'L')}</b></td><td>—</td><td class="hide-sm"></td><td></td></tr>`;
      }).join('') : '<tr><td colspan="9" class="empty">Nothing recorded for this day yet.</td></tr>';
    }
    mgSummary();
  }

  function initMilkingUi() {
    bindMilkingForm();
    renderMilking();
    mgSummary();
  }

  async function quickAddRawTank() {
    const n = (page.warehouses || []).filter(w => w.kind === 'raw_tank').length + 1;
    const name = (prompt('Name of the new raw milk tank:', `Raw Milk Tank ${n}`) || '').trim();
    if (!name) { renderMilking(); return; }
    await post('/Products/SaveWarehouse', { name, location: 'raw_tank' });
    reload();
  }

  async function quickAddDairyStore() {
    const n = (page.warehouses || []).filter(isDairyStore).length + 1;
    const name = (prompt('Name of the dairy cold store:', `Dairy cold store ${n}`) || '').trim();
    if (!name) { renderMake(); return; }
    await post('/Products/SaveWarehouse', { name, location: 'dairy_store' });
    reload();
  }

  async function quickAddDairyProduct() {
    const name = (prompt('Product name (e.g. Pasteurized milk · 1 L):') || '').trim();
    if (!name) { renderMake(); return; }
    const unit = (prompt('Unit (e.g. bottle, kg):', 'bottle') || 'bottle').trim();
    await post('/Products/SaveCatalog', {
      sku: name.replace(/\s+/g, '-').toUpperCase().slice(0, 24),
      name,
      unit: unit || 'bottle',
      defaultPrice: 0
    });
    reload();
  }

  async function renameDairyProduct() {
    const list = dairyCatalogProducts();
    if (!list.length) { alert('Add a product first.'); return; }
    const pick = prompt('Which product? Type its number:\n' + list.map((p, i) => `${i + 1}. ${productLabel(p)}`).join('\n'), '1');
    const p = list[(+pick || 0) - 1];
    if (!p) return;
    const name = (prompt('Product name:', p.name) || '').trim();
    if (!name) return;
    const unit = (prompt('Unit:', p.unit || 'bottle') || p.unit || 'bottle').trim();
    const price = prompt('Default selling price per unit (Rs):', String(p.defaultPrice ?? 0));
    await post('/Products/SaveCatalog', {
      id: p.id,
      sku: p.sku,
      name,
      unit,
      defaultPrice: price === null || price === '' ? p.defaultPrice : +price || 0
    });
    reload();
  }

  function bindMilkingForm() {
    const root = document.getElementById('view-milk');
    if (!root || root.dataset.milkBound) return;
    root.dataset.milkBound = '1';

    root.addEventListener('change', async e => {
      const t = e.target;
      const x = t.id || '';
      const v = t.value;

      if (v === '__manage') {
        t.value = t.options[0]?.value || '';
        setMilkSub('tanks');
        renderTankAdmin();
        document.getElementById('sl-tank-admin')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        e.stopImmediatePropagation();
        return;
      }
      if (v === '__add_raw_tank') {
        e.stopImmediatePropagation();
        await quickAddRawTank();
        return;
      }
      if (v === '__add_past_tank') {
        e.stopImmediatePropagation();
        await quickAddPastTank();
        return;
      }
      if (v === '__add_dairy_store') {
        e.stopImmediatePropagation();
        await quickAddDairyStore();
        return;
      }
      if (x === 'sl-mk-prod' && v === '__add') {
        e.stopImmediatePropagation();
        await quickAddDairyProduct();
        return;
      }
      if (x === 'sl-mk-prod' && v === '__rename') {
        e.stopImmediatePropagation();
        t.value = dairyCatalogProducts()[0]?.id || '';
        await renameDairyProduct();
        return;
      }
      if (x === 'sl-pz-src' || x === 'sl-pz-dst') {
        pzPreview();
        return;
      }
      if (x === 'sl-mvm-from') {
        renderMove();
        return;
      }
      if (x === 'sl-mvm-to') {
        mvPreview();
        return;
      }
      if (x === 'sl-mk-src' || x === 'sl-mk-prod' || x === 'sl-mk-dst' || x === 'sl-mk-heat') {
        mkPreview();
        return;
      }
      if (x.startsWith('sl-sp2-')) {
        spoilPreview();
        return;
      }
      if (x === 'mp-session' && (v === '__add' || v === '__rename')) {
        e.stopImmediatePropagation();
        if (v === '__add') {
          const n = (prompt('New milking time (e.g. Midday, Night):') || '').trim();
          if (n && !sessionNames().some(s => s.toLowerCase() === n.toLowerCase())) {
            customSessions.push(n);
            await persistCustomSessions();
          }
          renderMilking();
          if (n) {
            const match = sessionNames().find(s => s.toLowerCase() === n.toLowerCase());
            if (match) t.value = match;
          }
        } else {
          const custom = customSessions.filter(Boolean);
          if (!custom.length) {
            alert('Morning and Evening are built in. Add your own milking time first, then you can rename it.');
            renderMilking();
            return;
          }
          const pick = prompt('Rename which milking time? Type its number:\n' + custom.map((s, i) => `${i + 1}. ${s}`).join('\n'), '1');
          const old = custom[(+pick || 0) - 1];
          if (old) {
            const n = (prompt(`New name for “${old}”:`, old) || '').trim();
            if (n) {
              customSessions = customSessions.map(s => s === old ? n : s);
              await persistCustomSessions();
            }
          }
          renderMilking();
        }
        return;
      }
      if (t.classList.contains('sl-mg-b') && v === '__newbreed') {
        const n = (prompt('New breed name:') || '').trim();
        const tr = t.closest('tr');
        if (tr) {
          tr.innerHTML = breedRowHtml(n || 'Mixed');
          tr.querySelectorAll('input,select').forEach(el => el.addEventListener('input', mgSummary));
          tr.querySelector('.sl-mg-del')?.addEventListener('click', () => { tr.remove(); mgSummary(); });
        }
        mgSummary();
        return;
      }
      if (x === 'mp-date') renderMilking();
      if (x === 'mp-warehouse' || x.startsWith('sl-mg') || t.closest('#sl-mg-breed-rows,#sl-mg-goat-rows')) mgSummary();
    }, true);

    root.addEventListener('input', e => {
      const t = e.target;
      if (t.closest('#sl-mg-breed-rows,#sl-mg-goat-rows') || ['mp-goats', 'mp-total', 'mp-reject'].includes(t.id)) mgSummary();
      if (['sl-pz-in', 'sl-pz-out', 'sl-pz-cost'].includes(t.id)) pzPreview();
      if (t.id === 'sl-mvm-qty') mvPreview();
      if (t.id?.startsWith('sl-mk-')) mkPreview();
      if (t.id?.startsWith('sl-sp2-')) spoilPreview();
    });

    root.addEventListener('click', async e => {
      const tankBtn = e.target.closest('button[data-sl-tksave], button[data-sl-togloc], button[data-sl-defraw], button[data-sl-addloc]');
      if (tankBtn && root.contains(tankBtn)) {
        if (tankBtn.dataset.slTksave) {
          await saveTankAdminCard(tankBtn.dataset.slTksave);
          return;
        }
        if (tankBtn.dataset.slTogloc) {
          await toggleTankActive(tankBtn.dataset.slTogloc);
          return;
        }
        if (tankBtn.dataset.slDefraw) {
          lsSet('defaultRawTank', tankBtn.dataset.slDefraw);
          renderTankAdmin();
          renderMilking();
          return;
        }
        if (tankBtn.dataset.slAddloc) {
          await quickAddTankKind(tankBtn.dataset.slAddloc);
          return;
        }
      }
      if (e.target.closest('.sl-mg-del') && e.target.closest('#sl-mg-goat-rows')) {
        e.target.closest('tr')?.remove();
        mgSummary();
        return;
      }
      if (e.target.id === 'sl-mg-goat-add' || e.target.closest('#sl-mg-goat-add')) {
        const tag = document.getElementById('sl-mg-goat-find')?.value || '';
        const g = findGoatByTag(tag);
        if (!g) {
          alert(tag.trim() ? `No goat with tag ${tag.trim()} in the live herd.` : 'Type a tag first.');
          return;
        }
        addGoatRow(g);
        const findEl = document.getElementById('sl-mg-goat-find');
        if (findEl) findEl.value = '';
        const row = document.querySelector(`#sl-mg-goat-rows tr[data-goat="${CSS.escape(String(g.id))}"] .sl-mg-gl`);
        row?.focus();
        mgSummary();
      }
    });

    root.addEventListener('keydown', e => {
      if (e.target.id === 'sl-mg-goat-find' && e.key === 'Enter') {
        e.preventDefault();
        document.getElementById('sl-mg-goat-add')?.click();
      }
    });
  }

  function breedRowHtml(breed) {
    const breeds = ['Mixed', 'Beetal', 'Makhee Cheeni', 'Saanen', 'Boer'];
    const opts = breeds.map(b => `<option${b === breed ? ' selected' : ''}>${esc(b)}</option>`).join('') +
      '<option value="__newbreed">＋ New breed…</option>';
    return `<td><select class="sl-mg-b">${opts}</select></td><td class="num-cell"><input class="sl-mg-g" type="number" min="0" step="1" placeholder="0"></td><td class="num-cell"><input class="sl-mg-t" type="number" min="0" step="0.1" inputmode="decimal"></td><td class="num-cell"><input class="sl-mg-r" type="number" min="0" step="0.1" value="0" inputmode="decimal"></td><td class="num-cell sl-mg-u">—</td><td><button type="button" class="btn btn-ghost btn-sm sl-mg-del" aria-label="Remove">×</button></td>`;
  }

  /* Milking count mode */
  let mgMode = lsGet('mgMode', 'breed');
  if (!['breed', 'goat', 'total'].includes(mgMode)) mgMode = 'breed';
  document.querySelectorAll('[data-sl-mode]').forEach(btn => {
    btn.addEventListener('click', () => {
      mgMode = btn.dataset.slMode;
      lsSet('mgMode', mgMode);
      document.querySelectorAll('[data-sl-mode]').forEach(b => b.classList.toggle('active', b === btn));
      document.querySelectorAll('[data-sl-mode-pane]').forEach(p => { p.hidden = p.dataset.slModePane !== mgMode; });
      renderMilking();
    });
  });
  document.querySelectorAll('[data-sl-mode]').forEach(b => b.classList.toggle('active', b.dataset.slMode === mgMode));
  document.querySelectorAll('[data-sl-mode-pane]').forEach(p => { p.hidden = p.dataset.slModePane !== mgMode; });

  function mgSummary() {
    const e = document.getElementById('sl-mg-summary');
    if (!e) return;
    let total = 0, rejected = 0, goats = 0;
    if (mgMode === 'total') {
      total = +document.getElementById('mp-total')?.value || 0;
      rejected = +document.getElementById('mp-reject')?.value || 0;
      goats = +document.getElementById('mp-goats')?.value || 0;
    } else if (mgMode === 'breed') {
      document.querySelectorAll('#sl-mg-breed-rows tr').forEach(tr => {
        total += +tr.querySelector('.sl-mg-t')?.value || 0;
        rejected += +tr.querySelector('.sl-mg-r')?.value || 0;
        const g = tr.querySelector('.sl-mg-g')?.value;
        goats += g === '' ? 0 : +g;
      });
    } else if (mgMode === 'goat') {
      document.querySelectorAll('#sl-mg-goat-rows tr').forEach(tr => {
        const t = +tr.querySelector('.sl-mg-gl')?.value || 0;
        const r = +tr.querySelector('.sl-mg-gr')?.value || 0;
        total += t;
        rejected += r;
        if (t > 0 || r > 0) goats += 1;
      });
    }
    const usable = total - rejected;
    const wh = document.getElementById('mp-warehouse');
    const whName = wh?.selectedOptions?.[0]?.text || 'tank';
    e.innerHTML = `<div><span>Goats</span><b>${fmtN(goats, 0)}</b></div><div><span>Milk</span><b>${fmtQ(total, 'L')}</b></div><div><span>Rejected</span><b>${fmtQ(rejected, 'L')}</b></div><div class="sl-total-main"><span>Usable → ${esc(whName)}</span><b>${fmtQ(usable, 'L')}</b></div>`;
    document.querySelectorAll('#sl-mg-breed-rows tr').forEach(tr => {
      const t = +tr.querySelector('.sl-mg-t')?.value || 0;
      const r = +tr.querySelector('.sl-mg-r')?.value || 0;
      const u = tr.querySelector('.sl-mg-u');
      if (u) { u.textContent = t || r ? fmtN(t - r) + ' L' : '—'; u.classList.toggle('sl-bad', r > t); }
    });
  }

  function addBreedRow(breed) {
    const tb = document.getElementById('sl-mg-breed-rows');
    if (!tb) return;
    const tr = document.createElement('tr');
    tr.innerHTML = breedRowHtml(breed || 'Mixed');
    tr.querySelector('.sl-mg-del')?.addEventListener('click', () => { tr.remove(); mgSummary(); });
    tr.querySelectorAll('input,select').forEach(el => el.addEventListener('input', mgSummary));
    tb.appendChild(tr);
    mgSummary();
  }
  document.getElementById('sl-mg-add-breed')?.addEventListener('click', () => {
    const used = [...document.querySelectorAll('#sl-mg-breed-rows .sl-mg-b')].map(s => s.value);
    const next = ['Beetal', 'Makhee Cheeni', 'Mixed'].find(b => !used.includes(b)) || 'Mixed';
    addBreedRow(next);
  });

  document.getElementById('pc-category')?.addEventListener('change', e => {
    const meat = document.getElementById('v221-meat-catalog-type');
    if (meat) meat.style.display = e.target.value === 'meat' ? '' : 'none';
  });

  /* Meat operation tabs */
  function setMeatForm(name) {
    document.querySelectorAll('.sl-actions [data-sl-action]').forEach(b => b.classList.toggle('active', b.dataset.slAction === name));
    document.querySelectorAll('[data-sl-form]').forEach(p => { p.hidden = p.dataset.slForm !== name; });
  }
  document.querySelectorAll('.sl-actions [data-sl-action]').forEach(btn => {
    btn.addEventListener('click', () => setMeatForm(btn.dataset.slAction));
  });
  document.querySelectorAll('.sl-actions [data-sl-sell]').forEach(btn => {
    btn.addEventListener('click', () => document.querySelector('.prod-tab[data-ptab="sales"]')?.click());
  });
  setMeatForm('slaughter');

  /* Defaults & selects */
  ['mp-date', 'sl-pz-date', 'sl-mvm-date', 'sl-mk-date', 'sl-sp2-date', 'sl-sg-date', 'sl-sp-date', 'sl-ct-date', 'sl-sa-date', 'sl-tf-date', 'sl-adj-date', 'tp-date', 'ca-date'].forEach(id => {
    const e = document.getElementById(id);
    if (e && !e.value) e.value = today();
  });

  function bootProductsPage() {
    try {
      activateProductTab(document.querySelector('#productTabs .prod-tab.active')?.dataset.ptab || 'milk');
      setMilkSub('milking');
      initMilkingUi();
      renderPasteurize();
      renderMove();
      renderMake();
      renderSpoil();
    } catch (err) {
      console.error('Products page init failed', err);
    }
  }
  bootProductsPage();

  const bpMonth = document.getElementById('bp-month');
  if (bpMonth && page.month) bpMonth.value = page.month;

  function fillCustomerSelect(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.innerHTML = '<option value="">Walk-in / retail</option>' +
      (page.customers || []).map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
  }
  fillCustomerSelect('sl-sa-cust');
  fillCustomerSelect('cp-client');

  function fillProductSelect(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.innerHTML = (page.catalog || []).map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
  }
  ['pr-product', 'rp-product', 'cp-product', 'ca-product', 'sl-sa-prod'].forEach(fillProductSelect);

  const MILK_OP_LABEL = {
    Receive: 'Milk received',
    Issue: 'Issued from tank',
    Transfer: 'Tank / store transfer',
    Spoilage: 'Spoilage / write-off',
    Adjust: 'Stock adjustment'
  };

  function isMilkRelatedMovement(m) {
    const sku = (m.productSku || '').toUpperCase();
    if (sku.startsWith('MILK-') || sku === 'GHEE') return true;
    const name = (m.product || '').toLowerCase();
    if (name.includes('milk') || name.includes('pasteur') || name.includes('ghee') || name.includes('yogurt') || name.includes('cheese')) return true;
    return sku !== 'MEAT' && !name.includes('goat meat');
  }

  function describeMovement(m) {
    const unit = (m.productSku || '').toUpperCase().startsWith('MILK-') || (m.product || '').toLowerCase().includes('milk') ? 'L' : '';
    const outTypes = ['Issue', 'Spoilage'];
    const sign = outTypes.includes(m.type) ? '−' : '+';
    const qty = `${sign}${fmtN(m.qty)}${unit ? ' ' + unit : ''}`;
    const where = m.type === 'Transfer' && m.toWarehouse
      ? `${esc(m.warehouse)} → ${esc(m.toWarehouse)}`
      : esc(m.warehouse);
    const note = m.notes ? `<div class="name">${esc(m.notes)}</div>` : '';
    return `<span class="${outTypes.includes(m.type) ? 'sl-out' : 'sl-in'}">${qty} ${esc(m.product)}</span> <small>@ ${where}</small>${note}`;
  }

  function milkRecordSessions() {
    return [...(page.recentSessions || page.sessions || [])]
      .sort((a, b) => String(b.dateDisplay).localeCompare(String(a.dateDisplay)));
  }

  function breedNamesForReport() {
    const fromSessions = milkRecordSessions().map(s => s.breed).filter(Boolean);
    const fromLegacy = (page.legacyMilkEntries || []).map(e => e.breed).filter(Boolean);
    return [...new Set([...fromSessions, ...fromLegacy, 'Beetal', 'Makhee Cheeni', 'Mixed'])].sort((a, b) => a.localeCompare(b));
  }

  function renderMilkBreedReport() {
    const el = document.getElementById('mp-breed-report');
    const dateEl = document.getElementById('mp-report-date');
    const breedEl = document.getElementById('mp-report-breed');
    if (!el) return;
    const date = dateEl?.value || today();
    if (dateEl && !dateEl.value) dateEl.value = date;
    const month = date.slice(0, 7);
    const prevBreed = breedEl?.value || '';
    if (breedEl) {
      breedEl.innerHTML = '<option value="">All breeds</option>' +
        breedNamesForReport().map(b => `<option value="${esc(b)}">${esc(b)}</option>`).join('');
      if ([...breedEl.options].some(o => o.value === prevBreed)) breedEl.value = prevBreed;
    }
    const breedFilter = breedEl?.value || '';
    const sessionRows = milkRecordSessions().filter(s => !breedFilter || s.breed === breedFilter);
    const legacyRows = (page.legacyMilkEntries || []).filter(e => e.breed && e.breed !== 'Milking Parlor' && (!breedFilter || e.breed === breedFilter));
    const breeds = [...new Set([
      ...sessionRows.map(s => s.breed || 'Mixed'),
      ...legacyRows.map(e => e.breed)
    ])].filter(Boolean);
    const rows = breeds.map(b => {
      const daySessions = sessionRows.filter(s => (s.breed || 'Mixed') === b && s.dateDisplay === date);
      const monthSessions = sessionRows.filter(s => (s.breed || 'Mixed') === b && String(s.dateDisplay).startsWith(month));
      const dayLegacy = legacyRows.filter(e => e.breed === b && e.dateDisplay === date);
      const monthLegacy = legacyRows.filter(e => e.breed === b && String(e.dateDisplay).startsWith(month));
      const sumL = arr => arr.reduce((n, x) => n + N(x.liters), 0);
      const daily = sumL(daySessions) + sumL(dayLegacy);
      const monthly = sumL(monthSessions) + sumL(monthLegacy);
      return { breed: b, daily, monthly };
    }).filter(r => r.daily > 0 || r.monthly > 0);
    const dayTotal = rows.reduce((n, r) => n + r.daily, 0);
    const monthTotal = rows.reduce((n, r) => n + r.monthly, 0);
    el.innerHTML = `<div class="table-wrap"><table class="tbl"><thead><tr><th>Breed</th><th class="num-cell">${esc(date)} (L)</th><th class="num-cell">${esc(month)} (L)</th></tr></thead><tbody>${
      rows.map(r => `<tr><td><b>${esc(r.breed)}</b></td><td class="num-cell">${fmtN(r.daily, 1)}</td><td class="num-cell">${fmtN(r.monthly, 1)}</td></tr>`).join('') ||
      '<tr><td colspan="3">No breed-specific milk recorded for this period.</td></tr>'
    }<tr><td><b>Total</b></td><td class="num-cell"><b>${fmtN(dayTotal, 1)}</b></td><td class="num-cell"><b>${fmtN(monthTotal, 1)}</b></td></tr></tbody></table></div>`;
  }

  function renderMilkRecords() {
    const milkMoves = document.getElementById('sl-milk-moves');
    if (milkMoves) {
      const ops = (page.movements || []).filter(isMilkRelatedMovement).slice(0, 60);
      milkMoves.innerHTML = ops.length ? ops.map(m =>
        `<tr><td>${esc(m.dateDisplay)}</td><td><b>${esc(MILK_OP_LABEL[m.type] || m.type)}</b></td><td>${describeMovement(m)}</td><td class="hide-sm">—</td><td></td></tr>`
      ).join('') : '<tr><td colspan="5" class="empty">No milk movements yet. Record a milking, pasteurization, or tank move to start.</td></tr>';
    }

    const milkingSessionRows = document.getElementById('milkingSessionRows');
    if (milkingSessionRows) {
      const rows = milkRecordSessions();
      milkingSessionRows.innerHTML = rows.length ? rows.slice(0, 30).map(s => {
        const usable = Math.max(0, (s.liters || 0) - (s.rejectedLiters || 0));
        return `<tr><td>${esc(s.dateDisplay)}</td><td>${esc(s.session)}</td><td class="num-cell">—</td><td class="num-cell">${fmtQ(s.liters, 'L')}</td><td class="num-cell">${fmtQ(s.rejectedLiters, 'L')}</td><td class="num-cell"><b>${fmtQ(usable, 'L')}</b></td><td class="hide-sm">—</td><td></td></tr>`;
      }).join('') : '<tr><td colspan="8" class="empty">No milking parlor sessions recorded yet.</td></tr>';
    }

    renderMilkBreedReport();

    const legacy = page.legacyMilkEntries || [];
    const oldPanel = document.getElementById('sl-old-entries');
    const prodRows = document.getElementById('prodRows');
    if (oldPanel) oldPanel.hidden = legacy.length === 0;
    if (prodRows) {
      prodRows.innerHTML = legacy.length ? legacy.map(p =>
        `<tr><td>${esc(p.dateDisplay)}</td><td class="hide-sm">${esc(p.breed)}</td><td class="num-cell">${fmtQ(p.liters, 'L')}</td><td class="hide-sm">${esc(p.comment || '')}</td><td></td></tr>`
      ).join('') : '<tr><td colspan="5" class="empty">No legacy single-line milk entries.</td></tr>';
    }
  }

  document.getElementById('mp-report-date')?.addEventListener('change', renderMilkBreedReport);
  document.getElementById('mp-report-breed')?.addEventListener('change', renderMilkBreedReport);
  renderMilkRecords();

  const catRows = document.getElementById('productCatalogRows');
  if (catRows) {
    catRows.innerHTML = (page.catalog || []).map(c =>
      `<tr><td>—</td><td><b>${esc(c.name)}</b></td><td>—</td><td>${esc(c.unit)}</td><td class="num-cell">${rs(c.defaultPrice)}</td><td class="hide-sm">${esc(c.sku)}</td><td>Active</td><td></td></tr>`
    ).join('') || '<tr><td colspan="8" class="empty">No products in catalogue yet.</td></tr>';
  }

  const salesRows = document.getElementById('sl-sales-rows');
  if (salesRows) {
    salesRows.innerHTML = (page.sales || []).map(s =>
      `<tr><td>${esc(s.dateDisplay)}</td><td>—</td><td>${esc(s.customer || 'Cash')}</td><td>${esc(s.productName)}</td><td class="num-cell">${s.qty}</td><td class="num-cell hide-sm">—</td><td class="num-cell">${rs(s.amount)}</td><td class="num-cell hide-sm">—</td><td class="num-cell hide-sm">—</td><td>${s.isCredit ? 'Credit' : 'Paid'}</td><td></td></tr>`
    ).join('') || '<tr><td colspan="11" class="empty">No sales this month yet.</td></tr>';
  }

  const mvRows = document.getElementById('sl-mv-rows');
  if (mvRows) {
    mvRows.innerHTML = (page.movements || []).map(m =>
      `<tr><td>${esc(m.dateDisplay)}</td><td><b>${esc(m.type)}</b></td><td>${describeMovement(m)}</td><td class="hide-sm"></td><td></td></tr>`
    ).join('') || '<tr><td colspan="5" class="empty">No stock movements recorded yet.</td></tr>';
  }

  const carcassRows = document.getElementById('sl-carcass-rows');
  if (carcassRows) {
    carcassRows.innerHTML = (page.slaughters || []).map(s =>
      `<tr><td>${esc(s.tag || '—')}</td><td>Whole</td><td class="num-cell">1</td><td class="num-cell">${s.carcassKg}</td><td class="num-cell hide-sm">—</td><td class="num-cell hide-sm">${rs(s.saleAmount)}</td><td class="hide-sm">—</td><td></td></tr>`
    ).join('') || '<tr><td colspan="8" class="empty">No carcasses in stock yet.</td></tr>';
  }

  /* Breed performance (simplified) */
  function renderBreedPerformance() {
    const kpis = document.getElementById('bp-kpis');
    const table = document.getElementById('bp-table');
    if (!kpis && !table) return;
    const byBreed = {};
    (page.sessions || []).forEach(s => {
      const b = s.breed || 'Mixed';
      byBreed[b] = (byBreed[b] || 0) + Math.max(0, (s.liters || 0) - (s.rejectedLiters || 0));
    });
    const breeds = Object.entries(byBreed).sort((a, b) => b[1] - a[1]);
    if (kpis) {
      kpis.innerHTML = breeds.slice(0, 4).map(([b, l]) =>
        `<div class="product-mini-kpi"><span>${esc(b)}</span><b>${l.toFixed(1)} L</b></div>`
      ).join('') || '<div class="product-mini-kpi"><span>Milk</span><b>0 L</b></div>';
    }
    if (table) {
      table.innerHTML = `<table class="tbl"><thead><tr><th>Breed</th><th class="num-cell">Usable milk (L)</th></tr></thead><tbody>${
        breeds.map(([b, l]) => `<tr><td><b>${esc(b)}</b></td><td class="num-cell">${l.toFixed(1)}</td></tr>`).join('') ||
        '<tr><td colspan="2" class="empty">No breed data for this month.</td></tr>'
      }</tbody></table>`;
    }
  }
  renderBreedPerformance();

  /* Transport cost preview */
  function updateTpPreview() {
    const qty = +document.getElementById('tp-qty')?.value || 0;
    const cost = +document.getElementById('tp-cost')?.value || 0;
    const el = document.getElementById('tp-cost-preview');
    if (!el) return;
    if (qty > 0 && cost >= 0) el.innerHTML = `<strong>${rs(cost / qty)}</strong> per unit (${qty} units, ${rs(cost)} total)`;
  }
  ['tp-qty', 'tp-cost'].forEach(id => document.getElementById(id)?.addEventListener('input', updateTpPreview));

  /* Pricing margin preview */
  function updatePricingPreview() {
    const base = +document.getElementById('pr-base')?.value || 0;
    const pack = +document.getElementById('pr-pack')?.value || 0;
    const tr = +document.getElementById('pr-transport')?.value || 0;
    const st = +document.getElementById('pr-storage')?.value || 0;
    const ot = +document.getElementById('pr-other')?.value || 0;
    const sell = +document.getElementById('pr-sell')?.value || 0;
    const total = base + pack + tr + st + ot;
    const margin = sell - total;
    const set = (id, v) => { const e = document.getElementById(id); if (e) e.textContent = rs(v); };
    set('pr-total', total);
    set('pr-sell-show', sell);
    set('pr-margin', margin);
    const pctEl = document.getElementById('pr-marginpct');
    if (pctEl) pctEl.textContent = sell > 0 ? `${((margin / sell) * 100).toFixed(1)}%` : '0%';
  }
  ['pr-base', 'pr-pack', 'pr-transport', 'pr-storage', 'pr-other', 'pr-sell'].forEach(id =>
    document.getElementById(id)?.addEventListener('input', updatePricingPreview));

  /* Save: milking session (total + breed rows summed) */
  document.getElementById('sl-mg-save')?.addEventListener('click', async () => {
    const rows = readMilkingRows();
    let liters = 0, rejected = 0, breed = 'Mixed';
    if (mgMode === 'total') {
      liters = +document.getElementById('mp-total')?.value || 0;
      rejected = +document.getElementById('mp-reject')?.value || 0;
    } else if (mgMode === 'breed') {
      rows.forEach(r => {
        liters += r.total;
        rejected += r.rejected;
        if (r.breed && r.breed !== '__newbreed') breed = r.breed;
      });
      if (!liters) {
        alert('Add at least one breed line with milk litres, or switch to “One total”.');
        return;
      }
    } else if (mgMode === 'goat') {
      if (!rows.length) {
        alert('Type the litres for at least one goat.');
        return;
      }
      if (rows.some(r => r.rejected > r.total)) {
        alert('Rejected milk cannot be more than the milk on the same line.');
        return;
      }
      rows.forEach(r => {
        liters += r.total;
        rejected += r.rejected;
      });
      breed = rows[0].breed || 'Mixed';
    }
    if (liters <= 0) { alert('Enter milk litres.'); return; }
    const usable = liters - rejected;
    if (usable <= 0) { alert('There is no usable milk to put in the tank.'); return; }
    const sessionVal = document.getElementById('mp-session')?.value || '';
    if (!sessionVal || sessionVal.startsWith('__')) { alert('Choose Morning, Evening or another milking time.'); return; }
    const wh = document.getElementById('mp-warehouse')?.value;
    if (!wh || wh.startsWith('__')) { alert('Choose the raw milk tank the milk goes into.'); return; }
    await post('/Products/SaveMilkingSession', {
      date: document.getElementById('mp-date')?.value || today(),
      session: sessionVal,
      breed,
      liters,
      rejectedLiters: rejected,
      warehouseId: wh ? +wh : null
    });
    reload();
  });

  document.getElementById('addProductCatalog')?.addEventListener('click', async () => {
    const name = document.getElementById('pc-name')?.value?.trim();
    if (!name) { alert('Enter a product name.'); return; }
    await post('/Products/SaveCatalog', {
      sku: document.getElementById('pc-sku')?.value?.trim() || name.replace(/\s+/g, '-').toUpperCase().slice(0, 24),
      name,
      unit: document.getElementById('pc-unit')?.value || 'L',
      defaultPrice: +document.getElementById('pc-price')?.value || 0
    });
    reload();
  });

  document.getElementById('sl-sg-save')?.addEventListener('click', async () => {
    await post('/Products/SaveSlaughter', {
      date: document.getElementById('sl-sg-date')?.value || today(),
      tagText: document.getElementById('sl-sg-tag')?.value || '',
      carcassKg: +document.getElementById('sl-sg-kg')?.value || 0,
      saleAmount: 0,
      liveWeightKg: +document.getElementById('sl-sg-live')?.value || 0
    });
    reload();
  });

  document.getElementById('sl-sa-check')?.addEventListener('click', () => {
    const qty = +document.getElementById('sl-sa-qty')?.value || 0;
    const rate = +document.getElementById('sl-sa-rate')?.value || 0;
    const amt = qty * rate;
    const review = document.getElementById('sl-sa-review');
    if (!review) return;
    review.hidden = false;
    review.innerHTML = `<div class="sl-review"><h3>Review sale</h3><dl><dt>Quantity</dt><dd>${qty}</dd><dt>Rate</dt><dd>${rs(rate)}</dd><dt>Amount</dt><dd><b>${rs(amt)}</b></dd></dl><div class="sl-review-act"><button type="button" class="btn btn-green" id="sl-sa-post">Post sale</button><button type="button" class="btn btn-ghost" id="sl-sa-cancel-review">Back</button></div></div>`;
    document.getElementById('sl-sa-cancel-review')?.addEventListener('click', () => { review.hidden = true; });
    document.getElementById('sl-sa-post')?.addEventListener('click', async () => {
      const cust = document.getElementById('sl-sa-cust')?.value;
      const pay = document.getElementById('sl-sa-pay')?.value;
      const isCredit = pay === 'credit' && cust;
      const type = document.getElementById('sl-sa-type')?.value || 'other';
      const typeLabels = { raw: 'Raw milk', past: 'Pasteurized milk', dairy: 'Dairy product', whole: 'Whole carcass', half: 'Half carcass', cut: 'Meat cuts', other: 'Product' };
      const productId = document.getElementById('sl-sa-prod')?.value;
      const res = await fetch('/Products/SaveSale', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          date: document.getElementById('sl-sa-date')?.value || today(),
          productId: productId ? +productId : null,
          customerId: cust ? +cust : null,
          productName: typeLabels[type] || 'Sale',
          qty, rate, isCredit: !!isCredit
        })
      });
      const j = await res.json().catch(() => ({}));
      if (j.invoiceId) alert('Credit sale recorded — invoice #' + j.invoiceId + ' in Finance.');
      reload();
    });
  });

  document.getElementById('sl-mvm-all')?.addEventListener('click', () => {
    const from = document.getElementById('sl-mvm-from')?.value;
    const w = from && !from.startsWith('__') ? warehouseById(from) : null;
    const inp = document.getElementById('sl-mvm-qty');
    if (inp && w) {
      inp.value = fmtN(w.litersOrKg || 0, 1);
      mvPreview();
    }
  });

  document.getElementById('sl-sp2-save')?.addEventListener('click', async () => {
    const key = document.getElementById('sl-sp2-item')?.value;
    const meta = spoilSelectionMeta(key);
    const dir = document.getElementById('sl-sp2-dir')?.value || 'out';
    const q = +document.getElementById('sl-sp2-qty')?.value || 0;
    const reason = document.getElementById('sl-sp2-reason')?.value || '';
    const note = document.getElementById('sl-sp2-note')?.value?.trim() || '';
    const date = document.getElementById('sl-sp2-date')?.value || today();
    if (!meta?.productId || !meta.warehouseId) {
      alert('Choose the tank or product.');
      return;
    }
    if (!(q > 0)) {
      alert('Enter the quantity.');
      return;
    }
    if (reason === 'Other' && !note) {
      alert('Write what happened in the note.');
      return;
    }
    const notes = [reason, note].filter(Boolean).join(' · ');
    try {
      await postJson('/Products/SaveStockMovement', {
        date,
        type: dir === 'out' ? 'Spoilage' : 'Receive',
        productId: meta.productId,
        warehouseId: meta.warehouseId,
        qty: q,
        unitCost: 0,
        notes: notes || 'Spoilage adjustment'
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save spoilage.');
    }
  });

  document.getElementById('sl-mvm-save')?.addEventListener('click', async () => {
    const from = document.getElementById('sl-mvm-from')?.value;
    const to = document.getElementById('sl-mvm-to')?.value;
    const q = +document.getElementById('sl-mvm-qty')?.value || 0;
    const date = document.getElementById('sl-mvm-date')?.value || today();
    if (!from || from.startsWith('__')) { alert('Choose the tank to move milk from.'); return; }
    if (!to || to.startsWith('__')) {
      alert('Choose the tank to move milk into. Add a second tank first if you have only one.');
      return;
    }
    if (!(q > 0)) { alert('Enter the litres to move.'); return; }
    if (kindOfWhId(from) !== kindOfWhId(to)) {
      alert('Raw and pasteurized milk cannot be mixed.');
      return;
    }
    const dest = warehouseById(to);
    const cap = N(dest?.capacity);
    const destQty = N(dest?.litersOrKg);
    if (cap > 0 && destQty + q > cap + 0.001) {
      if (!confirm(`${dest.name} will be over its capacity of ${fmtN(cap)} L. Move anyway?`)) return;
    }
    const sku = kindOfWhId(from) === 'past_tank' ? 'MILK-PAST' : 'MILK-RAW';
    const productId = catalogId(sku);
    if (!productId) { alert('Milk product setup is missing. Contact your administrator.'); return; }
    const note = document.getElementById('sl-mvm-note')?.value?.trim() || '';
    try {
      await postJson('/Products/SaveStockMovement', {
        date,
        type: 'Transfer',
        productId,
        warehouseId: +from,
        toWarehouseId: +to,
        qty: q,
        unitCost: 0,
        notes: note ? `Move: ${whName(from)} → ${whName(to)} · ${note}` : `Move: ${whName(from)} → ${whName(to)}`
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not move milk.');
    }
  });

  document.getElementById('sl-pz-all')?.addEventListener('click', () => {
    const src = document.getElementById('sl-pz-src')?.value;
    const w = src && !src.startsWith('__') ? warehouseById(src) : null;
    const inp = document.getElementById('sl-pz-in');
    if (inp && w) {
      inp.value = fmtN(w.litersOrKg || 0, 1);
      pzPreview();
    }
  });

  document.getElementById('sl-pz-save')?.addEventListener('click', async () => {
    const date = document.getElementById('sl-pz-date')?.value || today();
    const src = document.getElementById('sl-pz-src')?.value;
    const dst = document.getElementById('sl-pz-dst')?.value;
    const inL = +document.getElementById('sl-pz-in')?.value || 0;
    const outL = +document.getElementById('sl-pz-out')?.value || 0;
    const cost = +document.getElementById('sl-pz-cost')?.value || 0;
    const acct = document.getElementById('sl-pz-acct')?.value;
    if (!src || src.startsWith('__')) { alert('Choose the raw milk tank the milk comes from.'); return; }
    if (!dst || dst.startsWith('__')) { alert('Choose a pasteurized milk tank for the output.'); return; }
    if (inL <= 0) { alert('Enter the raw milk taken (litres).'); return; }
    if (outL <= 0) { alert('Enter the pasteurized milk obtained (litres).'); return; }
    if (outL > inL) { alert('Pasteurized litres cannot be more than the raw litres taken.'); return; }
    if (!document.getElementById('sl-pz-qc')?.checked) {
      alert('Tick the box to confirm pasteurization and cooling were completed and the milk passed the check.');
      return;
    }
    if (cost > 0 && !acct) {
      alert('Choose Paid from: Cash in Hand or a Bank account (the same rule as Accounting → Expenses).');
      return;
    }
    try {
      await postJson('/Products/SavePasteurization', {
        date,
        sourceWarehouseId: +src,
        destWarehouseId: +dst,
        inputLiters: inL,
        outputLiters: outL,
        processCost: cost,
        paidFromAccountId: acct ? +acct : null,
        note: document.getElementById('sl-pz-note')?.value?.trim() || null,
        runRef: document.getElementById('sl-pz-ref')?.value?.trim() || null,
        heatTempC: document.getElementById('sl-pz-temp')?.value || null,
        coolTempC: document.getElementById('sl-pz-cool')?.value || null
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save pasteurization.');
    }
  });

  document.getElementById('sl-tf-save')?.addEventListener('click', async () => {
    const pid = page.catalog?.[0]?.id;
    const wid = page.warehouses?.[0]?.id;
    if (!pid || !wid) { alert('Add a product and warehouse in Product Setup first.'); return; }
    await post('/Products/SaveStockMovement', {
      date: document.getElementById('sl-tf-date')?.value || today(),
      type: 'Transfer',
      productId: pid,
      warehouseId: wid,
      qty: +document.getElementById('sl-tf-qty')?.value || 0,
      unitCost: 0,
      notes: document.getElementById('sl-tf-note')?.value || ''
    });
    reload();
  });

  document.getElementById('sl-adj-save')?.addEventListener('click', async () => {
    const pid = page.catalog?.[0]?.id;
    const wid = page.warehouses?.[0]?.id;
    if (!pid || !wid) { alert('Add a product and warehouse first.'); return; }
    const dir = document.getElementById('sl-adj-dir')?.value;
    await post('/Products/SaveStockMovement', {
      date: document.getElementById('sl-adj-date')?.value || today(),
      type: dir === 'in' ? 'Receive' : 'Spoilage',
      productId: pid,
      warehouseId: wid,
      qty: +document.getElementById('sl-adj-qty')?.value || 0,
      unitCost: 0,
      notes: document.getElementById('sl-adj-note')?.value || ''
    });
    reload();
  });

  const salesKpis = document.getElementById('sl-sales-kpis');
  if (salesKpis) {
    salesKpis.innerHTML = `
      <div class="sl-kpi"><span>Sales this month</span><b>${rs(page.monthSalesTotal)}</b></div>
      <div class="sl-kpi"><span>Transactions</span><b>${(page.sales || []).length}</b></div>
      <div class="sl-kpi"><span>Credit sales</span><b>${(page.sales || []).filter(s => s.isCredit).length}</b></div>
      <div class="sl-kpi"><span>Customers</span><b>${(page.customers || []).length}</b></div>`;
  }

  document.getElementById('addTransportShipment')?.addEventListener('click', () =>
    alert('Transport shipments will post to Finance in the next update. Use Accounting → Expenses for now.'));

  document.getElementById('addCostAllocation')?.addEventListener('click', () =>
    alert('Cost allocation will link to Finance expenses in the next update.'));

  document.getElementById('saveProductPricing')?.addEventListener('click', () =>
    alert('Pricing profiles will be saved to the catalogue in the next update. Set default price under Product Setup for now.'));

  document.getElementById('saveRoutePricing')?.addEventListener('click', () => alert('Route pricing — coming soon.'));
  document.getElementById('saveClientPrice')?.addEventListener('click', () => alert('Client-specific rates — coming soon.'));

  document.getElementById('bp-export')?.addEventListener('click', () => {
    const rows = [['Breed', 'Liters']];
    (page.sessions || []).forEach(s => rows.push([s.breed, String(Math.max(0, s.liters - s.rejectedLiters))]));
    const csv = rows.map(r => r.map(c => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\n');
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    a.download = 'breed-performance.csv';
    a.click();
  });
})();
