/* Goat Records — client-side AJAX layer (replaces local JS arrays) */
const GoatRecords = (() => {
  const rs = n => 'Rs ' + Math.round(n).toLocaleString('en-US');

  let modalDone = null;

  function initModal() {
    const overlay = document.getElementById('appModal');
    const input = document.getElementById('appModalInput');
    const okBtn = document.getElementById('appModalOk');
    const cancelBtn = document.getElementById('appModalCancel');
    if (!overlay || !okBtn || !cancelBtn) return;

    function close(result) {
      overlay.classList.remove('open');
      overlay.setAttribute('aria-hidden', 'true');
      document.body.classList.remove('modal-open');
      input.classList.remove('show');
      const done = modalDone;
      modalDone = null;
      if (done) done(result);
    }

    okBtn.addEventListener('click', () => {
      close(input.classList.contains('show') ? (input.value.trim() || null) : true);
    });
    cancelBtn.addEventListener('click', () => {
      close(input.classList.contains('show') ? null : false);
    });
    overlay.addEventListener('click', e => {
      if (e.target === overlay) close(false);
    });
    overlay.querySelector('.modal-box')?.addEventListener('click', e => e.stopPropagation());
    input.addEventListener('keydown', e => {
      if (e.key === 'Enter') { e.preventDefault(); okBtn.click(); }
    });
    document.addEventListener('keydown', e => {
      if (!overlay.classList.contains('open')) return;
      if (e.key === 'Escape') close(false);
    });
  }

  /** @returns {Promise<boolean|string|null>} */
  function showModal(message, { prompt = false, defaultValue = '', okText = 'OK', cancelText = 'Cancel' } = {}) {
    return new Promise(resolve => {
      const overlay = document.getElementById('appModal');
      const msg = document.getElementById('appModalMsg');
      const input = document.getElementById('appModalInput');
      const okBtn = document.getElementById('appModalOk');
      const cancelBtn = document.getElementById('appModalCancel');
      if (!overlay || !msg) { resolve(false); return; }

      msg.textContent = message;
      if (okBtn) okBtn.textContent = okText;
      if (cancelBtn) cancelBtn.textContent = cancelText;
      modalDone = resolve;

      if (prompt) {
        input.classList.add('show');
        input.value = defaultValue;
        setTimeout(() => input.focus(), 30);
      } else {
        input.classList.remove('show');
        setTimeout(() => okBtn?.focus(), 30);
      }

      overlay.classList.add('open');
      overlay.setAttribute('aria-hidden', 'false');
      document.body.classList.add('modal-open');
    });
  }

  function showConfirm(message) {
    return showModal(message, { okText: 'Yes', cancelText: 'No' });
  }

  function showToast(message, type = 'success') {
    const host = document.getElementById('toastHost');
    if (!host) return;
    const el = document.createElement('div');
    el.className = 'toast toast-' + type;
    el.textContent = message;
    host.appendChild(el);
    requestAnimationFrame(() => el.classList.add('show'));
    setTimeout(() => {
      el.classList.remove('show');
      setTimeout(() => el.remove(), 280);
    }, 3200);
  }

  function reloadWithToast(message) {
    sessionStorage.setItem('goatToast', message);
    location.reload();
  }

  function flashStoredToast() {
    const message = sessionStorage.getItem('goatToast');
    if (!message) return;
    sessionStorage.removeItem('goatToast');
    showToast(message);
  }

  initModal();
  flashStoredToast();

  const FarmPerms = {
    get(tab) {
      return window.FarmPermissions?.[tab] ?? { view: true, add: true, edit: true, delete: true };
    },
    can(tab, action) {
      const p = this.get(tab);
      if (action !== 'view' && !p.view) return false;
      return !!p[action];
    },
    hide(id) {
      const el = document.getElementById(id);
      if (el) el.classList.add('perm-hidden');
    },
    show(id) {
      document.getElementById(id)?.classList.remove('perm-hidden');
    },
    applyForm(tab, { addBtnId, deleteBtnId, rowSelector, extraHideIds = [] }) {
      const p = this.get(tab);
      const addBtn = addBtnId ? document.getElementById(addBtnId) : null;
      const panel = addBtn?.closest('.panel');
      if (!p.add && !p.edit) {
        if (panel) panel.classList.add('perm-hidden');
        return;
      }
      if (!p.add) {
        panel?.querySelector('.add-grid')?.classList.add('perm-hidden');
        addBtn?.classList.add('perm-hidden');
      }
      if (!p.delete && deleteBtnId) this.hide(deleteBtnId);
      if (!p.edit) {
        extraHideIds.forEach(id => this.hide(id));
        if (rowSelector) {
          document.querySelectorAll(rowSelector).forEach(row => {
            row.style.cursor = 'default';
            row.classList.remove('health-row', 'milk-row', 'fin-row', 'goat-row');
          });
        }
      }
    },
    revealEditForm(addBtnId) {
      const addBtn = document.getElementById(addBtnId);
      const panel = addBtn?.closest('.panel');
      panel?.classList.remove('perm-hidden');
      panel?.querySelector('.add-grid')?.classList.remove('perm-hidden');
      addBtn?.classList.remove('perm-hidden');
    },
    guardAddEdit(tab, editing) {
      return editing ? this.can(tab, 'edit') : this.can(tab, 'add');
    },
    readonlyInputs(selector) {
      document.querySelectorAll(selector).forEach(el => { el.disabled = true; });
    }
  };

  async function api(url, options = {}) {
    const res = await fetch(url, {
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', ...(options.headers || {}) },
      credentials: 'same-origin',
      ...options
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      const message = err.error || err.title || Object.values(err.errors || {}).flat?.()[0] ||
        (res.status === 403 ? 'You do not have permission for this action.' : 'Something went wrong. Please try again.');
      await showModal(message);
      throw new Error(message);
    }
    if (res.status === 204) return null;
    const ct = res.headers.get('content-type') || '';
    return ct.includes('json') ? res.json() : null;
  }

  const selected = new Set();

  function initHerd(opts = {}) {
    const filter = document.getElementById('filter');
    if (filter && opts.filter) filter.value = opts.filter;

    let editingId = null;

    function applySourcePriceState() {
      const p = document.getElementById('f-price');
      const source = document.getElementById('f-source')?.value;
      if (!p) return;
      p.disabled = source === 'Born';
      if (source === 'Born') p.value = '';
    }

    function setTagHint(msg, isError = false) {
      const hint = document.getElementById('tag-lookup-hint');
      if (!hint) return;
      hint.textContent = msg || 'Scan to load an existing goat, or enter a new tag to add.';
      hint.classList.toggle('error', isError);
    }

    function setEditMode(editing) {
      const cancelBtn = document.getElementById('cancelBtn');
      const deleteBtn = document.getElementById('deleteBtn');
      if (cancelBtn) cancelBtn.style.display = editing ? '' : 'none';
      if (deleteBtn) deleteBtn.style.display = editing ? '' : 'none';
    }

    function resetGoatForm() {
      editingId = null;
      document.getElementById('goatFormTitle').textContent = 'Add a goat';
      document.getElementById('addBtn').textContent = '+ Add goat';
      document.getElementById('f-tag').disabled = false;
      document.getElementById('f-tag').value = '';
      document.getElementById('f-name').value = '';
      document.getElementById('f-breed').selectedIndex = 0;
      document.getElementById('f-gender').value = 'Female';
      document.getElementById('f-source').value = 'Bought';
      document.getElementById('f-price').value = '';
      document.getElementById('f-status').value = 'Kid';
      document.getElementById('f-date').value = '';
      applySourcePriceState();
      document.querySelectorAll('#rows tr.goat-row.editing').forEach(r => r.classList.remove('editing'));
      setEditMode(false);
      setTagHint();
    }

    function enumValue(value, fallback) {
      return typeof value === 'string' ? value : fallback;
    }

    function fillGoatForm(goat) {
      editingId = goat.id;
      document.getElementById('goatFormTitle').textContent = 'Edit goat';
      document.getElementById('addBtn').textContent = 'Save';
      document.getElementById('f-tag').value = goat.tag || '';
      document.getElementById('f-tag').disabled = true;
      document.getElementById('f-name').value = goat.name || '';
      document.getElementById('f-breed').value = goat.breed || '';
      document.getElementById('f-gender').value = enumValue(goat.gender, 'Female');
      const source = enumValue(goat.source, 'Bought');
      document.getElementById('f-source').value = source;
      document.getElementById('f-price').value = source === 'Born' ? '' : (goat.purchasePrice ?? '');
      document.getElementById('f-status').value = enumValue(goat.status, 'Kid');
      document.getElementById('f-date').value = goat.eventDateDisplay || goat.eventDate || '';
      applySourcePriceState();
      setEditMode(true);
      if (!FarmPerms.can('herd', 'add')) FarmPerms.revealEditForm('addBtn');
    }

    function openGoatForEdit(goat) {
      if (!goat?.id || !FarmPerms.can('herd', 'edit')) return;
      fillGoatForm(goat);
      document.querySelectorAll('#rows tr.goat-row.editing').forEach(r => r.classList.remove('editing'));
      const row = document.querySelector(`#rows tr.goat-row[data-id="${goat.id}"]`);
      if (row) {
        row.classList.add('editing');
        row.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      } else {
        document.getElementById('goatFormPanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
      setTagHint(`Editing goat ${goat.tag}.`);
      document.getElementById('f-name').focus();
    }

    function loadGoatForEdit(row) {
      if (!FarmPerms.can('herd', 'edit')) return;
      fillGoatForm({
        id: +row.dataset.id,
        tag: row.dataset.tag,
        name: row.dataset.name,
        breed: row.dataset.breed,
        gender: row.dataset.gender,
        source: row.dataset.source,
        purchasePrice: row.dataset.price,
        status: row.dataset.status,
        eventDateDisplay: row.dataset.date
      });
      document.querySelectorAll('#rows tr.goat-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setTagHint(`Editing goat ${row.dataset.tag}.`);
      document.getElementById('f-name').focus();
    }

    function normalizeTag(value) {
      return String(value ?? '').replace(/[\x00-\x1F\x7F]/g, '').trim();
    }

    function findRowByTag(tag) {
      const needle = normalizeTag(tag).toLowerCase();
      if (!needle) return null;
      for (const row of document.querySelectorAll('#rows tr.goat-row')) {
        if ((row.dataset.tag || '').toLowerCase() === needle) return row;
      }
      return null;
    }

    async function lookupGoatByTag() {
      if (editingId) return;

      const tagInput = document.getElementById('f-tag');
      const tag = normalizeTag(tagInput?.value);
      if (!tag) {
        setTagHint('Enter or scan a tag / RFID ID.', true);
        tagInput?.focus();
        return;
      }

      if (!FarmPerms.can('herd', 'edit')) {
        setTagHint('You do not have permission to edit goats.', true);
        return;
      }

      const localRow = findRowByTag(tag);
      if (localRow) {
        loadGoatForEdit(localRow);
        return;
      }

      setTagHint('Looking up tag…');
      tagInput.disabled = true;

      try {
        const res = await fetch('/Goat/GetByTag?tag=' + encodeURIComponent(tag), {
          headers: { Accept: 'application/json' },
          credentials: 'same-origin'
        });

        if (res.status === 404) {
          setTagHint(`Tag "${tag}" not found — fill in the details below to add a new goat.`, true);
          return;
        }

        if (res.status === 403) {
          setTagHint('You do not have permission to look up goats.', true);
          return;
        }

        if (!res.ok) {
          const err = await res.json().catch(() => ({}));
          setTagHint(err.error || 'Could not look up that tag.', true);
          return;
        }

        const goat = await res.json();
        openGoatForEdit(goat);
      } catch {
        setTagHint('Could not reach the server. Try again.', true);
      } finally {
        if (!editingId) tagInput.disabled = false;
        else tagInput.disabled = true;
      }
    }

    function herdUrl(filter, page) {
      const params = new URLSearchParams();
      if (filter && filter !== 'all') params.set('filter', filter);
      if (page && page > 1) params.set('page', page);
      const q = params.toString();
      return '/Goat' + (q ? '?' + q : '');
    }

    function goatPayload() {
      const source = document.getElementById('f-source').value;
      return {
        tag: document.getElementById('f-tag').value.trim(),
        name: document.getElementById('f-name').value.trim(),
        breed: document.getElementById('f-breed').value,
        gender: document.getElementById('f-gender').value,
        status: document.getElementById('f-status').value,
        source,
        purchasePrice: source === 'Born' ? 0 : (+document.getElementById('f-price').value || 0),
        eventDate: document.getElementById('f-date').value
      };
    }

    document.getElementById('f-source')?.addEventListener('change', applySourcePriceState);

    document.getElementById('f-tag')?.addEventListener('keydown', e => {
      if (e.key === 'Enter') {
        e.preventDefault();
        lookupGoatByTag();
      }
    });

    document.getElementById('cancelBtn')?.addEventListener('click', resetGoatForm);

    document.getElementById('deleteBtn')?.addEventListener('click', async () => {
      if (!editingId || !FarmPerms.can('herd', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this goat?');
      if (!confirmed) return;
      await api('/Goat/Delete?id=' + editingId, { method: 'DELETE' });
      reloadWithToast('Goat deleted successfully');
    });

    document.getElementById('addBtn')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('herd', !!editingId)) return;
      const tag = document.getElementById('f-tag').value.trim();
      const date = document.getElementById('f-date').value;
      if (!tag) { await showModal('Please enter a Tag / RFID ID'); return; }
      if (!date) { await showModal('Please pick the date'); return; }
      const payload = goatPayload();
      if (editingId) {
        await api('/Goat/Update?id=' + editingId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadWithToast('Goat updated successfully');
      } else {
        await api('/Goat/Create', { method: 'POST', body: JSON.stringify(payload) });
        reloadWithToast('Goat added successfully');
      }
    });

    document.querySelectorAll('#rows tr.goat-row').forEach(row => {
      row.addEventListener('click', e => {
        if (!FarmPerms.can('herd', 'edit')) return;
        if (e.target.closest('input[type=checkbox]') || e.target.closest('a.tag-link')) return;
        loadGoatForEdit(row);
      });
    });

    filter?.addEventListener('change', () => {
      location.href = herdUrl(filter.value, 1);
    });

    document.querySelectorAll('#stats .stat').forEach(card => {
      card.addEventListener('click', () => {
        const f = card.dataset.filter;
        if (f) location.href = herdUrl(f, 1);
      });
    });

    document.getElementById('checkAll')?.addEventListener('change', e => {
      document.querySelectorAll('#rows input[type=checkbox]').forEach(cb => {
        cb.checked = e.target.checked;
        e.target.checked ? selected.add(+cb.dataset.id) : selected.delete(+cb.dataset.id);
      });
      renderBulk();
    });

    document.querySelectorAll('#rows input[type=checkbox]').forEach(cb => {
      cb.addEventListener('change', () => {
        cb.checked ? selected.add(+cb.dataset.id) : selected.delete(+cb.dataset.id);
        renderBulk();
        cb.closest('tr')?.classList.toggle('checked', cb.checked);
      });
    });

    document.getElementById('moveBtn')?.addEventListener('click', async () => {
      if (!selected.size || !FarmPerms.can('herd', 'edit')) return;
      await api('/Goat/BulkMove', {
        method: 'POST',
        body: JSON.stringify({ goatIds: [...selected], moveTarget: document.getElementById('moveTo').value })
      });
      location.reload();
    });

    document.getElementById('newGroupBtn')?.addEventListener('click', async () => {
      if (!FarmPerms.can('herd', 'add')) return;
      const name = await showModal('Name the new group', { prompt: true });
      if (name?.trim()) {
        await api('/Goat/CreateGroup', { method: 'POST', body: JSON.stringify({ name: name.trim() }) });
        location.reload();
      }
    });

    renderBulk();

    FarmPerms.applyForm('herd', {
      addBtnId: 'addBtn',
      deleteBtnId: 'deleteBtn',
      rowSelector: '#rows tr.goat-row',
      extraHideIds: ['bulkbar', 'newGroupBtn']
    });

    if (opts.editGoat && opts.editGoat.id) {
      openGoatForEdit(opts.editGoat);
    } else {
      document.getElementById('f-tag')?.focus();
    }
  }

  function renderBulk() {
    const bar = document.getElementById('bulkbar');
    const cnt = document.getElementById('selCount');
    if (bar) bar.classList.toggle('show', selected.size > 0);
    if (cnt) cnt.textContent = selected.size + ' selected';
  }

  function bindFeedInputs(onSave) {
    if (!FarmPerms.can('feed', 'edit')) return;
    document.querySelectorAll('[data-price]').forEach(inp => {
      inp.onchange = async () => {
        await api('/Feed/UpdatePrice', {
          method: 'POST',
          body: JSON.stringify({ feedType: inp.dataset.price, price: +inp.value || 0 })
        });
        await onSave();
      };
    });
    document.querySelectorAll('[data-ration]').forEach(inp => { inp.onchange = onSave; });
    const med = document.getElementById('medIn');
    if (med) med.onchange = onSave;
  }

  function initFeed() {
    const savePlan = async () => {
      const statusKey = document.getElementById('planGroup').value;
      const rations = {};
      document.querySelectorAll('[data-ration]').forEach(inp => { rations[inp.dataset.ration] = +inp.value || 0; });
      await api('/Feed/UpdatePlan', {
        method: 'POST',
        body: JSON.stringify({
          statusKey,
          medicineCostPerGoatPerMonth: +document.getElementById('medIn').value || 0,
          rations
        })
      });
      await reloadFeed();
    };

    async function reloadFeed() {
      const status = document.getElementById('planGroup')?.value;
      const data = await api('/Feed/GetData?status=' + encodeURIComponent(status || ''));
      document.getElementById('grandMonth').textContent = rs(data.grandMonthly);
      document.getElementById('grandDay').textContent = rs(data.grandDaily);
      document.getElementById('grandHead').textContent = 'for ' + data.totalGoats + ' goats';

      const plan = data.currentPlan;
      document.getElementById('medIn').value = plan.medicineCostPerGoatPerMonth;
      document.getElementById('rationList').innerHTML = plan.items.map(item =>
        `<div class="ration-row"><div class="rn">${item.displayName}</div>
          <div class="rin"><input type="number" min="0" data-ration="${item.feedType}" value="${item.gramsPerDay}"><span class="u">g/day</span></div>
          <div class="rcost">${rs(item.dailyCost)}/day</div></div>`).join('');
      document.getElementById('planResult').innerHTML =
        `<div class="r"><div class="v">${plan.goatCount}</div><div class="k">goats in this group</div></div>
         <div class="r"><div class="v">${rs(plan.dailyFeedCost * plan.goatCount)}</div><div class="k">feed cost per day</div></div>
         <div class="r"><div class="v">${rs(plan.dailyFeedCost * 30 * plan.goatCount + plan.medicineCostPerGoatPerMonth * plan.goatCount)}</div><div class="k">total per month</div></div>`;

      document.getElementById('summaryRows').innerHTML = data.summary.map(row =>
        `<tr><td><span class="chip ${row.statusCssClass}">${row.statusDisplay}</span></td>
          <td class="num-cell">${row.goatCount}</td><td class="num-cell">${rs(row.feedMonthly)}</td>
          <td class="num-cell hide-sm">${rs(row.medicineMonthly)}</td>
          <td class="num-cell" style="color:var(--green-dark)">${rs(row.totalMonthly)}</td></tr>`).join('') +
        `<tr style="background:var(--green-tint)"><td style="font-weight:800">TOTAL</td>
          <td class="num-cell" style="font-weight:800">${data.totalGoats}</td>
          <td class="num-cell" style="font-weight:800">${rs(data.summary.reduce((s,r)=>s+r.feedMonthly,0))}</td>
          <td class="num-cell hide-sm" style="font-weight:800">${rs(data.summary.reduce((s,r)=>s+r.medicineMonthly,0))}</td>
          <td class="num-cell" style="font-weight:800;color:var(--green-dark)">${rs(data.grandMonthly)}</td></tr>`;

      document.getElementById('buyRows').innerHTML = data.buyingList.map(b =>
        `<tr><td>${b.displayName}</td><td class="num-cell">${b.kgPerDay.toFixed(1)}</td>
          <td class="num-cell hide-sm">${Math.round(b.kgPerMonth).toLocaleString('en-US')}</td>
          <td class="num-cell" style="color:var(--green-dark)">${rs(b.costPerMonth)}</td></tr>`).join('');

      bindFeedInputs(savePlan);
    }

    document.getElementById('planGroup')?.addEventListener('change', reloadFeed);
    if (!FarmPerms.can('feed', 'edit')) {
      FarmPerms.readonlyInputs('[data-price], [data-ration], #medIn, #planGroup');
    } else {
      bindFeedInputs(async () => { await savePlan(); });
    }
  }

  function initMilk() {
    let editingProdId = null;
    let editingSaleId = null;
    let editingWasteId = null;

    function reloadMilkWithToast(message, { resetProd, resetSale, resetWaste } = {}) {
      const params = new URLSearchParams(window.location.search || '');
      if (resetProd) params.set('prodPage', '1');
      if (resetSale) params.set('salePage', '1');
      if (resetWaste) params.set('wastePage', '1');
      const q = params.toString();
      sessionStorage.setItem('goatToast', message);
      location.href = '/Milk' + (q ? '?' + q : '');
    }

    const milkAmt = () => {
      const l = +document.getElementById('s-liters').value || 0;
      const r = +document.getElementById('s-rate').value || 0;
      document.getElementById('s-amt').value = l && r ? Math.round(l * r) : '';
    };
    document.getElementById('s-liters')?.addEventListener('input', milkAmt);
    document.getElementById('s-rate')?.addEventListener('input', milkAmt);

    function setProdEditMode(editing) {
      document.getElementById('cancelProdBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteProdBtn').style.display = editing ? '' : 'none';
    }

    function setSaleEditMode(editing) {
      document.getElementById('cancelSaleBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteSaleBtn').style.display = editing ? '' : 'none';
    }

    function setWasteEditMode(editing) {
      document.getElementById('cancelWasteBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteWasteBtn').style.display = editing ? '' : 'none';
    }

    function resetProdForm() {
      editingProdId = null;
      document.getElementById('prodFormTitle').textContent = 'Milk collected';
      document.getElementById('addProd').textContent = '+ Add';
      document.getElementById('p-date').value = new Date().toISOString().slice(0, 10);
      document.getElementById('p-breed').selectedIndex = 0;
      document.getElementById('p-liters').value = '';
      document.querySelectorAll('#prodRows tr.milk-prod-row.editing').forEach(r => r.classList.remove('editing'));
      setProdEditMode(false);
    }

    function resetSaleForm() {
      editingSaleId = null;
      document.getElementById('saleFormTitle').textContent = 'Milk sold';
      document.getElementById('addSale').textContent = '+ Add';
      document.getElementById('s-date').value = new Date().toISOString().slice(0, 10);
      document.getElementById('s-liters').value = '';
      document.getElementById('s-rate').value = '';
      document.getElementById('s-amt').value = '';
      document.querySelectorAll('#saleRows tr.milk-sale-row.editing').forEach(r => r.classList.remove('editing'));
      setSaleEditMode(false);
    }

    function resetWasteForm() {
      editingWasteId = null;
      document.getElementById('wasteFormTitle').textContent = 'Milk waste';
      document.getElementById('addWaste').textContent = '+ Add';
      document.getElementById('w-date').value = new Date().toISOString().slice(0, 10);
      document.getElementById('w-liters').value = '';
      document.getElementById('w-notes').value = '';
      document.querySelectorAll('#wasteRows tr.milk-waste-row.editing').forEach(r => r.classList.remove('editing'));
      setWasteEditMode(false);
    }

    function loadProdForEdit(row) {
      if (!FarmPerms.can('milk', 'edit')) return;
      editingProdId = +row.dataset.id;
      document.getElementById('prodFormTitle').textContent = 'Edit milk collected';
      document.getElementById('addProd').textContent = 'Save';
      document.getElementById('p-date').value = row.dataset.date || '';
      document.getElementById('p-breed').value = row.dataset.breed || '';
      document.getElementById('p-liters').value = row.dataset.liters || '';
      document.querySelectorAll('#prodRows tr.milk-prod-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setProdEditMode(true);
      if (!FarmPerms.can('milk', 'add')) FarmPerms.revealEditForm('addProd');
      document.getElementById('p-liters').focus();
    }

    function loadSaleForEdit(row) {
      if (!FarmPerms.can('milk', 'edit')) return;
      editingSaleId = +row.dataset.id;
      document.getElementById('saleFormTitle').textContent = 'Edit milk sold';
      document.getElementById('addSale').textContent = 'Save';
      document.getElementById('s-date').value = row.dataset.date || '';
      document.getElementById('s-liters').value = row.dataset.liters || '';
      document.getElementById('s-rate').value = row.dataset.rate || '';
      milkAmt();
      document.querySelectorAll('#saleRows tr.milk-sale-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setSaleEditMode(true);
      if (!FarmPerms.can('milk', 'add')) FarmPerms.revealEditForm('addSale');
      document.getElementById('s-liters').focus();
    }

    function loadWasteForEdit(row) {
      if (!FarmPerms.can('milk', 'edit')) return;
      editingWasteId = +row.dataset.id;
      document.getElementById('wasteFormTitle').textContent = 'Edit milk waste';
      document.getElementById('addWaste').textContent = 'Save';
      document.getElementById('w-date').value = row.dataset.date || '';
      document.getElementById('w-liters').value = row.dataset.liters || '';
      document.getElementById('w-notes').value = row.dataset.notes || '';
      document.querySelectorAll('#wasteRows tr.milk-waste-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setWasteEditMode(true);
      if (!FarmPerms.can('milk', 'add')) FarmPerms.revealEditForm('addWaste');
      document.getElementById('w-liters').focus();
    }

    document.getElementById('cancelProdBtn')?.addEventListener('click', resetProdForm);
    document.getElementById('cancelSaleBtn')?.addEventListener('click', resetSaleForm);
    document.getElementById('cancelWasteBtn')?.addEventListener('click', resetWasteForm);

    document.getElementById('deleteProdBtn')?.addEventListener('click', async () => {
      if (!editingProdId || !FarmPerms.can('milk', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      await api('/Milk/DeleteProduction?id=' + editingProdId, { method: 'DELETE' });
      reloadMilkWithToast('Milk collection deleted successfully');
    });

    document.getElementById('deleteSaleBtn')?.addEventListener('click', async () => {
      if (!editingSaleId || !FarmPerms.can('milk', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      await api('/Milk/DeleteSale?id=' + editingSaleId, { method: 'DELETE' });
      reloadMilkWithToast('Milk sale deleted successfully');
    });

    document.getElementById('deleteWasteBtn')?.addEventListener('click', async () => {
      if (!editingWasteId || !FarmPerms.can('milk', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      await api('/Milk/DeleteWaste?id=' + editingWasteId, { method: 'DELETE' });
      reloadMilkWithToast('Milk waste deleted successfully');
    });

    document.getElementById('addProd')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('milk', !!editingProdId)) return;
      const date = document.getElementById('p-date').value;
      const liters = +document.getElementById('p-liters').value || 0;
      if (!date || !liters) { await showModal('Enter date and litres'); return; }
      const payload = { date, breed: document.getElementById('p-breed').value, liters };
      if (editingProdId) {
        await api('/Milk/UpdateProduction?id=' + editingProdId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadMilkWithToast('Milk collection updated successfully');
      } else {
        await api('/Milk/AddProduction', { method: 'POST', body: JSON.stringify(payload) });
        reloadMilkWithToast('Milk collection added successfully', { resetProd: true });
      }
    });

    document.getElementById('addSale')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('milk', !!editingSaleId)) return;
      const date = document.getElementById('s-date').value;
      const liters = +document.getElementById('s-liters').value || 0;
      const rate = +document.getElementById('s-rate').value || 0;
      if (!date || !liters || !rate) { await showModal('Enter date, litres and rate'); return; }
      const payload = { date, liters, rate };
      if (editingSaleId) {
        await api('/Milk/UpdateSale?id=' + editingSaleId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadMilkWithToast('Milk sale updated successfully');
      } else {
        await api('/Milk/AddSale', { method: 'POST', body: JSON.stringify(payload) });
        reloadMilkWithToast('Milk sale added successfully', { resetSale: true });
      }
    });

    document.getElementById('addWaste')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('milk', !!editingWasteId)) return;
      const date = document.getElementById('w-date').value;
      const liters = +document.getElementById('w-liters').value || 0;
      if (!date || !liters) { await showModal('Enter date and litres'); return; }
      const notes = document.getElementById('w-notes').value.trim();
      const payload = { date, liters, notes: notes || null };
      if (editingWasteId) {
        await api('/Milk/UpdateWaste?id=' + editingWasteId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadMilkWithToast('Milk waste updated successfully');
      } else {
        await api('/Milk/AddWaste', { method: 'POST', body: JSON.stringify(payload) });
        reloadMilkWithToast('Milk waste added successfully', { resetWaste: true });
      }
    });

    document.querySelectorAll('#prodRows tr.milk-prod-row').forEach(row => {
      row.addEventListener('click', () => { if (FarmPerms.can('milk', 'edit')) loadProdForEdit(row); });
    });
    document.querySelectorAll('#saleRows tr.milk-sale-row').forEach(row => {
      row.addEventListener('click', () => { if (FarmPerms.can('milk', 'edit')) loadSaleForEdit(row); });
    });
    document.querySelectorAll('#wasteRows tr.milk-waste-row').forEach(row => {
      row.addEventListener('click', () => { if (FarmPerms.can('milk', 'edit')) loadWasteForEdit(row); });
    });

    [
      ['addProd', 'deleteProdBtn', '#prodRows tr.milk-prod-row'],
      ['addSale', 'deleteSaleBtn', '#saleRows tr.milk-sale-row'],
      ['addWaste', 'deleteWasteBtn', '#wasteRows tr.milk-waste-row']
    ].forEach(([addBtnId, deleteBtnId, rowSelector]) => {
      FarmPerms.applyForm('milk', { addBtnId, deleteBtnId, rowSelector });
    });
  }

  function initFinance() {
    let editingAssetId = null;
    let editingIncomeId = null;
    let editingExpenseId = null;
    let editingOwnerId = null;

    function financeUrl(date) {
      const month = date ? date.slice(0, 7) : (document.getElementById('finMonth')?.value || '');
      return '/Finance' + (month ? '?month=' + encodeURIComponent(month) : '');
    }

    function reloadFinanceWithToast(message, date) {
      sessionStorage.setItem('goatToast', message);
      location.href = financeUrl(date);
    }

    document.getElementById('finMonth')?.addEventListener('change', e => {
      location.href = '/Finance?month=' + encodeURIComponent(e.target.value);
    });

    function setAssetEditMode(editing) {
      document.getElementById('cancelAssetBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteAssetBtn').style.display = editing ? '' : 'none';
    }

    function setIncomeEditMode(editing) {
      document.getElementById('cancelIncomeBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteIncomeBtn').style.display = editing ? '' : 'none';
    }

    function setExpenseEditMode(editing) {
      document.getElementById('cancelExpenseBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteExpenseBtn').style.display = editing ? '' : 'none';
    }

    function setOwnerEditMode(editing) {
      document.getElementById('cancelOwnerBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteOwnerBtn').style.display = editing ? '' : 'none';
    }

    function resetAssetForm() {
      editingAssetId = null;
      document.getElementById('assetFormTitle').textContent = 'Capital — what the farm owns';
      document.getElementById('addAsset').textContent = '+ Add';
      document.getElementById('a-name').value = '';
      document.getElementById('a-type').selectedIndex = 0;
      document.getElementById('a-cost').value = '';
      document.querySelectorAll('#assetRows tr.fin-asset-row.editing').forEach(r => r.classList.remove('editing'));
      setAssetEditMode(false);
    }

    function resetIncomeForm() {
      editingIncomeId = null;
      document.getElementById('incomeFormTitle').textContent = 'Cash received';
      document.getElementById('addIncome').textContent = '+ Add';
      document.getElementById('i-type').selectedIndex = 0;
      document.getElementById('i-date').value = new Date().toISOString().slice(0, 10);
      document.getElementById('i-amt').value = '';
      document.querySelectorAll('#incomeRows tr.fin-income-row.editing').forEach(r => r.classList.remove('editing'));
      setIncomeEditMode(false);
    }

    function resetExpenseForm() {
      editingExpenseId = null;
      document.getElementById('expenseFormTitle').textContent = 'Running costs';
      document.getElementById('addExpense').textContent = '+ Add';
      document.getElementById('e-type').selectedIndex = 0;
      document.getElementById('e-date').value = new Date().toISOString().slice(0, 10);
      document.getElementById('e-amt').value = '';
      document.querySelectorAll('#expenseRows tr.fin-expense-row.editing').forEach(r => r.classList.remove('editing'));
      setExpenseEditMode(false);
    }

    function resetOwnerForm() {
      editingOwnerId = null;
      document.getElementById('ownerFormTitle').textContent = 'Owner investment — money you put in';
      document.getElementById('addOwner').textContent = '+ Add';
      document.getElementById('o-note').value = '';
      document.getElementById('o-date').value = new Date().toISOString().slice(0, 10);
      document.getElementById('o-amt').value = '';
      document.querySelectorAll('#ownerRows tr.fin-owner-row.editing').forEach(r => r.classList.remove('editing'));
      setOwnerEditMode(false);
    }

    function loadAssetForEdit(row) {
      if (!FarmPerms.can('finance', 'edit')) return;
      editingAssetId = +row.dataset.id;
      document.getElementById('assetFormTitle').textContent = 'Edit asset';
      document.getElementById('addAsset').textContent = 'Save';
      document.getElementById('a-name').value = row.dataset.name || '';
      document.getElementById('a-type').value = row.dataset.type || '';
      document.getElementById('a-cost').value = row.dataset.cost || '';
      document.querySelectorAll('#assetRows tr.fin-asset-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setAssetEditMode(true);
      if (!FarmPerms.can('finance', 'add')) FarmPerms.revealEditForm('addAsset');
      document.getElementById('a-name').focus();
    }

    function loadIncomeForEdit(row) {
      if (!FarmPerms.can('finance', 'edit')) return;
      editingIncomeId = +row.dataset.id;
      document.getElementById('incomeFormTitle').textContent = 'Edit cash received';
      document.getElementById('addIncome').textContent = 'Save';
      document.getElementById('i-type').value = row.dataset.type || '';
      document.getElementById('i-date').value = row.dataset.date || '';
      document.getElementById('i-amt').value = row.dataset.amount || '';
      document.querySelectorAll('#incomeRows tr.fin-income-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setIncomeEditMode(true);
      if (!FarmPerms.can('finance', 'add')) FarmPerms.revealEditForm('addIncome');
      document.getElementById('i-amt').focus();
    }

    function loadExpenseForEdit(row) {
      if (!FarmPerms.can('finance', 'edit')) return;
      editingExpenseId = +row.dataset.id;
      document.getElementById('expenseFormTitle').textContent = 'Edit running cost';
      document.getElementById('addExpense').textContent = 'Save';
      document.getElementById('e-type').value = row.dataset.type || '';
      document.getElementById('e-date').value = row.dataset.date || '';
      document.getElementById('e-amt').value = row.dataset.amount || '';
      document.querySelectorAll('#expenseRows tr.fin-expense-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setExpenseEditMode(true);
      if (!FarmPerms.can('finance', 'add')) FarmPerms.revealEditForm('addExpense');
      document.getElementById('e-amt').focus();
    }

    function loadOwnerForEdit(row) {
      if (!FarmPerms.can('finance', 'edit')) return;
      editingOwnerId = +row.dataset.id;
      document.getElementById('ownerFormTitle').textContent = 'Edit owner investment';
      document.getElementById('addOwner').textContent = 'Save';
      document.getElementById('o-note').value = row.dataset.note || '';
      document.getElementById('o-date').value = row.dataset.date || '';
      document.getElementById('o-amt').value = row.dataset.amount || '';
      document.querySelectorAll('#ownerRows tr.fin-owner-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setOwnerEditMode(true);
      if (!FarmPerms.can('finance', 'add')) FarmPerms.revealEditForm('addOwner');
      document.getElementById('o-amt').focus();
    }

    document.getElementById('cancelAssetBtn')?.addEventListener('click', resetAssetForm);
    document.getElementById('cancelIncomeBtn')?.addEventListener('click', resetIncomeForm);
    document.getElementById('cancelExpenseBtn')?.addEventListener('click', resetExpenseForm);
    document.getElementById('cancelOwnerBtn')?.addEventListener('click', resetOwnerForm);

    document.getElementById('deleteAssetBtn')?.addEventListener('click', async () => {
      if (!editingAssetId || !FarmPerms.can('finance', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      await api('/Finance/DeleteAsset?id=' + editingAssetId, { method: 'DELETE' });
      reloadWithToast('Asset deleted successfully');
    });

    document.getElementById('deleteIncomeBtn')?.addEventListener('click', async () => {
      if (!editingIncomeId || !FarmPerms.can('finance', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      const date = document.getElementById('i-date').value;
      await api('/Finance/DeleteIncome?id=' + editingIncomeId, { method: 'DELETE' });
      reloadFinanceWithToast('Cash received deleted successfully', date);
    });

    document.getElementById('deleteExpenseBtn')?.addEventListener('click', async () => {
      if (!editingExpenseId || !FarmPerms.can('finance', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      const date = document.getElementById('e-date').value;
      await api('/Finance/DeleteExpense?id=' + editingExpenseId, { method: 'DELETE' });
      reloadFinanceWithToast('Running cost deleted successfully', date);
    });

    document.getElementById('deleteOwnerBtn')?.addEventListener('click', async () => {
      if (!editingOwnerId || !FarmPerms.can('finance', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      const date = document.getElementById('o-date').value;
      await api('/Finance/DeleteOwnerInvestment?id=' + editingOwnerId, { method: 'DELETE' });
      reloadFinanceWithToast('Owner investment deleted successfully', date);
    });

    document.getElementById('addAsset')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('finance', !!editingAssetId)) return;
      const name = document.getElementById('a-name').value.trim();
      const cost = +document.getElementById('a-cost').value || 0;
      if (!name || !cost) { await showModal('Enter asset name and cost'); return; }
      const payload = { name, type: document.getElementById('a-type').value, cost };
      if (editingAssetId) {
        await api('/Finance/UpdateAsset?id=' + editingAssetId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadWithToast('Asset updated successfully');
      } else {
        await api('/Finance/AddAsset', { method: 'POST', body: JSON.stringify(payload) });
        reloadWithToast('Asset added successfully');
      }
    });

    document.getElementById('addIncome')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('finance', !!editingIncomeId)) return;
      const amt = +document.getElementById('i-amt').value || 0;
      const date = document.getElementById('i-date').value;
      if (!amt || !date) { await showModal('Enter date and amount'); return; }
      const payload = { type: document.getElementById('i-type').value, amount: amt, date };
      if (editingIncomeId) {
        await api('/Finance/UpdateIncome?id=' + editingIncomeId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadFinanceWithToast('Cash received updated successfully', date);
      } else {
        await api('/Finance/AddIncome', { method: 'POST', body: JSON.stringify(payload) });
        reloadFinanceWithToast('Cash received added successfully', date);
      }
    });

    document.getElementById('addExpense')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('finance', !!editingExpenseId)) return;
      const amt = +document.getElementById('e-amt').value || 0;
      const date = document.getElementById('e-date').value;
      if (!amt || !date) { await showModal('Enter date and amount'); return; }
      const payload = { type: document.getElementById('e-type').value, amount: amt, date };
      if (editingExpenseId) {
        await api('/Finance/UpdateExpense?id=' + editingExpenseId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadFinanceWithToast('Running cost updated successfully', date);
      } else {
        await api('/Finance/AddExpense', { method: 'POST', body: JSON.stringify(payload) });
        reloadFinanceWithToast('Running cost added successfully', date);
      }
    });

    document.getElementById('addOwner')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('finance', !!editingOwnerId)) return;
      const amt = +document.getElementById('o-amt').value || 0;
      const date = document.getElementById('o-date').value;
      const note = document.getElementById('o-note').value.trim();
      if (!amt || !date) { await showModal('Enter date and amount'); return; }
      if (!note) { await showModal('Enter a note'); return; }
      const payload = { note, amount: amt, date };
      if (editingOwnerId) {
        await api('/Finance/UpdateOwnerInvestment?id=' + editingOwnerId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadFinanceWithToast('Owner investment updated successfully', date);
      } else {
        await api('/Finance/AddOwnerInvestment', { method: 'POST', body: JSON.stringify(payload) });
        reloadFinanceWithToast('Owner investment added successfully', date);
      }
    });

    document.querySelectorAll('#assetRows tr.fin-asset-row').forEach(row => {
      row.addEventListener('click', () => { if (FarmPerms.can('finance', 'edit')) loadAssetForEdit(row); });
    });
    document.querySelectorAll('#incomeRows tr.fin-income-row').forEach(row => {
      row.addEventListener('click', () => { if (FarmPerms.can('finance', 'edit')) loadIncomeForEdit(row); });
    });
    document.querySelectorAll('#expenseRows tr.fin-expense-row').forEach(row => {
      row.addEventListener('click', () => { if (FarmPerms.can('finance', 'edit')) loadExpenseForEdit(row); });
    });
    document.querySelectorAll('#ownerRows tr.fin-owner-row').forEach(row => {
      row.addEventListener('click', () => { if (FarmPerms.can('finance', 'edit')) loadOwnerForEdit(row); });
    });

    [
      ['addAsset', 'deleteAssetBtn', '#assetRows tr.fin-asset-row'],
      ['addIncome', 'deleteIncomeBtn', '#incomeRows tr.fin-income-row'],
      ['addExpense', 'deleteExpenseBtn', '#expenseRows tr.fin-expense-row'],
      ['addOwner', 'deleteOwnerBtn', '#ownerRows tr.fin-owner-row']
    ].forEach(([addBtnId, deleteBtnId, rowSelector]) => {
      FarmPerms.applyForm('finance', { addBtnId, deleteBtnId, rowSelector });
    });
  }

  function initHealth() {
    let editingRemId = null;
    let editingVaccId = null;
    let editingHist = null;

    function reloadHealthWithToast(message) {
      sessionStorage.setItem('goatToast', message);
      location.href = '/Vaccine' + (window.location.search || '');
    }

    function applyVaccRuleLabel() {
      const type = document.getElementById('v-type')?.value;
      const label = document.getElementById('v-vlabel');
      const val = document.getElementById('v-val');
      if (!label || !val) return;
      label.textContent = type === 'Age' ? 'Days' : 'Months';
      val.placeholder = type === 'Age' ? '30' : '12';
    }

    document.getElementById('v-type')?.addEventListener('change', applyVaccRuleLabel);

    function setRemEditMode(editing) {
      document.getElementById('cancelRemBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteRemBtn').style.display = editing ? '' : 'none';
      document.getElementById('r-relative-wrap').style.display = editing ? 'none' : '';
      document.getElementById('r-unit-wrap').style.display = editing ? 'none' : '';
      document.getElementById('r-date-wrap').style.display = editing ? '' : 'none';
    }

    function setVaccEditMode(editing) {
      document.getElementById('cancelVaccBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteVaccBtn').style.display = editing ? '' : 'none';
    }

    function resetRemForm() {
      editingRemId = null;
      document.getElementById('remFormTitle').textContent = 'My reminders';
      document.getElementById('addReminder').textContent = '+ Add';
      document.getElementById('r-note').value = '';
      document.getElementById('r-scope').value = 'None';
      document.getElementById('r-num').value = '';
      document.getElementById('r-unit').value = '30';
      document.getElementById('r-date').value = '';
      document.querySelectorAll('#reminderRows tr.reminder-row.editing').forEach(r => r.classList.remove('editing'));
      setRemEditMode(false);
    }

    function resetVaccForm() {
      editingVaccId = null;
      document.getElementById('vaccFormTitle').textContent = 'Vaccine schedule';
      document.getElementById('addVacc').textContent = '+ Add';
      document.getElementById('v-name').value = '';
      document.getElementById('v-scope').value = 'All';
      document.getElementById('v-type').value = 'Age';
      document.getElementById('v-val').value = '';
      applyVaccRuleLabel();
      document.querySelectorAll('#vaccRows tr.vacc-row.editing').forEach(r => r.classList.remove('editing'));
      setVaccEditMode(false);
    }

    function resetHistForm() {
      editingHist = null;
      document.getElementById('histFormTitle').textContent = 'Recent vaccinations';
      document.getElementById('h-date').value = '';
      document.getElementById('h-vaccine').value = '';
      document.getElementById('h-count').value = '';
      document.getElementById('histForm').style.display = 'none';
      document.getElementById('histActions').style.display = 'none';
      document.querySelectorAll('#vaccLogRows tr.hist-row.editing').forEach(r => r.classList.remove('editing'));
    }

    function loadRemForEdit(row) {
      if (!FarmPerms.can('vaccines', 'edit')) return;
      editingRemId = +row.dataset.id;
      document.getElementById('remFormTitle').textContent = 'Edit reminder';
      document.getElementById('addReminder').textContent = 'Save';
      document.getElementById('r-note').value = row.dataset.title || '';
      document.getElementById('r-scope').value = row.dataset.scope || 'None';
      document.getElementById('r-date').value = row.dataset.date || '';
      document.querySelectorAll('#reminderRows tr.reminder-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setRemEditMode(true);
      if (!FarmPerms.can('vaccines', 'add')) FarmPerms.revealEditForm('addReminder');
      resetHistForm();
      resetVaccForm();
      document.getElementById('r-note').focus();
    }

    function loadVaccForEdit(row) {
      if (!FarmPerms.can('vaccines', 'edit')) return;
      editingVaccId = +row.dataset.id;
      document.getElementById('vaccFormTitle').textContent = 'Edit vaccine';
      document.getElementById('addVacc').textContent = 'Save';
      document.getElementById('v-name').value = row.dataset.name || '';
      document.getElementById('v-scope').value = row.dataset.scope || 'All';
      document.getElementById('v-type').value = row.dataset.ruleType || 'Age';
      document.getElementById('v-val').value = row.dataset.value || '';
      applyVaccRuleLabel();
      document.querySelectorAll('#vaccRows tr.vacc-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setVaccEditMode(true);
      if (!FarmPerms.can('vaccines', 'add')) FarmPerms.revealEditForm('addVacc');
      resetHistForm();
      resetRemForm();
      document.getElementById('v-name').focus();
    }

    function loadHistForEdit(row) {
      if (!FarmPerms.can('vaccines', 'edit')) return;
      editingHist = {
        vaccineId: +row.dataset.vaccineId,
        date: row.dataset.date || '',
        vaccineName: row.dataset.vaccineName || '',
        goatCount: row.dataset.goatCount || ''
      };
      document.getElementById('histFormTitle').textContent = 'Edit vaccination record';
      document.getElementById('h-date').value = editingHist.date;
      document.getElementById('h-vaccine').value = editingHist.vaccineName;
      document.getElementById('h-count').value = editingHist.goatCount;
      document.getElementById('histForm').style.display = '';
      document.getElementById('histActions').style.display = '';
      document.querySelectorAll('#vaccLogRows tr.hist-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      resetRemForm();
      resetVaccForm();
      document.getElementById('h-date').focus();
    }

    document.getElementById('cancelRemBtn')?.addEventListener('click', resetRemForm);
    document.getElementById('cancelVaccBtn')?.addEventListener('click', resetVaccForm);
    document.getElementById('cancelHistBtn')?.addEventListener('click', resetHistForm);

    document.getElementById('deleteRemBtn')?.addEventListener('click', async () => {
      if (!editingRemId || !FarmPerms.can('vaccines', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      await api('/Reminder/Delete?id=' + editingRemId, { method: 'DELETE' });
      reloadHealthWithToast('Reminder deleted successfully');
    });

    document.getElementById('deleteVaccBtn')?.addEventListener('click', async () => {
      if (!editingVaccId || !FarmPerms.can('vaccines', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      await api('/Vaccine/Delete?id=' + editingVaccId, { method: 'DELETE' });
      reloadHealthWithToast('Vaccine deleted successfully');
    });

    document.getElementById('deleteHistBtn')?.addEventListener('click', async () => {
      if (!editingHist || !FarmPerms.can('vaccines', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this entry?');
      if (!confirmed) return;
      await api('/Vaccine/DeleteHistoryBatch?vaccineId=' + editingHist.vaccineId + '&date=' + editingHist.date, { method: 'DELETE' });
      reloadHealthWithToast('Vaccination record deleted successfully');
    });

    document.getElementById('addReminder')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('vaccines', !!editingRemId)) return;
      const title = document.getElementById('r-note').value.trim();
      if (!title) { await showModal('Enter a reminder'); return; }
      if (editingRemId) {
        const date = document.getElementById('r-date').value;
        if (!date) { await showModal('Pick a date'); return; }
        await api('/Reminder/Update?id=' + editingRemId, {
          method: 'PUT',
          body: JSON.stringify({ title, scope: document.getElementById('r-scope').value, reminderDate: date })
        });
        reloadHealthWithToast('Reminder updated successfully');
      } else {
        const n = +document.getElementById('r-num').value || 0;
        if (!n) { await showModal('Enter how many months/weeks/days'); return; }
        await api('/Reminder/Create', {
          method: 'POST',
          body: JSON.stringify({
            title,
            scope: document.getElementById('r-scope').value,
            number: n,
            unitDays: +document.getElementById('r-unit').value
          })
        });
        reloadHealthWithToast('Reminder added successfully');
      }
    });

    document.getElementById('addVacc')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('vaccines', !!editingVaccId)) return;
      const name = document.getElementById('v-name').value.trim();
      const val = +document.getElementById('v-val').value || 0;
      if (!name || !val) { await showModal('Enter a vaccine name and a number'); return; }
      const payload = {
        name,
        scope: document.getElementById('v-scope').value,
        ruleType: document.getElementById('v-type').value,
        value: val
      };
      if (editingVaccId) {
        await api('/Vaccine/Update?id=' + editingVaccId, { method: 'PUT', body: JSON.stringify(payload) });
        reloadHealthWithToast('Vaccine updated successfully');
      } else {
        await api('/Vaccine/Add', { method: 'POST', body: JSON.stringify(payload) });
        reloadHealthWithToast('Vaccine added successfully');
      }
    });

    document.getElementById('saveHistBtn')?.addEventListener('click', async () => {
      if (!editingHist || !FarmPerms.can('vaccines', 'edit')) return;
      const newDate = document.getElementById('h-date').value;
      if (!newDate) { await showModal('Pick a date'); return; }
      await api('/Vaccine/UpdateHistoryBatch', {
        method: 'PUT',
        body: JSON.stringify({
          vaccineId: editingHist.vaccineId,
          date: editingHist.date,
          newDate
        })
      });
      reloadHealthWithToast('Vaccination record updated successfully');
    });

    document.getElementById('remindWin')?.addEventListener('change', async e => {
      if (!FarmPerms.can('vaccines', 'edit')) return;
      await api('/Vaccine/SetReminderWindow', {
        method: 'POST',
        body: JSON.stringify({ days: +e.target.value || 30 })
      });
      reloadHealthWithToast('Reminder window updated');
    });

    document.querySelectorAll('[data-dovacc]').forEach(b => b.onclick = async () => {
      if (!FarmPerms.can('vaccines', 'edit')) return;
      await api('/Vaccine/MarkDone?vaccineId=' + b.dataset.dovacc, { method: 'POST' });
      reloadHealthWithToast('Vaccination marked done');
    });

    document.querySelectorAll('#reminderRows tr.reminder-row').forEach(row => {
      row.addEventListener('click', () => loadRemForEdit(row));
    });
    document.querySelectorAll('#vaccRows tr.vacc-row').forEach(row => {
      row.addEventListener('click', () => loadVaccForEdit(row));
    });
    document.querySelectorAll('#vaccLogRows tr.hist-row').forEach(row => {
      row.addEventListener('click', () => loadHistForEdit(row));
    });

    [
      ['addReminder', 'deleteRemBtn', '#reminderRows tr.reminder-row'],
      ['addVacc', 'deleteVaccBtn', '#vaccRows tr.vacc-row']
    ].forEach(([addBtnId, deleteBtnId, rowSelector]) => {
      FarmPerms.applyForm('vaccines', { addBtnId, deleteBtnId, rowSelector });
    });
    if (!FarmPerms.can('vaccines', 'edit')) {
      FarmPerms.readonlyInputs('#remindWin');
      document.querySelectorAll('[data-dovacc]').forEach(b => b.classList.add('perm-hidden'));
      FarmPerms.hide('histForm');
      FarmPerms.hide('histActions');
      FarmPerms.hide('deleteHistBtn');
      FarmPerms.hide('saveHistBtn');
      FarmPerms.hide('cancelHistBtn');
    } else if (!FarmPerms.can('vaccines', 'delete')) {
      FarmPerms.hide('deleteHistBtn');
    }
  }

  function initSearch(opts = {}) {
    const form = document.getElementById('search-form');
    const panelEl = document.getElementById('search-panel');
    const input = document.getElementById('search-tag');
    const btn = document.getElementById('search-btn');
    const statusEl = document.getElementById('search-status');
    const resultsEl = document.getElementById('search-results');
    if (!input || !resultsEl) return;

    function setStatus(msg, isError = false) {
      if (!statusEl) return;
      statusEl.textContent = msg || '';
      statusEl.classList.toggle('error', isError);
    }

    function setLoading(loading) {
      panelEl?.classList.toggle('is-loading', loading);
      if (btn) btn.disabled = loading;
      if (input) input.disabled = loading;
      if (loading) {
        setStatus('');
        resultsEl.innerHTML = `
          <div class="search-results-loading" role="status" aria-live="polite">
            <div class="search-loading-spinner" aria-hidden="true"></div>
            <span>Loading goat profile…</span>
          </div>`;
      }
    }

    function waitForPaint() {
      return new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    }

    function esc(s) {
      return String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
    }

    function renderProfile(data) {
      const g = data.goat || {};
      const nameLine = g.name ? `<div class="name">${esc(g.name)}</div>` : '';
      const groupLine = g.groupName ? esc(g.groupName) : '—';
      const gender = g.gender === 'Male' ? 'Male' : 'Female';
      const source = g.source === 'Born' ? 'Born on farm' : 'Bought';
      const herdLink = FarmPerms.can('herd', 'edit')
        ? `<a class="btn btn-ghost" href="/Goat?editId=${encodeURIComponent(g.id || '')}">Edit in Herd</a>`
        : '';

      const history = data.vaccinationHistory || [];
      const historyHtml = history.length
        ? `<table class="tbl"><thead><tr><th>Date</th><th>Vaccine</th><th class="hide-sm">Scope</th></tr></thead><tbody>
          ${history.map(h => `<tr><td>${esc(h.dateDisplay)}</td><td>${esc(h.vaccineName)}</td><td class="hide-sm">${esc(h.scopeDisplay)}</td></tr>`).join('')}
          </tbody></table>`
        : '<div class="search-empty">No vaccinations recorded yet for this goat.</div>';

      const schedule = data.vaccineSchedule || [];
      const scheduleHtml = schedule.length
        ? `<table class="tbl"><thead><tr><th>Vaccine</th><th>Rule</th><th>Status</th><th class="hide-sm">Last</th><th class="hide-sm">Next</th></tr></thead><tbody>
          ${schedule.map(v => `<tr><td>${esc(v.vaccineName)}</td><td class="hide-sm">${esc(v.ruleDisplay)}</td>
            <td><span class="chip ${esc(v.statusCss)}">${esc(v.status)}</span></td>
            <td class="hide-sm">${esc(v.lastDate || '—')}</td><td class="hide-sm">${esc(v.dueDate || '—')}</td></tr>`).join('')}
          </tbody></table>`
        : '<div class="search-empty">No vaccines apply to this goat\'s status.</div>';

      const plan = data.feedPlan;
      let feedHtml = '<div class="search-empty">No feed plan set for this status.</div>';
      if (plan) {
        const rationRows = (plan.items || []).filter(i => i.gramsPerDay > 0).map(i =>
          `<tr><td>${esc(i.displayName)}</td><td class="num-cell">${i.gramsPerDay} g</td><td class="num-cell hide-sm">${rs(i.dailyCost)}</td></tr>`
        ).join('');
        feedHtml = `<div class="note" style="margin-bottom:12px">Based on the <b>${esc(plan.statusDisplay)}</b> feed plan (farm-level ration per goat).</div>
          <table class="tbl"><thead><tr><th>Feed</th><th>Daily</th><th class="hide-sm">Cost/day</th></tr></thead><tbody>
          ${rationRows || '<tr><td colspan="3" class="search-empty">No rations configured.</td></tr>'}
          </tbody></table>
          <div style="margin-top:12px;font-size:14px">
            <span><b>Daily feed:</b> ${rs(plan.dailyFeedCost)}</span> ·
            <span><b>Medicine/mo:</b> ${rs(plan.medicineCostPerGoatPerMonth)}</span> ·
            <span><b>Est. monthly:</b> ${rs(plan.monthlyTotalCost)}</span>
          </div>`;
      }

      const reminders = data.reminders || [];
      const reminderHtml = reminders.length
        ? `<ul style="margin:0;padding-left:18px;line-height:1.7">
          ${reminders.map(r => `<li><span style="color:${esc(r.whenColor)};font-weight:600">${esc(r.whenDisplay)}</span> — ${esc(r.title)}${r.scopeDisplay ? ` <span class="breed">(${esc(r.scopeDisplay)})</span>` : ''} <span class="breed">· ${esc(r.dateDisplay)}</span></li>`).join('')}
          </ul>`
        : '<div class="search-empty">No reminders for this goat.</div>';

      resultsEl.innerHTML = `
        <div class="panel">
          <div class="panel-body">
            <div class="search-hero">
              <div class="search-hero-main">
                <h2><span class="tag">${esc(g.tag)}</span></h2>
                ${nameLine}
                <span class="chip ${esc(g.statusCssClass)}">${esc(g.statusDisplay)}</span>
                <div class="search-hero-meta">
                  <div class="search-meta-item">Breed<b>${esc(g.breed)}</b></div>
                  <div class="search-meta-item">Gender<b>${esc(gender)}</b></div>
                  <div class="search-meta-item">Age<b>${esc(g.ageLabel)}</b></div>
                  <div class="search-meta-item">Group<b>${groupLine}</b></div>
                  <div class="search-meta-item">Source<b>${esc(source)}</b></div>
                  <div class="search-meta-item">Price<b>${esc(g.priceDisplay)}</b></div>
                  <div class="search-meta-item">Date<b>${esc(g.eventDateDisplay)}</b></div>
                </div>
              </div>
              ${herdLink}
            </div>
          </div>
        </div>
        <div class="panel"><div class="panel-head"><h2>Vaccine schedule</h2></div><div class="panel-body">${scheduleHtml}</div></div>
        <div class="panel"><div class="panel-head"><h2>Vaccination history</h2></div><div class="panel-body">${historyHtml}</div></div>
        <div class="panel"><div class="panel-head"><h2>Feed plan</h2></div><div class="panel-body">${feedHtml}</div></div>
        <div class="panel"><div class="panel-head"><h2>Reminders</h2></div><div class="panel-body">${reminderHtml}</div></div>
        <div class="note">Milk production is tracked at farm level on the Milk tab, not per individual goat.</div>`;
    }

    async function runSearch() {
      const tag = input.value.trim();
      if (!tag) {
        setStatus('Enter or scan a tag / RFID ID.', true);
        resultsEl.innerHTML = '';
        input.focus();
        return;
      }

      setLoading(true);
      await waitForPaint();

      try {
        const url = '/Search/Lookup?tag=' + encodeURIComponent(tag);
        const res = await fetch(url, {
          headers: { Accept: 'application/json' },
          credentials: 'same-origin'
        });

        if (res.status === 404 || res.status === 400) {
          const err = await res.json().catch(() => ({}));
          setLoading(false);
          setStatus(err.error || 'Goat not found.', true);
          resultsEl.innerHTML = '';
          return;
        }

        if (res.status === 403) {
          setLoading(false);
          setStatus('You do not have permission to search.', true);
          resultsEl.innerHTML = '';
          return;
        }

        if (!res.ok) {
          setLoading(false);
          setStatus('Something went wrong. Please try again.', true);
          resultsEl.innerHTML = '';
          return;
        }

        const data = await res.json();
        setLoading(false);
        setStatus('');
        renderProfile(data);
        if (history.replaceState) {
          const next = '/Search?tag=' + encodeURIComponent(tag);
          if (location.pathname + location.search !== next) history.replaceState(null, '', next);
        }
        input.select();
      } catch {
        setLoading(false);
        setStatus('Could not reach the server. Try again or press Search for a full page reload.', true);
        resultsEl.innerHTML = '';
      }
    }

    form?.addEventListener('submit', e => {
      e.preventDefault();
      runSearch();
    });
    btn?.addEventListener('click', e => {
      e.preventDefault();
      runSearch();
    });
    input.addEventListener('keydown', e => {
      if (e.key === 'Enter') { e.preventDefault(); runSearch(); }
    });

    input.focus();
    if (opts.initialTag && String(opts.initialTag).trim() && !opts.hasServerProfile) runSearch();
  }

  function initSettings() {
    let editingUserId = null;
    const rolePermissions = window.RolePermissions || {};

    function reloadSettingsWithToast(message) {
      sessionStorage.setItem('goatToast', message);
      location.href = '/Settings';
    }

    function setUserPermPanelEnabled(customEnabled) {
      const wrap = document.getElementById('userPermTableWrap');
      if (wrap) wrap.classList.toggle('disabled', !customEnabled);
    }

    function applyUserPermCheckboxes(permissions) {
      const role = document.getElementById('u-role')?.value || '';
      const isAdmin = role === 'Admin';
      document.querySelectorAll('.user-perm-cb').forEach(cb => {
        const tab = cb.dataset.tab;
        const action = cb.dataset.action;
        const perm = permissions?.[tab] || {};
        if (cb.dataset.settingsTab === 'true' && isAdmin) {
          cb.checked = true;
          cb.disabled = true;
          cb.dataset.permLocked = 'true';
          return;
        }
        delete cb.dataset.permLocked;
        cb.disabled = false;
        cb.checked = !!perm[action];
      });
      document.querySelectorAll('.user-perm-cb[data-action="view"]').forEach(cb => {
        const tab = cb.dataset.tab;
        const enabled = cb.checked;
        document.querySelectorAll(`.user-perm-cb[data-tab="${tab}"]`).forEach(other => {
          if (other.dataset.action === 'view' || other.dataset.permLocked === 'true') return;
          other.disabled = !enabled;
          if (!enabled) other.checked = false;
        });
      });
    }

    function collectUserPermissions() {
      const permissions = {};
      document.querySelectorAll('.user-perm-cb').forEach(cb => {
        const tab = cb.dataset.tab;
        const action = cb.dataset.action;
        if (!permissions[tab]) permissions[tab] = { view: false, add: false, edit: false, delete: false };
        if (cb.checked) permissions[tab][action] = true;
      });
      Object.values(permissions).forEach(p => {
        if (p.add || p.edit || p.delete) p.view = true;
      });
      return permissions;
    }

    function copyRolePermissionsToUser() {
      const role = document.getElementById('u-role')?.value || 'Staff';
      applyUserPermCheckboxes(rolePermissions[role] || {});
    }

    async function loadUserPermissions(userId) {
      const data = await api('/Settings/GetUserPermissions?id=' + encodeURIComponent(userId));
      document.getElementById('u-use-role-perms').checked = data.usesRolePermissions;
      setUserPermPanelEnabled(!data.usesRolePermissions);
      applyUserPermCheckboxes(data.permissions || {});
    }

    async function saveUserPermissions(userId) {
      const usesRolePermissions = document.getElementById('u-use-role-perms')?.checked ?? true;
      await api('/Settings/SaveUserPermissions?id=' + encodeURIComponent(userId), {
        method: 'PUT',
        body: JSON.stringify({
          usesRolePermissions,
          permissions: usesRolePermissions ? {} : collectUserPermissions()
        })
      });
    }

    function setUserEditMode(editing) {
      document.getElementById('cancelUserBtn').style.display = editing ? '' : 'none';
      document.getElementById('deleteUserBtn').style.display = editing ? '' : 'none';
      document.getElementById('u-edit-extra').style.display = editing ? '' : 'none';
      document.getElementById('u-password-wrap').style.display = editing ? 'none' : '';
      document.getElementById('userPermPanel').style.display = editing ? '' : 'none';
      document.getElementById('u-email').disabled = editing;
    }

    function resetUserForm() {
      editingUserId = null;
      document.getElementById('userFormTitle').textContent = 'Users';
      document.getElementById('addUser').textContent = '+ Add user';
      document.getElementById('u-name').value = '';
      document.getElementById('u-email').value = '';
      document.getElementById('u-email').disabled = false;
      document.getElementById('u-password').value = '';
      document.getElementById('u-role').selectedIndex = 0;
      document.getElementById('u-locked').checked = false;
      document.getElementById('u-reset-password').value = '';
      document.getElementById('u-use-role-perms').checked = true;
      setUserPermPanelEnabled(false);
      document.querySelectorAll('#userRows tr.user-row.editing').forEach(r => r.classList.remove('editing'));
      setUserEditMode(false);
    }

    async function loadUserForEdit(row) {
      if (!FarmPerms.can('settings', 'edit')) return;
      editingUserId = row.dataset.id;
      document.getElementById('userFormTitle').textContent = 'Edit user';
      document.getElementById('addUser').textContent = 'Save';
      document.getElementById('u-name').value = row.dataset.name || '';
      document.getElementById('u-email').value = row.dataset.email || '';
      document.getElementById('u-role').value = row.dataset.role || 'Staff';
      document.getElementById('u-locked').checked = row.dataset.locked === 'true';
      document.getElementById('u-password').value = '';
      document.getElementById('u-reset-password').value = '';
      document.querySelectorAll('#userRows tr.user-row.editing').forEach(r => r.classList.remove('editing'));
      row.classList.add('editing');
      setUserEditMode(true);
      if (!FarmPerms.can('settings', 'add')) FarmPerms.revealEditForm('addUser');
      await loadUserPermissions(editingUserId);
      document.getElementById('u-name').focus();
    }

    document.getElementById('cancelUserBtn')?.addEventListener('click', resetUserForm);

    document.getElementById('deleteUserBtn')?.addEventListener('click', async () => {
      if (!editingUserId || !FarmPerms.can('settings', 'delete')) return;
      const confirmed = await showConfirm('Do you want to delete this user?');
      if (!confirmed) return;
      await api('/Settings/DeleteUser?id=' + encodeURIComponent(editingUserId), { method: 'DELETE' });
      reloadSettingsWithToast('User deleted successfully');
    });

    document.getElementById('resetPasswordBtn')?.addEventListener('click', async () => {
      if (!editingUserId || !FarmPerms.can('settings', 'edit')) return;
      const newPassword = document.getElementById('u-reset-password').value;
      if (!newPassword) { await showModal('Enter a new password'); return; }
      await api('/Settings/ResetPassword?id=' + encodeURIComponent(editingUserId), {
        method: 'POST',
        body: JSON.stringify({ newPassword })
      });
      document.getElementById('u-reset-password').value = '';
      reloadSettingsWithToast('Password reset successfully');
    });

    document.getElementById('addUser')?.addEventListener('click', async () => {
      if (!FarmPerms.guardAddEdit('settings', !!editingUserId)) return;
      const fullName = document.getElementById('u-name').value.trim();
      const role = document.getElementById('u-role').value;
      if (!fullName) { await showModal('Enter full name'); return; }
      if (editingUserId) {
        await api('/Settings/UpdateUser?id=' + encodeURIComponent(editingUserId), {
          method: 'PUT',
          body: JSON.stringify({
            fullName,
            role,
            isLocked: document.getElementById('u-locked').checked
          })
        });
        await saveUserPermissions(editingUserId);
        reloadSettingsWithToast('User updated successfully');
      } else {
        const email = document.getElementById('u-email').value.trim();
        const password = document.getElementById('u-password').value;
        if (!email) { await showModal('Enter email'); return; }
        if (!password) { await showModal('Enter password'); return; }
        await api('/Settings/CreateUser', {
          method: 'POST',
          body: JSON.stringify({ fullName, email, password, role })
        });
        reloadSettingsWithToast('User added successfully');
      }
    });

    document.getElementById('savePermissions')?.addEventListener('click', async () => {
      if (!FarmPerms.can('settings', 'edit')) return;
      const permissions = {};
      document.querySelectorAll('.perm-cb').forEach(cb => {
        const role = cb.dataset.role;
        const tab = cb.dataset.tab;
        const action = cb.dataset.action;
        if (!permissions[role]) permissions[role] = {};
        if (!permissions[role][tab]) permissions[role][tab] = { view: false, add: false, edit: false, delete: false };
        if (cb.checked) permissions[role][tab][action] = true;
      });
      Object.values(permissions).forEach(tabs => {
        Object.values(tabs).forEach(p => {
          if (p.add || p.edit || p.delete) p.view = true;
        });
      });
      await api('/Settings/SaveRolePermissions', {
        method: 'PUT',
        body: JSON.stringify({ permissions })
      });
      reloadSettingsWithToast('Permissions saved successfully');
    });

    document.getElementById('savePolicy')?.addEventListener('click', async () => {
      if (!FarmPerms.can('settings', 'edit')) return;
      await api('/Settings/SavePasswordPolicy', {
        method: 'PUT',
        body: JSON.stringify({
          requiredLength: +document.getElementById('pp-length').value || 8,
          requireDigit: document.getElementById('pp-digit').checked,
          requireLowercase: document.getElementById('pp-lower').checked,
          requireUppercase: document.getElementById('pp-upper').checked,
          requireNonAlphanumeric: document.getElementById('pp-symbol').checked
        })
      });
      reloadSettingsWithToast('Password policy saved successfully');
    });

    document.getElementById('copyRolePermsBtn')?.addEventListener('click', () => {
      document.getElementById('u-use-role-perms').checked = false;
      setUserPermPanelEnabled(true);
      copyRolePermissionsToUser();
    });

    document.getElementById('u-use-role-perms')?.addEventListener('change', e => {
      setUserPermPanelEnabled(!e.target.checked);
      if (e.target.checked) copyRolePermissionsToUser();
    });

    document.getElementById('u-role')?.addEventListener('change', () => {
      if (!document.getElementById('u-use-role-perms')?.checked) copyRolePermissionsToUser();
      else applyUserPermCheckboxes(rolePermissions[document.getElementById('u-role')?.value || 'Staff'] || {});
    });

    document.querySelectorAll('.user-perm-cb[data-action="view"]').forEach(cb => {
      cb.addEventListener('change', () => {
        const tab = cb.dataset.tab;
        document.querySelectorAll(`.user-perm-cb[data-tab="${tab}"]`).forEach(other => {
          if (other.dataset.action === 'view' || other.dataset.permLocked === 'true') return;
          other.disabled = !cb.checked;
          if (!cb.checked) other.checked = false;
        });
      });
    });

    document.querySelectorAll('#userRows tr.user-row').forEach(row => {
      row.addEventListener('click', () => loadUserForEdit(row));
    });

    FarmPerms.applyForm('settings', {
      addBtnId: 'addUser',
      deleteBtnId: 'deleteUserBtn',
      rowSelector: '#userRows tr.user-row'
    });
    if (!FarmPerms.can('settings', 'edit')) {
      FarmPerms.readonlyInputs('#permissionsPanel input:not([disabled]), #policyPanel input');
      FarmPerms.hide('savePermissions');
      FarmPerms.hide('savePolicy');
      FarmPerms.hide('resetPasswordBtn');
    }
    if (!FarmPerms.can('settings', 'add') && !FarmPerms.can('settings', 'edit')) {
      FarmPerms.hide('userFormGrid');
      FarmPerms.hide('userFormActions');
    }

    document.querySelectorAll('.perm-cb[data-action="view"]').forEach(cb => {
      const syncRow = () => {
        const role = cb.dataset.role;
        const tab = cb.dataset.tab;
        document.querySelectorAll(`.perm-cb[data-role="${role}"][data-tab="${tab}"]`).forEach(other => {
          if (other.dataset.action === 'view' || other.dataset.permLocked === 'true') return;
          other.disabled = !cb.checked;
          if (!cb.checked) other.checked = false;
        });
      };
      cb.addEventListener('change', syncRow);
      syncRow();
    });
  }

  return { initHerd, initFeed, initMilk, initFinance, initHealth, initSearch, initSettings, showModal, showConfirm, showToast };
})();
