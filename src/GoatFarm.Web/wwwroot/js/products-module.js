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
  const productDropdownOptions = { ...(page.productDropdownOptions || {}) };
  ['costTypes', 'shipmentTypes', 'units', 'categories', 'warehouseTypes'].forEach(k => {
    if (!Array.isArray(productDropdownOptions[k])) productDropdownOptions[k] = [];
  });
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

    fillMeatFormSelects();
    renderMeatTables();
  }

  function slaughterRemaining(s) {
    const r = N(s?.remainingKg);
    return r > 0.0001 ? r : N(s?.carcassKg);
  }

  function carcassOptionLabel(s) {
    const form = (s.carcassForm || 'whole') === 'half' ? 'Half' : 'Whole';
    const pcs = N(s.halfPieces) > 1 ? ` · ${fmtN(s.halfPieces, 0)} pc` : '';
    return `${esc(s.tag || 'Carcass')} · ${form}${pcs} · ${fmtQ(slaughterRemaining(s), 'kg')} · ${esc(s.dateDisplay || '')}`;
  }

  function carcassOpts(emptyLabel, forms) {
    const want = forms ? new Set(forms) : null;
    const rows = (page.slaughters || []).filter(s => {
      const rem = slaughterRemaining(s);
      if (rem <= 0.0001) return false;
      const form = (s.carcassForm || 'whole').toLowerCase();
      if (want && !want.has(form)) return false;
      return true;
    });
    if (!rows.length) return `<option value="">${esc(emptyLabel)}</option>`;
    return rows.map(s => `<option value="${s.id}">${carcassOptionLabel(s)}</option>`).join('');
  }

  function slaughterById(id) {
    return (page.slaughters || []).find(s => String(s.id) === String(id)) || null;
  }

  function fillMeatFormSelects() {
    setSelectHtml('sl-sg-store', tankOptsMeat());
    setSelectHtml('sl-ct-store', tankOptsMeat());
    setSelectHtml('sl-sp-src', carcassOpts('No whole carcass in stock', ['whole']));
    setSelectHtml('sl-ct-src', carcassOpts('No carcass in stock', ['whole', 'half']));
    fillPaidFromSelects();
    refreshSlaughterGoatDatalist();
    sgLookup();
    spPreview();
    ctPreview();
  }

  function cutProducts() {
    return catalogActiveOnly().filter(p =>
      (p.category || '').toLowerCase() === 'meat' &&
      (p.sku || '').toUpperCase() !== 'MEAT'
    );
  }

  function cutAllocation(totalValue, rows, method) {
    const ok = rows.filter(r => r.kg > 0 && r.productId);
    let weights = ok.map(r => r.kg);
    if (method === 'value') {
      const vw = ok.map(r => r.kg * N((catalogById(r.productId) || {}).defaultPrice));
      if (vw.every(x => x > 0)) weights = vw;
      else method = 'weight';
    }
    const tw = weights.reduce((a, b) => a + b, 0);
    let left = Math.round(totalValue * 100) / 100;
    const vals = ok.map((r, i) => {
      if (i === ok.length - 1) return left;
      const v = tw > 0 ? Math.round(totalValue * weights[i] / tw * 100) / 100 : 0;
      left = Math.round((left - v) * 100) / 100;
      return v;
    });
    return { rows: ok, vals, method };
  }

  function spPreview() {
    const el = document.getElementById('sl-sp-preview');
    if (!el) return;
    const s = slaughterById(document.getElementById('sl-sp-src')?.value);
    if (!s) { el.innerHTML = ''; return; }
    const total = slaughterRemaining(s);
    const aIn = document.getElementById('sl-sp-a');
    const a = N(aIn?.value) > 0 ? N(aIn?.value) : total / 2;
    const b = total - a;
    el.innerHTML = `<span>Carcass weight: <b>${fmtQ(total, 'kg')}</b></span><span>Half A: <b>${fmtQ(a, 'kg')}</b></span><span>Half B: <b class="${b < -0.01 ? 'sl-bad' : ''}">${fmtQ(b, 'kg')}</b></span>`;
  }

  function readCutRows() {
    return [...document.querySelectorAll('#sl-ct-rows tr')].map(tr => ({
      tr,
      productId: +tr.querySelector('.sl-ct-prod')?.value || 0,
      kg: N(tr.querySelector('.sl-ct-kg')?.value)
    }));
  }

  function refreshCutRowOptions() {
    const list = cutProducts();
    const html = list.length
      ? list.map(p => `<option value="${p.id}">${esc(productLabel(p))}</option>`).join('')
      : '<option value="">Add meat cuts in Product Setup first</option>';
    document.querySelectorAll('#sl-ct-rows .sl-ct-prod').forEach(s => {
      const v = s.value;
      s.innerHTML = html;
      if (v && [...s.options].some(o => o.value === v)) s.value = v;
    });
  }

  function addCutRow(productId, kg) {
    const body = document.getElementById('sl-ct-rows');
    if (!body) return;
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><select class="sl-ct-prod"></select></td><td><input class="sl-ct-kg" type="number" min="0" step="0.01" placeholder="0.00" value="${kg || ''}"></td><td class="num-cell sl-ct-val">—</td><td><button type="button" class="btn btn-ghost btn-sm sl-ct-del" aria-label="Remove row">×</button></td>`;
    body.appendChild(tr);
    refreshCutRowOptions();
    if (productId) tr.querySelector('.sl-ct-prod').value = String(productId);
    tr.querySelector('.sl-ct-del')?.addEventListener('click', () => { tr.remove(); ctPreview(); });
    tr.querySelectorAll('input,select').forEach(el => el.addEventListener('input', ctPreview));
    ctPreview();
  }

  function ensureCutRows() {
    const body = document.getElementById('sl-ct-rows');
    if (!body) return;
    if (!body.querySelector('tr')) addCutRow();
  }

  function ctPreview() {
    const el = document.getElementById('sl-ct-preview');
    if (!el) return;
    const s = slaughterById(document.getElementById('sl-ct-src')?.value);
    if (!s) { el.innerHTML = 'Record a carcass first.'; return; }
    const avail = slaughterRemaining(s);
    const inKg = N(document.getElementById('sl-ct-in')?.value) || avail;
    const rws = readCutRows();
    const outKg = rws.reduce((a, r) => a + (r.kg > 0 ? r.kg : 0), 0);
    const waste = inKg - outKg;
    const unitCost = N(s.unitCostPerKg);
    const V = unitCost > 0 ? unitCost * Math.min(inKg, avail) : 0;
    const method = document.getElementById('sl-ct-method')?.value || 'weight';
    const al = cutAllocation(V, rws, method);
    rws.forEach(r => {
      const i = al.rows.indexOf(r);
      const cell = r.tr.querySelector('.sl-ct-val');
      if (cell) cell.textContent = i >= 0 && r.kg > 0 && V > 0 ? `${money(al.vals[i] / r.kg)}/kg` : '—';
    });
    el.innerHTML = `<span>Available: <b>${fmtQ(avail, 'kg')}</b>${N(s.halfPieces) > 1 ? ` (${fmtN(s.halfPieces, 0)} pieces)` : ''}</span><span>Cutting: <b>${fmtQ(inKg, 'kg')}</b></span><span>Cuts weighed: <b>${fmtQ(outKg, 'kg')}</b></span><span>Bone / trim / waste: <b class="${waste < -0.01 ? 'sl-bad' : ''}">${fmtQ(waste, 'kg')}${inKg > 0 ? ` (${pct(waste, inKg)})` : ''}</b></span>${inKg > avail + 0.01 ? '<span class="sl-bad">More than the carcass weighs</span>' : ''}${waste < -0.01 ? '<span class="sl-bad">Cuts weigh more than the carcass — check the scale</span>' : ''}`;
  }

  function renderMeatTables() {
    const carcassRows = document.getElementById('sl-carcass-rows');
    if (carcassRows) {
      const rows = (page.slaughters || []).filter(s => slaughterRemaining(s) > 0.001);
      carcassRows.innerHTML = rows.length ? rows.map(s => {
        const form = (s.carcassForm || 'whole') === 'half' ? 'Half' : 'Whole';
        const pcs = N(s.halfPieces) || 1;
        return `<tr><td>${esc(s.tag || '—')}</td><td>${form}</td><td class="num-cell">${pcs}</td><td class="num-cell">${fmtQ(slaughterRemaining(s), 'kg')}</td><td class="num-cell hide-sm">${s.unitCostPerKg ? money(s.unitCostPerKg) : '—'}</td><td class="num-cell hide-sm">${rs(s.saleAmount)}</td><td class="hide-sm">${s.warehouseId ? esc(whName(s.warehouseId)) : '—'}</td><td></td></tr>`;
      }).join('') : '<tr><td colspan="8" class="empty">No carcasses in stock yet.</td></tr>';
    }
    const cutsRows = document.getElementById('sl-cuts-rows');
    if (cutsRows) {
      const cuts = page.meatCutStock || [];
      cutsRows.innerHTML = cuts.length ? cuts.map(c =>
        `<tr><td>${esc(c.name)}</td><td>${esc(whName(c.warehouseId))}</td><td class="num-cell">${fmtQ(c.qty, c.unit || 'kg')}</td><td class="num-cell hide-sm">${c.unitCost ? money(c.unitCost) : '—'}</td><td class="num-cell hide-sm">—</td><td class="num-cell">—</td><td></td></tr>`
      ).join('') : '<tr><td colspan="7" class="empty">No cuts in stock yet.</td></tr>';
    }
  }

  function liveHerdGoats() {
    return (page.herdGoats || []).slice().sort((a, b) =>
      String(a.tag).localeCompare(String(b.tag), undefined, { numeric: true }));
  }

  function refreshSlaughterGoatDatalist() {
    const dl = document.getElementById('sl-sg-tags');
    if (!dl) return;
    dl.innerHTML = liveHerdGoats().slice(0, 800).map(g =>
      `<option value="${esc(g.tag)}">${esc(`${g.breed || ''} · ${g.gender || ''} · ${g.status || ''}`.trim())}</option>`
    ).join('');
  }

  function sgLookup() {
    const out = document.getElementById('sl-sg-lookup');
    if (!out) return;
    const tag = (document.getElementById('sl-sg-tag')?.value || '').trim();
    if (!tag) {
      out.innerHTML = 'Scan or type the goat tag.';
      out.className = 'sl-lookup';
      return;
    }
    const g = findGoatByTag(tag);
    if (!g) {
      out.innerHTML = 'Tag not in herd — will be recorded as an outside animal.';
      out.className = 'sl-lookup warn';
      return;
    }
    const cost = N(g.purchasePrice);
    out.innerHTML = `✓ <b>${esc(g.tag)}</b>${g.name ? ` · ${esc(g.name)}` : ''} · ${esc(g.breed || '')} · ${esc(g.gender || '')} · ${esc(g.status || '')}` +
      (cost > 0 ? `<br><small>Animal cost: ${rs(cost)} — purchase price</small>` : '');
    out.className = 'sl-lookup ok';
    const ac = document.getElementById('sl-sg-acost');
    if (ac && !ac.dataset.touched && cost > 0) ac.value = Math.round(cost);
    const live = document.getElementById('sl-sg-live');
    if (live && !live.value && N(g.liveWeightKg) > 0) live.value = N(g.liveWeightKg);
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
    else if (tab === 'sales') renderSalesPane();
    else if (tab === 'stock') renderStockPane();
    else if (tab === 'performance') renderBreedPerformance();
    else if (tab === 'transport') renderTransportPane();
    else if (tab === 'costing') renderCostAllocationPane();
    else if (tab === 'pricing') renderPricingPane();
    else if (tab === 'catalog') renderCatalogPane();
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
    const sellBtn = e.target.closest('[data-sl-sell]');
    if (sellBtn?.dataset.slSell) {
      openSale(sellBtn.dataset.slSell, {
        src: sellBtn.dataset.slSrc,
        key: sellBtn.dataset.slKey,
        item: sellBtn.dataset.slItem
      });
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
  function isMeatStore(w) { return tankKind(w) === 'meat_store'; }
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
    dairy_store: '<option value="__add_dairy_store">＋ Add new cold store…</option>',
    meat_store: '<option value="__add_meat_store">＋ Add new meat chiller…</option>'
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

  function tankOptsMeat() {
    const stores = (page.warehouses || []).filter(w => isMeatStore(w) && isActiveWh(w));
    const opts = stores.map(w =>
      `<option value="${w.id}">${esc(w.name)}${N(w.litersOrKg) ? ` — ${fmtQ(w.litersOrKg, 'kg')}` : ''}</option>`
    ).join('');
    const head = stores.length ? '' : '<option value="" disabled selected>No meat chiller yet — add one below</option>';
    return head + opts + TANK_EXTRA.meat_store;
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

  function fillTransportAccountSelect() {
    const el = document.getElementById('tp-account');
    if (!el) return;
    const prev = el.value;
    const accts = page.moneyAccounts || [];
    el.innerHTML = '<option value="">— record expense only —</option>' +
      (accts.length
        ? accts.map(a => `<option value="${a.id}">${esc(a.name)} · ${esc(a.kind || '')}</option>`).join('')
        : '<option value="" disabled>Add cash/bank on Finance → Bank &amp; Cash</option>');
    if (prev && [...el.options].some(o => o.value === prev)) el.value = prev;
  }

  function fillPaidFromSelects() {
    const html = accountOptionsHtml();
    ['sl-pz-acct', 'sl-mk-acct', 'sl-sg-acct', 'sl-sa-acct'].forEach(id => {
      const el = document.getElementById(id);
      if (!el) return;
      const prev = el.value;
      el.innerHTML = html;
      if (prev && [...el.options].some(o => o.value === prev)) el.value = prev;
    });
  }

  function catalogActiveOnly() {
    return (page.catalog || []).filter(c => c.isActive !== false);
  }

  function catalogCategoryLabel(cat) {
    const c = (cat || 'other').toLowerCase();
    if (c === 'dairy') return 'DAIRY';
    if (c === 'meat') return 'MEAT';
    return String(cat || 'other').toUpperCase();
  }

  function catalogSaleGroup(p) {
    if (!p) return 'other';
    const cat = (p.category || '').toLowerCase();
    if (cat === 'meat') return 'meat';
    if (cat === 'dairy') return 'dairy';
    if (cat === 'other') return 'other';
    const sku = (p.sku || '').toUpperCase();
    const name = (p.name || '').toLowerCase();
    if (sku === 'MILK-RAW' || sku === 'MILK-PAST') return 'bulk';
    if (sku === 'MEAT' || /carcass|cut|meat|mince|keema|\braan\b|dasti|rib|chop|liver|kaleji|shank|flank|loin|neck|brisket|trotter|paye/.test(name)) return 'meat';
    if (/milk|yogurt|yoghurt|ghee|cheese|butter|lassi|kefir|dairy|paneer/.test(name) || /bottle/i.test(p.unit || '')) return 'dairy';
    return 'other';
  }

  function saleProducts(type) {
    const group = type === 'dairy' ? 'dairy' : type === 'cut' ? 'meat' : type === 'other' ? 'other' : '';
    if (!group) return [];
    return catalogActiveOnly().filter(p => catalogSaleGroup(p) === group);
  }

  function dairyRowsForProduct(pid) {
    const p = catalogById(pid);
    return (milk.dairyProducts || []).filter(d =>
      (d.productId && d.productId === +pid) ||
      (p && d.name && p.name && d.name.toLowerCase() === p.name.toLowerCase())
    );
  }

  function saleSources(type) {
    if (type === 'raw' || type === 'past') {
      return warehousesOfKind(type === 'raw' ? 'raw_tank' : 'past_tank').map(w => ({
        key: String(w.id),
        label: w.name,
        qty: N(w.litersOrKg),
        unit: 'L',
        productId: catalogId(type === 'raw' ? 'MILK-RAW' : 'MILK-PAST')
      }));
    }
    if (type === 'whole' || type === 'half') {
      const form = type === 'half' ? 'half' : 'whole';
      return (page.slaughters || []).filter(s => {
        const rem = slaughterRemaining(s);
        return rem > 0.001 && (s.carcassForm || 'whole').toLowerCase() === form;
      }).map(s => ({
        key: String(s.id),
        label: carcassOptionLabel(s).replace(/<[^>]+>/g, ''),
        qty: slaughterRemaining(s),
        unit: 'kg',
        count: N(s.halfPieces) || 1,
        productId: catalogId('MEAT')
      }));
    }
    const pid = document.getElementById('sl-sa-prod')?.value;
    if (!pid || String(pid).startsWith('__')) return [];
    const p = catalogById(pid);
    const unit = p?.unit || (type === 'cut' ? 'kg' : 'units');
    if (type === 'dairy') {
      const rows = dairyRowsForProduct(pid).filter(d => d.warehouseId);
      if (rows.length) {
        return rows.map(d => ({
          key: String(d.warehouseId),
          label: whName(d.warehouseId),
          qty: N(d.qty),
          unit: d.unit || unit,
          productId: +pid
        }));
      }
      return warehousesOfKind('dairy_store').map(w => ({
        key: String(w.id),
        label: w.name,
        qty: dairyStockQty(pid),
        unit,
        productId: +pid
      }));
    }
    const kind = type === 'cut' ? 'meat_store' : 'other';
    let stores = warehousesOfKind(kind);
    if (!stores.length && type !== 'cut') {
      stores = (page.warehouses || []).filter(w => isActiveWh(w) && !isRawTank(w) && !isPastTank(w) && !isMeatStore(w));
    }
    return stores.map(w => ({
      key: String(w.id),
      label: w.name,
      qty: N(w.litersOrKg),
      unit,
      productId: +pid
    }));
  }

  function currentSaleSource() {
    const type = document.getElementById('sl-sa-type')?.value || 'raw';
    const key = document.getElementById('sl-sa-src')?.value;
    return saleSources(type).find(s => String(s.key) === String(key)) || null;
  }

  function renderSaleForm(pref) {
    pref = pref || {};
    const typeSel = document.getElementById('sl-sa-type');
    if (!typeSel) return;
    if (pref.type) typeSel.value = pref.type;
    const type = typeSel.value || 'raw';
    const needProd = ['dairy', 'cut', 'other'].includes(type);
    const prodF = document.getElementById('sl-sa-prod-f');
    if (prodF) prodF.hidden = !needProd;
    if (needProd) {
      const list = saleProducts(type);
      const html = list.length
        ? list.map(p => {
            const q = type === 'dairy' ? dairyStockQty(p.id) : 0;
            const extra = type === 'dairy' ? ` — ${fmtQ(q, p.unit || '')}${q > 0.0005 ? '' : ' (none in stock)'}` : '';
            return `<option value="${p.id}">${esc(productLabel(p))}${extra}</option>`;
          }).join('')
        : '<option value="">No products — add one in Product Setup</option>';
      setSelectHtml('sl-sa-prod', html, pref.item);
    }
    const src = saleSources(type);
    const srcL = document.getElementById('sl-sa-src-l');
    if (srcL) {
      srcL.textContent = (type === 'whole' || type === 'half') ? 'Carcass (batch)'
        : (type === 'raw' || type === 'past') ? 'Sell from tank' : 'Sell from store';
    }
    const srcHtml = src.length
      ? src.map(s => {
          const extra = (type === 'raw' || type === 'past') ? ` — ${fmtQ(s.qty, 'L')}`
            : (type === 'whole' || type === 'half') ? '' : ` — ${fmtQ(s.qty, s.unit)}`;
          return `<option value="${esc(s.key)}">${esc(s.label)}${extra}</option>`;
        }).join('')
      : '<option value="">Nothing in stock</option>';
    setSelectHtml('sl-sa-src', srcHtml, pref.key || pref.src);
    const carcass = type === 'whole' || type === 'half';
    const countF = document.getElementById('sl-sa-count-f');
    const basisF = document.getElementById('sl-sa-basis-f');
    if (countF) countF.hidden = type !== 'half';
    if (basisF) basisF.hidden = !carcass;
    if (!carcass) {
      const b = document.getElementById('sl-sa-basis');
      if (b) b.value = 'unit';
    }
    if (pref.type || pref.reset) saleSourceChanged(true);
    else saleAvail();
  }

  function saleSourceChanged(setDefaults) {
    const s = currentSaleSource();
    const type = document.getElementById('sl-sa-type')?.value || 'raw';
    const qtyEl = document.getElementById('sl-sa-qty');
    if (qtyEl) qtyEl.readOnly = !!(s && type === 'whole');
    if (s && type === 'whole' && qtyEl) qtyEl.value = s.qty;
    if (s && type === 'half' && setDefaults) {
      const countEl = document.getElementById('sl-sa-count');
      if (countEl) countEl.value = s.count >= 1 ? 1 : 0;
      if (qtyEl) qtyEl.value = s.count > 1 ? (s.qty / s.count) : s.qty;
    }
    if (s && setDefaults && s.productId) {
      const cid = document.getElementById('sl-sa-cust')?.value;
      const rateEl = document.getElementById('sl-sa-rate');
      if (rateEl && !rateEl.dataset.touched) {
        const rate = cid && cid !== '__add'
          ? clientRateForSale(cid, s.productId)
          : defaultSellPrice(s.productId);
        if (rate > 0) rateEl.value = rate;
      }
    }
    saleAvail();
  }

  function saleAvail() {
    const s = currentSaleSource();
    const type = document.getElementById('sl-sa-type')?.value || 'raw';
    const u = s ? s.unit : (type === 'raw' || type === 'past' ? 'L' : '');
    const unitEl = document.getElementById('sl-sa-unit');
    if (unitEl) unitEl.textContent = u || 'units';
    const basis = document.getElementById('sl-sa-basis')?.value || 'unit';
    const rateL = document.getElementById('sl-sa-rate-l');
    if (rateL) rateL.textContent = basis === 'fixed' ? 'Agreed total price (Rs)' : `Rate per ${u || 'unit'} (Rs)`;
    const qty = +document.getElementById('sl-sa-qty')?.value || 0;
    const rate = +document.getElementById('sl-sa-rate')?.value || 0;
    const amount = basis === 'fixed' ? rate : qty * rate;
    const amtEl = document.getElementById('sl-sa-amt');
    if (amtEl) amtEl.value = amount ? fmtN(amount, 2) : '';
    const av = document.getElementById('sl-sa-avail');
    if (av) {
      av.innerHTML = s
        ? `<b>${fmtQ(s.qty, s.unit)}</b>${s.count ? ` · ${fmtN(s.count, 0)} piece${s.count === 1 ? '' : 's'}` : ''}`
        : '<b>Nothing to sell</b><small>No stock of this type.</small>';
    }
    const pay = document.getElementById('sl-sa-pay')?.value || 'paid';
    const recvF = document.getElementById('sl-sa-recv-f');
    const dueF = document.getElementById('sl-sa-due-f');
    const acctF = document.getElementById('sl-sa-acct-f');
    if (recvF) recvF.hidden = pay !== 'partial';
    if (dueF) dueF.hidden = pay === 'paid';
    if (acctF) acctF.hidden = pay === 'credit';
  }

  function renderSalesPane() {
    fillCustomerSelect('sl-sa-cust');
    fillPaidFromSelects();
    renderSaleForm();
  }

  function openSale(type, opts) {
    opts = opts || {};
    activateProductTab('sales');
    const typeSel = document.getElementById('sl-sa-type');
    if (typeSel && type) typeSel.value = type;
    renderSaleForm({ type, key: opts.key, src: opts.src, item: opts.item, reset: true });
    const review = document.getElementById('sl-sa-review');
    if (review) review.hidden = true;
    document.getElementById('sl-sale-form')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  const KIND_LABEL = {
    raw_tank: 'Raw milk tank (chilled)',
    past_tank: 'Pasteurized milk tank (chilled)',
    dairy_store: 'Dairy cold store',
    meat_store: 'Meat chiller / freezer',
    other: 'Other store'
  };

  const STOCK_OP_LABEL = {
    Receive: 'Stock received',
    Issue: 'Issued from stock',
    Transfer: 'Tank / store transfer',
    Spoilage: 'Spoilage / write-off',
    Adjust: 'Stock adjustment'
  };

  function parseStockItemKey(key) {
    const parts = String(key || '').split('|');
    if (parts.length < 2) return null;
    return {
      productId: +parts[0],
      warehouseId: +parts[1],
      batch: parts[2] || ''
    };
  }

  function stockItemKey(row) {
    return `${row.productId}|${row.warehouseId}|${row.batch || ''}`;
  }

  function productUnitCost(productId) {
    const p = catalogById(productId);
    return p ? N(p.defaultPrice) : 0;
  }

  function bulkMilkQtyAtWarehouse(w, sku) {
    let q = N(w.litersOrKg);
    if (q > 0.0001) return q;
    const tanks = warehousesOfKind(sku === 'MILK-PAST' ? 'past_tank' : 'raw_tank', false);
    if (tanks.length === 1 && tanks[0].id === w.id) {
      return sku === 'MILK-PAST' ? N(milk.pastTankLiters) : N(milk.rawTankLiters);
    }
    return q;
  }

  function bulkMilkLabel(productId, chilledFallback) {
    const p = catalogById(productId);
    return p?.name || chilledFallback;
  }

  function acceptsWarehouse(productId, destWhId) {
    const p = catalogById(productId);
    const w = warehouseById(destWhId);
    if (!p || !w || !isActiveWh(w)) return false;
    const sku = (p.sku || '').toUpperCase();
    const kind = tankKind(w);
    if (sku === 'MILK-RAW') return kind === 'raw_tank';
    if (sku === 'MILK-PAST') return kind === 'past_tank';
    if (sku === 'MEAT') return kind === 'meat_store';
    const group = catalogSaleGroup(p);
    if (group === 'dairy') return kind === 'dairy_store';
    if (group === 'meat') return kind === 'meat_store';
    return kind === 'dairy_store' || kind === 'other' || kind === 'meat_store';
  }

  function balancesAtLocation(locId, includeZero) {
    const w = warehouseById(locId);
    if (!w) return [];
    const kind = tankKind(w);
    const items = [];
    if (kind === 'raw_tank') {
      const pid = catalogId('MILK-RAW');
      const q = bulkMilkQtyAtWarehouse(w, 'MILK-RAW');
      if (pid && (includeZero || q > 0.0001)) {
        items.push({
          productId: pid,
          warehouseId: +locId,
          batch: '',
          qty: q,
          unit: 'L',
          name: bulkMilkLabel(pid, 'Raw milk (chilled)')
        });
      }
    } else if (kind === 'past_tank') {
      const pid = catalogId('MILK-PAST');
      const q = bulkMilkQtyAtWarehouse(w, 'MILK-PAST');
      if (pid && (includeZero || q > 0.0001)) {
        items.push({
          productId: pid,
          warehouseId: +locId,
          batch: '',
          qty: q,
          unit: 'L',
          name: bulkMilkLabel(pid, 'Pasteurized milk (chilled)')
        });
      }
    } else if (kind === 'dairy_store') {
      (milk.dairyProducts || []).filter(d => d.warehouseId === +locId).forEach(d => {
        const q = N(d.qty);
        if (!includeZero && q <= 0.0001) return;
        const pid = d.productId || catalogById(d.name)?.id;
        if (!pid) return;
        const p = catalogById(pid);
        items.push({
          productId: pid,
          warehouseId: +locId,
          batch: '',
          qty: q,
          unit: d.unit || p?.unit || 'units',
          name: p?.name || d.name || 'Product'
        });
      });
    } else if (kind === 'meat_store') {
      const pid = catalogId('MEAT');
      if (pid) {
        (page.slaughters || []).forEach(s => {
          if (s.warehouseId !== +locId) return;
          const q = slaughterRemaining(s);
          if (!includeZero && q <= 0.0001) return;
          const form = (s.carcassForm || 'whole') === 'half' ? 'Half' : 'Whole';
          items.push({
            productId: pid,
            warehouseId: +locId,
            batch: String(s.id),
            qty: q,
            unit: 'kg',
            name: `${form} carcass · ${s.tag || 'Carcass'}`
          });
        });
      }
      (page.meatCutStock || []).filter(c => c.warehouseId === +locId).forEach(c => {
        const q = N(c.qty);
        if (!includeZero && q <= 0.0001) return;
        items.push({
          productId: c.productId,
          warehouseId: +locId,
          batch: '',
          qty: q,
          unit: c.unit || 'kg',
          name: c.name || 'Cut'
        });
      });
    }
    return items;
  }

  function allStockBalanceRows(includeZero) {
    const rows = [];
    (page.warehouses || []).filter(w => isActiveWh(w) || includeZero).forEach(w => {
      balancesAtLocation(w.id, includeZero).forEach(r => rows.push(r));
    });
    return rows;
  }

  function fillStockItemSelect(selId, locId, allowZero) {
    const items = balancesAtLocation(locId, allowZero);
    const html = items.length
      ? items.map(r =>
        `<option value="${esc(stockItemKey(r))}">${esc(r.name)}${r.batch ? ` · ${esc(r.batch)}` : ''} — ${fmtQ(r.qty, r.unit)}</option>`
      ).join('')
      : '<option value="">Nothing in this location</option>';
    setSelectHtml(selId, html);
  }

  function tfTargets() {
    const k = parseStockItemKey(document.getElementById('sl-tf-item')?.value);
    const unitEl = document.getElementById('sl-tf-unit');
    if (!k?.productId) {
      if (unitEl) unitEl.textContent = '';
      setSelectHtml('sl-tf-to', '<option value="">Choose what to move first</option>');
      return;
    }
    const p = catalogById(k.productId);
    if (unitEl) unitEl.textContent = p?.unit || 'L';
    const fromLoc = k.warehouseId;
    const dests = (page.warehouses || []).filter(w =>
      isActiveWh(w) && String(w.id) !== String(fromLoc) && acceptsWarehouse(k.productId, w.id)
    );
    const html = dests.length
      ? dests.map(w => `<option value="${w.id}">${esc(w.name)}</option>`).join('')
      : '<option value="">No other compatible location</option>';
    setSelectHtml('sl-tf-to', html);
  }

  function adjPreview() {
    const k = parseStockItemKey(document.getElementById('sl-adj-item')?.value);
    const u = document.getElementById('sl-adj-unit');
    if (!u) return;
    if (!k?.productId) { u.textContent = ''; return; }
    const p = catalogById(k.productId);
    u.textContent = p?.unit || 'units';
  }

  function isMeatMovement(m) {
    const sku = (m.productSku || '').toUpperCase();
    if (sku === 'MEAT') return true;
    const name = (m.product || '').toLowerCase();
    return name.includes('carcass') || name.includes('meat');
  }

  function ledgerRows() {
    const from = document.getElementById('sl-mv-from')?.value;
    const to = document.getElementById('sl-mv-to')?.value;
    const type = document.getElementById('sl-mv-type')?.value;
    const grp = document.getElementById('sl-mv-group')?.value;
    return (page.movements || []).filter(m => {
      if (from && String(m.dateDisplay) < from) return false;
      if (to && String(m.dateDisplay) > to) return false;
      if (type && m.type !== type) return false;
      if (grp === 'milk' && !isMilkRelatedMovement(m)) return false;
      if (grp === 'meat' && !isMeatMovement(m)) return false;
      return true;
    }).sort((a, b) => String(b.dateDisplay).localeCompare(String(a.dateDisplay)));
  }

  function renderLedger() {
    const typeSel = document.getElementById('sl-mv-type');
    if (typeSel && typeSel.options.length < 2) {
      typeSel.innerHTML = '<option value="">All operations</option>' +
        Object.entries(STOCK_OP_LABEL).map(([k, v]) => `<option value="${k}">${esc(v)}</option>`).join('');
    }
    const tb = document.getElementById('sl-mv-rows');
    if (!tb) return;
    const rows = ledgerRows();
    tb.innerHTML = rows.length
      ? rows.slice(0, 200).map(m =>
        `<tr><td>${esc(m.dateDisplay)}</td><td><b>${esc(STOCK_OP_LABEL[m.type] || m.type)}</b></td><td>${describeMovement(m)}</td><td class="hide-sm">—</td><td></td></tr>`
      ).join('') + (rows.length > 200 ? '<tr><td colspan="5" class="empty">Showing 200 of ' + rows.length + '. Narrow the dates or export CSV.</td></tr>' : '')
      : '<tr><td colspan="5" class="empty">No movements for this filter.</td></tr>';
  }

  function exportStockLedgerCsv() {
    const rows = [['Date', 'Operation', 'Product', 'Warehouse', 'To warehouse', 'Qty', 'Notes']];
    ledgerRows().forEach(m => {
      rows.push([
        m.dateDisplay,
        STOCK_OP_LABEL[m.type] || m.type,
        m.product,
        m.warehouse,
        m.toWarehouse || '',
        m.qty,
        m.notes || ''
      ]);
    });
    const csv = rows.map(r => r.map(c => {
      const s = String(c ?? '');
      return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
    }).join(',')).join('\n');
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    a.download = 'stock-movements-' + today() + '.csv';
    a.click();
  }

  function renderRecon() {
    const el = document.getElementById('sl-recon');
    if (!el) return;
    const neg = allStockBalanceRows(false).filter(r => r.qty < -0.0005);
    const moves = page.movements || [];
    const sales = page.sales || [];
    const ok = !neg.length;
    el.innerHTML = `<div class="sl-recon ${ok ? 'ok' : 'bad'}"><b>${ok ? '✓ Stock levels look consistent' : '⚠ Items need checking'}</b>
      <ul>
        <li>${moves.length} stock movement${moves.length === 1 ? '' : 's'} in the ledger (latest ${Math.min(moves.length, 120)} loaded on this page).</li>
        <li>${neg.length ? `<b class="sl-bad">${neg.length} location${neg.length === 1 ? '' : 's'} below zero</b> — post a “Stock count correction” after a physical count.` : 'No location is below zero based on current balances.'}</li>
        <li>${sales.length} product sale${sales.length === 1 ? '' : 's'} this month on the sales register.</li>
      </ul></div>`;
  }

  function renderLocationRows() {
    const lr = document.getElementById('sl-loc-rows');
    if (!lr) return;
    const defRaw = defaultRawTankId();
    lr.innerHTML = (page.warehouses || []).map(w => {
      const k = tankKind(w);
      const off = w.isActive === false;
      const isDef = k === 'raw_tank' && String(w.id) === defRaw;
      const meta = tankMetaGet(w.id);
      const cap = meta.capacity != null ? meta.capacity : (w.capacity != null ? w.capacity : '');
      const temp = meta.targetTempC != null ? meta.targetTempC : (w.targetTempC != null ? w.targetTempC : '');
      const site = meta.site != null ? meta.site : 'Farm';
      return `<tr class="${off ? 'sl-muted' : ''}"><td><b>${esc(w.name)}</b>${isDef ? ' <span class="chip chip-milk">milking goes here</span>' : ''}${off ? ' <span class="chip chip-exp">inactive</span>' : ''}<div class="name">${esc(site)}</div></td><td>${esc(KIND_LABEL[k] || k)}</td><td class="num-cell">${cap === '' ? '—' : fmtN(cap)}</td><td class="num-cell">${temp === '' ? '—' : esc(String(temp)) + '°C'}</td><td style="white-space:nowrap">${k === 'raw_tank' && !isDef && !off ? `<button type="button" class="btn btn-ghost btn-sm" data-sl-defraw="${w.id}">Use for milking</button>` : ''}<button type="button" class="btn btn-ghost btn-sm" data-sl-togloc="${w.id}">${off ? 'Enable' : 'Disable'}</button></td></tr>`;
    }).join('') || '<tr><td colspan="5" class="empty">No tanks or stores yet.</td></tr>';
  }

  function renderStockPane() {
    try {
    document.querySelectorAll('.prod-pane[data-ppane="stock"] [data-sl-form]').forEach(p => { p.hidden = false; });
    const g = document.getElementById('sl-stock-groups');
    if (g) {
      const tanks = (page.warehouses || []).filter(w => isRawTank(w) || isPastTank(w));
      const tankRows = tanks.map(w => {
        const q = isRawTank(w)
          ? bulkMilkQtyAtWarehouse(w, 'MILK-RAW')
          : bulkMilkQtyAtWarehouse(w, 'MILK-PAST');
        if (w.isActive === false && Math.abs(q) < 0.0005) return '';
        const k = tankKind(w);
        const cost = productUnitCost(k === 'raw_tank' ? catalogId('MILK-RAW') : catalogId('MILK-PAST'));
        const val = q * cost;
        const cap = N(w.capacity);
        const fill = cap > 0 ? pct(q, cap) : '—';
        return `<tr><td><b>${esc(w.name)}</b>${w.isActive === false ? ' <span class="chip chip-exp">inactive</span>' : ''}</td><td>${esc(KIND_LABEL[k] || k)}</td><td class="num-cell"><b class="${q < -0.001 ? 'sl-bad' : ''}">${fmtQ(q, 'L')}</b></td><td class="num-cell hide-sm">${q > 0.0005 && cost > 0 ? rs(cost) + '/L' : '—'}</td><td class="num-cell hide-sm">${val > 0 ? rs(val) : '—'}</td><td class="num-cell hide-sm">${fill}</td></tr>`;
      }).join('');
      const prodRows = allStockBalanceRows(false)
        .filter(r => {
          const w = warehouseById(r.warehouseId);
          return w && !isRawTank(w) && !isPastTank(w);
        })
        .sort((a, b) => whName(a.warehouseId).localeCompare(whName(b.warehouseId)) || a.name.localeCompare(b.name));
      const totalV = prodRows.reduce((n, r) => n + r.qty * productUnitCost(r.productId), 0)
        + tanks.reduce((n, w) => {
          const q = isRawTank(w)
            ? bulkMilkQtyAtWarehouse(w, 'MILK-RAW')
            : bulkMilkQtyAtWarehouse(w, 'MILK-PAST');
          const pid = isRawTank(w) ? catalogId('MILK-RAW') : catalogId('MILK-PAST');
          return n + q * productUnitCost(pid);
        }, 0);
      const prodHtml = prodRows.map(r => {
        const cost = productUnitCost(r.productId);
        const val = r.qty * cost;
        return `<tr><td>${esc(whName(r.warehouseId))}</td><td><b>${esc(r.name)}</b>${r.batch ? `<div class="name">${esc(r.batch)}</div>` : ''}</td><td class="num-cell"><b class="${r.qty < -0.001 ? 'sl-bad' : ''}">${fmtQ(r.qty, r.unit)}</b></td><td class="num-cell hide-sm">${cost > 0 ? rs(cost) + '/' + esc(r.unit) : '—'}</td><td class="num-cell hide-sm">${val > 0 ? rs(val) : '—'}</td></tr>`;
      }).join('');
      g.innerHTML = `<div class="panel"><div class="panel-head"><h2>Milk tanks</h2><span class="sub">litres · raw and pasteurized are never mixed</span></div><div class="panel-body table-wrap"><table class="tbl"><thead><tr><th>Tank</th><th>Type</th><th class="num-cell">In tank</th><th class="num-cell hide-sm">Cost</th><th class="num-cell hide-sm">Value</th><th class="num-cell hide-sm">Full</th></tr></thead><tbody>${tankRows || '<tr><td colspan="6" class="empty">No tanks.</td></tr>'}</tbody></table></div></div>
        <div class="panel"><div class="panel-head"><h2>Products, carcasses &amp; cuts</h2><span class="sub">each in its own unit · stock value at cost ${rs(totalV)}</span></div><div class="panel-body table-wrap"><table class="tbl"><thead><tr><th>Store</th><th>Item</th><th class="num-cell">On hand</th><th class="num-cell hide-sm">Cost</th><th class="num-cell hide-sm">Value</th></tr></thead><tbody>${prodHtml || '<tr><td colspan="5" class="empty">No finished products or meat in stock.</td></tr>'}</tbody></table></div></div>`;
    }
    const locAll = (page.warehouses || []).filter(w => isActiveWh(w)).map(w =>
      `<option value="${w.id}">${esc(w.name)}</option>`
    ).join('');
    const locOpts = locAll || '<option value="">No locations</option>';
    const defLoc = defaultRawTankId()
      || String((page.warehouses || []).find(w => isActiveWh(w))?.id || '');
    setSelectHtml('sl-tf-from', locOpts, defLoc);
    setSelectHtml('sl-adj-loc', locOpts, defLoc);
    const fromLoc = document.getElementById('sl-tf-from')?.value;
    const adjLoc = document.getElementById('sl-adj-loc')?.value;
    fillStockItemSelect('sl-tf-item', fromLoc, false);
    fillStockItemSelect('sl-adj-item', adjLoc, true);
    tfTargets();
    adjPreview();
    ['sl-tf-date', 'sl-adj-date'].forEach(id => {
      const el = document.getElementById(id);
      if (el && !el.value) el.value = today();
    });
    renderLocationRows();
    renderLedger();
    renderRecon();
    const locPanel = document.getElementById('sl-locations');
    if (locPanel && (page.warehouses || []).length) locPanel.hidden = false;
    } catch (err) {
      console.error('renderStockPane failed', err);
    }
  }

  async function addLocationFromForm() {
    const name = document.getElementById('sl-loc-name')?.value?.trim();
    const kind = document.getElementById('sl-loc-kind')?.value || 'other';
    if (!name) { alert('Enter a name.'); return; }
    if ((page.warehouses || []).some(x => String(x.name).toLowerCase() === name.toLowerCase())) {
      alert('A location with this name already exists.');
      return;
    }
    await post('/Products/SaveWarehouse', {
      name,
      location: kind,
      isActive: true
    });
    reload();
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
    const labels = { raw_tank: 'raw milk tank', past_tank: 'pasteurized milk tank', dairy_store: 'cold store', meat_store: 'meat chiller / freezer' };
    const prefixes = { raw_tank: 'Raw Milk Tank ', past_tank: 'Pasteurized Milk Tank ', dairy_store: 'Cold Store ', meat_store: 'Meat chiller ' };
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
    dl.innerHTML = (page.herdGoats || [])
      .filter(g => String(g.gender || '').toLowerCase() !== 'male')
      .map(g =>
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

  async function quickAddMeatStore() {
    const n = warehousesOfKind('meat_store', true).length + 1;
    const name = (prompt('Name of the new meat chiller / freezer:', `Meat chiller ${n}`) || '').trim();
    if (!name) { fillMeatFormSelects(); return; }
    if ((page.warehouses || []).some(x => String(x.name).toLowerCase() === name.toLowerCase())) {
      alert('That name is already used.');
      fillMeatFormSelects();
      return;
    }
    await post('/Products/SaveWarehouse', { name, location: 'meat_store' });
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

      if (t.matches?.('select[data-custom-product-dropdown]') && v === '__addnew__') {
        e.stopImmediatePropagation();
        await addProductDropdownValue(t);
        return;
      }
      if (v === '__addproduct' && t.tagName === 'SELECT') {
        e.stopImmediatePropagation();
        openProductSetup('');
        t.value = '';
        return;
      }
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
      if (v === '__add_meat_store') {
        e.stopImmediatePropagation();
        await quickAddMeatStore();
        return;
      }
      if (x === 'sl-sg-date') {
        sgLookup();
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
      if ((x === 'sl-sa-cust' || x === 'cp-client') && v === '__add') {
        e.stopImmediatePropagation();
        const name = (prompt('Customer name:') || '').trim();
        if (!name) { t.value = ''; return; }
        try {
          const data = await postJson('/Finance/SaveBusinessCustomer', { name });
          page.customers = [...(page.customers || []), { id: data.id, name }];
          fillCustomerSelect('sl-sa-cust');
          fillCustomerSelect('cp-client');
          t.value = String(data.id);
        } catch (err) {
          alert(err.message || 'Could not add customer. Add them on Accounting → Sales & receivables.');
          t.value = '';
        }
        if (x === 'sl-sa-cust') saleSourceChanged(true);
        return;
      }
      if (x === 'pr-product') {
        if (v === '__addproduct') {
          e.stopImmediatePropagation();
          openProductSetup('');
          t.value = '';
          return;
        }
        if (pricingEditingProductId != null) return;
        loadPricingEditorFromProduct();
        return;
      }
      if (x === 'rp-product' || x === 'cp-product') {
        if (v === '__addproduct') {
          e.stopImmediatePropagation();
          openProductSetup('');
          t.value = '';
        }
        return;
      }
      if (x === 'sl-sa-type') {
        const review = document.getElementById('sl-sa-review');
        if (review) review.hidden = true;
        const rateEl = document.getElementById('sl-sa-rate');
        if (rateEl) { delete rateEl.dataset.touched; rateEl.value = ''; }
        const qtyEl = document.getElementById('sl-sa-qty');
        if (qtyEl) qtyEl.value = '';
        renderSaleForm({ type: v, reset: true });
        return;
      }
      if (x === 'sl-sa-prod') {
        const review = document.getElementById('sl-sa-review');
        if (review) review.hidden = true;
        const rateEl = document.getElementById('sl-sa-rate');
        if (rateEl) delete rateEl.dataset.touched;
        renderSaleForm({});
        saleSourceChanged(true);
        return;
      }
      if (x === 'sl-sa-src' || x === 'sl-sa-cust') {
        const review = document.getElementById('sl-sa-review');
        if (review) review.hidden = true;
        if (x === 'sl-sa-cust') {
          const rateEl = document.getElementById('sl-sa-rate');
          if (rateEl) delete rateEl.dataset.touched;
        }
        saleSourceChanged(true);
        return;
      }
      if (x === 'sl-sa-basis' || x === 'sl-sa-pay' || x === 'sl-sa-date') {
        const review = document.getElementById('sl-sa-review');
        if (review) review.hidden = true;
        saleAvail();
      }
      if (x === 'sl-tf-from') {
        fillStockItemSelect('sl-tf-item', v, false);
        tfTargets();
        return;
      }
      if (x === 'sl-tf-item') {
        tfTargets();
        return;
      }
      if (x === 'sl-adj-loc') {
        fillStockItemSelect('sl-adj-item', v, true);
        adjPreview();
        return;
      }
      if (x === 'sl-adj-item') {
        adjPreview();
        return;
      }
      if (['sl-mv-from', 'sl-mv-to', 'sl-mv-type', 'sl-mv-group'].includes(x)) {
        renderLedger();
      }
    }, true);

    root.addEventListener('input', e => {
      const t = e.target;
      if (t.closest('#sl-mg-breed-rows,#sl-mg-goat-rows') || ['mp-goats', 'mp-total', 'mp-reject'].includes(t.id)) mgSummary();
      if (['sl-pz-in', 'sl-pz-out', 'sl-pz-cost'].includes(t.id)) pzPreview();
      if (t.id === 'sl-mvm-qty') mvPreview();
      if (t.id?.startsWith('sl-mk-')) mkPreview();
      if (t.id?.startsWith('sl-sp2-')) spoilPreview();
      if (t.id === 'sl-sg-tag') sgLookup();
      if (t.id === 'sl-sg-acost') t.dataset.touched = '1';
      if (t.id === 'sl-sa-rate') t.dataset.touched = '1';
      if (['sl-sa-qty', 'sl-sa-rate', 'sl-sa-recv', 'sl-sa-count'].includes(t.id)) {
        const review = document.getElementById('sl-sa-review');
        if (review) review.hidden = true;
        saleAvail();
      }
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
    document.querySelectorAll('.prod-pane[data-ppane="meat"] .sl-actions [data-sl-action]').forEach(b => {
      b.classList.toggle('active', b.dataset.slAction === name);
    });
    const meatPane = document.querySelector('.prod-pane[data-ppane="meat"]');
    if (!meatPane) return;
    meatPane.querySelectorAll('[data-sl-form]').forEach(p => { p.hidden = p.dataset.slForm !== name; });
    if (name === 'split') spPreview();
    if (name === 'cut') { ensureCutRows(); ctPreview(); }
  }
  document.querySelectorAll('.sl-actions [data-sl-action]').forEach(btn => {
    btn.addEventListener('click', () => setMeatForm(btn.dataset.slAction));
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
      fillMeatFormSelects();
      renderSalesPane();
      renderStockPane();
      renderBreedPerformance();
      renderTransportPane();
      renderCostAllocationPane();
      renderPricingPane();
      renderCatalogPane();
      refreshProductCustomDropdowns();
    } catch (err) {
      console.error('Products page init failed', err);
    }
  }

  const bpMonth = document.getElementById('bp-month');
  if (bpMonth && page.month) bpMonth.value = page.month;

  function fillCustomerSelect(id) {
    const el = document.getElementById(id);
    if (!el) return;
    const prev = el.value;
    const lead = id === 'cp-client'
      ? '<option value="">Select client…</option>'
      : '<option value="">Walk-in / retail</option>';
    el.innerHTML = lead +
      (page.customers || []).map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('') +
      ((id === 'sl-sa-cust' || id === 'cp-client') ? '<option value="__add">＋ Add new customer…</option>' : '');
    if (prev && [...el.options].some(o => o.value === prev)) el.value = prev;
  }
  fillCustomerSelect('sl-sa-cust');
  fillCustomerSelect('cp-client');

  function pricingForProduct(productId) {
    return (page.productPricing || []).find(x => x.productId === +productId) || null;
  }

  function landedUnitCostPricing(productId) {
    const x = pricingForProduct(productId);
    if (!x) return 0;
    return N(x.base) + N(x.packaging) + N(x.transport) + N(x.storage) + N(x.other);
  }

  function defaultSellPrice(productId) {
    const x = pricingForProduct(productId);
    const p = catalogById(productId);
    if (x && N(x.sell) > 0) return N(x.sell);
    return p ? N(p.defaultPrice) : 0;
  }

  function clientRateForSale(clientId, productId) {
    const row = (page.clientPrices || []).find(x => x.clientId === +clientId && x.productId === +productId);
    return row && N(row.rate) > 0 ? N(row.rate) : defaultSellPrice(productId);
  }

  function routeUnitCost(route) {
    return route && N(route.qty) > 0 ? N(route.cost) / N(route.qty) : 0;
  }

  let pricingEditingProductId = null;

  function hasStoredPricingProfile(productId) {
    return !!(page.productPricing || []).some(x => x.productId === +productId);
  }

  function setProductPricingEditMode(editing) {
    const cancelBtn = document.getElementById('cancelProductPricing');
    if (cancelBtn) cancelBtn.style.display = editing ? '' : 'none';
  }

  function resetProductPricingForm() {
    pricingEditingProductId = null;
    const title = document.getElementById('productPricingFormTitle');
    const saveBtn = document.getElementById('saveProductPricing');
    if (title) title.textContent = 'Product cost & default selling price';
    if (saveBtn) saveBtn.textContent = '+ Save pricing';
    const prod = document.getElementById('pr-product');
    if (prod) prod.disabled = false;
    const deleteBtn = document.getElementById('deleteProductPricing');
    if (deleteBtn) deleteBtn.style.display = 'none';
    ['pr-base', 'pr-pack', 'pr-transport', 'pr-storage', 'pr-other', 'pr-sell'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.value = '';
    });
    document.querySelectorAll('#productPricingRows tr.pricing-row.editing').forEach(r => r.classList.remove('editing'));
    setProductPricingEditMode(false);
    updatePricingPreview();
  }

  function loadPricingEditorFromProduct() {
    const pidRaw = document.getElementById('pr-product')?.value;
    if (!pidRaw || pidRaw.startsWith('__')) return;
    const x = pricingForProduct(+pidRaw) || {};
    const set = (id, v) => {
      const e = document.getElementById(id);
      if (e && document.activeElement !== e) e.value = (v === 0 || v) ? v : '';
    };
    set('pr-base', x.base);
    set('pr-pack', x.packaging);
    set('pr-transport', x.transport);
    set('pr-storage', x.storage);
    set('pr-other', x.other);
    set('pr-sell', N(x.sell) > 0 ? x.sell : defaultSellPrice(+pidRaw));
    updatePricingPreview();
  }

  function fillProductPricingForm(productId) {
    const p = catalogById(productId);
    if (!p) return;
    pricingEditingProductId = productId;
    const title = document.getElementById('productPricingFormTitle');
    const saveBtn = document.getElementById('saveProductPricing');
    if (title) title.textContent = 'Edit product pricing';
    if (saveBtn) saveBtn.textContent = 'Save';
    fillPricingProductSelect('pr-product');
    const prod = document.getElementById('pr-product');
    if (prod) {
      prod.value = String(productId);
      prod.disabled = true;
    }
    loadPricingEditorFromProduct();
    const deleteBtn = document.getElementById('deleteProductPricing');
    if (deleteBtn) deleteBtn.style.display = hasStoredPricingProfile(productId) ? '' : 'none';
    setProductPricingEditMode(true);
    document.querySelectorAll('#productPricingRows tr.pricing-row.editing').forEach(r => r.classList.remove('editing'));
    const row = document.querySelector(`#productPricingRows tr[data-pricing-product-id="${productId}"]`);
    if (row) {
      row.classList.add('editing');
      row.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
    document.getElementById('productPricingFormPanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function loadProductPricingForEdit(productId) {
    fillProductPricingForm(productId);
  }

  function readProductPricingPayload() {
    const pidRaw = document.getElementById('pr-product')?.value;
    if (!pidRaw || pidRaw.startsWith('__')) throw new Error('Select a product.');
    const productId = pricingEditingProductId != null ? pricingEditingProductId : +pidRaw;
    return {
      productId,
      base: N(document.getElementById('pr-base')?.value),
      packaging: N(document.getElementById('pr-pack')?.value),
      transport: N(document.getElementById('pr-transport')?.value),
      storage: N(document.getElementById('pr-storage')?.value),
      other: N(document.getElementById('pr-other')?.value),
      sell: N(document.getElementById('pr-sell')?.value)
    };
  }

  function fillPricingProductSelect(id) {
    const el = document.getElementById(id);
    if (!el) return;
    const prev = el.value;
    const wasDisabled = el.disabled;
    const opts = catalogActiveOnly().map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
    el.innerHTML = (opts || '<option value="">No products yet</option>') +
      '<option value="__addproduct">＋ Add new product…</option>';
    if (prev && [...el.options].some(o => o.value === prev)) el.value = prev;
    if (id === 'pr-product') el.disabled = wasDisabled;
  }

  function updateRoutePreview() {
    const q = N(document.getElementById('rp-qty')?.value);
    const c = N(document.getElementById('rp-cost')?.value);
    const el = document.getElementById('rp-unit');
    if (el) el.textContent = rs(q > 0 ? c / q : 0) + ' / unit';
  }

  function renderPricingPane() {
    fillPricingProductSelect('pr-product');
    fillPricingProductSelect('rp-product');
    fillPricingProductSelect('cp-product');
    fillCustomerSelect('cp-client');

    const rows = document.getElementById('productPricingRows');
    if (rows) {
      rows.innerHTML = (page.catalog || []).map(p => {
        const x = pricingForProduct(p.id) || {};
        const cost = landedUnitCostPricing(p.id);
        const sell = defaultSellPrice(p.id);
        const editing = pricingEditingProductId === p.id ? ' editing' : '';
        return `<tr class="pricing-row${editing}" data-pricing-product-id="${p.id}" title="Click to edit"><td><b>${esc(p.name)}</b></td><td class="num-cell">${rs(x.base || 0)}</td><td class="num-cell hide-sm">${rs(x.packaging || 0)}</td><td class="num-cell hide-sm">${rs(x.transport || 0)}</td><td class="num-cell hide-sm">${rs(x.storage || 0)}</td><td class="num-cell"><b>${rs(cost)}</b></td><td class="num-cell">${rs(sell)}</td><td class="num-cell"><b>${rs(sell - cost)}</b></td></tr>`;
      }).join('') || '<tr><td colspan="8" class="empty">No products configured.</td></tr>';
    }

    const routes = page.deliveryRoutes || [];
    const routeBody = document.getElementById('routePricingRows');
    if (routeBody) {
      routeBody.innerHTML = routes.map(r => {
        const u = routeUnitCost(r);
        const base = landedUnitCostPricing(r.productId);
        return `<tr><td><b>${esc(r.name)}</b></td><td>${esc(r.productName || '')}</td><td class="num-cell">${fmtN(r.qty)}</td><td class="num-cell">${rs(r.cost)}</td><td class="num-cell"><b>${rs(u)}</b></td><td class="num-cell hide-sm">${rs(base)}</td><td class="num-cell"><b>${rs(base + u)}</b></td><td><button type="button" class="del" data-del-route="${r.id}" title="Delete">×</button></td></tr>`;
      }).join('') || '<tr><td colspan="8" class="empty">No delivery routes saved yet.</td></tr>';
    }

    const clientBody = document.getElementById('clientPricingRows');
    if (clientBody) {
      clientBody.innerHTML = (page.clientPrices || []).map(x => {
        return `<tr><td><b>${esc(x.clientName || 'Unknown')}</b></td><td>${esc(x.productName || '')}</td><td class="num-cell"><b>${rs(x.rate)}</b></td><td class="num-cell hide-sm">${rs(defaultSellPrice(x.productId))}</td><td><button type="button" class="del" data-del-client-price="${x.id}" title="Delete">×</button></td></tr>`;
      }).join('') || '<tr><td colspan="5" class="empty">No special client prices yet.</td></tr>';
    }

    updateRoutePreview();
    if (pricingEditingProductId != null) {
      const prod = document.getElementById('pr-product');
      if (prod) {
        prod.value = String(pricingEditingProductId);
        prod.disabled = true;
      }
      loadPricingEditorFromProduct();
    } else {
      loadPricingEditorFromProduct();
    }
  }

  function refreshProductCustomDropdowns() {
    document.querySelectorAll('#view-milk select[data-custom-product-dropdown]').forEach(sel => {
      const key = sel.dataset.customProductDropdown;
      if (!key) return;
      const current = sel.value;
      const add = sel.querySelector('option[value="__addnew__"]');
      (productDropdownOptions[key] || []).forEach(v => {
        if (![...sel.options].some(o => o.value === v)) {
          const o = document.createElement('option');
          o.value = v;
          o.textContent = v;
          if (add) sel.insertBefore(o, add);
          else sel.appendChild(o);
        }
      });
      if ([...sel.options].some(o => o.value === current)) sel.value = current;
    });
  }

  async function addProductDropdownValue(sel) {
    const key = sel.dataset.customProductDropdown;
    if (!key) return;
    const labels = {
      costTypes: 'cost type',
      shipmentTypes: 'shipment type',
      units: 'unit',
      categories: 'product category',
      warehouseTypes: 'warehouse type'
    };
    const name = (prompt('Enter new ' + (labels[key] || 'option') + ':') || '').trim();
    if (!name) { sel.selectedIndex = 0; return; }
    productDropdownOptions[key] = productDropdownOptions[key] || [];
    if (!productDropdownOptions[key].some(x => String(x).toLowerCase() === name.toLowerCase())) {
      productDropdownOptions[key].push(name);
    }
    try {
      await postJson('/Products/SaveProductDropdownOptions', productDropdownOptions);
    } catch (err) {
      alert(err.message || 'Could not save custom option.');
      return;
    }
    refreshProductCustomDropdowns();
    sel.value = name;
  }

  let catalogEditingId = null;

  function isSystemCatalogSku(sku) {
    const s = (sku || '').toUpperCase();
    return s === 'MILK-RAW' || s === 'MILK-PAST' || s === 'MEAT';
  }

  function ensureCatalogSelectOption(sel, value) {
    if (!sel || value == null || value === '' || String(value).startsWith('__')) return;
    const v = String(value);
    if ([...sel.options].some(o => o.value === v || o.textContent === v)) return;
    const add = sel.querySelector('option[value="__addnew__"]');
    const opt = document.createElement('option');
    opt.value = v;
    opt.textContent = v;
    if (add) sel.insertBefore(opt, add);
    else sel.appendChild(opt);
  }

  function setCatalogEditMode(editing) {
    const cancelBtn = document.getElementById('cancelProductCatalog');
    const deleteBtn = document.getElementById('deleteProductCatalog');
    if (cancelBtn) cancelBtn.style.display = editing ? '' : 'none';
    if (deleteBtn) deleteBtn.style.display = editing ? '' : 'none';
  }

  function resetCatalogForm() {
    catalogEditingId = null;
    const title = document.getElementById('catalogFormTitle');
    const saveBtn = document.getElementById('addProductCatalog');
    if (title) title.textContent = 'Add product / pack size';
    if (saveBtn) saveBtn.textContent = '+ Add product';
    const cat = document.getElementById('pc-category');
    if (cat) cat.value = 'dairy';
    const meat = document.getElementById('v221-meat-catalog-type');
    if (meat) meat.style.display = 'none';
    ['pc-name', 'pc-pack', 'pc-price', 'pc-sku'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.value = '';
    });
    const unit = document.getElementById('pc-unit');
    if (unit) unit.value = 'Pack';
    const treat = document.getElementById('pc-treatment');
    if (treat) treat.value = 'other';
    const meatType = document.getElementById('v221-pc-meat-type');
    if (meatType) meatType.value = 'cuts';
    const sku = document.getElementById('pc-sku');
    if (sku) sku.disabled = false;
    document.querySelectorAll('#productCatalogRows tr.catalog-row.editing').forEach(r => r.classList.remove('editing'));
    setCatalogEditMode(false);
  }

  function fillCatalogForm(p) {
    if (!p) return;
    catalogEditingId = p.id;
    const title = document.getElementById('catalogFormTitle');
    const saveBtn = document.getElementById('addProductCatalog');
    if (title) title.textContent = 'Edit product';
    if (saveBtn) saveBtn.textContent = 'Save';
    const cat = document.getElementById('pc-category');
    const category = p.category || 'other';
    ensureCatalogSelectOption(cat, category);
    if (cat) cat.value = category;
    const meat = document.getElementById('v221-meat-catalog-type');
    if (meat) meat.style.display = category === 'meat' ? '' : 'none';
    const unitEl = document.getElementById('pc-unit');
    ensureCatalogSelectOption(unitEl, p.unit);
    if (unitEl) unitEl.value = p.unit || 'L';
    document.getElementById('pc-name').value = p.name || '';
    document.getElementById('pc-pack').value = p.pack || '';
    document.getElementById('pc-price').value = p.defaultPrice ?? '';
    document.getElementById('pc-sku').value = p.sku || '';
    document.getElementById('pc-sku').disabled = isSystemCatalogSku(p.sku);
    const treat = document.getElementById('pc-treatment');
    if (treat) treat.value = p.milkTreatment || 'other';
    const meatType = document.getElementById('v221-pc-meat-type');
    if (meatType && p.meatType) meatType.value = p.meatType;
    const deleteBtn = document.getElementById('deleteProductCatalog');
    if (deleteBtn) deleteBtn.style.display = isSystemCatalogSku(p.sku) ? 'none' : '';
    setCatalogEditMode(true);
    document.querySelectorAll('#productCatalogRows tr.catalog-row.editing').forEach(r => r.classList.remove('editing'));
    const row = document.querySelector(`#productCatalogRows tr[data-catalog-id="${p.id}"]`);
    if (row) {
      row.classList.add('editing');
      row.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
    document.getElementById('catalogFormPanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function loadCatalogForEdit(id) {
    fillCatalogForm(catalogById(id));
  }

  function readCatalogFormPayload() {
    let category = document.getElementById('pc-category')?.value || 'other';
    if (category.startsWith('__')) throw new Error('Choose a category.');
    let unit = document.getElementById('pc-unit')?.value || 'L';
    if (unit.startsWith('__')) throw new Error('Choose a stock unit.');
    const name = document.getElementById('pc-name')?.value?.trim() || '';
    if (!name) throw new Error('Enter a product name.');
    const pack = document.getElementById('pc-pack')?.value?.trim() || '';
    const meatType = category === 'meat'
      ? (document.getElementById('v221-pc-meat-type')?.value || 'cuts')
      : null;
    const skuRaw = document.getElementById('pc-sku')?.value?.trim();
    const sku = skuRaw || name.replace(/\s+/g, '-').toUpperCase().slice(0, 24);
    return {
      id: catalogEditingId || undefined,
      sku,
      name,
      category,
      pack,
      unit,
      defaultPrice: N(document.getElementById('pc-price')?.value),
      milkTreatment: document.getElementById('pc-treatment')?.value || null,
      meatType
    };
  }

  function openProductSetup(category) {
    activateProductTab('catalog');
    resetCatalogForm();
    setTimeout(() => {
      const c = document.getElementById('pc-category');
      if (c && category && [...c.options].some(o => o.value === category)) c.value = category;
      const meat = document.getElementById('v221-meat-catalog-type');
      if (meat) meat.style.display = c?.value === 'meat' ? '' : 'none';
      document.getElementById('pc-name')?.focus();
    }, 60);
  }

  function renderCatalogPane() {
    const meat = document.getElementById('v221-meat-catalog-type');
    const catSel = document.getElementById('pc-category');
    if (meat && catSel) meat.style.display = catSel.value === 'meat' ? '' : 'none';

    const body = document.getElementById('productCatalogRows');
    if (body) {
      body.innerHTML = (page.catalog || []).map(p => {
        const active = p.isActive !== false;
        const editing = catalogEditingId === p.id ? ' editing' : '';
        return `<tr class="catalog-row${editing}" data-catalog-id="${p.id}" title="Click to edit"><td><span class="chip">${esc(catalogCategoryLabel(p.category))}</span></td><td><b>${esc(p.name)}</b></td><td>${esc(p.pack || '—')}</td><td>${esc(p.unit || 'Unit')}</td><td class="num-cell">${rs(p.defaultPrice)}</td><td class="hide-sm">${esc(p.sku || '—')}</td><td><span class="chip ${active ? 'chip-milk' : 'chip-exp'}">${active ? 'Active' : 'Inactive'}</span></td><td><button type="button" class="btn btn-ghost btn-sm" data-toggle-product="${p.id}">${active ? 'Disable' : 'Enable'}</button></td></tr>`;
      }).join('') || '<tr><td colspan="8" class="empty">No products in catalogue yet.</td></tr>';
    }
    refreshProductCustomDropdowns();
  }

  function fillCaProductSelect() {
    const el = document.getElementById('ca-product');
    if (!el) return;
    const prev = el.value;
    const opts = (page.catalog || []).map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
    el.innerHTML = '<option value="">— optional —</option>' + opts +
      '<option value="__addproduct">＋ Add new product…</option>';
    if (prev && [...el.options].some(o => o.value === prev)) el.value = prev;
  }

  function resolveProductIdForCostAllocation(scope, productId, ref) {
    if (productId) return productId;
    const r = (ref || '').trim();
    if (!r) return null;
    if (scope === 'shipment' || scope === 'batch') {
      const move = (page.movements || []).find(x => String(x.id) === r);
      if (move?.productId) return +move.productId;
      const tp = (page.transportShipments || []).find(x =>
        String(x.id) === r || (x.dispatchRef || '').toLowerCase() === r.toLowerCase());
      if (tp?.productId) return +tp.productId;
    }
    return null;
  }

  function allocatedCostForProduct(productId) {
    return (page.costAllocations || [])
      .filter(c => c.productId === productId)
      .reduce((s, c) => s + N(c.amount), 0);
  }

  function onHandQtyForProduct(productId) {
    return allStockBalanceRows(false)
      .filter(r => r.productId === productId)
      .reduce((s, r) => s + N(r.qty), 0);
  }

  function directBatchCostForProduct(productId) {
    const p = catalogById(productId);
    if (!p) return 0;
    if ((p.sku || '').toUpperCase() === 'MEAT') {
      return (page.slaughters || []).reduce((s, sl) => s + N(sl.saleAmount), 0);
    }
    return 0;
  }

  function costScopeLabel(scope) {
    const map = {
      general: 'General farm / overhead',
      product: 'Product',
      batch: 'Production batch',
      shipment: 'Dispatch / transfer'
    };
    return map[scope] || scope;
  }

  function renderCostAllocationPane() {
    const d = document.getElementById('ca-date');
    if (d && !d.value) d.value = today();
    fillCaProductSelect();
    const rows = [...(page.costAllocations || [])].sort((a, b) =>
      String(b.dateDisplay).localeCompare(String(a.dateDisplay))
    );
    const cr = document.getElementById('costAllocationRows');
    if (cr) {
      cr.innerHTML = rows.slice(0, 40).map(c => {
        const prodLabel = c.productName || c.ref || (c.scope === 'general' ? 'General' : '—');
        const cpu = N(c.qty) > 0 ? rs(N(c.amount) / N(c.qty)) : '—';
        return `<tr><td>${esc(c.dateDisplay)}</td><td>${esc(c.costType)}</td><td>${esc(costScopeLabel(c.scope))}</td><td class="hide-sm">${esc(prodLabel)}</td><td class="num-cell"><b>${rs(c.amount)}</b></td><td class="num-cell hide-sm">${cpu}</td><td class="hide-sm">${esc(c.note || '')}</td><td><button type="button" class="del" data-del-cost="${c.id}" title="Delete">×</button></td></tr>`;
      }).join('') || '<tr><td colspan="8" class="empty">No allocated operating/logistics costs yet.</td></tr>';
    }
    const lr = document.getElementById('landedCostRows');
    if (lr) {
      lr.innerHTML = (page.catalog || []).map(p => {
        const made = onHandQtyForProduct(p.id);
        const direct = directBatchCostForProduct(p.id);
        const alloc = allocatedCostForProduct(p.id);
        const denom = made > 0.0005 ? made : 0;
        const unit = denom ? (direct + alloc) / denom : 0;
        const sell = N(p.defaultPrice);
        const margin = sell - unit;
        return `<tr><td><b>${esc(p.name)}</b></td><td class="num-cell">${fmtN(made)}</td><td class="num-cell">${rs(direct)}</td><td class="num-cell">${rs(alloc)}</td><td class="num-cell"><b>${rs(unit)}</b></td><td class="num-cell">${rs(sell)}</td><td class="num-cell"><b>${rs(margin)}</b></td></tr>`;
      }).join('') || '<tr><td colspan="7" class="empty">No products in catalogue yet.</td></tr>';
    }
    refreshProductCustomDropdowns();
  }

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

  const salesRows = document.getElementById('sl-sales-rows');
  if (salesRows) {
    salesRows.innerHTML = (page.sales || []).map(s =>
      `<tr><td>${esc(s.dateDisplay)}</td><td><b>${esc(s.invoiceNo || '—')}</b></td><td>${esc(s.customer || 'Cash')}</td><td>${esc(s.productName)}</td><td class="num-cell">${s.qty}</td><td class="num-cell hide-sm">—</td><td class="num-cell">${rs(s.amount)}</td><td class="num-cell hide-sm">—</td><td class="num-cell hide-sm">—</td><td>${s.isCredit ? 'Credit' : 'Paid'}</td><td></td></tr>`
    ).join('') || '<tr><td colspan="11" class="empty">No sales this month yet.</td></tr>';
  }

  let breedPerfRows = [];

  function getBpMonth() {
    const el = document.getElementById('bp-month');
    if (el && !el.value) el.value = page.month || today().slice(0, 7);
    return el?.value || page.month || today().slice(0, 7);
  }

  function breedPerfNames() {
    const names = new Set(['Beetal', 'Makhee Cheeni', 'Mixed']);
    (page.herdGoats || []).forEach(g => { if (g.breed) names.add(g.breed); });
    (page.analyticsMilkingSessions || []).forEach(s => { if (s.breed) names.add(s.breed); });
    (page.analyticsLegacyMilk || []).forEach(m => { if (m.breed) names.add(m.breed); });
    (page.analyticsGoatSales || []).forEach(s => { if (s.breed) names.add(s.breed); });
    (page.analyticsSlaughters || []).forEach(s => { if (s.breed) names.add(s.breed); });
    return [...names].sort((a, b) => a.localeCompare(b));
  }

  function inBpMonth(dateStr, month) {
    return String(dateStr || '').startsWith(month);
  }

  function getBreedPerfRows() {
    const month = getBpMonth();
    const filter = document.getElementById('bp-breed')?.value || '';
    const mode = document.getElementById('bp-mode')?.value || 'milk';
    const names = breedPerfNames().filter(b => !filter || b === filter);
    return names.map(breed => {
      const sessions = (page.analyticsMilkingSessions || []).filter(s =>
        (s.breed || 'Mixed') === breed && inBpMonth(s.dateDisplay, month)
      );
      const legacy = (page.analyticsLegacyMilk || []).filter(m =>
        (m.breed || 'Mixed') === breed && inBpMonth(m.dateDisplay, month)
      );
      const milkLitres = sessions.reduce((n, s) => n + Math.max(0, N(s.liters) - N(s.rejectedLiters)), 0)
        + legacy.reduce((n, m) => n + N(m.liters), 0);
      const goatSessions = sessions.length + legacy.length;
      const sold = (page.analyticsGoatSales || []).filter(s =>
        (s.breed || '') === breed && inBpMonth(s.dateDisplay, month)
      );
      const salesKg = sold.reduce((n, s) => n + N(s.weightKg), 0);
      const revenue = sold.reduce((n, s) => n + N(s.price), 0);
      const cost = sold.reduce((n, s) => n + N(s.cost), 0);
      const carcasses = (page.analyticsSlaughters || []).filter(s =>
        (s.breed || '') === breed && inBpMonth(s.dateDisplay, month)
      );
      const carcassKg = carcasses.reduce((n, s) => n + N(s.carcassKg), 0);
      const carcassLiveKg = carcasses.reduce((n, s) => n + N(s.liveWeightKg), 0);
      return {
        breed,
        milkLitres,
        goatSessions,
        avg: goatSessions ? milkLitres / goatSessions : null,
        head: sold.length,
        salesKg,
        revenue,
        cost,
        margin: revenue - cost,
        carcasses: carcasses.length,
        carcassKg,
        carcassLiveKg
      };
    }).filter(r => mode === 'milk' ? r.milkLitres > 0.0005 : r.head > 0 || r.carcasses > 0);
  }

  function bpKpi(label, value) {
    return `<div class="panel" style="padding:15px"><div class="sub">${esc(label)}</div><div style="font-size:22px;font-weight:800">${esc(value)}</div></div>`;
  }

  function renderBreedPerformance() {
    const pane = document.getElementById('breedPerformancePane');
    if (!pane) return;
    const month = getBpMonth();
    const mode = document.getElementById('bp-mode')?.value || 'milk';
    const sel = document.getElementById('bp-breed');
    if (sel) {
      const prev = sel.value;
      sel.innerHTML = '<option value="">All breeds</option>' +
        breedPerfNames().map(b => `<option value="${esc(b)}">${esc(b)}</option>`).join('');
      if (prev && [...sel.options].some(o => o.value === prev)) sel.value = prev;
    }
    const rows = getBreedPerfRows();
    breedPerfRows = rows;
    const total = key => rows.reduce((n, r) => n + N(r[key]), 0);
    const kpis = document.getElementById('bp-kpis');
    if (kpis) {
      kpis.innerHTML = mode === 'milk'
        ? [
          bpKpi('Milk collected', `${fmtN(total('milkLitres'))} L`),
          bpKpi('Breed pools / entries', `${fmtN(rows.length, 0)} breeds`),
          bpKpi('Milking records', fmtN(total('goatSessions'), 0)),
          bpKpi('Average L per record', total('goatSessions') ? `${fmtN(total('milkLitres') / total('goatSessions'))} L` : '—')
        ].join('')
        : [
          bpKpi('Live goats sold', fmtN(total('head'), 0)),
          bpKpi('Recorded sale revenue', rs(total('revenue'))),
          bpKpi('Carcass intake', `${fmtN(total('carcassKg'))} kg`),
          bpKpi('Recorded sale margin*', rs(total('margin')))
        ].join('');
    }
    const key = mode === 'milk' ? 'milkLitres' : 'carcassKg';
    const max = Math.max(1, ...rows.map(r => N(r[key])));
    const chartTitle = document.getElementById('bp-chart-title');
    if (chartTitle) {
      chartTitle.textContent = mode === 'milk'
        ? `Milk collected by breed — ${month}`
        : `Carcass intake by breed — ${month}`;
    }
    const chart = document.getElementById('bp-chart');
    if (chart) {
      chart.innerHTML = rows.length
        ? rows.map(r =>
          `<div style="display:grid;grid-template-columns:minmax(100px,160px) 1fr 95px;gap:12px;align-items:center;margin:13px 0"><strong>${esc(r.breed)}</strong><div style="height:23px;background:var(--green-tint,#e8f2e9);border-radius:6px;overflow:hidden"><div style="width:${Math.min(100, 100 * N(r[key]) / max)}%;height:100%;background:${mode === 'milk' ? '#2f6b3d' : '#b86a37'};border-radius:6px"></div></div><b style="text-align:right">${fmtN(r[key])} ${mode === 'milk' ? 'L' : 'kg'}</b></div>`
        ).join('')
        : `<div class="empty">No recorded ${mode === 'milk' ? 'breed-specific milk' : 'breed-linked carcass'} data for this month. Start recording to populate the chart.</div>`;
    }
    const head = mode === 'milk'
      ? ['Breed', 'Milk (L)', 'Milking records', 'L per record']
      : ['Breed', 'Live goats sold', 'Live sale kg*', 'Revenue (Rs)', 'Recorded cost (Rs)', 'Margin (Rs)*', 'Carcasses', 'Carcass kg', 'Carcass yield*'];
    const cells = r => mode === 'milk'
      ? [r.breed, fmtN(r.milkLitres), r.goatSessions ? fmtN(r.goatSessions, 0) : '—', r.avg == null ? '—' : fmtN(r.avg)]
      : [r.breed, fmtN(r.head, 0), r.salesKg ? fmtN(r.salesKg) : '—', fmtN(r.revenue), fmtN(r.cost), fmtN(r.margin), fmtN(r.carcasses, 0), fmtN(r.carcassKg), r.carcassLiveKg ? fmtN(r.carcassKg / r.carcassLiveKg * 100, 1) + '%' : '—'];
    const table = document.getElementById('bp-table');
    if (table) {
      table.innerHTML = `<table class="tbl"><thead><tr>${head.map(h => `<th>${esc(h)}</th>`).join('')}</tr></thead><tbody>${
        rows.length
          ? rows.map(r => `<tr>${cells(r).map(v => `<td>${esc(v)}</td>`).join('')}</tr>`).join('')
          : `<tr><td colspan="${head.length}">No records yet for ${esc(month)}.</td></tr>`
      }</tbody></table>`;
    }
    const meth = document.getElementById('bp-methodology');
    if (meth) {
      meth.innerHTML = mode === 'milk'
        ? '<b>Milk methodology:</b> Uses milking sessions and legacy single-line milk entries from the database, filtered by month and breed. Each saved session or legacy line counts as one record. Milk already flows into stock through normal milking saves.'
        : '<b>Meat methodology:</b> Live goat sales from the herd module and carcass intake from slaughter records. Carcass breed comes from the linked goat; records without a breed are excluded. *Margin is sale price minus recorded purchase cost only, not full lifetime cost.';
    }
    const breed = document.getElementById('bp-breed')?.value || '';
    const herdLink = document.getElementById('bp-link-herd');
  const breedLink = document.getElementById('bp-link-breeding');
    if (herdLink) {
      herdLink.href = breed ? `/Goat?q=${encodeURIComponent(breed)}` : '/Goat';
      herdLink.title = breed ? `Search herd for ${breed}` : 'Open herd list';
    }
    if (breedLink) {
      breedLink.href = '/Breeding?view=does';
      breedLink.title = breed
        ? `Breeding pipeline (filter does in Herd search for ${breed})`
        : 'Open breeding does pipeline';
    }
  }

  ['bp-month', 'bp-breed', 'bp-mode'].forEach(id => {
    document.getElementById(id)?.addEventListener('change', renderBreedPerformance);
  });

  function thisCalendarMonth() {
    return today().slice(0, 7);
  }

  function renderTransportPane() {
    const td = document.getElementById('tp-date');
    if (td && !td.value) td.value = today();
    fillTransportAccountSelect();
    updateTpPreview();
    const arr = [...(page.transportShipments || [])].sort((a, b) =>
      String(b.dateDisplay).localeCompare(String(a.dateDisplay))
    );
    const month = thisCalendarMonth();
    const mm = arr.filter(x => String(x.dateDisplay || '').startsWith(month));
    const cost = mm.reduce((s, x) => s + N(x.cost), 0);
    const qty = mm.reduce((s, x) => s + N(x.qty), 0);
    const k = document.getElementById('transportKpis');
    if (k) {
      k.innerHTML = `<div class="product-mini-kpi"><span>Shipments this month</span><b>${mm.length}</b></div>
        <div class="product-mini-kpi"><span>Transport cost</span><b>${rs(cost)}</b></div>
        <div class="product-mini-kpi"><span>Total quantity moved</span><b>${fmtN(qty, 0)}</b></div>`;
    }
    const rows = document.getElementById('transportRows');
    if (rows) {
      rows.innerHTML = arr.slice(0, 80).map(x => {
        const route = `${esc(x.fromLocation || 'Farm')} → ${esc(x.toLocation || '—')}`;
        const cpu = N(x.qty) > 0 ? rs(N(x.cost) / N(x.qty)) + ' / ' + esc(x.unit) : '—';
        return `<tr><td>${esc(x.dateDisplay)}</td><td><b>${esc(x.shipmentType)}</b></td><td class="num-cell">${fmtN(x.qty)} ${esc(x.unit)}</td><td>${route}</td><td class="num-cell"><b>${rs(x.cost)}</b></td><td class="num-cell">${cpu}</td><td class="hide-sm">${esc(x.transporter || '')}</td><td><button type="button" class="del" data-del-tp="${x.id}" title="Delete">×</button></td></tr>`;
      }).join('') || '<tr><td colspan="8" class="empty">No transportation records yet.</td></tr>';
    }
    refreshProductCustomDropdowns();
  }

  function updateTpPreview() {
    const type = document.getElementById('tp-type')?.value || 'Milk';
    const qty = N(document.getElementById('tp-qty')?.value);
    const cost = N(document.getElementById('tp-cost')?.value);
    const unit = document.getElementById('tp-unit')?.value || 'L';
    const el = document.getElementById('tp-cost-preview');
    if (!el) return;
    el.classList.toggle('milk', type === 'Milk');
    el.classList.toggle('meat', type === 'Meat');
    el.innerHTML = qty > 0
      ? `Transport cost: <strong style="margin:0 5px">${rs(cost / qty)}</strong> per ${esc(unit)} · ${fmtN(qty)} ${esc(unit)} × ${rs(cost / qty)} = ${rs(cost)}`
      : 'Enter quantity and transport cost — the system will calculate Rs/L, Rs/kg or Rs/item automatically.';
  }

  function onTpTypeChanged() {
    const t = document.getElementById('tp-type')?.value;
    const u = document.getElementById('tp-unit');
    if (!u || !t || t.startsWith('__')) return;
    if (t === 'Milk') u.value = 'L';
    else if (t === 'Meat') u.value = 'kg';
    else if (t === 'Dairy Products') u.value = 'items';
    updateTpPreview();
  }

  ['tp-qty', 'tp-cost', 'tp-unit'].forEach(id => {
    document.getElementById(id)?.addEventListener('input', updateTpPreview);
  });
  document.getElementById('tp-type')?.addEventListener('change', onTpTypeChanged);

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
  ['rp-qty', 'rp-cost'].forEach(id =>
    document.getElementById(id)?.addEventListener('input', updateRoutePreview));

  document.getElementById('routePricingRows')?.addEventListener('click', async e => {
    const btn = e.target.closest('[data-del-route]');
    if (!btn) return;
    const id = +btn.dataset.delRoute;
    if (!id) return;
    if (!confirm('Delete this delivery route?')) return;
    try {
      await postJson('/Products/DeleteDeliveryRoute', { id });
      reload();
    } catch (err) {
      alert(err.message || 'Could not delete route.');
    }
  });

  document.getElementById('clientPricingRows')?.addEventListener('click', async e => {
    const btn = e.target.closest('[data-del-client-price]');
    if (!btn) return;
    const id = +btn.dataset.delClientPrice;
    if (!id) return;
    if (!confirm('Delete this client-specific rate?')) return;
    try {
      await postJson('/Products/DeleteClientProductPrice', { id });
      reload();
    } catch (err) {
      alert(err.message || 'Could not delete client rate.');
    }
  });

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
    try {
      const payload = readCatalogFormPayload();
      await postJson('/Products/SaveCatalog', payload);
      resetCatalogForm();
      reload();
    } catch (err) {
      alert(err.message || 'Could not save product.');
    }
  });

  document.getElementById('cancelProductCatalog')?.addEventListener('click', () => resetCatalogForm());

  document.getElementById('deleteProductCatalog')?.addEventListener('click', async () => {
    if (!catalogEditingId) return;
    const p = catalogById(catalogEditingId);
    if (!p || isSystemCatalogSku(p.sku)) return;
    if (!confirm(`Delete “${p.name}” from the catalogue? This cannot be undone.`)) return;
    try {
      await postJson('/Products/DeleteCatalog', { id: catalogEditingId });
      resetCatalogForm();
      reload();
    } catch (err) {
      alert(err.message || 'Could not delete product.');
    }
  });

  document.getElementById('productCatalogRows')?.addEventListener('click', async e => {
    const toggleBtn = e.target.closest('[data-toggle-product]');
    if (toggleBtn) {
      e.stopPropagation();
      const id = +toggleBtn.dataset.toggleProduct;
      const p = catalogById(id);
      if (!p) return;
      const nextActive = p.isActive === false;
      try {
        await postJson('/Products/SaveCatalog', {
          id: p.id,
          sku: p.sku,
          name: p.name,
          category: p.category || 'other',
          pack: p.pack || null,
          unit: p.unit || 'L',
          defaultPrice: p.defaultPrice,
          milkTreatment: p.milkTreatment || null,
          meatType: p.meatType || null,
          isActive: nextActive
        });
        reload();
      } catch (err) {
        alert(err.message || 'Could not update product status.');
      }
      return;
    }
    const row = e.target.closest('tr[data-catalog-id]');
    if (row) loadCatalogForEdit(+row.dataset.catalogId);
  });

  document.getElementById('sl-sg-save')?.addEventListener('click', async () => {
    const tag = document.getElementById('sl-sg-tag')?.value || '';
    const g = findGoatByTag(tag);
    const store = document.getElementById('sl-sg-store')?.value;
    const carcassKg = +document.getElementById('sl-sg-kg')?.value || 0;
    if (!store || store.startsWith('__')) {
      alert('Choose the meat chiller / freezer.');
      return;
    }
    if (carcassKg <= 0) {
      alert('Enter carcass weight (kg).');
      return;
    }
    try {
      await postJson('/Products/SaveSlaughter', {
        date: document.getElementById('sl-sg-date')?.value || today(),
        goatId: g ? g.id : null,
        tagText: tag,
        carcassKg,
        saleAmount: 0,
        liveWeightKg: +document.getElementById('sl-sg-live')?.value || 0,
        warehouseId: +store,
        animalCost: +document.getElementById('sl-sg-acost')?.value || 0,
        removeFromHerd: document.getElementById('sl-sg-herd')?.checked !== false,
        notes: document.getElementById('sl-sg-note')?.value?.trim() || null
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save carcass.');
    }
  });

  document.getElementById('sl-sp-save')?.addEventListener('click', async () => {
    const id = +document.getElementById('sl-sp-src')?.value;
    if (!id) { alert('Choose a whole carcass.'); return; }
    try {
      await postJson('/Products/SaveMeatSplit', {
        slaughterId: id,
        date: document.getElementById('sl-sp-date')?.value || today(),
        halfAKg: +document.getElementById('sl-sp-a')?.value || 0,
        note: document.getElementById('sl-sp-note')?.value?.trim() || null
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not split carcass.');
    }
  });

  document.getElementById('sl-ct-save')?.addEventListener('click', async () => {
    const id = +document.getElementById('sl-ct-src')?.value;
    const store = document.getElementById('sl-ct-store')?.value;
    if (!id) { alert('Choose the carcass to cut.'); return; }
    if (!store || store.startsWith('__')) { alert('Choose where the cuts are stored.'); return; }
    const cuts = readCutRows().filter(r => r.kg > 0 && r.productId).map(r => ({ productId: r.productId, kg: r.kg }));
    if (!cuts.length) { alert('Weigh at least one cut.'); return; }
    try {
      await postJson('/Products/SaveMeatCut', {
        slaughterId: id,
        date: document.getElementById('sl-ct-date')?.value || today(),
        cutWarehouseId: +store,
        inputKg: +document.getElementById('sl-ct-in')?.value || 0,
        costMethod: document.getElementById('sl-ct-method')?.value || 'weight',
        note: document.getElementById('sl-ct-note')?.value?.trim() || null,
        cuts
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save cuts.');
    }
  });

  document.getElementById('sl-ct-add')?.addEventListener('click', () => addCutRow());
  ['sl-sp-src', 'sl-sp-a'].forEach(id => {
    document.getElementById(id)?.addEventListener('input', spPreview);
    document.getElementById(id)?.addEventListener('change', spPreview);
  });
  ['sl-ct-src', 'sl-ct-in', 'sl-ct-method'].forEach(id => {
    document.getElementById(id)?.addEventListener('input', ctPreview);
    document.getElementById(id)?.addEventListener('change', ctPreview);
  });

  document.getElementById('sl-sa-check')?.addEventListener('click', () => {
    const qty = +document.getElementById('sl-sa-qty')?.value || 0;
    const rate = +document.getElementById('sl-sa-rate')?.value || 0;
    const basis = document.getElementById('sl-sa-basis')?.value || 'unit';
    const amt = basis === 'fixed' ? rate : qty * rate;
    const src = currentSaleSource();
    const type = document.getElementById('sl-sa-type')?.value || 'other';
    const typeLabels = { raw: 'Raw milk', past: 'Pasteurized milk', dairy: 'Dairy product', whole: 'Whole carcass', half: 'Half carcass', cut: 'Meat cuts', other: 'Product' };
    const review = document.getElementById('sl-sa-review');
    if (!review) return;
    review.hidden = false;
    review.innerHTML = `<div class="sl-review"><h3>Review sale</h3><dl><dt>Item</dt><dd>${esc(typeLabels[type] || 'Sale')}${src ? ` · ${esc(src.label)}` : ''}</dd><dt>Quantity</dt><dd>${qty}${src?.unit ? ' ' + esc(src.unit) : ''}</dd><dt>Rate</dt><dd>${rs(rate)}</dd><dt>Amount</dt><dd><b>${rs(amt)}</b></dd></dl><div class="sl-review-act"><button type="button" class="btn btn-green" id="sl-sa-post">Post sale</button><button type="button" class="btn btn-ghost" id="sl-sa-cancel-review">Back</button></div></div>`;
    document.getElementById('sl-sa-cancel-review')?.addEventListener('click', () => { review.hidden = true; });
    document.getElementById('sl-sa-post')?.addEventListener('click', async () => {
      const cust = document.getElementById('sl-sa-cust')?.value;
      const pay = document.getElementById('sl-sa-pay')?.value;
      const isCredit = pay === 'credit' && cust;
      const prodVal = document.getElementById('sl-sa-prod')?.value;
      const productId = src?.productId || (prodVal && !String(prodVal).startsWith('__') ? +prodVal : null);
      const src = currentSaleSource();
      const whId = (type === 'whole' || type === 'half') ? null : (src?.key ? +src.key : null);
      const res = await fetch('/Products/SaveSale', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          date: document.getElementById('sl-sa-date')?.value || today(),
          productId: productId || null,
          warehouseId: whId,
          customerId: cust && cust !== '__add' ? +cust : null,
          productName: typeLabels[type] || 'Sale',
          qty, rate, isCredit: !!isCredit
        })
      });
      const j = await res.json().catch(() => ({}));
      if (!res.ok) { alert(j.error || 'Could not save sale.'); return; }
      if (j.invoiceNo) {
        const msg = j.invoiceId
          ? `Sale ${j.invoiceNo} recorded — Finance invoice #${j.invoiceId}.`
          : `Sale ${j.invoiceNo} recorded.`;
        alert(msg);
      } else if (j.invoiceId) alert('Credit sale recorded — invoice #' + j.invoiceId + ' in Finance.');
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
    const k = parseStockItemKey(document.getElementById('sl-tf-item')?.value);
    const toWh = +document.getElementById('sl-tf-to')?.value;
    const qty = N(document.getElementById('sl-tf-qty')?.value);
    const date = document.getElementById('sl-tf-date')?.value || today();
    const note = document.getElementById('sl-tf-note')?.value?.trim() || '';
    if (!k?.productId) { alert('Choose what to move.'); return; }
    if (!toWh) { alert('Choose where it goes.'); return; }
    if (!(qty > 0)) { alert('Enter the quantity to move.'); return; }
    const batchNote = k.batch ? `Batch/carcass ${k.batch}` : '';
    const notes = [batchNote, note].filter(Boolean).join(' · ') || null;
    try {
      await postJson('/Products/SaveStockMovement', {
        date,
        type: 'Transfer',
        productId: k.productId,
        warehouseId: k.warehouseId,
        toWarehouseId: toWh,
        qty,
        unitCost: productUnitCost(k.productId),
        notes
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save transfer.');
    }
  });

  document.getElementById('sl-adj-save')?.addEventListener('click', async () => {
    const k = parseStockItemKey(document.getElementById('sl-adj-item')?.value);
    const dir = document.getElementById('sl-adj-dir')?.value;
    const qty = N(document.getElementById('sl-adj-qty')?.value);
    const reason = document.getElementById('sl-adj-reason')?.value || '';
    const date = document.getElementById('sl-adj-date')?.value || today();
    const note = document.getElementById('sl-adj-note')?.value?.trim() || '';
    if (!k?.productId) { alert('Choose the item.'); return; }
    if (!(qty > 0)) { alert('Enter the quantity.'); return; }
    if (!note && reason === 'Other') { alert('Explain the adjustment in the note.'); return; }
    const cost = productUnitCost(k.productId);
    const notes = [reason, k.batch ? `Batch/carcass ${k.batch}` : '', note].filter(Boolean).join(' · ');
    try {
      await postJson('/Products/SaveStockMovement', {
        date,
        type: dir === 'in' ? 'Receive' : 'Spoilage',
        productId: k.productId,
        warehouseId: k.warehouseId,
        qty,
        unitCost: dir === 'in' ? cost : 0,
        notes
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save adjustment.');
    }
  });

  document.getElementById('sl-mv-csv')?.addEventListener('click', () => exportStockLedgerCsv());
  document.getElementById('sl-loc-add')?.addEventListener('click', () => addLocationFromForm());

  const salesKpis = document.getElementById('sl-sales-kpis');
  if (salesKpis) {
    salesKpis.innerHTML = `
      <div class="sl-kpi"><span>Sales this month</span><b>${rs(page.monthSalesTotal)}</b></div>
      <div class="sl-kpi"><span>Transactions</span><b>${(page.sales || []).length}</b></div>
      <div class="sl-kpi"><span>Credit sales</span><b>${(page.sales || []).filter(s => s.isCredit).length}</b></div>
      <div class="sl-kpi"><span>Customers</span><b>${(page.customers || []).length}</b></div>`;
  }

  document.getElementById('addTransportShipment')?.addEventListener('click', async () => {
    const typeEl = document.getElementById('tp-type');
    let type = typeEl?.value || 'Milk';
    if (type.startsWith('__')) { alert('Choose a shipment type.'); return; }
    let unit = document.getElementById('tp-unit')?.value || 'L';
    if (unit.startsWith('__')) { alert('Choose a unit.'); return; }
    const date = document.getElementById('tp-date')?.value || today();
    const qty = N(document.getElementById('tp-qty')?.value);
    const to = document.getElementById('tp-to')?.value?.trim();
    const cost = N(document.getElementById('tp-cost')?.value);
    if (!date || !(qty > 0) || !to || cost < 0) {
      alert('Enter date, total quantity and destination.');
      return;
    }
    const acct = document.getElementById('tp-account')?.value;
    try {
      await postJson('/Products/SaveTransportShipment', {
        date,
        shipmentType: type,
        qty,
        unit,
        fromLocation: document.getElementById('tp-from')?.value?.trim() || 'Farm',
        toLocation: to,
        cost,
        transporter: document.getElementById('tp-transporter')?.value?.trim() || null,
        dispatchRef: document.getElementById('tp-ref')?.value?.trim() || null,
        note: document.getElementById('tp-note')?.value?.trim() || null,
        paidFromAccountId: acct ? +acct : null
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save transport shipment.');
    }
  });

  document.getElementById('transportRows')?.addEventListener('click', async e => {
    const btn = e.target.closest('[data-del-tp]');
    if (!btn) return;
    const id = +btn.dataset.delTp;
    if (!id) return;
    if (!confirm('Delete this transportation record and its linked Finance expense?')) return;
    try {
      await postJson('/Products/DeleteTransportShipment', { id });
      reload();
    } catch (err) {
      alert(err.message || 'Could not delete shipment.');
    }
  });

  document.getElementById('addCostAllocation')?.addEventListener('click', async () => {
    let type = document.getElementById('ca-type')?.value || '';
    if (type.startsWith('__')) { alert('Choose a cost type.'); return; }
    const date = document.getElementById('ca-date')?.value || today();
    const amount = N(document.getElementById('ca-amount')?.value);
    const scope = document.getElementById('ca-scope')?.value || 'general';
    const pidRaw = document.getElementById('ca-product')?.value;
    let productId = pidRaw && !pidRaw.startsWith('__') ? +pidRaw : null;
    const ref = document.getElementById('ca-ref')?.value?.trim() || null;
    productId = resolveProductIdForCostAllocation(scope, productId, ref);
    const qty = N(document.getElementById('ca-qty')?.value);
    const note = document.getElementById('ca-note')?.value?.trim() || null;
    if (!date || !(amount > 0)) { alert('Enter date and amount.'); return; }
    if (scope === 'product' && !productId) { alert('Choose the product for this allocation.'); return; }
    try {
      await postJson('/Products/SaveProductCostAllocation', {
        date,
        costType: type,
        amount,
        scope,
        productId,
        ref,
        qty,
        note
      });
      reload();
    } catch (err) {
      alert(err.message || 'Could not save cost allocation.');
    }
  });

  document.getElementById('costAllocationRows')?.addEventListener('click', async e => {
    const btn = e.target.closest('[data-del-cost]');
    if (!btn) return;
    const id = +btn.dataset.delCost;
    if (!id) return;
    if (!confirm('Delete this allocated cost and its linked Finance expense?')) return;
    try {
      await postJson('/Products/DeleteProductCostAllocation', { id });
      reload();
    } catch (err) {
      alert(err.message || 'Could not delete allocation.');
    }
  });

  document.getElementById('saveProductPricing')?.addEventListener('click', async () => {
    try {
      const payload = readProductPricingPayload();
      await postJson('/Products/SaveProductPricing', payload);
      resetProductPricingForm();
      reload();
    } catch (err) {
      alert(err.message || 'Could not save pricing.');
    }
  });

  document.getElementById('cancelProductPricing')?.addEventListener('click', () => resetProductPricingForm());

  document.getElementById('deleteProductPricing')?.addEventListener('click', async () => {
    if (!pricingEditingProductId) return;
    const p = catalogById(pricingEditingProductId);
    if (!hasStoredPricingProfile(pricingEditingProductId)) return;
    if (!confirm(`Remove saved pricing profile for “${p?.name || 'this product'}”? Catalog default price is kept.`)) return;
    try {
      await postJson('/Products/DeleteProductPricing', { productId: pricingEditingProductId });
      resetProductPricingForm();
      reload();
    } catch (err) {
      alert(err.message || 'Could not delete pricing profile.');
    }
  });

  document.getElementById('productPricingRows')?.addEventListener('click', e => {
    const row = e.target.closest('tr[data-pricing-product-id]');
    if (row) loadProductPricingForEdit(+row.dataset.pricingProductId);
  });

  document.getElementById('saveRoutePricing')?.addEventListener('click', async () => {
    const name = document.getElementById('rp-name')?.value?.trim() || '';
    const pidRaw = document.getElementById('rp-product')?.value;
    const qty = N(document.getElementById('rp-qty')?.value);
    const cost = N(document.getElementById('rp-cost')?.value);
    if (!name || !pidRaw || pidRaw.startsWith('__') || !(qty > 0) || cost < 0) {
      alert('Enter destination/route, product, shipment quantity and transport cost.');
      return;
    }
    try {
      await postJson('/Products/SaveDeliveryRoute', {
        name,
        productId: +pidRaw,
        qty,
        cost
      });
      document.getElementById('rp-name').value = '';
      document.getElementById('rp-qty').value = '';
      document.getElementById('rp-cost').value = '';
      reload();
    } catch (err) {
      alert(err.message || 'Could not save route.');
    }
  });

  document.getElementById('saveClientPrice')?.addEventListener('click', async () => {
    const cidRaw = document.getElementById('cp-client')?.value;
    const pidRaw = document.getElementById('cp-product')?.value;
    const rate = N(document.getElementById('cp-rate')?.value);
    if (!cidRaw || cidRaw === '__add' || !pidRaw || pidRaw.startsWith('__') || !(rate > 0)) {
      alert('Select client, product and special rate.');
      return;
    }
    try {
      await postJson('/Products/SaveClientProductPrice', {
        clientId: +cidRaw,
        productId: +pidRaw,
        rate
      });
      document.getElementById('cp-rate').value = '';
      reload();
    } catch (err) {
      alert(err.message || 'Could not save client rate.');
    }
  });

  document.getElementById('bp-export')?.addEventListener('click', () => {
    const mode = document.getElementById('bp-mode')?.value || 'milk';
    const columns = mode === 'milk'
      ? ['breed', 'milkLitres', 'goatSessions', 'avg']
      : ['breed', 'head', 'salesKg', 'revenue', 'cost', 'margin', 'carcasses', 'carcassKg'];
    const csv = [columns.join(','), ...breedPerfRows.map(r =>
      columns.map(k => {
        const s = String(r[k] ?? '');
        return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
      }).join(',')
    )].join('\r\n');
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    a.download = `breed-${mode}-${getBpMonth()}.csv`;
    a.click();
  });

  bootProductsPage();
})();
