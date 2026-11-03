(() => {
  const GR = window.GoatRecords || {};
  const api = GR.api || (async (url, opts = {}) => {
    const res = await fetch(url, { credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', ...(opts.headers || {}) }, ...opts });
    const data = await res.json().catch(() => ({}));
    if (res.status === 202) { alert(data.message || 'Submitted for manager approval.'); return data; }
    if (!res.ok) throw new Error(data.error || data.title || 'Request failed');
    return data;
  });
  const showModal = GR.showModal || (msg => Promise.resolve(window.prompt(msg)));
  const showConfirm = GR.showConfirm || (msg => Promise.resolve(window.confirm(msg)));
  const toast = GR.showToast || (msg => alert(msg));

  const rs = n => 'Rs ' + Math.round(+n || 0).toLocaleString('en-US');
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const today = () => {
    const d = new Date();
    const z = n => String(n).padStart(2, '0');
    return d.getFullYear() + '-' + z(d.getMonth() + 1) + '-' + z(d.getDate());
  };
  const val = id => document.getElementById(id)?.value?.trim() || '';
  const num = id => +document.getElementById(id)?.value || 0;
  const setVal = (id, v) => { const e = document.getElementById(id); if (e) e.value = v; };
  const parseId = v => { const n = parseInt(v, 10); return Number.isFinite(n) ? n : null; };

  function can(act) {
    const p = window.FarmPermissions?.projects || { view: true, add: true, edit: true, delete: true };
    const map = { create: 'add', del: 'delete', edit: 'edit', view: 'view' };
    return !!p[map[act] || act];
  }

  let page = {};
  try { page = JSON.parse(document.getElementById('projects-page-json')?.textContent || '{}'); }
  catch { page = {}; }

  const lists = {
    categories: () => page.projectCategories || [],
    costTypes: () => page.costTypes || [],
    assetTypes: () => page.assetCategories || [],
    fundingSources: () => page.fundingSources || []
  };

  function kpi(label, value, sub) {
    return `<div class="stat"><div class="num">${value}</div><div class="lbl">${esc(label)}</div>${sub ? `<div class="breed">${esc(sub)}</div>` : ''}</div>`;
  }

  function fillSelect(el, html, preferred) {
    if (!el) return;
    const old = el.value;
    el.innerHTML = html;
    const opts = [...el.options].map(o => o.value);
    if (preferred != null && opts.includes(String(preferred))) el.value = String(preferred);
    else if (opts.includes(old)) el.value = old;
  }

  function refreshProjectCustomDropdowns() {
    const cfg = [
      ['pj-cat', 'categories', '＋ Add new category…'],
      ['cx-type', 'costTypes', '＋ Add new cost type…'],
      ['fa-type', 'assetTypes', '＋ Add new asset category…'],
      ['pj-funding', 'fundingSources', '＋ Add new funding source…']
    ];
    cfg.forEach(([id, key, label]) => {
      const el = document.getElementById(id);
      if (!el) return;
      const current = el.value;
      const values = lists[key]() || [];
      el.innerHTML = values.map(v => `<option>${esc(v)}</option>`).join('') + `<option value="__addnew__">${label}</option>`;
      if ([...el.options].some(o => o.value === current)) el.value = current;
    });
  }

  async function handleProjectAddNewSelect(el, key, label) {
    if (el.value !== '__addnew__') return;
    const previous = [...el.options].find(o => o.value !== '__addnew__')?.value || '';
    const name = ((await showModal('Enter new ' + label + ':', { prompt: true })) || '').trim();
    if (!name) { el.value = previous; return; }
    const lookupKey = {
      categories: 'Lookup.ProjectCategories',
      costTypes: 'Lookup.CapexCostTypes',
      assetTypes: 'Lookup.ProjectAssetCategories',
      fundingSources: 'Lookup.ProjectFundingSources'
    }[key];
    try {
      const updated = await api('/Lookup/AddOption', { method: 'POST', body: JSON.stringify({ key: lookupKey, value: name }) });
      if (key === 'categories') page.projectCategories = updated;
      if (key === 'costTypes') page.costTypes = updated;
      if (key === 'assetTypes') page.assetCategories = updated;
      if (key === 'fundingSources') page.fundingSources = updated;
      refreshProjectCustomDropdowns();
      el.value = name;
    } catch (err) {
      el.value = previous;
      await showModal(err.message || 'Could not add that option.');
    }
  }

  function syncCapexFunding() {
    const pid = parseId(val('cx-project'));
    const p = (page.projects || []).find(x => x.id === pid);
    const f = document.getElementById('cx-funding');
    if (f) f.value = (p && p.funding) || 'Farm funds / not specified';
  }

  function syncCapexPaymentFields() {
    const st = document.getElementById('cx-paystatus');
    const amt = document.getElementById('cx-amount');
    const paid = document.getElementById('cx-paid');
    const acc = document.getElementById('cx-account');
    const due = document.getElementById('cx-due');
    if (!st || !paid || !acc || !due) return;
    const v = st.value;
    if (v === 'Paid') {
      paid.value = (+amt.value || 0) || '';
      paid.readOnly = true;
      acc.disabled = false;
      due.value = '';
      due.disabled = true;
    } else if (v === 'Credit') {
      paid.value = '0';
      paid.readOnly = true;
      acc.value = '';
      acc.disabled = true;
      due.disabled = false;
    } else {
      paid.readOnly = false;
      acc.disabled = false;
      due.disabled = false;
      if (+paid.value >= (+amt.value || 0)) paid.value = '';
    }
  }

  function renderProjects() {
    refreshProjectCustomDropdowns();
    const projects = page.projects || [];
    const exp = page.expenditures || [];
    const assets = page.fixedAssets || [];
    const solar = page.solarLogs || [];
    const accounts = page.accounts || [];

    const k = document.getElementById('projectKpis');
    if (k) k.innerHTML =
      kpi('Project budget', rs(page.totalBudget), projects.length + ' projects') +
      kpi('Capital spent', rs(page.totalSpent), 'cash invested') +
      kpi('CIP', rs(page.totalCip), 'not yet capitalized') +
      kpi('Fixed assets', rs(page.totalCapex), assets.length + ' assets');

    const rows = document.getElementById('projectRows');
    if (rows) {
      rows.innerHTML = projects.length ? projects.map(p => {
        const rem = +p.remaining || 0;
        const done = (+p.cip || 0) <= 0 && (+p.spent || 0) > 0;
        return `<tr>
          <td><b>${esc(p.name)}</b><div class="breed">${esc(p.note || '')}</div></td>
          <td>${esc(p.category || '—')}</td>
          <td><span class="chip project-status ${p.status === 'Completed' ? 'completed' : ''}">${esc(p.status)}</span></td>
          <td class="num-cell">${rs(p.budgetAmount)}</td>
          <td class="num-cell">${rs(p.spent)}</td>
          <td class="num-cell"><b>${rs(p.cip)}</b></td>
          <td class="num-cell" style="${rem < 0 ? 'color:#8a261c;font-weight:700' : ''}">${(rem < 0 ? '– ' : '') + rs(Math.abs(rem))}</td>
          <td>${esc(p.funding || '—')}</td>
          <td><div style="display:flex;gap:6px;flex-wrap:wrap">${(+p.cip || 0) > 0
            ? `<button type="button" class="btn btn-green btn-sm" data-capitalize="${p.id}">Complete / Convert to Asset</button>`
            : `<span class="chip">${done ? 'Capitalized' : 'No CAPEX'}</span>`}
            <button type="button" class="btn btn-ghost btn-sm" data-delproject="${p.id}">Delete</button></div></td>
        </tr>`;
      }).join('') : '<tr><td colspan="9" class="empty">No capital projects yet.</td></tr>';
    }

    const opts = projects.map(p => `<option value="${p.id}">${esc(p.name)}</option>`).join('');
    ['cx-project', 'sol-project'].forEach(id => {
      const e = document.getElementById(id);
      if (!e) return;
      fillSelect(e, opts || '<option value="">Add a project first</option>', e.value);
    });
    const fa = document.getElementById('fa-project');
    if (fa) fillSelect(fa, '<option value="">Independent asset</option>' + opts, fa.value);

    const acctHtml = '<option value="">Select Cash / Bank</option>' + accounts.map(a => `<option value="${a.id}">${esc(a.name)}</option>`).join('');
    fillSelect(document.getElementById('cx-account'), acctHtml, document.getElementById('cx-account')?.value);
    fillSelect(document.getElementById('fa-account'), '<option value="">Not linked to account</option>' + accounts.map(a => `<option value="${a.id}">${esc(a.name)}</option>`).join(''), document.getElementById('fa-account')?.value);

    syncCapexFunding();
    syncCapexPaymentFields();

    const cr = document.getElementById('capexRows');
    if (cr) {
      cr.innerHTML = exp.length ? exp.map(x => {
        const due = +x.payable || 0;
        const qty = x.qty ? esc(x.qty + ' ' + (x.unit || 'Nos')) : '—';
        return `<tr>
          <td>${esc(x.dateDisplay)}</td>
          <td>${esc(x.projectName)}</td>
          <td>${esc(x.costType || x.description)}</td>
          <td><b>${esc(x.item || '—')}</b>${x.model ? `<div class="breed">${esc(x.model)}</div>` : ''}</td>
          <td>${qty}</td>
          <td>${esc(x.spec || '—')}${x.warranty ? `<div class="breed">Warranty: ${esc(x.warranty)}</div>` : ''}</td>
          <td>${esc(x.supplier || '—')}</td>
          <td>${esc(x.fundingSource || '—')}</td>
          <td><span class="chip">${esc(x.paymentStatus)}</span></td>
          <td>${esc(x.accountName || '—')}</td>
          <td>${esc(x.dueDate || '—')}</td>
          <td>${esc(x.note || '')}</td>
          <td class="num-cell"><b>${rs(x.amount)}</b></td>
          <td class="num-cell">${rs(x.paidAmount)}</td>
          <td class="num-cell" style="${due ? 'color:#8a261c;font-weight:700' : ''}">${rs(due)}</td>
          <td>${due ? `<button type="button" class="btn btn-green btn-sm" data-paycapex="${x.id}">Pay</button>` : ''} <button type="button" class="btn btn-ghost btn-sm" data-delcapex="${x.id}">Delete</button></td>
        </tr>`;
      }).join('') : '<tr><td colspan="16" class="empty">No capital expenditure recorded.</td></tr>';
    }

    const ar = document.getElementById('fixedAssetRows');
    if (ar) {
      ar.innerHTML = assets.length ? assets.map(a => `<tr>
        <td><b>${esc(a.name)}</b><div class="breed">${esc(a.supplier || '')}</div></td>
        <td>${esc(a.category || '—')}</td>
        <td>${esc(a.projectName || '—')}</td>
        <td>${esc(a.location || '—')}</td>
        <td>${esc(a.purchaseDate || '—')}</td>
        <td class="num-cell"><b>${rs(a.cost)}</b>${!a.projectCapitalized && (+a.due || 0) ? `<div class="breed" style="color:#8a261c">Due ${rs(a.due)}</div>` : ''}</td>
        <td><button type="button" class="btn btn-ghost btn-sm" data-delfixed="${a.id}">Delete</button></td>
      </tr>`).join('') : '<tr><td colspan="7" class="empty">No fixed assets recorded.</td></tr>';
    }

    const sav = solar.reduce((a, x) => a + (+x.savingsEstimate || 0), 0);
    const maint = solar.reduce((a, x) => a + (+x.maintenanceAmount || 0), 0);
    const gen = solar.reduce((a, x) => a + (+x.kwhGenerated || 0), 0);
    const invest = projects.filter(p => p.category === 'Solar / Power').reduce((a, p) => a + (+p.spent || 0), 0);
    const ss = document.getElementById('solarSummary');
    if (ss) ss.innerHTML =
      kpi('Solar investment', rs(invest), 'recorded capex') +
      kpi('Generation', gen.toFixed(0) + ' kWh', 'recorded output') +
      kpi('Net savings', rs(sav - maint), 'savings less maintenance') +
      kpi('Simple payback', sav - maint > 0 ? (invest / (sav - maint)).toFixed(1) + ' periods' : '—', 'based on recorded savings');

    const sr = document.getElementById('solarRows');
    if (sr) {
      sr.innerHTML = solar.length ? solar.map(x => `<tr>
        <td>${esc(x.dateDisplay)}</td>
        <td>${esc(x.projectName)}</td>
        <td class="num-cell">${(+x.kwhGenerated || 0).toFixed(1)}</td>
        <td class="num-cell">${rs(x.savingsEstimate)}</td>
        <td class="num-cell">${rs(x.maintenanceAmount)}</td>
        <td class="num-cell">${x.installedCapacityKw ? x.installedCapacityKw + ' kW' : '—'}</td>
        <td>${esc(x.notes || '')}</td>
      </tr>`).join('') : '<tr><td colspan="7" class="empty">No solar performance records.</td></tr>';
    }
  }

  async function reloadPage() {
    page = await api('/Projects/GetData');
    renderProjects();
  }

  function showTab(name) {
    document.querySelectorAll('[data-projecttab]').forEach(x => x.classList.toggle('active', x.dataset.projecttab === name));
    document.querySelectorAll('.projectview').forEach(v => { v.style.display = v.id === 'proj-' + name ? 'block' : 'none'; });
    try { localStorage.setItem('sfpj:tab', name); } catch { /* ignore */ }
    const url = new URL(location.href);
    url.searchParams.set('tab', name);
    history.replaceState(null, '', url);
  }

  document.querySelectorAll('[data-projecttab]').forEach(t => t.addEventListener('click', () => {
    showTab(t.dataset.projecttab);
    renderProjects();
  }));

  ['pj-start', 'cx-date', 'fa-date', 'sol-date'].forEach(id => {
    const e = document.getElementById(id);
    if (e && !e.value) e.value = today();
  });

  [['pj-cat', 'categories', 'project category'], ['cx-type', 'costTypes', 'CAPEX cost type'], ['fa-type', 'assetTypes', 'asset category'], ['pj-funding', 'fundingSources', 'funding source']].forEach(([id, key, label]) => {
    const el = document.getElementById(id);
    if (el) el.addEventListener('change', () => handleProjectAddNewSelect(el, key, label));
  });

  document.getElementById('cx-project')?.addEventListener('change', syncCapexFunding);
  document.getElementById('cx-paystatus')?.addEventListener('change', syncCapexPaymentFields);
  document.getElementById('cx-amount')?.addEventListener('input', () => {
    if (document.getElementById('cx-paystatus')?.value === 'Paid') syncCapexPaymentFields();
  });

  document.getElementById('addProject')?.addEventListener('click', async () => {
    if (!can('create')) return;
    const name = val('pj-name');
    if (!name) return showModal('Enter a project name.');
    try {
      const saved = await api('/Projects/SaveProject', {
        method: 'POST',
        body: JSON.stringify({
          name,
          category: val('pj-cat'),
          budgetAmount: num('pj-budget'),
          startDate: val('pj-start') || today(),
          status: val('pj-status'),
          funding: val('pj-funding'),
          description: val('pj-note')
        })
      });
      if (saved?.pendingApproval) return;
      setVal('pj-name', ''); setVal('pj-budget', ''); setVal('pj-note', '');
      toast('Project saved.');
      await reloadPage();
    } catch (err) { await showModal(err.message); }
  });

  document.getElementById('addCapex')?.addEventListener('click', async () => {
    if (!can('create')) return;
    const pid = parseId(val('cx-project'));
    const amt = num('cx-amount');
    if (!pid || amt <= 0) return showModal('Select a project and enter the amount.');
    const status = val('cx-paystatus') || 'Paid';
    let paid = Math.max(0, Math.min(amt, num('cx-paid')));
    if (status === 'Paid') paid = amt;
    if (status === 'Credit') paid = 0;
    if (status === 'Part Paid' && (paid <= 0 || paid >= amt)) return showModal('For Part Paid, enter an amount greater than 0 and less than the total.');
    const accountId = parseId(val('cx-account'));
    if (paid > 0 && !accountId) return showModal('Select the Cash / Bank account used for this payment.');
    const dueDate = val('cx-due');
    if (status !== 'Paid' && !dueDate) return showModal('Enter the due date for the outstanding amount.');
    try {
      await api('/Projects/SaveExpenditure', {
        method: 'POST',
        body: JSON.stringify({
          projectId: pid,
          date: val('cx-date') || today(),
          costType: val('cx-type'),
          item: val('cx-item'),
          qty: num('cx-qty'),
          unit: val('cx-unit'),
          model: val('cx-model'),
          spec: val('cx-spec'),
          warrantyUntil: val('cx-warranty') || null,
          vendor: val('cx-supplier'),
          amount: amt,
          paidAmount: paid,
          paymentStatus: status,
          accountId: accountId,
          fundingSource: val('cx-funding'),
          dueDate: dueDate || null,
          note: val('cx-note')
        })
      });
      setVal('cx-amount', ''); setVal('cx-paid', ''); setVal('cx-due', ''); setVal('cx-note', '');
      setVal('cx-item', ''); setVal('cx-qty', ''); setVal('cx-model', ''); setVal('cx-spec', ''); setVal('cx-warranty', '');
      setVal('cx-paystatus', 'Paid');
      syncCapexPaymentFields();
      toast('Capital cost recorded.');
      await reloadPage();
    } catch (err) { await showModal(err.message); }
  });

  document.getElementById('addFixedAsset')?.addEventListener('click', async () => {
    if (!can('create')) return;
    const name = val('fa-name');
    const cost = num('fa-cost');
    if (!name || cost <= 0) return showModal('Enter asset name and value.');
    const pid = parseId(val('fa-project'));
    const paid = Math.max(0, Math.min(cost, num('fa-paid')));
    const accountId = parseId(val('fa-account'));
    if (pid && paid > 0) return showModal('Project-linked assets should be capitalized from CIP. Do not pay them again here.');
    try {
      await api('/Projects/SaveFixedAsset', {
        method: 'POST',
        body: JSON.stringify({
          name,
          category: val('fa-type'),
          cost,
          purchaseDate: val('fa-date') || today(),
          projectId: pid,
          location: val('fa-location'),
          supplier: val('fa-supplier'),
          paidAmount: pid ? 0 : paid,
          accountId: pid ? null : accountId,
          warrantyUntil: val('fa-warranty') || null,
          notes: val('fa-note')
        })
      });
      setVal('fa-name', ''); setVal('fa-cost', ''); setVal('fa-paid', '');
      toast('Asset saved.');
      await reloadPage();
    } catch (err) { await showModal(err.message); }
  });

  document.getElementById('addSolarLog')?.addEventListener('click', async () => {
    if (!can('create')) return;
    const pid = parseId(val('sol-project'));
    if (!pid) return showModal('Select a solar project.');
    try {
      await api('/Projects/SaveSolarLog', {
        method: 'POST',
        body: JSON.stringify({
          projectId: pid,
          date: val('sol-date') || today(),
          kwhGenerated: num('sol-kwh'),
          savingsEstimate: num('sol-saving'),
          maintenanceAmount: num('sol-maint'),
          installedCapacityKw: num('sol-kw'),
          notes: val('sol-note')
        })
      });
      setVal('sol-kwh', ''); setVal('sol-saving', ''); setVal('sol-maint', ''); setVal('sol-kw', ''); setVal('sol-note', '');
      toast('Solar record saved.');
      await reloadPage();
    } catch (err) { await showModal(err.message); }
  });

  document.getElementById('view-projects')?.addEventListener('click', async e => {
    const btn = e.target.closest('[data-delproject],[data-delcapex],[data-delfixed],[data-capitalize],[data-paycapex]');
    if (!btn) return;
    const p = btn.dataset.delproject;
    const c = btn.dataset.delcapex;
    const a = btn.dataset.delfixed;
    const cap = btn.dataset.capitalize;
    const payc = btn.dataset.paycapex;

    try {
      if (payc) {
        if (!can('edit')) return;
        const x = (page.expenditures || []).find(z => String(z.id) === String(payc));
        if (!x) return;
        const due = +x.payable || 0;
        if (!due) return;
        const accs = page.accounts || [];
        if (!accs.length) return showModal('Add a Cash / Bank account under Accounting first.');
        const amtStr = await showModal('Pay CAPEX balance · ' + rs(due) + ' due. Payment amount (Rs):', { prompt: true, defaultValue: String(due) });
        if (amtStr == null || amtStr === false) return;
        const amt = Math.max(0, Math.min(due, +amtStr || 0));
        if (!amt) return;
        const pick = await showModal('Paid from — type the number:\n' + accs.map((ac, i) => (i + 1) + '. ' + ac.name).join('\n'), { prompt: true, defaultValue: '1' });
        if (pick == null || pick === false) return;
        const acc = accs[+pick - 1];
        if (!acc) return showModal('Choose the Cash/Bank account.');
        await api('/Projects/PayExpenditure', { method: 'POST', body: JSON.stringify({ id: +payc, amount: amt, accountId: acc.id }) });
        toast('Payment recorded.');
        await reloadPage();
        return;
      }
      if (cap) {
        if (!can('edit')) return;
        const pr = (page.projects || []).find(x => String(x.id) === String(cap));
        if (!pr) return;
        if (!(+pr.cip > 0)) return showModal('There is no uncapitalized project cost to convert.');
        const assetName = await showModal('Fixed asset name', { prompt: true, defaultValue: pr.name });
        if (!assetName) return;
        const inService = (await showModal('In-service date (YYYY-MM-DD)', { prompt: true, defaultValue: today() })) || today();
        if (!await showConfirm('Convert ' + rs(pr.cip) + ' of project CIP into fixed asset “' + assetName + '”? This does not create another cash payment or P&L expense.')) return;
        await api('/Projects/CapitalizeProject', { method: 'POST', body: JSON.stringify({ projectId: +cap, assetName, inServiceDate: inService }) });
        toast('Project converted to a fixed asset.');
        await reloadPage();
        return;
      }
      if (p) {
        if (!can('del') || !await showConfirm('Delete this project? Linked capital expenditure will also be removed.')) return;
        await api('/Projects/DeleteProject?id=' + encodeURIComponent(p), { method: 'DELETE' });
      }
      if (c) {
        if (!can('del') || !await showConfirm('Delete this capital expenditure and its linked Bank/Cash movements?')) return;
        await api('/Projects/DeleteExpenditure?id=' + encodeURIComponent(c), { method: 'DELETE' });
      }
      if (a) {
        if (!can('del') || !await showConfirm('Delete this fixed asset? If it came from a project, its value will return to CIP. Linked direct-asset Bank/Cash movements will also be removed.')) return;
        await api('/Projects/DeleteFixedAsset?id=' + encodeURIComponent(a), { method: 'DELETE' });
      }
      if (p || c || a) {
        toast('Deleted.');
        await reloadPage();
      }
    } catch (err) {
      await showModal(err.message);
    }
  });

  const startTab = new URL(location.href).searchParams.get('tab') || (() => { try { return localStorage.getItem('sfpj:tab'); } catch { return null; } })();
  if (startTab && document.getElementById('proj-' + startTab)) showTab(startTab);
  renderProjects();
})();
