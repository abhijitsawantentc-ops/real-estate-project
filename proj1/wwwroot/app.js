const api = { auth: '/api/auth', properties: '/api/properties', interests: '/api/interests', bids: '/api/bids', transactions: '/api/transactions', notifications: '/api/notifications', admin: '/api/admin' };
let profile;

async function request(url, options = {}) {
  const response = await fetch(url, { credentials: 'same-origin', ...options, headers: { ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers } });
  if (!response.ok) {
    let message = `Request failed (${response.status})`;
    try { message = (await response.json()).message || message; } catch { }
    throw new Error(message);
  }
  if (response.status === 204) return null;
  return response.json();
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));
}
function safeImageUrl(value) {
  try { const url = new URL(value, window.location.origin); return ['https:', 'http:'].includes(url.protocol) ? url.href : ''; }
  catch { return ''; }
}
function dateText(value) { return value ? new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—'; }
function money(value) { return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(Number(value || 0)); }
function statusTag(value) { return `<span class="status status-${escapeHtml(value)}">${escapeHtml(value)}</span>`; }
function adminPropertyAction(property) {
  if (property.status === 'pending_approval') return `<button class="button button-small button-primary" data-review-property="${property.id}" data-approved="true">Publish</button> <button class="text-button danger-text" data-review-property="${property.id}" data-approved="false">Reject</button>`;
  if (['published', 'rejected'].includes(property.status)) return `<button class="text-button danger-text" data-property-status="${property.id}" data-next-status="inactive">Set inactive</button>`;
  if (property.status === 'inactive') return `<button class="text-button" data-property-status="${property.id}" data-next-status="published">Republish</button>`;
  return '—';
}
function adminInterestAction(interest) {
  if (interest.status === 'new') return `<button class="text-button" data-interest-status="${interest.id}" data-next-status="under_review">Review</button>`;
  if (['under_review', 'contacted'].includes(interest.status)) return `<button class="text-button" data-interest-status="${interest.id}" data-next-status="approved">Approve</button> <button class="text-button danger-text" data-interest-status="${interest.id}" data-next-status="rejected">Reject</button>`;
  if (['approved', 'rejected'].includes(interest.status)) return `<button class="text-button" data-interest-status="${interest.id}" data-next-status="closed">Close</button>`;
  return '—';
}
function showFeedback(message, isError = false) {
  const element = document.getElementById('feedback');
  element.textContent = message;
  element.classList.toggle('error', isError);
  element.classList.remove('hidden');
  window.setTimeout(() => element.classList.add('hidden'), 5000);
}
function showAuthFeedback(message, isError = false) {
  const element = document.getElementById('authFeedback');
  element.textContent = message;
  element.classList.toggle('error', isError);
  element.classList.remove('hidden');
}
function setAuthenticatedView(isSignedIn) {
  document.getElementById('authView').classList.toggle('hidden', isSignedIn);
  document.getElementById('dashboardView').classList.toggle('hidden', !isSignedIn);
  document.getElementById('logoutButton').classList.toggle('hidden', !isSignedIn);
  document.getElementById('identity').classList.toggle('hidden', !isSignedIn);
  if (!isSignedIn) return;
  document.getElementById('identity').textContent = `${profile.fullName} · ${profile.role}`;
  document.getElementById('roleEyebrow').textContent = `${profile.role} WORKSPACE`;
  document.getElementById('welcomeTitle').textContent = `Good to have you, ${profile.fullName.split(' ')[0]}.`;
  document.getElementById('welcomeText').textContent = profile.role === 'CUSTOMER' ? 'Discover approved homes and follow your offers.' : profile.role === 'AGENT' ? 'Manage your listings and respond to customer offers.' : 'Review the platform and keep each transaction moving.';
  for (const role of ['customer', 'agent', 'admin']) document.getElementById(`${role}Dashboard`).classList.toggle('hidden', profile.role !== role.toUpperCase());
}
async function signIn(profileResponse) { profile = profileResponse; setAuthenticatedView(true); await refreshDashboard(); }

async function loadNotifications() {
  const rows = await request(api.notifications);
  document.getElementById('notificationCount').textContent = rows.filter(row => !row.isRead).length;
  const root = document.getElementById('notifications');
  root.innerHTML = rows.length ? rows.slice(0, 6).map(item => `<article class="notification ${item.isRead ? '' : 'unread'}"><span class="notification-dot"></span><div><strong>${escapeHtml(item.title || item.type)}</strong><p>${escapeHtml(item.message)}</p><time>${dateText(item.createdAt)}</time></div>${item.isRead ? '' : `<button class="text-button" data-read-notification="${item.id}" type="button">Mark read</button>`}</article>`).join('') : '<p class="empty-state">You are all caught up.</p>';
  root.querySelectorAll('[data-read-notification]').forEach(button => button.addEventListener('click', async () => { await request(`${api.notifications}/${button.dataset.readNotification}/read`, { method: 'PUT' }); await loadNotifications(); }));
}
function renderCustomerProperties(rows) {
  const root = document.getElementById('customerProperties');
  root.innerHTML = rows.length ? rows.map(item => {
    const image = safeImageUrl(item.photoUrl);
    return `<article class="listing-card"><div class="listing-image">${image ? `<img src="${escapeHtml(image)}" alt="Property photo" loading="lazy">` : '<span class="image-placeholder">HAVEN SELECT</span>'}</div><div class="listing-copy"><div class="listing-location">${escapeHtml(item.city)}, ${escapeHtml(item.state)}</div><p class="listing-address">${escapeHtml(item.address)}</p><div class="listing-bottom"><strong>${money(item.price)}</strong><button class="button button-small button-primary" data-interest-property="${item.id}" type="button">I’m interested <span aria-hidden="true">→</span></button></div></div></article>`;
  }).join('') : '<div class="empty-card"><span class="empty-icon">⌂</span><h3>Nothing on the market just yet</h3><p>New approved listings will appear here.</p></div>';
  root.querySelectorAll('[data-interest-property]').forEach(button => button.addEventListener('click', async () => {
    const customerMessage = window.prompt('Add a note for the property team (optional):');
    if (customerMessage === null) return;
    try { await request(api.interests, { method: 'POST', body: JSON.stringify({ propertyId: button.dataset.interestProperty, customerMessage }) }); showFeedback('Your interest has been sent to the property team.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
}
function renderSimpleTable(targetId, headers, rows) {
  const root = document.getElementById(targetId);
  root.innerHTML = rows.length ? `<table><thead><tr>${headers.map(x => `<th>${x}</th>`).join('')}</tr></thead><tbody>${rows.join('')}</tbody></table>` : '<p class="empty-state">Nothing to show yet.</p>';
}
function renderTransactions(targetId, rows, isAdmin) {
  const isAgent = !isAdmin && profile.role === 'AGENT';
  renderSimpleTable(targetId, ['Property', 'Customer', 'Agent', 'Stage', 'Agreed price', 'Updated', ...(isAdmin || isAgent ? ['Action'] : [])], rows.map(row => {
    let action = '';
    if (isAdmin) {
      const adminNext = { initiated: 'admin_review', admin_review: 'negotiation', negotiation: 'agent_review', approved: 'agreement_pending', agreement_pending: 'completed' };
      if (adminNext[row.status]) action = `<button class="button button-small button-primary" data-transaction="${row.id}" data-next-status="${adminNext[row.status]}">${adminNext[row.status].replace(/_/g, ' ')}</button>`;
      if (!['completed', 'cancelled'].includes(row.status)) action += ` <button class="text-button danger-text" data-transaction="${row.id}" data-next-status="cancelled">Cancel</button>`;
    }
    if (isAgent && row.status === 'agent_review') action = `<button class="button button-small button-primary" data-transaction="${row.id}" data-next-status="approved">Approve</button> <button class="button button-small button-outline" data-transaction="${row.id}" data-next-status="negotiation">Return to negotiation</button>`;
    return `<tr><td>${escapeHtml(row.propertyTitle)}</td><td>${escapeHtml(row.customerName)}</td><td>${escapeHtml(row.agencyName)}</td><td>${statusTag(row.status)}</td><td>${row.agreedPrice == null ? '—' : money(row.agreedPrice)}</td><td>${dateText(row.completedAt || row.startedAt)}</td>${isAdmin || isAgent ? `<td>${action}</td>` : ''}</tr>`;
  }));
  bindTransactionButtons();
}
function bindTransactionButtons() {
  document.querySelectorAll('[data-transaction]').forEach(button => button.addEventListener('click', async () => {
    try {
      const notes = profile.role === 'ADMIN' ? window.prompt(`Optional admin note for ${button.dataset.nextStatus}:`) || '' : null;
      await request(`${api.transactions}/${button.dataset.transaction}/status`, { method: 'PUT', body: JSON.stringify({ status: button.dataset.nextStatus, agreedPrice: null, adminNotes: notes }) });
      showFeedback(`Transaction moved to ${button.dataset.nextStatus}.`);
      await refreshDashboard();
    } catch (error) { showFeedback(error.message, true); }
  }));
}
async function loadCustomer() {
  const [properties, interests, bids, transactions] = await Promise.all([request(api.properties), request(api.interests), request(api.bids), request(api.transactions)]);
  renderCustomerProperties(properties);
  renderSimpleTable('customerInterests', ['Property', 'Status', 'Submitted'], interests.map(x => `<tr><td>${escapeHtml(x.propertyTitle)}</td><td>${statusTag(x.status)}</td><td>${dateText(x.createdAt)}</td></tr>`));
  renderSimpleTable('customerBids', ['Property', 'Offer', 'Status', 'Created'], bids.map(x => `<tr><td>${escapeHtml(x.propertyTitle)}</td><td>${money(x.amount)}</td><td>${statusTag(x.status)}</td><td>${dateText(x.createdAt)}</td></tr>`));
  renderTransactions('customerTransactions', transactions, false);
}
async function loadAgent() {
  const [properties, bids, interests, transactions] = await Promise.all([request(api.properties), request(api.bids), request(api.interests), request(api.transactions)]);
  renderSimpleTable('agentProperties', ['Property', 'Location', 'Price', 'Status', 'Added'], properties.map(x => `<tr><td>${escapeHtml(x.title)}</td><td>${escapeHtml(x.city)}, ${escapeHtml(x.state)}</td><td>${money(x.price)}</td><td>${statusTag(x.status)}</td><td>${dateText(x.createdAt)}</td></tr>`));
  renderSimpleTable('agentInterests', ['Customer', 'Property', 'Message', 'Status', 'Date', 'Action'], interests.map(x => `<tr><td>${escapeHtml(x.customerName)}</td><td>${escapeHtml(x.propertyTitle)}</td><td>${escapeHtml(x.customerMessage || '—')}</td><td>${statusTag(x.status)}</td><td>${dateText(x.createdAt)}</td><td>${['new', 'under_review'].includes(x.status) ? `<button class="text-button" data-contact-interest="${x.id}">Mark contacted</button>` : '—'}</td></tr>`));
  document.querySelectorAll('[data-contact-interest]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.interests}/${button.dataset.contactInterest}/status`, { method: 'PUT', body: JSON.stringify({ status: 'contacted' }) }); showFeedback('Interest marked contacted.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  const bidRoot = document.getElementById('agentBids');
  bidRoot.innerHTML = bids.length ? bids.map(x => `<article class="offer-card"><div class="offer-top">${statusTag(x.status)}<time>${dateText(x.createdAt)}</time></div><h3>${escapeHtml(x.propertyTitle)}</h3><p>For ${escapeHtml(x.customerName)} · offer from admin</p><strong>${money(x.amount)}</strong>${x.message ? `<p class="offer-note">${escapeHtml(x.message)}</p>` : ''}${x.status === 'sent' ? `<div class="offer-actions"><button class="button button-small button-outline" data-view-bid="${x.id}" type="button">Mark viewed</button></div>` : ''}${x.status === 'viewed' ? `<div class="offer-actions"><button class="button button-small button-primary" data-bid="${x.id}" data-accepted="true" type="button">Accept offer</button><button class="button button-small button-outline" data-bid="${x.id}" data-accepted="false" type="button">Decline</button></div>` : ''}</article>`).join('') : '<p class="empty-state">No admin-created offers to review.</p>';
  bidRoot.querySelectorAll('[data-view-bid]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.bids}/${button.dataset.viewBid}/viewed`, { method: 'PUT' }); showFeedback('Offer marked viewed.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  bidRoot.querySelectorAll('[data-bid]').forEach(button => button.addEventListener('click', async () => {
    try { const accepted = button.dataset.accepted === 'true'; await request(`${api.bids}/${button.dataset.bid}/decision`, { method: 'PUT', body: JSON.stringify({ accepted }) }); showFeedback(accepted ? 'Offer accepted. An admin will coordinate the transaction.' : 'Offer declined.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  renderTransactions('agentTransactions', transactions, false);
}
async function loadAdmin() {
  const [properties, agents, interests, bids, transactions, profiles, audit] = await Promise.all([request(api.properties), request(`${api.admin}/agents`), request(api.interests), request(api.bids), request(api.transactions), request(`${api.admin}/profiles`), request(`${api.admin}/audit-logs`)]);
  const pendingListings = properties.filter(x => x.status === 'pending_approval');
  document.getElementById('adminStats').innerHTML = [['Pending listings', pendingListings.length], ['New interests', interests.filter(x => x.status === 'new').length], ['Offers awaiting agent', bids.filter(x => ['sent', 'viewed'].includes(x.status)).length], ['Active transactions', transactions.filter(x => !['completed', 'cancelled'].includes(x.status)).length]].map(([label, count]) => `<div class="stat-card"><span>${label}</span><strong>${count}</strong></div>`).join('');
  renderSimpleTable('adminProperties', ['Property', 'Agent', 'Location', 'Price', 'Status', 'Action'], properties.map(x => `<tr><td>${escapeHtml(x.title)}</td><td>${escapeHtml(x.agentId.slice(0, 8))}</td><td>${escapeHtml(x.city)}, ${escapeHtml(x.state)}</td><td>${money(x.price)}</td><td>${statusTag(x.status)}</td><td>${adminPropertyAction(x)}</td></tr>`));
  document.querySelectorAll('[data-review-property]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.properties}/${button.dataset.reviewProperty}/approval`, { method: 'PUT', body: JSON.stringify({ approved: button.dataset.approved === 'true' }) }); showFeedback(button.dataset.approved === 'true' ? 'Listing approved and published.' : 'Listing rejected.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  document.querySelectorAll('[data-property-status]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.properties}/${button.dataset.propertyStatus}/status`, { method: 'PUT', body: JSON.stringify({ status: button.dataset.nextStatus }) }); showFeedback(`Property status set to ${button.dataset.nextStatus}.`); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  const agentRoot = document.getElementById('adminAgents');
  agentRoot.innerHTML = agents.length ? agents.map(x => {
    const actions = x.verificationStatus === 'approved'
      ? `<button class="text-button danger-text" data-verify-agent="${x.id}" data-agent-status="suspended">Suspend</button>`
      : `<button class="button button-small button-primary" data-verify-agent="${x.id}" data-agent-status="approved">Approve</button><button class="text-button danger-text" data-verify-agent="${x.id}" data-agent-status="rejected">Reject</button>`;
    return `<article class="review-card"><div><h3>${escapeHtml(x.agencyName)}</h3><p>${escapeHtml(x.name)} · ${escapeHtml(x.email)}</p><p>${escapeHtml(x.licenseNumber || 'No license supplied')} · ${escapeHtml(x.city || '')}</p>${statusTag(x.verificationStatus)}</div><div class="button-stack">${actions}</div></article>`;
  }).join('') : '<p class="empty-state">No agent accounts yet.</p>';
  document.querySelectorAll('[data-verify-agent]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.admin}/agents/${button.dataset.verifyAgent}/verification`, { method: 'PUT', body: JSON.stringify({ status: button.dataset.agentStatus }) }); showFeedback(`Agent status set to ${button.dataset.agentStatus}.`); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  renderSimpleTable('adminInterests', ['Customer', 'Property', 'Message', 'Status', 'Action'], interests.map(x => `<tr><td>${escapeHtml(x.customerName)}</td><td>${escapeHtml(x.propertyTitle)}</td><td>${escapeHtml(x.customerMessage || '—')}</td><td>${statusTag(x.status)}</td><td>${adminInterestAction(x)}</td></tr>`));
  document.querySelectorAll('[data-interest-status]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.interests}/${button.dataset.interestStatus}/status`, { method: 'PUT', body: JSON.stringify({ status: button.dataset.nextStatus }) }); showFeedback(`Customer interest status set to ${button.dataset.nextStatus}.`); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  const select = document.getElementById('interestSelect');
  const eligibleInterests = interests.filter(x => x.status === 'approved');
  select.innerHTML = eligibleInterests.map(x => `<option value="${x.id}">${escapeHtml(x.customerName)} — ${escapeHtml(x.propertyTitle)}</option>`).join('');
  const bidForm = document.getElementById('bidForm');
  bidForm.elements.customerInterestId.disabled = !eligibleInterests.length;
  bidForm.querySelector('button[type="submit"]').disabled = !eligibleInterests.length;
  renderSimpleTable('adminBids', ['Property', 'Customer', 'Offer', 'Status', 'Action'], bids.map(x => {
    let action = '—';
    if (x.status === 'draft') action = `<button class="button button-small button-primary" data-send-bid="${x.id}">Send to agent</button>`;
    else if (['sent', 'viewed'].includes(x.status)) action = `<button class="text-button danger-text" data-expire-bid="${x.id}">Expire</button>`;
    else if (x.status === 'accepted') action = `<button class="button button-small button-primary" data-start-transaction="${x.id}">Start transaction</button>`;
    return `<tr><td>${escapeHtml(x.propertyTitle)}</td><td>${escapeHtml(x.customerName)}</td><td>${money(x.amount)}</td><td>${statusTag(x.status)}</td><td>${action}</td></tr>`;
  }));
  document.querySelectorAll('[data-send-bid]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.bids}/${button.dataset.sendBid}/send`, { method: 'PUT' }); showFeedback('Bid sent to the agent.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  document.querySelectorAll('[data-expire-bid]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.bids}/${button.dataset.expireBid}/status`, { method: 'PUT', body: JSON.stringify({ status: 'expired' }) }); showFeedback('Bid expired.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  document.querySelectorAll('[data-start-transaction]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.transactions}/from-bid/${button.dataset.startTransaction}`, { method: 'POST' }); showFeedback('Transaction initiated.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  renderTransactions('adminTransactions', transactions, true);
  renderSimpleTable('adminProfiles', ['Name', 'Email', 'Role', 'Status', 'Action'], profiles.map(x => `<tr><td>${escapeHtml(x.fullName)}</td><td>${escapeHtml(x.email)}</td><td>${escapeHtml(x.role)}</td><td><span class="status">${x.isActive ? 'ACTIVE' : 'INACTIVE'}</span></td><td>${x.id === profile.id ? 'You' : `<button class="text-button ${x.isActive ? 'danger-text' : ''}" data-profile="${x.id}" data-active="${!x.isActive}">${x.isActive ? 'Deactivate' : 'Activate'}</button>`}</td></tr>`));
  document.querySelectorAll('[data-profile]').forEach(button => button.addEventListener('click', async () => {
    try { await request(`${api.admin}/profiles/${button.dataset.profile}/active`, { method: 'PUT', body: JSON.stringify({ isActive: button.dataset.active === 'true' }) }); showFeedback('Profile access updated.'); await refreshDashboard(); }
    catch (error) { showFeedback(error.message, true); }
  }));
  renderSimpleTable('adminAudit', ['Action', 'Entity', 'Record', 'By', 'When'], audit.map(x => `<tr><td>${escapeHtml(x.action)}</td><td>${escapeHtml(x.entityType)}</td><td>${escapeHtml(x.entityId || '—')}</td><td>${escapeHtml(x.userName || 'System')}</td><td>${dateText(x.createdAt)}</td></tr>`));
}
async function refreshDashboard() {
  try {
    if (!profile) return;
    await loadNotifications();
    if (profile.role === 'CUSTOMER') await loadCustomer();
    if (profile.role === 'AGENT') await loadAgent();
    if (profile.role === 'ADMIN') await loadAdmin();
  } catch (error) { showFeedback(error.message, true); }
}

document.getElementById('loginTab').addEventListener('click', () => {
  document.getElementById('loginTab').classList.add('active'); document.getElementById('registerTab').classList.remove('active');
  document.getElementById('loginForm').classList.remove('hidden'); document.getElementById('registerForm').classList.add('hidden');
});
document.getElementById('registerTab').addEventListener('click', () => {
  document.getElementById('registerTab').classList.add('active'); document.getElementById('loginTab').classList.remove('active');
  document.getElementById('registerForm').classList.remove('hidden'); document.getElementById('loginForm').classList.add('hidden');
});
document.getElementById('registerRole').addEventListener('change', event => document.getElementById('agentFields').classList.toggle('hidden', event.target.value !== 'AGENT'));
document.getElementById('loginForm').addEventListener('submit', async event => {
  event.preventDefault(); const form = new FormData(event.currentTarget);
  try { await signIn(await request(`${api.auth}/login`, { method: 'POST', body: JSON.stringify({ email: form.get('email'), password: form.get('password') }) })); }
  catch (error) { showAuthFeedback(error.message, true); }
});
document.getElementById('registerForm').addEventListener('submit', async event => {
  event.preventDefault(); const form = new FormData(event.currentTarget);
  const email = form.get('email');
  try {
    const result = await request(`${api.auth}/register`, { method: 'POST', body: JSON.stringify(Object.fromEntries(form.entries())) });
    event.currentTarget.reset();
    document.getElementById('loginTab').click();
    document.querySelector('#loginForm [name="email"]').value = email;
    document.querySelector('#loginForm [name="password"]').focus();
    showAuthFeedback(result.message || 'Account created. Please sign in to continue.');
  }
  catch (error) { showAuthFeedback(error.message, true); }
});
document.getElementById('logoutButton').addEventListener('click', async () => {
  try { await request(`${api.auth}/logout`, { method: 'POST' }); profile = null; setAuthenticatedView(false); }
  catch (error) { showFeedback(error.message, true); }
});
document.getElementById('refreshButton').addEventListener('click', refreshDashboard);
document.getElementById('propertyForm').addEventListener('submit', async event => {
  event.preventDefault(); const form = new FormData(event.currentTarget);
  const numberOrNull = name => form.get(name) ? Number(form.get(name)) : null;
  const submitter = event.submitter;
  const property = {
    title: form.get('title'), propertyType: form.get('propertyType'), listingType: form.get('listingType'), price: Number(form.get('price')),
    address: form.get('address'), city: form.get('city'), state: form.get('state'), pincode: form.get('pincode') || null,
    description: form.get('description') || null, status: submitter?.dataset.propertyStatus || 'pending_approval', bedrooms: numberOrNull('bedrooms'), bathrooms: numberOrNull('bathrooms'),
    area: numberOrNull('area'), areaUnit: form.get('areaUnit') || null
  };
  try {
    const created = await request(api.properties, { method: 'POST', body: JSON.stringify(property) });
    const imageUrl = String(form.get('imageUrl') || '').trim();
    if (imageUrl) await request(`${api.properties}/${created.id}/images`, { method: 'POST', body: JSON.stringify({ imageUrl, storagePath: imageUrl, isPrimary: true, displayOrder: 0 }) });
    event.currentTarget.reset();
    showFeedback(property.status === 'draft' ? 'Property saved as draft.' : 'Listing submitted for admin approval.');
    await refreshDashboard();
  } catch (error) { showFeedback(error.message, true); }
});
document.getElementById('bidForm').addEventListener('submit', async event => {
  event.preventDefault(); const form = new FormData(event.currentTarget);
  try {
    await request(api.bids, { method: 'POST', body: JSON.stringify({ customerInterestId: form.get('customerInterestId'), amount: Number(form.get('amount')), message: form.get('message') || null }) });
    event.currentTarget.reset(); showFeedback('Offer sent to the listing agent.'); await refreshDashboard();
  } catch (error) { showFeedback(error.message, true); }
});

(async function initialize() {
  try {
    const response = await fetch(`${api.auth}/me`, { credentials: 'same-origin' });
    if (response.ok) {
      profile = await response.json();
      setAuthenticatedView(true);
      await refreshDashboard();
    } else {
      setAuthenticatedView(false);
    }
  }
  catch { setAuthenticatedView(false); }
})();