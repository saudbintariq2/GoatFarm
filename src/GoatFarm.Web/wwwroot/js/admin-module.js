(() => {
  document.querySelectorAll('#admin-tabs [data-admin-tab]').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#admin-tabs .tab').forEach(t => t.classList.remove('active'));
      btn.classList.add('active');
      const tab = btn.dataset.adminTab;
      document.querySelectorAll('[data-admin-pane]').forEach(p => { p.hidden = p.dataset.adminPane !== tab; });
    });
  });

  const review = async (id, approve) => {
    await fetch('/Admin/ReviewApproval?id=' + id, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ approve, note: '' })
    });
    location.reload();
  };
  document.querySelectorAll('.appr-yes').forEach(b => b.addEventListener('click', () => review(b.dataset.id, true)));
  document.querySelectorAll('.appr-no').forEach(b => b.addEventListener('click', () => review(b.dataset.id, false)));

  document.getElementById('pw-save')?.addEventListener('click', async () => {
    const res = await fetch('/Admin/ChangePassword', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        currentPassword: document.getElementById('pw-current').value,
        newPassword: document.getElementById('pw-new').value
      })
    });
    const j = await res.json().catch(() => ({}));
    alert(res.ok ? 'Password updated.' : (j.error || 'Could not update password.'));
  });
})();
