(() => {
  const monthEl = document.getElementById('finMonth');
  const reloadLedger = async () => {
    const month = monthEl?.value || '';
    const res = await fetch('/Finance/LedgerData?month=' + encodeURIComponent(month));
    if (!res.ok) return;
    const data = await res.json();
    window.__ledgerData = data.ledger;
    window.__ledgerStatements = data.statements;
  };

  document.querySelectorAll('#ledger-tabs [data-ledger-tab]').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#ledger-tabs .tab').forEach(t => t.classList.remove('active'));
      btn.classList.add('active');
      const tab = btn.dataset.ledgerTab;
      document.querySelectorAll('[data-ledger-pane]').forEach(p => { p.hidden = p.dataset.ledgerPane !== tab; });
    });
  });

  const post = async (url, body) => {
    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    if (res.status === 202) {
      const j = await res.json().catch(() => ({}));
      alert(j.message || 'Submitted for approval.');
      return null;
    }
    if (!res.ok) throw new Error('Save failed');
    return res.json().catch(() => ({}));
  };

  document.getElementById('inv-save')?.addEventListener('click', async () => {
    const cid = document.getElementById('inv-customer')?.value;
    if (!cid) { alert('Add a customer first.'); return; }
    await post('/Finance/SaveCustomerInvoice', {
      customerId: +cid,
      date: document.getElementById('inv-date').value,
      dueDate: document.getElementById('inv-due')?.value || null,
      description: document.getElementById('inv-desc').value,
      amount: +document.getElementById('inv-amt').value || 0,
      paidAmount: +document.getElementById('inv-paid')?.value || 0
    });
    location.reload();
  });

  document.getElementById('pay-save')?.addEventListener('click', async () => {
    await post('/Finance/SaveSupplierPayable', {
      supplierName: document.getElementById('pay-supplier').value,
      date: document.getElementById('pay-date').value,
      dueDate: document.getElementById('pay-due')?.value || null,
      description: document.getElementById('pay-desc').value,
      amount: +document.getElementById('pay-amt').value || 0,
      paidAmount: +document.getElementById('pay-paid')?.value || 0
    });
    location.reload();
  });

  document.getElementById('bud-save')?.addEventListener('click', async () => {
    const parts = (monthEl?.value || '2026-01').split('-');
    await post('/Finance/SaveBudgetLine', {
      year: +parts[0],
      month: +parts[1],
      category: document.getElementById('bud-cat').value,
      plannedAmount: +document.getElementById('bud-amt').value || 0
    });
    location.reload();
  });

  document.getElementById('part-save')?.addEventListener('click', async () => {
    await post('/Finance/SaveProfitPartner', {
      name: document.getElementById('part-name').value,
      sharePercent: +document.getElementById('part-pct').value || 0
    });
    location.reload();
  });

  document.getElementById('tx-save')?.addEventListener('click', async () => {
    await post('/Finance/SaveLedgerTransaction', {
      date: document.getElementById('tx-date').value,
      kind: document.getElementById('tx-kind').value,
      accountId: +document.getElementById('tx-account').value,
      amount: +document.getElementById('tx-amt').value || 0,
      category: document.getElementById('tx-cat').value,
      notes: document.getElementById('tx-note').value
    });
    location.reload();
  });

  document.getElementById('cust-save')?.addEventListener('click', async () => {
    await post('/Finance/SaveBusinessCustomer', {
      name: document.getElementById('cust-name').value,
      phone: document.getElementById('cust-phone').value || null
    });
    location.reload();
  });
})();
