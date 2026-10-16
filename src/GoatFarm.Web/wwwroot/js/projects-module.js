(() => {
  const post = (url, body) => fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const reload = () => location.reload();

  document.getElementById('pj-save')?.addEventListener('click', async () => {
    await post('/Projects/SaveProject', {
      name: document.getElementById('pj-name').value,
      budgetAmount: +document.getElementById('pj-budget').value || 0,
      startDate: document.getElementById('pj-start').value,
      status: 'Active'
    });
    reload();
  });

  document.getElementById('fa-save')?.addEventListener('click', async () => {
    const pid = document.getElementById('fa-project').value;
    await post('/Projects/SaveFixedAsset', {
      name: document.getElementById('fa-name').value,
      cost: +document.getElementById('fa-cost').value || 0,
      category: 'Machinery',
      projectId: pid ? +pid : null
    });
    reload();
  });

  document.getElementById('so-add')?.addEventListener('click', async () => {
    await post('/Projects/SaveSolarLog', {
      date: document.getElementById('so-date').value,
      kwhGenerated: +document.getElementById('so-kwh').value || 0,
      savingsEstimate: +document.getElementById('so-save').value || null
    });
    reload();
  });
})();
