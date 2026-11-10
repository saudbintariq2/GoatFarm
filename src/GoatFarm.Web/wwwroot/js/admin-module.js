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

  function auditQuery() {
    const p = new URLSearchParams();
    const m = document.getElementById('audit-module')?.value;
    const u = document.getElementById('audit-user')?.value?.trim();
    const f = document.getElementById('audit-from')?.value;
    const t = document.getElementById('audit-to')?.value;
    if (m) p.set('module', m);
    if (u) p.set('user', u);
    if (f) p.set('from', f);
    if (t) p.set('to', t);
    return p;
  }

  async function loadAudit() {
    const body = document.getElementById('audit-rows');
    if (!body) return;
    const res = await fetch('/Admin/AuditLog?' + auditQuery().toString());
    const rows = await res.json();
    body.replaceChildren();
    if (!rows.length) {
      const tr = document.createElement('tr');
      tr.innerHTML = '<td colspan="5" class="empty">No audit rows match these filters.</td>';
      body.appendChild(tr);
      return;
    }
  rows.forEach(a => {
      const tr = document.createElement('tr');
      tr.innerHTML = `<td>${esc(a.whenDisplay)}</td><td>${esc(a.module)}</td><td>${esc(a.action)}</td><td>${esc(a.summary)}</td><td>${esc(a.userDisplay)}</td>`;
      body.appendChild(tr);
    });
  }

  function esc(s) {
    return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }

  document.getElementById('audit-apply')?.addEventListener('click', () => loadAudit().catch(() => alert('Could not load audit log.')));
  document.getElementById('audit-csv')?.addEventListener('click', e => {
    e.preventDefault();
    location.href = '/Admin/ExportAuditCsv?' + auditQuery().toString();
  });

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
