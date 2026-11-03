(() => {
  const monthEl = document.getElementById('finMonth');
  const reload = () => {
    const tab = document.querySelector('#finance-tabs .tab.active')?.dataset.fintab || 'overview';
    const url = new URL(location.href);
    if (monthEl?.value) url.searchParams.set('month', monthEl.value);
    url.searchParams.set('tab', tab);
    location.href = url.pathname + url.search;
  };

  const post = async (url, body, method = 'POST') => {
    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
      body: body == null ? undefined : JSON.stringify(body)
    });
    if (res.status === 202) { alert('Submitted for approval.'); return null; }
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Could not save that.');
    }
    return res.json().catch(() => ({}));
  };

  const ymd = d => {
    const z = n => String(n).padStart(2, '0');
    return d.getFullYear() + '-' + z(d.getMonth() + 1) + '-' + z(d.getDate());
  };

  const rs = n => 'Rs ' + Math.round(+n || 0).toLocaleString('en-US');

  document.querySelectorAll('#finance-tabs [data-fintab]').forEach(btn => {
    btn.addEventListener('click', () => {
      const name = btn.dataset.fintab;
      document.querySelectorAll('#finance-tabs .tab').forEach(t => t.classList.toggle('active', t.dataset.fintab === name));
      document.querySelectorAll('#view-finance .finview').forEach(p => {
        p.style.display = p.id === 'fin-' + name ? '' : 'none';
      });
      const url = new URL(location.href);
      url.searchParams.set('tab', name);
      history.replaceState(null, '', url);
    });
  });

  document.getElementById('ar-payment-status')?.addEventListener('change', () => {
    const paid = document.getElementById('ar-payment-status').value === 'paid';
    document.getElementById('ar-account-wrap').style.display = paid ? '' : 'none';
  });

  document.getElementById('ar-customer')?.addEventListener('change', async () => {
    const sel = document.getElementById('ar-customer');
    if (sel.value !== '__add') return;
    const name = prompt('Customer name');
    sel.value = '';
    if (!name?.trim()) return;
    try {
      await post('/Finance/SaveBusinessCustomer', { name: name.trim() });
      reload();
    } catch (e) { alert(e.message); }
  });

  const chooseAccount = () => {
    const opts = [...document.querySelectorAll('#bank-tx-account option')].filter(o => o.value);
    if (!opts.length) { alert('Add a Cash or Bank account first.'); return 0; }
    const n = +prompt('Choose account:\n' + opts.map((o, i) => (i + 1) + '. ' + o.textContent).join('\n'), '1');
    if (!(n >= 1 && n <= opts.length)) return 0;
    return +opts[n - 1].value;
  };

  document.getElementById('addCustomerInvoice')?.addEventListener('click', async () => {
    const customerId = +document.getElementById('ar-customer').value || 0;
    const amount = +document.getElementById('ar-amount').value || 0;
    const date = document.getElementById('ar-date').value;
    if (!customerId) { alert('Choose a customer.'); return; }
    if (!amount || !date) { alert('Enter the invoice date and total.'); return; }
    const paid = document.getElementById('ar-payment-status').value === 'paid';
    const accountId = +document.getElementById('ar-account').value || 0;
    if (paid && !accountId) { alert('Choose the Cash/Bank account that received the money.'); return; }
    const type = document.getElementById('ar-type').value;
    const note = document.getElementById('ar-note').value.trim();
    try {
      await post('/Finance/SaveCustomerInvoice', {
        customerId,
        date,
        dueDate: document.getElementById('ar-due').value || null,
        description: note ? type + ' — ' + note : type,
        amount,
        paidAmount: paid ? amount : 0,
        accountId: paid ? accountId : null
      });
      reload();
    } catch (e) { alert(e.message); }
  });

  document.getElementById('addPayable')?.addEventListener('click', async () => {
    const supplierName = document.getElementById('ap-supplier').value.trim();
    const amount = +document.getElementById('ap-amount').value || 0;
    const date = document.getElementById('ap-date').value;
    const type = document.getElementById('ap-type').value;
    const note = document.getElementById('ap-note').value.trim();
    const paid = +document.getElementById('ap-paid-now').value || 0;
    const accountId = +document.getElementById('ap-account').value || 0;
    if (!supplierName || !amount || !date) { alert('Enter the supplier, date and amount.'); return; }
    if (paid > 0 && !accountId && !document.getElementById('ap-post-expense').checked) {
      alert('Choose the account the payment came from.');
      return;
    }
    try {
      const postExpense = document.getElementById('ap-post-expense').checked;
      await post('/Finance/SaveSupplierPayable', {
        supplierName,
        date,
        dueDate: document.getElementById('ap-due').value || null,
        description: note ? type + ' — ' + note : type,
        amount,
        paidAmount: paid,
        accountId: postExpense ? null : (accountId || null)
      });
      if (postExpense) {
        await post('/Finance/AddExpense', {
          type,
          date,
          amount,
          comment: supplierName + (note ? ' — ' + note : ''),
          accountId: accountId || null
        });
      }
      reload();
    } catch (e) { alert(e.message); }
  });

  document.getElementById('addLoan')?.addEventListener('click', async () => {
    const name = document.getElementById('ln-name').value.trim();
    const amount = +document.getElementById('ln-amt').value || 0;
    const note = document.getElementById('ln-note').value.trim();
    if (!name || !amount) { alert('Enter who the money is owed to, and the amount.'); return; }
    try {
      await post('/Finance/SaveSupplierPayable', {
        supplierName: name,
        date: new Date().toISOString().slice(0, 10),
        description: note ? 'Loan — ' + note : 'Loan',
        amount,
        paidAmount: 0
      });
      reload();
    } catch (e) { alert(e.message); }
  });

  document.querySelectorAll('[data-receive-invoice]').forEach(btn => {
    btn.addEventListener('click', async () => {
      const due = +btn.dataset.due || 0;
      const amount = +prompt('Outstanding ' + rs(due) + '. Amount received:', String(due));
      if (!(amount > 0)) return;
      const accountId = chooseAccount();
      if (!accountId) return;
      try {
        await post('/Finance/ReceiveInvoice', { id: +btn.dataset.receiveInvoice, amount, accountId, date: ymd(new Date()) });
        reload();
      } catch (e) { alert(e.message); }
    });
  });

  document.querySelectorAll('[data-pay-ap]').forEach(btn => {
    btn.addEventListener('click', async () => {
      const due = +btn.dataset.due || 0;
      const amount = +prompt('Outstanding ' + rs(due) + '. Amount paid:', String(due));
      if (!(amount > 0)) return;
      const accountId = chooseAccount();
      if (!accountId) return;
      try {
        await post('/Finance/PayPayable', { id: +btn.dataset.payAp, amount, accountId, date: ymd(new Date()) });
        reload();
      } catch (e) { alert(e.message); }
    });
  });

  document.querySelectorAll('[data-del-invoice]').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Delete this invoice?')) return;
      try { await post('/Finance/DeleteInvoice?id=' + btn.dataset.delInvoice, null, 'DELETE'); reload(); }
      catch (e) { alert(e.message); }
    });
  });
  document.querySelectorAll('[data-del-ap]').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Delete this bill?')) return;
      try { await post('/Finance/DeletePayable?id=' + btn.dataset.delAp, null, 'DELETE'); reload(); }
      catch (e) { alert(e.message); }
    });
  });
  document.querySelectorAll('[data-del-bank]').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Delete this account movement?')) return;
      try { await post('/Finance/DeleteLedgerTransaction?id=' + btn.dataset.delBank, null, 'DELETE'); reload(); }
      catch (e) { alert(e.message); }
    });
  });

  const openBank = mode => {
    const panel = document.getElementById('bankEntryPanel');
    panel.style.display = '';
    panel.dataset.mode = mode;
    document.getElementById('bankEntryTitle').textContent = mode === 'in' ? 'Record receipt / deposit' : mode === 'out' ? 'Record payment / withdrawal' : 'Transfer between accounts';
    document.getElementById('bankToWrap').style.display = mode === 'transfer' ? '' : 'none';
    document.getElementById('bank-tx-source').value = mode === 'transfer' ? 'transfer' : mode === 'out' ? 'expense' : 'sale';
    document.getElementById('bank-tx-date').value = ymd(new Date());
  };
  document.getElementById('bankDepositBtn')?.addEventListener('click', () => openBank('in'));
  document.getElementById('bankWithdrawBtn')?.addEventListener('click', () => openBank('out'));
  document.getElementById('bankTransferBtn')?.addEventListener('click', () => openBank('transfer'));
  document.getElementById('bankEntryClose')?.addEventListener('click', () => { document.getElementById('bankEntryPanel').style.display = 'none'; });

  document.getElementById('saveBankTx')?.addEventListener('click', async () => {
    const mode = document.getElementById('bankEntryPanel').dataset.mode || 'in';
    const amount = +document.getElementById('bank-tx-amount').value || 0;
    const accountId = +document.getElementById('bank-tx-account').value || 0;
    const date = document.getElementById('bank-tx-date').value;
    const source = document.getElementById('bank-tx-source').value;
    if (!amount || !accountId || !date) { alert('Enter the date, account and amount.'); return; }
    if (mode !== 'transfer' && ['sale', 'capital', 'expense'].includes(source)) {
      if (!confirm('Use this only when the money was not already posted from Sales, Funding or Expenses. Continue?')) return;
    }
    const person = document.getElementById('bank-tx-person').value.trim();
    const note = document.getElementById('bank-tx-note').value.trim();
    const to = +document.getElementById('bank-tx-to').value || 0;
    if (mode === 'transfer' && (!to || to === accountId)) { alert('Choose a different account to transfer to.'); return; }
    try {
      await post('/Finance/SaveLedgerTransaction', {
        date,
        kind: mode === 'transfer' ? 'Transfer' : mode === 'out' ? 'Expense' : 'Income',
        accountId,
        counterAccountId: mode === 'transfer' ? to : null,
        amount,
        category: source,
        notes: [person, note].filter(Boolean).join(' — ')
      });
      reload();
    } catch (e) { alert(e.message); }
  });

  document.getElementById('addMoneyAccount')?.addEventListener('click', () => {
    const row = document.createElement('div');
    row.className = 'money-account-row';
    row.innerHTML = '<input data-ma-name placeholder="Account name"><select data-ma-type><option value="Cash">Cash</option><option value="Bank">Bank</option></select><input type="number" data-ma-opening value="0"><label class="partner-active"><input type="checkbox" data-ma-active checked> Active</label>';
    document.getElementById('moneyAccountRows').appendChild(row);
  });

  document.getElementById('saveMoneyAccounts')?.addEventListener('click', async () => {
    const rows = [...document.querySelectorAll('#moneyAccountRows .money-account-row')];
    try {
      for (const row of rows) {
        const name = row.querySelector('[data-ma-name]').value.trim();
        if (!name) continue;
        await post('/Finance/SaveMoneyAccount', {
          id: +row.dataset.accountId || null,
          name,
          kind: row.querySelector('[data-ma-type]').value,
          openingBalance: +row.querySelector('[data-ma-opening]').value || 0,
          isActive: row.querySelector('[data-ma-active]').checked
        });
      }
      reload();
    } catch (e) { alert(e.message); }
  });

  document.querySelectorAll('[data-budget]').forEach(inp => {
    inp.addEventListener('change', async () => {
      const parts = (monthEl?.value || '2026-01').split('-');
      try {
        await post('/Finance/SaveBudgetLine', {
          id: +inp.dataset.budgetId || null,
          year: +parts[0],
          month: +parts[1],
          category: inp.dataset.budget,
          plannedAmount: +inp.value || 0
        });
        reload();
      } catch (e) { alert(e.message); }
    });
  });

  document.getElementById('addProfitPartner')?.addEventListener('click', () => {
    const row = document.createElement('div');
    row.className = 'profit-partner-row';
    row.innerHTML = '<input data-pp-name placeholder="Partner name"><input type="number" data-pp-share min="0" max="100" step="0.01" value="0"><label class="partner-active"><input type="checkbox" data-pp-active checked> Active</label><button type="button" class="btn btn-sm" data-pp-remove>Remove</button>';
    row.querySelector('[data-pp-remove]').onclick = () => row.remove();
    document.getElementById('profitPartnerRows').appendChild(row);
  });
  document.querySelectorAll('[data-pp-remove]').forEach(btn => {
    btn.addEventListener('click', () => {
      const row = btn.closest('.profit-partner-row');
      row.dataset.removed = '1';
      row.style.display = 'none';
      const box = row.querySelector('[data-pp-active]');
      if (box) box.checked = false;
    });
  });
  document.getElementById('saveProfitPartners')?.addEventListener('click', async () => {
    const rows = [...document.querySelectorAll('#profitPartnerRows .profit-partner-row')];
    try {
      for (const row of rows) {
        const name = row.querySelector('[data-pp-name]').value.trim();
        const id = +row.dataset.partnerId || null;
        if (!name && !id) continue;
        if (!name) continue;
        await post('/Finance/SaveProfitPartner', {
          id,
          name,
          sharePercent: +row.querySelector('[data-pp-share]').value || 0,
          isActive: row.dataset.removed === '1' ? false : row.querySelector('[data-pp-active]').checked
        });
      }
      reload();
    } catch (e) { alert(e.message); }
  });

  const loadStatement = async () => {
    const from = document.getElementById('acc-from')?.value;
    const to = document.getElementById('acc-to')?.value;
    if (!from || !to) return;
    const res = await fetch('/Finance/Statement?from=' + encodeURIComponent(from) + '&to=' + encodeURIComponent(to));
    if (!res.ok) return;
    const data = await res.json();
    const lines = (data.incomeLines || []).map(x => '<tr><td style="padding-left:22px">' + x.label + '</td><td class="num-cell" style="color:var(--green-dark)">' + rs(x.amount) + '</td></tr>').join('');
    const costs = (data.expenseLines || []).map(x => '<tr><td style="padding-left:22px">' + x.label + '</td><td class="num-cell" style="color:#8a261c">' + rs(x.amount) + '</td></tr>').join('');
    const net = (+data.income || 0) - (+data.expense || 0);
    document.getElementById('plSub').textContent = from + ' → ' + to;
    document.getElementById('plRows').innerHTML =
      '<tr style="background:#F6F8F4"><td colspan="2" style="font-weight:800">MONEY THE FARM EARNED</td></tr>' +
      (lines || '<tr><td style="padding-left:22px">nothing recorded</td><td class="num-cell">' + rs(0) + '</td></tr>') +
      '<tr><td style="font-weight:800">Total earned</td><td class="num-cell" style="font-weight:800;color:var(--green-dark)">' + rs(data.income) + '</td></tr>' +
      '<tr style="background:#F6F8F4"><td colspan="2" style="font-weight:800">WHAT IT COST TO RUN</td></tr>' +
      costs +
      '<tr><td style="font-weight:800">Total costs</td><td class="num-cell" style="font-weight:800;color:#8a261c">' + rs(data.expense) + '</td></tr>' +
      '<tr style="background:' + (net < 0 ? 'var(--red-tint)' : 'var(--green-tint)') + '"><td style="font-weight:800">' + (net < 0 ? 'LOSS' : 'PROFIT') + ' for the period</td><td class="num-cell" style="font-weight:800">' + (net < 0 ? '– ' : '') + rs(Math.abs(net)) + '</td></tr>';
  };

  document.getElementById('acc-quick')?.addEventListener('change', () => {
    const v = document.getElementById('acc-quick').value;
    if (!v) return;
    const now = new Date();
    let from = new Date(now.getFullYear(), now.getMonth(), 1);
    let to = now;
    if (v === 'lm') { from = new Date(now.getFullYear(), now.getMonth() - 1, 1); to = new Date(now.getFullYear(), now.getMonth(), 0); }
    if (v === 'q') from = new Date(now.getFullYear(), now.getMonth() - 2, 1);
    if (v === 'y') from = new Date(now.getFullYear(), 0, 1);
    if (v === 'ly') { from = new Date(now.getFullYear() - 1, 0, 1); to = new Date(now.getFullYear() - 1, 11, 31); }
    if (v === 'all') from = new Date(2000, 0, 1);
    document.getElementById('acc-from').value = ymd(from);
    document.getElementById('acc-to').value = ymd(to);
    loadStatement();
  });
  document.getElementById('acc-from')?.addEventListener('change', loadStatement);
  document.getElementById('acc-to')?.addEventListener('change', loadStatement);
  document.getElementById('plCsv')?.addEventListener('click', () => {
    const rows = [...document.querySelectorAll('#plRows tr')].map(tr => [...tr.cells].map(td => '"' + td.textContent.trim().replaceAll('"', '""') + '"').join(','));
    const blob = new Blob([rows.join('\n')], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'profit-and-loss.csv';
    a.click();
  });

  document.getElementById('pay-month')?.addEventListener('change', e => {
    location.href = '/Finance?month=' + encodeURIComponent(e.target.value) + '&tab=salary';
  });
})();
