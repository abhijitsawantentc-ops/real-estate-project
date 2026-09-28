const api = {
  auth: '/api/auth',
  properties: '/api/properties',
  interests: '/api/interests',
  bids: '/api/bids',
  transactions: '/api/transactions',
  notifications: '/api/notifications',
  admin: '/api/admin'
};

let profile = null;
let currentPropertyList = [];
let currentAdminBids = [];

async function request(url, options = {}) {
  const response = await fetch(url, {
    credentials: 'same-origin',
    ...options,
    headers: {
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...options.headers
    }
  });

  if (!response.ok) {
    let message = '';
    try {
      const data = await response.json();
      if (data.message) {
        message = data.message;
      } else if (data.errors && typeof data.errors === 'object') {
        const errorList = Object.values(data.errors).flat().filter(Boolean);
        if (errorList.length) message = errorList.join(' ');
      } else if (data.title && !data.title.includes('One or more validation errors occurred')) {
        message = data.title;
      } else if (typeof data === 'string') {
        message = data;
      }
    } catch {
      try {
        const text = await response.text();
        if (text) message = text;
      } catch { }
    }
    if (!message) {
      if (response.status === 400) message = 'Invalid request details. Please check the entered fields.';
      else if (response.status === 401) message = 'Authentication required or invalid credentials.';
      else if (response.status === 403) message = 'You do not have permission for this action.';
      else if (response.status === 404) message = 'The requested resource was not found.';
      else if (response.status === 409) message = 'A conflicting record already exists.';
      else message = `Request failed (${response.status})`;
    }
    throw new Error(message);
  }

  if (response.status === 204) return null;
  return response.json();
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, c => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  }[c]));
}

function safeImageUrl(value) {
  try {
    const url = new URL(value, window.location.origin);
    return ['https:', 'http:'].includes(url.protocol) ? url.href : '';
  } catch {
    return '';
  }
}

function dateText(value) {
  return value ? new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—';
}

function money(value) {
  if (value == null || isNaN(value)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(Number(value));
}

function statusTag(value) {
  const labels = {
    pending_admin: 'Admin Review',
    pending_approval: 'Pending Approval',
    sent: 'Sent to Seller',
    viewed: 'Viewed by Seller',
    accepted: 'Accepted',
    rejected: 'Declined by Seller',
    rejected_by_admin: 'Declined by Admin',
    published: 'Published',
    draft: 'Draft',
    inactive: 'Inactive',
    under_offer: 'Under Offer',
    sold: 'Sold'
  };
  const label = labels[value] || value.replace(/_/g, ' ');
  return `<span class="status status-${escapeHtml(value)}">${escapeHtml(label)}</span>`;
}

function showFeedback(message, isError = false) {
  const el = document.getElementById('feedback');
  el.textContent = message;
  el.classList.toggle('error', isError);
  el.classList.remove('hidden');
  window.setTimeout(() => el.classList.add('hidden'), 5000);
}

function showAuthFeedback(message, isError = false) {
  const el = document.getElementById('authFeedback');
  el.textContent = message;
  el.classList.toggle('error', isError);
  el.classList.remove('hidden');
}

function setAuthenticatedView(isSignedIn) {
  document.getElementById('authView').classList.toggle('hidden', isSignedIn);
  document.getElementById('dashboardView').classList.toggle('hidden', !isSignedIn);
  document.getElementById('logoutButton').classList.toggle('hidden', !isSignedIn);
  document.getElementById('identity').classList.toggle('hidden', !isSignedIn);

  if (!isSignedIn) return;

  const roleTitle = profile.role === 'CUSTOMER' ? 'BUYER' : profile.role;
  document.getElementById('identity').textContent = `${profile.fullName} · ${roleTitle}`;
  document.getElementById('roleEyebrow').textContent = `${roleTitle} WORKSPACE`;
  document.getElementById('welcomeTitle').textContent = `Welcome, ${profile.fullName.split(' ')[0]}.`;

  document.getElementById('welcomeText').textContent =
    profile.role === 'CUSTOMER'
      ? 'Browse approved property listings, inspect permissible details, and submit bids.'
      : profile.role === 'AGENT'
        ? 'Post listings with confidential floor prices, review incoming admin-approved bids, and browse the network.'
        : 'Review listings, configure buyer field visibility, and vet/modify buyer bids before sending to sellers.';

  for (const role of ['customer', 'agent', 'admin']) {
    const el = document.getElementById(`${role}Dashboard`);
    if (el) el.classList.toggle('hidden', profile.role !== role.toUpperCase());
  }
}

async function signIn(profileResponse) {
  profile = profileResponse;
  setAuthenticatedView(true);
  await refreshDashboard();
}

async function loadNotifications() {
  const rows = await request(api.notifications);
  const unreadCount = rows.filter(r => !r.isRead).length;
  document.getElementById('notificationCount').textContent = unreadCount;

  const root = document.getElementById('notifications');
  root.innerHTML = rows.length
    ? rows.slice(0, 6).map(item => `
      <article class="notification ${item.isRead ? '' : 'unread'}">
        <span class="notification-dot"></span>
        <div>
          <strong>${escapeHtml(item.title || item.type)}</strong>
          <p>${escapeHtml(item.message)}</p>
          <time>${dateText(item.createdAt)}</time>
        </div>
        ${item.isRead ? '' : `<button class="text-button" data-read-notification="${item.id}" type="button">Mark read</button>`}
      </article>`).join('')
    : '<p class="empty-state">You are all caught up.</p>';

  root.querySelectorAll('[data-read-notification]').forEach(btn =>
    btn.addEventListener('click', async () => {
      await request(`${api.notifications}/${btn.dataset.readNotification}/read`, { method: 'PUT' });
      await loadNotifications();
    })
  );
}

function renderPropertyCard(item, canBid = true) {
  const image = item.showPhotos && item.photoUrl ? safeImageUrl(item.photoUrl) : '';
  const priceDisplay = item.showAskingPrice && item.askingPrice != null
    ? money(item.askingPrice)
    : '<span class="price-masked">Price on request</span>';

  const negotiableBadge = item.showNegotiablePrice && item.negotiablePrice != null
    ? `<span class="badge-negotiable">Negotiable to ${money(item.negotiablePrice)}</span>`
    : '';

  const addressDisplay = item.showExactAddress
    ? escapeHtml(item.address)
    : `${escapeHtml(item.address)} <span class="badge-masked">(Address masked by Admin)</span>`;

  const sellerDisplay = item.showSellerContact && (item.agentName || item.agencyName)
    ? `<div class="listing-location">Listed by ${escapeHtml(item.agentName || item.agencyName)}</div>`
    : `<div class="listing-location">${escapeHtml(item.city)}, ${escapeHtml(item.state)}</div>`;

  const specs = [
    item.bedrooms != null ? `${item.bedrooms} Beds` : null,
    item.bathrooms != null ? `${item.bathrooms} Baths` : null,
    item.area != null ? `${item.area} ${item.areaUnit || 'sq ft'}` : null
  ].filter(Boolean).join(' · ');

  return `
    <article class="listing-card">
      <div class="listing-image">
        ${image
          ? `<img src="${escapeHtml(image)}" alt="Property photo" loading="lazy">`
          : `<span class="image-placeholder">${item.showPhotos ? 'NO PHOTO PROVIDED' : 'PHOTO RESTRICTED BY ADMIN'}</span>`}
        ${item.propertyType ? `<span class="listing-badge">${escapeHtml(item.propertyType)}</span>` : ''}
      </div>
      <div class="listing-copy">
        ${sellerDisplay}
        <h3 style="margin: 6px 0 2px;">${escapeHtml(item.title)}</h3>
        <p class="listing-address">${addressDisplay}</p>
        ${specs ? `<p class="muted" style="font-size: .8rem; margin: -6px 0 8px;">${specs}</p>` : ''}
        ${item.showDescription && item.description ? `<p class="muted" style="font-size: .78rem; margin-bottom: 12px; max-height: 40px; overflow: hidden; text-overflow: ellipsis;">${escapeHtml(item.description)}</p>` : ''}
        ${negotiableBadge ? `<div style="margin-bottom: 10px;">${negotiableBadge}</div>` : ''}
        <div class="listing-bottom">
          <strong>${priceDisplay}</strong>
          ${canBid ? `<button class="button button-small button-primary" data-bid-property="${item.id}" type="button">Place a Bid <span aria-hidden="true">→</span></button>` : ''}
        </div>
      </div>
    </article>
  `;
}

function bindBidButtons(container) {
  container.querySelectorAll('[data-bid-property]').forEach(btn => {
    btn.addEventListener('click', () => {
      const propId = btn.dataset.bidProperty;
      const prop = currentPropertyList.find(p => p.id === propId);
      if (prop) openBidModal(prop);
    });
  });
}

function renderSimpleTable(targetId, headers, rows) {
  const root = document.getElementById(targetId);
  if (!root) return;
  root.innerHTML = rows.length
    ? `<table><thead><tr>${headers.map(x => `<th>${x}</th>`).join('')}</tr></thead><tbody>${rows.join('')}</tbody></table>`
    : '<p class="empty-state">Nothing to show yet.</p>';
}

function openBidModal(prop) {
  const modal = document.getElementById('bidModal');
  document.getElementById('bidModalPropertyId').value = prop.id;
  document.getElementById('bidModalTitle').textContent = `Place a Bid on "${prop.title}"`;
  document.getElementById('bidModalSub').textContent = `${prop.city}, ${prop.state}`;

  const priceText = prop.showAskingPrice && prop.askingPrice != null ? money(prop.askingPrice) : 'Price on request';
  const negText = prop.showNegotiablePrice && prop.negotiablePrice != null ? ` · Negotiable to ${money(prop.negotiablePrice)}` : '';

  document.getElementById('bidModalSummary').innerHTML = `
    <strong>Asking Price:</strong> ${priceText}${negText}<br />
    <span class="muted" style="font-size: .82rem;">${prop.showExactAddress ? prop.address : (prop.locality || `${prop.city}, ${prop.state}`)}</span>
  `;

  document.getElementById('bidModalAmount').value = prop.askingPrice || '';
  document.getElementById('bidModalMessage').value = '';
  modal.showModal();
}

function openAdminListingModal(prop) {
  const modal = document.getElementById('adminListingModal');
  document.getElementById('adminListingId').value = prop.id;
  document.getElementById('adminListingTitle').textContent = `Review & Configure Visibility: ${prop.title}`;

  const preview = document.getElementById('adminListingPreview');
  preview.innerHTML = `
    <div class="review-item"><span class="r-label">Asking Price</span><span class="r-val">${money(prop.price)}</span></div>
    <div class="review-item"><span class="r-label">Negotiable Floor Price</span><span class="r-val highlight-val">${money(prop.negotiablePrice) || 'None specified'}</span></div>
    <div class="review-item"><span class="r-label">Property Type</span><span class="r-val">${escapeHtml(prop.propertyType)} (${escapeHtml(prop.listingType)})</span></div>
    <div class="review-item"><span class="r-label">Street Address</span><span class="r-val">${escapeHtml(prop.address)}</span></div>
    <div class="review-item"><span class="r-label">Locality / City</span><span class="r-val">${escapeHtml(prop.locality || '—')}, ${escapeHtml(prop.city)}, ${escapeHtml(prop.state)}</span></div>
    <div class="review-item"><span class="r-label">Specs</span><span class="r-val">${prop.bedrooms ?? '—'} Beds, ${prop.bathrooms ?? '—'} Baths, ${prop.area ?? '—'} ${prop.areaUnit || 'sq ft'}</span></div>
    <div class="review-item" style="grid-column: 1 / -1;"><span class="r-label">Description</span><span class="r-val" style="font-weight: 400; font-size: .85rem;">${escapeHtml(prop.description || 'No description provided')}</span></div>
    ${prop.locationUrl ? `<div class="review-item" style="grid-column: 1 / -1;"><span class="r-label">Location URL</span><a href="${escapeHtml(prop.locationUrl)}" target="_blank" style="font-size: .82rem; color: var(--green);">${escapeHtml(prop.locationUrl)}</a></div>` : ''}
  `;

  // Set toggle controls
  document.getElementById('visShowPhotos').checked = prop.showPhotos !== false;
  document.getElementById('visShowAskingPrice').checked = prop.showAskingPrice !== false;
  document.getElementById('visShowNegotiablePrice').checked = prop.showNegotiablePrice === true;
  document.getElementById('visShowExactAddress').checked = prop.showExactAddress !== false;
  document.getElementById('visShowDescription').checked = prop.showDescription !== false;
  document.getElementById('visShowSellerContact').checked = prop.showSellerContact === true;
  document.getElementById('adminListingComment').value = prop.adminComment || '';

  modal.showModal();
}

function openAdminBidModal(bid) {
  const modal = document.getElementById('adminBidModal');
  document.getElementById('adminBidId').value = bid.id;

  const context = document.getElementById('adminBidContext');
  context.innerHTML = `
    <div style="margin-bottom: 8px;">
      <strong>Property:</strong> ${escapeHtml(bid.propertyTitle)}<br />
      <span class="muted" style="font-size: .82rem;">Asking Price: ${money(bid.propertyAskingPrice)} | Seller's Floor Negotiable Price: <strong>${money(bid.propertyNegotiablePrice)}</strong></span>
    </div>
    <div style="margin-bottom: 8px;">
      <strong>Buyer:</strong> ${escapeHtml(bid.buyerName)} (${escapeHtml(bid.buyerRole || 'Customer')})<br />
      <strong>Buyer's Original Offer:</strong> <span style="font-size: 1.1rem; font-weight: 700; color: var(--green);">${money(bid.originalAmount)}</span>
    </div>
    ${bid.buyerMessage ? `<div style="background: #eef2ec; padding: 7px 10px; border-radius: 6px; font-size: .8rem;"><strong>Buyer Note:</strong> "${escapeHtml(bid.buyerMessage)}"</div>` : ''}
  `;

  document.getElementById('adminBidModifiedAmount').value = bid.amount || bid.originalAmount;
  document.getElementById('adminBidNotes').value = bid.adminNotes || '';

  modal.showModal();
}

async function loadCustomer() {
  const [marketplaceProperties, bids, transactions] = await Promise.all([
    request(`${api.properties}?scope=marketplace`),
    request(api.bids),
    request(api.transactions)
  ]);

  currentPropertyList = marketplaceProperties;

  // Render marketplace
  const propRoot = document.getElementById('customerProperties');
  propRoot.innerHTML = marketplaceProperties.length
    ? marketplaceProperties.map(p => renderPropertyCard(p, true)).join('')
    : '<div class="empty-card"><span class="empty-icon">⌂</span><h3>No properties on the market yet</h3><p>Approved listings curated by Admin will appear here.</p></div>';
  bindBidButtons(propRoot);

  // Render buyer bids
  renderSimpleTable(
    'customerBids',
    ['Property', 'Your Bid', 'Official Offer', 'Admin Notes', 'Status', 'Submitted'],
    bids.map(b => `
      <tr>
        <td>${escapeHtml(b.propertyTitle)}</td>
        <td><strong>${money(b.originalAmount ?? b.amount)}</strong></td>
        <td>${b.isModifiedByAdmin ? `<span style="color: #235d4f; font-weight: 700;">${money(b.amount)}</span> <span class="badge-negotiable">Admin adjusted</span>` : money(b.amount)}</td>
        <td>${escapeHtml(b.adminNotes || '—')}</td>
        <td>${statusTag(b.status)}</td>
        <td>${dateText(b.createdAt)}</td>
      </tr>
    `)
  );

  renderTransactions('customerTransactions', transactions, false);
}

async function loadAgent() {
  const [myProperties, bids, marketplaceProperties, transactions] = await Promise.all([
    request(api.properties),
    request(api.bids),
    request(`${api.properties}?scope=marketplace`),
    request(api.transactions)
  ]);

  currentPropertyList = marketplaceProperties;

  // Render Agent's own inventory
  renderSimpleTable(
    'agentProperties',
    ['Property', 'Location', 'Asking Price', 'Negotiable Up To', 'Status', 'Admin Feedback', 'Added'],
    myProperties.map(p => `
      <tr>
        <td>${escapeHtml(p.title)}</td>
        <td>${escapeHtml(p.locality ? `${p.locality}, ${p.city}` : `${p.city}, ${p.state}`)}</td>
        <td><strong>${money(p.price)}</strong></td>
        <td>${p.negotiablePrice ? `<span class="badge-negotiable">${money(p.negotiablePrice)}</span>` : '—'}</td>
        <td>${statusTag(p.status)}</td>
        <td>${p.adminComment ? `<span style="font-size: .8rem; color: #876529;">${escapeHtml(p.adminComment)}</span>` : '—'}</td>
        <td>${dateText(p.createdAt)}</td>
      </tr>
    `)
  );

  // Render incoming forwarded bids for seller
  const sellerBids = bids.filter(b => b.isSeller);
  const bidRoot = document.getElementById('agentBids');
  bidRoot.innerHTML = sellerBids.length
    ? sellerBids.map(x => `
      <article class="offer-card">
        <div class="offer-top">
          ${statusTag(x.status)}
          <time>${dateText(x.createdAt)}</time>
        </div>
        <h3 style="margin-bottom: 2px;">${escapeHtml(x.propertyTitle)}</h3>
        <p>Offer forwarded by Admin for verified buyer</p>
        <div style="margin: 8px 0 10px;">
          <span style="font-size: .8rem; color: var(--muted); text-transform: uppercase;">Approved Offer Amount:</span><br />
          <strong style="color: var(--green);">${money(x.amount)}</strong>
        </div>
        ${x.adminNotes ? `<div class="offer-note" style="margin-bottom: 8px;"><strong>Admin Note:</strong> ${escapeHtml(x.adminNotes)}</div>` : ''}
        ${x.message ? `<div class="offer-note"><strong>Buyer Terms:</strong> ${escapeHtml(x.message)}</div>` : ''}
        ${x.status === 'sent'
          ? `<div class="offer-actions"><button class="button button-small button-outline" data-view-bid="${x.id}" type="button">Mark Viewed</button></div>`
          : ''}
        ${x.status === 'viewed' || x.status === 'sent'
          ? `<div class="offer-actions">
              <button class="button button-small button-primary" data-bid="${x.id}" data-accepted="true" type="button">Accept Offer</button>
              <button class="button button-small button-outline danger-text" data-bid="${x.id}" data-accepted="false" type="button">Decline</button>
            </div>`
          : ''}
      </article>
    `).join('')
    : '<p class="empty-state">No offers forwarded by Admin to review at this time.</p>';

  bidRoot.querySelectorAll('[data-view-bid]').forEach(btn =>
    btn.addEventListener('click', async () => {
      try {
        await request(`${api.bids}/${btn.dataset.viewBid}/viewed`, { method: 'PUT' });
        showFeedback('Offer marked viewed.');
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );

  bidRoot.querySelectorAll('[data-bid]').forEach(btn =>
    btn.addEventListener('click', async () => {
      try {
        const accepted = btn.dataset.accepted === 'true';
        await request(`${api.bids}/${btn.dataset.bid}/decision`, {
          method: 'PUT',
          body: JSON.stringify({ accepted })
        });
        showFeedback(accepted ? 'Offer accepted! A transaction record has been created.' : 'Offer declined.');
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );

  // Render top marketplace properties window for agent (who is also a buyer)
  const agentTopMarketRoot = document.getElementById('agentTopMarketplaceProperties');
  if (agentTopMarketRoot) {
    const topProps = marketplaceProperties.slice(0, 4);
    agentTopMarketRoot.innerHTML = topProps.length
      ? topProps.map(p => renderPropertyCard(p, true)).join('')
      : '<div class="empty-card"><span class="empty-icon">⌂</span><h3>No properties on the market yet</h3><p>Approved listings on the Horizon network will appear here.</p></div>';
    bindBidButtons(agentTopMarketRoot);
  }

  const browseAllBtn = document.getElementById('agentBrowseAllBtn');
  if (browseAllBtn && agentMarketTab) {
    browseAllBtn.onclick = () => agentMarketTab.click();
  }

  // Render marketplace for agent browsing
  const agentMarketRoot = document.getElementById('agentMarketplaceProperties');
  agentMarketRoot.innerHTML = marketplaceProperties.length
    ? marketplaceProperties.map(p => renderPropertyCard(p, true)).join('')
    : '<div class="empty-card"><span class="empty-icon">⌂</span><h3>No properties on the market yet</h3></div>';
  bindBidButtons(agentMarketRoot);

  renderTransactions('agentTransactions', transactions, false);
}

async function loadAdmin() {
  const [properties, agents, bids, transactions, profiles, audit] = await Promise.all([
    request(api.properties),
    request(`${api.admin}/agents`),
    request(api.bids),
    request(api.transactions),
    request(`${api.admin}/profiles`),
    request(`${api.admin}/audit-logs`)
  ]);

  currentPropertyList = properties;
  currentAdminBids = bids;

  const pendingListings = properties.filter(x => x.status === 'pending_approval');
  const pendingBids = bids.filter(x => x.status === 'pending_admin');

  // Stats
  document.getElementById('adminStats').innerHTML = [
    ['Listings Pending Review', pendingListings.length],
    ['Bids Pending Admin Review', pendingBids.length],
    ['Active Market Listings', properties.filter(x => x.status === 'published').length],
    ['Active Transactions', transactions.filter(x => !['completed', 'cancelled'].includes(x.status)).length]
  ].map(([label, count]) => `<div class="stat-card"><span>${label}</span><strong>${count}</strong></div>`).join('');

  // 1. Pending Listings Table
  renderSimpleTable(
    'adminPendingProperties',
    ['Property', 'Asking Price', 'Confidential Floor', 'Location', 'Submitted', 'Action'],
    pendingListings.map(p => `
      <tr>
        <td><strong>${escapeHtml(p.title)}</strong><br /><span class="muted" style="font-size: .75rem;">${escapeHtml(p.propertyType)} · ${escapeHtml(p.listingType)}</span></td>
        <td>${money(p.price)}</td>
        <td><strong style="color: #235d4f;">${money(p.negotiablePrice) || '—'}</strong></td>
        <td>${escapeHtml(p.locality ? `${p.locality}, ${p.city}` : `${p.city}, ${p.state}`)}</td>
        <td>${dateText(p.createdAt)}</td>
        <td>
          <button class="button button-small button-primary" data-review-modal="${p.id}" type="button">Review &amp; Set Visibility</button>
        </td>
      </tr>
    `)
  );

  document.querySelectorAll('[data-review-modal]').forEach(btn =>
    btn.addEventListener('click', () => {
      const prop = currentPropertyList.find(p => p.id === btn.dataset.reviewModal);
      if (prop) openAdminListingModal(prop);
    })
  );

  // 2. Pending Bids Review Panel
  const pendingBidsRoot = document.getElementById('adminPendingBids');
  pendingBidsRoot.innerHTML = pendingBids.length
    ? pendingBids.map(b => `
      <article class="review-card" style="display: block;">
        <div style="display: flex; justify-content: space-between; align-items: flex-start; gap: 14px; margin-bottom: 10px;">
          <div>
            <h3 style="margin: 0 0 3px;">Offer on "${escapeHtml(b.propertyTitle)}"</h3>
            <p style="margin: 0; color: var(--muted); font-size: .83rem;">
              Seller Asking: <strong>${money(b.propertyAskingPrice)}</strong> | Confidential Floor: <strong style="color: var(--green);">${money(b.propertyNegotiablePrice)}</strong>
            </p>
          </div>
          ${statusTag(b.status)}
        </div>

        <div style="display: flex; gap: 24px; background: #f6f8f4; padding: 12px 14px; border-radius: 8px; margin-bottom: 12px;">
          <div>
            <span class="muted" style="font-size: .73rem; text-transform: uppercase;">Buyer</span><br />
            <strong>${escapeHtml(b.buyerName)}</strong> <span style="font-size: .78rem; color: var(--muted);">(${escapeHtml(b.buyerRole || 'Customer')})</span>
          </div>
          <div>
            <span class="muted" style="font-size: .73rem; text-transform: uppercase;">Buyer Bid Amount</span><br />
            <strong style="font-size: 1.15rem; color: #174739;">${money(b.originalAmount)}</strong>
          </div>
          ${b.buyerMessage ? `<div style="flex: 1;"><span class="muted" style="font-size: .73rem; text-transform: uppercase;">Buyer Notes</span><br /><span style="font-size: .84rem;">${escapeHtml(b.buyerMessage)}</span></div>` : ''}
        </div>

        <div style="display: flex; gap: 8px; justify-content: flex-end;">
          <button class="button button-small button-outline danger-text" data-bid-quick-reject="${b.id}" type="button">Reject Offer</button>
          <button class="button button-small button-outline" data-bid-modify-modal="${b.id}" type="button">Modify &amp; Send</button>
          <button class="button button-small button-primary" data-bid-quick-approve="${b.id}" type="button">Approve As-Is (${money(b.originalAmount)})</button>
        </div>
      </article>
    `).join('')
    : '<p class="empty-state">No buyer bids currently awaiting admin review.</p>';

  pendingBidsRoot.querySelectorAll('[data-bid-quick-approve]').forEach(btn =>
    btn.addEventListener('click', async () => {
      try {
        await request(`${api.bids}/${btn.dataset.bidQuickApprove}/admin-review`, {
          method: 'PUT',
          body: JSON.stringify({ action: 'approve' })
        });
        showFeedback('Bid approved and forwarded to listing seller.');
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );

  pendingBidsRoot.querySelectorAll('[data-bid-quick-reject]').forEach(btn =>
    btn.addEventListener('click', async () => {
      const reason = window.prompt('Reason for rejecting this bid (sent to buyer):');
      if (reason === null) return;
      try {
        await request(`${api.bids}/${btn.dataset.bidQuickReject}/admin-review`, {
          method: 'PUT',
          body: JSON.stringify({ action: 'reject', adminNotes: reason })
        });
        showFeedback('Bid declined and returned to buyer.');
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );

  pendingBidsRoot.querySelectorAll('[data-bid-modify-modal]').forEach(btn =>
    btn.addEventListener('click', () => {
      const bid = currentAdminBids.find(b => b.id === btn.dataset.bidModifyModal);
      if (bid) openAdminBidModal(bid);
    })
  );

  // 3. All Properties
  renderSimpleTable(
    'adminProperties',
    ['Property', 'Price', 'Location', 'Visibility Config', 'Status', 'Action'],
    properties.map(p => {
      const visLabels = [
        p.showPhotos ? 'Photos' : null,
        p.showAskingPrice ? 'Price' : null,
        p.showNegotiablePrice ? 'Floor Price' : null,
        p.showExactAddress ? 'Exact Addr' : 'Masked Addr'
      ].filter(Boolean).join(' · ');

      let action = '—';
      if (p.status === 'pending_approval') {
        action = `<button class="button button-small button-primary" data-review-modal="${p.id}">Review</button>`;
      } else if (['published', 'rejected'].includes(p.status)) {
        action = `<button class="text-button danger-text" data-property-status="${p.id}" data-next-status="inactive">Set Inactive</button>`;
      } else if (p.status === 'inactive') {
        action = `<button class="text-button" data-property-status="${p.id}" data-next-status="published">Republish</button>`;
      }

      return `
        <tr>
          <td><strong>${escapeHtml(p.title)}</strong></td>
          <td>${money(p.price)}</td>
          <td>${escapeHtml(p.city)}, ${escapeHtml(p.state)}</td>
          <td><span style="font-size: .75rem; color: var(--muted);">${visLabels}</span></td>
          <td>${statusTag(p.status)}</td>
          <td>${action}</td>
        </tr>
      `;
    })
  );

  document.querySelectorAll('[data-property-status]').forEach(btn =>
    btn.addEventListener('click', async () => {
      try {
        await request(`${api.properties}/${btn.dataset.propertyStatus}/status`, {
          method: 'PUT',
          body: JSON.stringify({ status: btn.dataset.nextStatus })
        });
        showFeedback(`Property status updated to ${btn.dataset.nextStatus}.`);
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );

  // 4. All Bids Pipeline Table
  renderSimpleTable(
    'adminBids',
    ['Property', 'Buyer', 'Original', 'Official Amount', 'Admin Adjusted', 'Status', 'Date'],
    bids.map(b => `
      <tr>
        <td>${escapeHtml(b.propertyTitle)}</td>
        <td>${escapeHtml(b.buyerName)}</td>
        <td>${money(b.originalAmount)}</td>
        <td><strong>${money(b.amount)}</strong></td>
        <td>${b.isModifiedByAdmin ? '<span class="status status-approved">YES</span>' : '—'}</td>
        <td>${statusTag(b.status)}</td>
        <td>${dateText(b.createdAt)}</td>
      </tr>
    `)
  );

  // 5. Agent Verification
  const agentRoot = document.getElementById('adminAgents');
  agentRoot.innerHTML = agents.length
    ? agents.map(x => {
      const actions = x.verificationStatus === 'approved'
        ? `<button class="text-button danger-text" data-verify-agent="${x.id}" data-agent-status="suspended">Suspend</button>`
        : `<button class="button button-small button-primary" data-verify-agent="${x.id}" data-agent-status="approved">Approve</button>
           <button class="text-button danger-text" data-verify-agent="${x.id}" data-agent-status="rejected">Reject</button>`;
      return `
        <article class="review-card">
          <div>
            <h3>${escapeHtml(x.agencyName || 'Independent Agent')}</h3>
            <p>${escapeHtml(x.name)} · ${escapeHtml(x.email)}</p>
            <p>${escapeHtml(x.licenseNumber || 'No license supplied')} · ${escapeHtml(x.city || '')}</p>
            ${statusTag(x.verificationStatus)}
          </div>
          <div class="button-stack">${actions}</div>
        </article>
      `;
    }).join('')
    : '<p class="empty-state">No agent accounts yet.</p>';

  document.querySelectorAll('[data-verify-agent]').forEach(btn =>
    btn.addEventListener('click', async () => {
      try {
        await request(`${api.admin}/agents/${btn.dataset.verifyAgent}/verification`, {
          method: 'PUT',
          body: JSON.stringify({ status: btn.dataset.agentStatus })
        });
        showFeedback(`Agent status updated to ${btn.dataset.agentStatus}.`);
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );

  renderTransactions('adminTransactions', transactions, true);

  // Profiles
  renderSimpleTable(
    'adminProfiles',
    ['Name', 'Email', 'Role', 'Status', 'Action'],
    profiles.map(x => `
      <tr>
        <td>${escapeHtml(x.fullName)}</td>
        <td>${escapeHtml(x.email)}</td>
        <td>${escapeHtml(x.role)}</td>
        <td><span class="status">${x.isActive ? 'ACTIVE' : 'INACTIVE'}</span></td>
        <td>${x.id === profile.id ? 'You' : `<button class="text-button ${x.isActive ? 'danger-text' : ''}" data-profile="${x.id}" data-active="${!x.isActive}">${x.isActive ? 'Deactivate' : 'Activate'}</button>`}</td>
      </tr>
    `)
  );

  document.querySelectorAll('[data-profile]').forEach(btn =>
    btn.addEventListener('click', async () => {
      try {
        await request(`${api.admin}/profiles/${btn.dataset.profile}/active`, {
          method: 'PUT',
          body: JSON.stringify({ isActive: btn.dataset.active === 'true' })
        });
        showFeedback('Profile access updated.');
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );

  // Audit
  renderSimpleTable(
    'adminAudit',
    ['Action', 'Entity', 'Record', 'By', 'When'],
    audit.map(x => `
      <tr>
        <td>${escapeHtml(x.action)}</td>
        <td>${escapeHtml(x.entityType)}</td>
        <td>${escapeHtml((x.entityId || '').slice(0, 8))}</td>
        <td>${escapeHtml(x.userName || 'System')}</td>
        <td>${dateText(x.createdAt)}</td>
      </tr>
    `)
  );
}

function renderTransactions(targetId, rows, isAdmin) {
  const isAgent = !isAdmin && profile.role === 'AGENT';
  renderSimpleTable(
    targetId,
    ['Property', 'Buyer', 'Agent', 'Stage', 'Agreed Price', 'Updated', ...(isAdmin || isAgent ? ['Action'] : [])],
    rows.map(row => {
      let action = '';
      if (isAdmin) {
        const adminNext = {
          initiated: 'admin_review',
          admin_review: 'negotiation',
          negotiation: 'agent_review',
          approved: 'agreement_pending',
          agreement_pending: 'completed'
        };
        if (adminNext[row.status]) {
          action = `<button class="button button-small button-primary" data-transaction="${row.id}" data-next-status="${adminNext[row.status]}">${adminNext[row.status].replace(/_/g, ' ')}</button>`;
        }
        if (!['completed', 'cancelled'].includes(row.status)) {
          action += ` <button class="text-button danger-text" data-transaction="${row.id}" data-next-status="cancelled">Cancel</button>`;
        }
      }
      if (isAgent && row.status === 'agent_review') {
        action = `
          <button class="button button-small button-primary" data-transaction="${row.id}" data-next-status="approved">Approve</button>
          <button class="button button-small button-outline" data-transaction="${row.id}" data-next-status="negotiation">Return</button>
        `;
      }
      return `
        <tr>
          <td>${escapeHtml(row.propertyTitle)}</td>
          <td>${escapeHtml(row.customerName)}</td>
          <td>${escapeHtml(row.agencyName)}</td>
          <td>${statusTag(row.status)}</td>
          <td>${row.agreedPrice == null ? '—' : money(row.agreedPrice)}</td>
          <td>${dateText(row.completedAt || row.startedAt)}</td>
          ${isAdmin || isAgent ? `<td>${action}</td>` : ''}
        </tr>
      `;
    })
  );
  bindTransactionButtons();
}

function bindTransactionButtons() {
  document.querySelectorAll('[data-transaction]').forEach(btn =>
    btn.addEventListener('click', async () => {
      try {
        const notes = profile.role === 'ADMIN'
          ? window.prompt(`Admin note for moving to ${btn.dataset.nextStatus}:`) || ''
          : null;
        await request(`${api.transactions}/${btn.dataset.transaction}/status`, {
          method: 'PUT',
          body: JSON.stringify({ status: btn.dataset.nextStatus, agreedPrice: null, adminNotes: notes })
        });
        showFeedback(`Transaction updated to ${btn.dataset.nextStatus}.`);
        await refreshDashboard();
      } catch (err) {
        showFeedback(err.message, true);
      }
    })
  );
}

async function refreshDashboard() {
  try {
    if (!profile) return;
    await loadNotifications();
    if (profile.role === 'CUSTOMER') await loadCustomer();
    if (profile.role === 'AGENT') await loadAgent();
    if (profile.role === 'ADMIN') await loadAdmin();
  } catch (err) {
    showFeedback(err.message, true);
  }
}

// Modal closing helpers
document.querySelectorAll('[data-close-modal]').forEach(btn => {
  btn.addEventListener('click', () => {
    const modal = document.getElementById(btn.dataset.closeModal);
    if (modal) modal.close();
  });
});

// Close on backdrop click
document.querySelectorAll('dialog.app-modal').forEach(dialog => {
  dialog.addEventListener('click', e => {
    if (e.target === dialog) dialog.close();
  });
});

// Auth Tabs
document.getElementById('loginTab').addEventListener('click', () => {
  document.getElementById('loginTab').classList.add('active');
  document.getElementById('registerTab').classList.remove('active');
  document.getElementById('loginForm').classList.remove('hidden');
  document.getElementById('registerForm').classList.add('hidden');
});

document.getElementById('registerTab').addEventListener('click', () => {
  document.getElementById('registerTab').classList.add('active');
  document.getElementById('loginTab').classList.remove('active');
  document.getElementById('registerForm').classList.remove('hidden');
  document.getElementById('loginForm').classList.add('hidden');
});

document.getElementById('registerRole').addEventListener('change', e => {
  document.getElementById('agentFields').classList.toggle('hidden', e.target.value !== 'AGENT');
});

// Quick Demo Logins (Horizon Realty)
const quickCredentials = {
  seller: { email: 'seller@horizon.local', password: 'Seller' },
  buyer: { email: 'buyer@horizon.local', password: 'Buyer' },
  admin: { email: 'admin@horizon.local', password: 'Admin' }
};

document.querySelectorAll('[data-quick-login]').forEach(btn => {
  btn.addEventListener('click', async () => {
    const roleKey = btn.dataset.quickLogin;
    const creds = quickCredentials[roleKey];
    if (!creds) return;
    try {
      showAuthFeedback(`Signing in as ${roleKey}…`);
      try {
        const user = await request(`${api.auth}/login`, {
          method: 'POST',
          body: JSON.stringify(creds)
        });
        await signIn(user);
      } catch {
        // Fallback for previous session domain if needed
        const fallbackCreds = {
          email: `${roleKey}@haven.local`,
          password: creds.password
        };
        const user = await request(`${api.auth}/login`, {
          method: 'POST',
          body: JSON.stringify(fallbackCreds)
        });
        await signIn(user);
      }
    } catch (err) {
      showAuthFeedback(err.message, true);
    }
  });
});

// Login
document.getElementById('loginForm').addEventListener('submit', async e => {
  e.preventDefault();
  const form = new FormData(e.currentTarget);
  const email = (form.get('email') || '').trim();
  const password = form.get('password') || '';
  try {
    const user = await request(`${api.auth}/login`, {
      method: 'POST',
      body: JSON.stringify({ email, password })
    });
    await signIn(user);
  } catch (err) {
    showAuthFeedback(err.message, true);
  }
});

// Register
document.getElementById('registerForm').addEventListener('submit', async e => {
  e.preventDefault();
  const form = new FormData(e.currentTarget);
  const payload = Object.fromEntries(form.entries());
  if (payload.email) payload.email = payload.email.trim();
  if (payload.fullName) payload.fullName = payload.fullName.trim();
  try {
    const user = await request(`${api.auth}/register`, {
      method: 'POST',
      body: JSON.stringify(payload)
    });
    e.currentTarget.reset();
    await signIn(user);
    showFeedback('Welcome! Your account has been activated.');
  } catch (err) {
    showAuthFeedback(err.message, true);
  }
});

// Sign Out
document.getElementById('logoutButton').addEventListener('click', async () => {
  try {
    await request(`${api.auth}/logout`, { method: 'POST' });
    profile = null;
    setAuthenticatedView(false);
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Refresh button
document.getElementById('refreshButton').addEventListener('click', refreshDashboard);

// Agent Sub-nav Tabs
const agentListingsTab = document.getElementById('agentViewListingsTab');
const agentMarketTab = document.getElementById('agentViewMarketplaceTab');
if (agentListingsTab && agentMarketTab) {
  agentListingsTab.addEventListener('click', () => {
    agentListingsTab.classList.add('active');
    agentMarketTab.classList.remove('active');
    document.getElementById('agentMainView').classList.remove('hidden');
    document.getElementById('agentMarketplaceView').classList.add('hidden');
  });
  agentMarketTab.addEventListener('click', () => {
    agentMarketTab.classList.add('active');
    agentListingsTab.classList.remove('active');
    document.getElementById('agentMainView').classList.add('hidden');
    document.getElementById('agentMarketplaceView').classList.remove('hidden');
  });
}

// Image Upload Dropzone & Selection Handling
let propertySelectedFiles = [];

const uploadDropZone = document.getElementById('uploadDropZone');
const propertyImagesInput = document.getElementById('propertyImagesInput');
const imagePreviewContainer = document.getElementById('imagePreviewContainer');
const imagePreviewList = document.getElementById('imagePreviewList');
const previewCount = document.getElementById('previewCount');
const clearImagesBtn = document.getElementById('clearImagesBtn');

if (propertyImagesInput && uploadDropZone) {
  propertyImagesInput.addEventListener('change', e => {
    addFiles(Array.from(e.target.files));
  });

  ['dragenter', 'dragover'].forEach(eventName => {
    uploadDropZone.addEventListener(eventName, e => {
      e.preventDefault();
      uploadDropZone.classList.add('dragover');
    });
  });

  ['dragleave', 'drop'].forEach(eventName => {
    uploadDropZone.addEventListener(eventName, e => {
      e.preventDefault();
      uploadDropZone.classList.remove('dragover');
    });
  });

  uploadDropZone.addEventListener('drop', e => {
    if (e.dataTransfer?.files) {
      addFiles(Array.from(e.dataTransfer.files));
    }
  });

  clearImagesBtn?.addEventListener('click', () => {
    propertySelectedFiles = [];
    renderImagePreviews();
  });
}

function addFiles(files) {
  const validFiles = files.filter(f => f.type.startsWith('image/'));
  if (!validFiles.length) return;
  propertySelectedFiles = propertySelectedFiles.concat(validFiles);
  renderImagePreviews();
}

function renderImagePreviews() {
  if (!imagePreviewContainer || !imagePreviewList) return;
  imagePreviewList.innerHTML = '';

  if (propertySelectedFiles.length === 0) {
    imagePreviewContainer.classList.add('hidden');
    return;
  }

  imagePreviewContainer.classList.remove('hidden');
  if (previewCount) previewCount.textContent = propertySelectedFiles.length;

  propertySelectedFiles.forEach((file, index) => {
    const item = document.createElement('div');
    item.className = 'image-preview-item';

    const img = document.createElement('img');
    img.src = URL.createObjectURL(file);
    img.alt = file.name;
    img.onload = () => URL.revokeObjectURL(img.src);

    const removeBtn = document.createElement('button');
    removeBtn.type = 'button';
    removeBtn.className = 'preview-remove-btn';
    removeBtn.innerHTML = '&times;';
    removeBtn.title = 'Remove photo';
    removeBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      propertySelectedFiles.splice(index, 1);
      renderImagePreviews();
    });

    item.appendChild(img);
    item.appendChild(removeBtn);

    if (index === 0) {
      const primaryTag = document.createElement('span');
      primaryTag.className = 'preview-primary-tag';
      primaryTag.textContent = 'Primary';
      item.appendChild(primaryTag);
    }

    imagePreviewList.appendChild(item);
  });
}

// Minimalist Property Post Form (Agent)
document.getElementById('propertyForm').addEventListener('submit', async e => {
  e.preventDefault();
  const form = new FormData(e.currentTarget);
  const numberOrNull = name => form.get(name) ? Number(form.get(name)) : null;
  const submitter = e.submitter;

  const property = {
    title: String(form.get('title') || '').trim(),
    propertyType: String(form.get('propertyType') || 'Apartment').trim(),
    listingType: String(form.get('listingType') || 'SALE').trim(),
    price: Number(form.get('price')),
    negotiablePrice: numberOrNull('negotiablePrice'),
    address: String(form.get('address') || '').trim(),
    city: String(form.get('city') || '').trim(),
    state: String(form.get('state') || '').trim(),
    description: form.get('description') || null,
    status: submitter?.dataset.propertyStatus || 'pending_approval'
  };

  try {
    const created = await request(api.properties, {
      method: 'POST',
      body: JSON.stringify(property)
    });

    // Upload selected image files if any
    if (propertySelectedFiles.length > 0) {
      const uploadData = new FormData();
      propertySelectedFiles.forEach(file => {
        uploadData.append('files', file);
      });

      await fetch(`${api.properties}/${created.id}/upload-images`, {
        method: 'POST',
        body: uploadData,
        credentials: 'same-origin'
      });
    }

    e.currentTarget.reset();
    propertySelectedFiles = [];
    renderImagePreviews();

    showFeedback(
      property.status === 'draft'
        ? 'Property saved as draft.'
        : 'Listing submitted for Admin approval & field visibility review!'
    );
    await refreshDashboard();
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Buyer Place Bid Modal Submission
document.getElementById('submitBidForm').addEventListener('submit', async e => {
  e.preventDefault();
  const form = new FormData(e.currentTarget);
  const propertyId = form.get('propertyId');
  const amount = Number(form.get('amount'));
  const message = form.get('message');

  try {
    await request(api.bids, {
      method: 'POST',
      body: JSON.stringify({ propertyId, amount, message })
    });
    document.getElementById('bidModal').close();
    showFeedback('Your bid has been submitted to the platform Administrator for review!');
    await refreshDashboard();
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Admin Review Listing & Set Field Visibility Form
document.getElementById('adminListingForm').addEventListener('submit', async e => {
  e.preventDefault();
  const form = new FormData(e.currentTarget);
  const propertyId = form.get('propertyId');

  const payload = {
    approved: true,
    showPhotos: form.get('showPhotos') === 'on',
    showAskingPrice: form.get('showAskingPrice') === 'on',
    showNegotiablePrice: form.get('showNegotiablePrice') === 'on',
    showExactAddress: form.get('showExactAddress') === 'on',
    showDescription: form.get('showDescription') === 'on',
    showSellerContact: form.get('showSellerContact') === 'on',
    adminComment: form.get('adminComment') || null
  };

  try {
    await request(`${api.properties}/${propertyId}/approval`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
    document.getElementById('adminListingModal').close();
    showFeedback('Listing approved and published with configured field visibility!');
    await refreshDashboard();
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Admin Reject Listing
document.getElementById('adminListingRejectBtn').addEventListener('click', async () => {
  const propertyId = document.getElementById('adminListingId').value;
  const adminComment = document.getElementById('adminListingComment').value;
  try {
    await request(`${api.properties}/${propertyId}/approval`, {
      method: 'PUT',
      body: JSON.stringify({ approved: false, adminComment })
    });
    document.getElementById('adminListingModal').close();
    showFeedback('Listing was rejected and seller has been notified.');
    await refreshDashboard();
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Admin Bid Form (Modify & Send)
document.getElementById('adminBidForm').addEventListener('submit', async e => {
  e.preventDefault();
  const form = new FormData(e.currentTarget);
  const bidId = form.get('bidId');
  const modifiedAmount = Number(form.get('modifiedAmount'));
  const adminNotes = form.get('adminNotes');

  try {
    await request(`${api.bids}/${bidId}/admin-review`, {
      method: 'PUT',
      body: JSON.stringify({
        action: 'modify',
        modifiedAmount,
        adminNotes
      })
    });
    document.getElementById('adminBidModal').close();
    showFeedback(`Adjusted offer of ${money(modifiedAmount)} forwarded to the seller.`);
    await refreshDashboard();
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Admin Bid Approve As-Is button in modal
document.getElementById('adminBidApproveAsIsBtn').addEventListener('click', async () => {
  const bidId = document.getElementById('adminBidId').value;
  const adminNotes = document.getElementById('adminBidNotes').value;
  try {
    await request(`${api.bids}/${bidId}/admin-review`, {
      method: 'PUT',
      body: JSON.stringify({ action: 'approve', adminNotes })
    });
    document.getElementById('adminBidModal').close();
    showFeedback('Offer approved as-is and sent to seller.');
    await refreshDashboard();
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Admin Bid Reject button in modal
document.getElementById('adminBidRejectBtn').addEventListener('click', async () => {
  const bidId = document.getElementById('adminBidId').value;
  const adminNotes = document.getElementById('adminBidNotes').value;
  try {
    await request(`${api.bids}/${bidId}/admin-review`, {
      method: 'PUT',
      body: JSON.stringify({ action: 'reject', adminNotes })
    });
    document.getElementById('adminBidModal').close();
    showFeedback('Offer declined and returned to buyer.');
    await refreshDashboard();
  } catch (err) {
    showFeedback(err.message, true);
  }
});

// Initialize session check
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
  } catch {
    setAuthenticatedView(false);
  }
})();