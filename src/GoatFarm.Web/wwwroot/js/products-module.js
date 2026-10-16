(() => {
  const monthEl = document.getElementById('prodMonth');
  monthEl?.addEventListener('change', () => { location.search = '?month=' + encodeURIComponent(monthEl.value); });

  document.querySelectorAll('#prod-tabs [data-prod-tab]').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#prod-tabs .tab').forEach(t => t.classList.remove('active'));
      btn.classList.add('active');
      const tab = btn.dataset.prodTab;
      document.querySelectorAll('[data-prod-pane]').forEach(p => { p.hidden = p.dataset.prodPane !== tab; });
    });
  });

  const post = (url, body) => fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const reload = () => location.reload();

  document.getElementById('ms-save')?.addEventListener('click', async () => {
    await post('/Products/SaveMilkingSession', {
      date: document.getElementById('ms-date').value,
      session: document.getElementById('ms-session').value,
      breed: document.getElementById('ms-breed').value,
      liters: +document.getElementById('ms-liters').value || 0,
      rejectedLiters: +document.getElementById('ms-rej').value || 0
    });
    reload();
  });

  document.getElementById('st-save')?.addEventListener('click', async () => {
    await post('/Products/SaveStockMovement', {
      date: document.getElementById('st-date').value,
      type: 'Receive',
      productId: +document.getElementById('st-product').value,
      warehouseId: +document.getElementById('st-wh').value,
      qty: +document.getElementById('st-qty').value || 0,
      unitCost: 0
    });
    reload();
  });

  document.getElementById('sl-save')?.addEventListener('click', async () => {
    await post('/Products/SaveSlaughter', {
      date: document.getElementById('sl-date').value,
      tagText: document.getElementById('sl-tag').value,
      carcassKg: +document.getElementById('sl-carcass').value || 0,
      saleAmount: +document.getElementById('sl-sale').value || 0,
      liveWeightKg: 0
    });
    reload();
  });

  document.getElementById('ps-product')?.addEventListener('change', e => {
    const opt = e.target.selectedOptions[0];
    if (!opt || !opt.value) return;
    document.getElementById('ps-name').value = opt.dataset.name || '';
    if (opt.dataset.price) document.getElementById('ps-rate').value = opt.dataset.price;
  });

  document.getElementById('cat-save')?.addEventListener('click', async () => {
    await post('/Products/SaveCatalog', {
      sku: document.getElementById('cat-sku').value,
      name: document.getElementById('cat-name').value,
      unit: document.getElementById('cat-unit').value,
      defaultPrice: +document.getElementById('cat-price').value || 0
    });
    reload();
  });

  document.getElementById('ps-save')?.addEventListener('click', async () => {
    const qty = +document.getElementById('ps-qty').value || 0;
    const rate = +document.getElementById('ps-rate').value || 0;
    const customerId = document.getElementById('ps-customer').value;
    const isCredit = document.getElementById('ps-credit').checked && customerId;
    const productId = document.getElementById('ps-product').value;
    const res = await fetch('/Products/SaveSale', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        date: document.getElementById('ps-date').value,
        productId: productId ? +productId : null,
        customerId: customerId ? +customerId : null,
        productName: document.getElementById('ps-name').value,
        qty, rate, isCredit: !!isCredit
      })
    });
    const j = await res.json().catch(() => ({}));
    if (j.invoiceId) alert('Credit sale recorded — invoice #' + j.invoiceId + ' created in Finance ledger.');
    reload();
  });
})();
