(() => {
  const monthEl = document.getElementById('tradeMonth');
  const reload = () => { if (monthEl?.value) location.search = '?month=' + encodeURIComponent(monthEl.value); };
  monthEl?.addEventListener('change', reload);

  document.querySelectorAll('#trade-tabs [data-trade-tab]').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#trade-tabs .tab').forEach(t => t.classList.remove('active'));
      btn.classList.add('active');
      const tab = btn.dataset.tradeTab;
      document.querySelectorAll('[data-trade-pane]').forEach(p => { p.hidden = p.dataset.tradePane !== tab; });
    });
  });

  const post = async (url, body) => {
    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    if (!res.ok) {
      const j = await res.json().catch(() => ({}));
      alert(j.error || 'Save failed.');
      throw new Error('fail');
    }
    return res;
  };

  document.getElementById('tb-save')?.addEventListener('click', async () => {
    const date = document.getElementById('tb-date').value;
    const qty = +document.getElementById('tb-qty').value || 0;
    const rate = +document.getElementById('tb-rate').value || 0;
    await post('/Trading/SaveEntry', {
      type: 'Buy', date, itemId: +document.getElementById('tb-item').value,
      storeId: +document.getElementById('tb-store').value, accountId: +document.getElementById('tb-account').value,
      qty, rate, amount: qty * rate, notes: document.getElementById('tb-note').value
    });
    reload();
  });

  document.getElementById('ts-save')?.addEventListener('click', async () => {
    const date = document.getElementById('ts-date').value;
    const qty = +document.getElementById('ts-qty').value || 0;
    const rate = +document.getElementById('ts-rate').value || 0;
    await post('/Trading/SaveEntry', {
      type: 'Sell', date, itemId: +document.getElementById('ts-item').value,
      storeId: +document.getElementById('ts-store').value, accountId: +document.getElementById('ts-account').value,
      qty, rate, amount: qty * rate
    });
    reload();
  });

  document.getElementById('te-save')?.addEventListener('click', async () => {
    try {
      await post('/Trading/SaveEntry', {
        type: 'Expense', date: document.getElementById('te-date').value,
        categoryId: +document.getElementById('te-cat').value,
        accountId: +document.getElementById('te-account').value,
        amount: +document.getElementById('te-amt').value || 0, qty: 0, rate: 0
      });
      reload();
    } catch { /* alerted */ }
  });

  document.getElementById('tm-save')?.addEventListener('click', async () => {
    await post('/Trading/SaveMaster', { kind: 'item', name: document.getElementById('tm-name').value, unit: document.getElementById('tm-unit').value });
    reload();
  });

  document.getElementById('tt-save')?.addEventListener('click', async () => {
    const qty = +document.getElementById('tt-qty').value || 0;
    await post('/Trading/SaveEntry', {
      type: 'Transfer', date: document.getElementById('tt-date').value,
      itemId: +document.getElementById('tt-item').value,
      storeId: +document.getElementById('tt-from').value,
      toStoreId: +document.getElementById('tt-to').value,
      qty, rate: 0, amount: 0
    });
    reload();
  });

  document.getElementById('tp-save')?.addEventListener('click', async () => {
    await post('/Trading/SaveMaster', { kind: 'party', name: document.getElementById('tp-name').value });
    reload();
  });

  document.querySelectorAll('.trade-del').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Cancel this entry?')) return;
      const res = await fetch('/Trading/DeleteEntry?id=' + btn.dataset.id, { method: 'DELETE' });
      if (res.status === 202) {
        const j = await res.json().catch(() => ({}));
        alert(j.message || 'Submitted for approval.');
        return;
      }
      reload();
    });
  });
})();
