// Monolith Store — UI controller for the single-process monolith.
// Talks to the API layer (/api/*) only over HTTP.
//
// Routes (hash based):
//   #/catalog     -> product grid + filters
//   #/orders      -> order list + detail modal
//   #/inventory   -> stock management

const $ = (id) => document.getElementById(id);
const money = (v) => Number(v).toLocaleString("en-US", { style: "currency", currency: "BRL" });
const ENTITIES = {
  "&": "&#" + 38 + ";",
  "<": "&#" + 60 + ";",
  ">": "&#" + 62 + ";",
  '"': "&#" + 34 + ";",
  "'": "&#" + 39 + ";"
};
const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ENTITIES[c]);

const state = {
  route: "catalog",
  products: [],
  categories: [],
  category: "",
  q: "",
  cartId: localStorage.getItem("cartId") || (crypto.randomUUID?.() ?? String(Date.now())),
  cart: { items: [], subtotal: 0, totalItems: 0 },
  orders: [],
  inventory: []
};
localStorage.setItem("cartId", state.cartId);

// ---------------------------------------------------------------- helpers
async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json" },
    ...options,
    body: options.body ? JSON.stringify(options.body) : undefined
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    const msg = data.error || data.detail || `Error ${res.status}`;
    throw new Error(msg);
  }
  return data;
}

function toast(msg, kind = "") {
  const el = document.createElement("div");
  el.className = `toast ${kind}`;
  el.textContent = msg;
  $("toasts").appendChild(el);
  setTimeout(() => el.remove(), 3200);
}

const ORDER_STATUS = { Paid: "Paid", Pending: "Pending", Shipped: "Shipped", Cancelled: "Cancelled" };

// ---------------------------------------------------------------- cart
async function loadCart() {
  state.cart = await api(`/api/cart/${state.cartId}`);
  renderCartBadge();
  renderCart();
}

function renderCartBadge() {
  $("cartBadge").textContent = state.cart.totalItems ?? 0;
}

function renderCart() {
  const body = $("cartBody");
  const foot = $("cartFoot");
  const items = state.cart.items ?? [];

  if (!items.length) {
    body.innerHTML = `<div class="empty"><span class="big">🛒</span>Your cart is empty.<br>Add products from the catalog.</div>`;
    foot.innerHTML = `<button class="btn wide" onclick="closeCart()">Continue shopping</button>`;
    return;
  }

  body.innerHTML = items.map(i => `
    <div class="cart-line">
      <div class="cart-emoji">${i.emoji}</div>
      <div class="cart-info">
        <div class="cart-name">${esc(i.name)}</div>
        <div class="cart-unit">${money(i.price)} · stock: ${i.stock}</div>
        <div class="qty">
          <button onclick="setQty(${i.productId}, ${i.quantity - 1})">−</button>
          <span>${i.quantity}</span>
          <button onclick="setQty(${i.productId}, ${i.quantity + 1})">+</button>
        </div>
      </div>
      <div class="cart-right">
        <div class="cart-sub">${money(i.subtotal)}</div>
        <button class="link-danger" onclick="removeLine(${i.productId})">remove</button>
      </div>
    </div>`).join("");

  foot.innerHTML = `
    <div class="total-row"><span>Subtotal (${state.cart.totalItems} items)</span><span>${money(state.cart.subtotal)}</span></div>
    <div class="total-row"><span>Shipping</span><span>Free</span></div>
    <div class="total-row grand"><span>Total</span><span>${money(state.cart.subtotal)}</span></div>
    <button class="btn primary wide" onclick="openCheckout()">Checkout</button>
    <button class="btn ghost wide" style="margin-top:8px" onclick="clearCart()">Empty cart</button>`;
}

async function addToCart(productId) {
  try {
    state.cart = await api(`/api/cart/${state.cartId}/items`, { method: "POST", body: { productId, quantity: 1 } });
    renderCartBadge(); renderCart();
    toast("Added to cart ✓", "ok");
    openCart();
  } catch (e) { toast(e.message, "err"); }
}

async function setQty(productId, quantity) {
  try {
    state.cart = quantity <= 0
      ? await api(`/api/cart/${state.cartId}/items/${productId}`, { method: "DELETE" })
      : await api(`/api/cart/${state.cartId}/items/${productId}`, { method: "PUT", body: { quantity } });
    renderCartBadge(); renderCart();
  } catch (e) { toast(e.message, "err"); }
}

async function removeLine(productId) {
  state.cart = await api(`/api/cart/${state.cartId}/items/${productId}`, { method: "DELETE" });
  renderCartBadge(); renderCart();
}

async function clearCart() {
  state.cart = await api(`/api/cart/${state.cartId}`, { method: "DELETE" });
  renderCartBadge(); renderCart();
}

function openCart() { $("cartDrawer").classList.add("show"); $("overlay").classList.add("show"); }
function closeCart() { $("cartDrawer").classList.remove("show"); $("overlay").classList.remove("show"); }
window.closeCart = closeCart;

// ---------------------------------------------------------------- catalog
async function renderCatalog() {
  const view = $("view");
  view.innerHTML = `
    <div class="page-head">
      <div><h1>Catalog</h1><p>Catalog, cart, orders, payments and inventory — one process, one database.</p></div>
      <input class="search" id="searchBox" placeholder="Search product, SKU…" value="${esc(state.q)}" />
    </div>
    <div class="filters" id="filters"></div>
    <div class="grid" id="grid"></div>`;

  $("searchBox").addEventListener("input", (e) => { state.q = e.target.value; drawGrid(); });

  const cats = await api("/api/catalog/categories");
  state.categories = cats;
  drawFilters();
  await drawGrid();
}

function drawFilters() {
  $("filters").innerHTML =
    [`<button class="chip ${state.category === "" ? "active" : ""}" data-cat="">All</button>`]
      .concat(state.categories.map(c =>
        `<button class="chip ${state.category === c ? "active" : ""}" data-cat="${esc(c)}">${esc(c)}</button>`))
      .join("");

  $("filters").querySelectorAll(".chip").forEach(btn =>
    btn.addEventListener("click", () => { state.category = btn.dataset.cat; drawFilters(); drawGrid(); }));
}

async function drawGrid() {
  const params = new URLSearchParams();
  if (state.q) params.set("q", state.q);
  if (state.category) params.set("category", state.category);

  const [products, stockMap] = await Promise.all([
    api(`/api/catalog/products?${params}`),
    api("/api/inventory")
  ]);
  state.products = products;
  const stock = Object.fromEntries(stockMap.map(i => [i.productId, i]));

  $("grid").innerHTML = products.length ? products.map(p => {
    const s = stock[p.id]?.quantity ?? 0;
    const tag = s === 0 ? `<span class="stock-tag out">Out of stock</span>`
      : s <= 8 ? `<span class="stock-tag low">Only ${s} left</span>`
        : `<span class="stock-tag">${s} in stock</span>`;
    return `
      <article class="card">
        <div class="card-img">${p.emoji}</div>
        <div class="card-body">
          <div class="card-cat">${esc(p.category)}</div>
          <div class="card-name">${esc(p.name)}</div>
          <div class="card-desc">${esc(p.description)}</div>
          <div class="card-foot">
            <div><div class="price">${money(p.price)}</div>${tag}</div>
            <button class="btn primary sm" ${s === 0 ? "disabled" : ""} onclick="addToCart(${p.id})">
              ${s === 0 ? "Out of stock" : "+ Cart"}
            </button>
          </div>
        </div>
      </article>`;
  }).join("") : `<div class="empty" style="grid-column:1/-1"><span class="big">🔍</span>No products found.</div>`;
}

// ---------------------------------------------------------------- checkout
function openCheckout() {
  if (!state.cart.items?.length) { toast("Your cart is empty.", "err"); return; }
  closeCart();
  $("checkoutSummary").innerHTML = `
    <div class="total-row"><span>Items</span><span>${state.cart.totalItems}</span></div>
    <div class="total-row grand" style="margin-bottom:0"><span>Total to pay</span><span>${money(state.cart.subtotal)}</span></div>`;
  $("checkoutError").hidden = true;
  $("checkoutModal").classList.add("show");
}
window.openCheckout = openCheckout;

$("checkoutForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  const form = new FormData(e.target);
  const btn = $("payBtn");
  btn.disabled = true; btn.textContent = "Processing…";
  $("checkoutError").hidden = true;

  try {
    const order = await api("/api/orders/checkout", {
      method: "POST",
      body: {
        cartId: state.cartId,
        customerName: form.get("customerName"),
        email: form.get("email"),
        address: form.get("address"),
        city: form.get("city"),
        zip: form.get("zip"),
        paymentMethod: form.get("paymentMethod"),
        simulateFailure: form.get("simulateFailure") === "on"
      }
    });
    $("checkoutModal").classList.remove("show");
    e.target.reset();
    await loadCart();
    if (order.status === "Cancelled") {
      toast("Payment declined — order cancelled, stock untouched.", "err");
    } else {
      toast(`Order ${order.code} created and paid! ✓`, "ok");
    }
    if (location.hash === "#/orders") await route();
    else location.hash = "#/orders";
  } catch (err) {
    $("checkoutError").textContent = err.message;
    $("checkoutError").hidden = false;
  } finally {
    btn.disabled = false; btn.textContent = "Pay now";
  }
});

// ---------------------------------------------------------------- orders
async function renderOrders() {
  $("view").innerHTML = `
    <div class="page-head"><div><h1>Orders</h1><p>Order and payment history of the monolith.</p></div></div>
    <div id="ordersArea"></div>`;

  const orders = await api("/api/orders");
  state.orders = orders;

  if (!orders.length) {
    $("ordersArea").innerHTML = `<div class="empty"><span class="big">📦</span>No orders yet.</div>`;
    return;
  }

  $("ordersArea").innerHTML = `
    <div class="table-wrap"><table class="table">
      <thead><tr><th>Order</th><th>Customer</th><th>Items</th><th>Total</th><th>Payment</th><th>Status</th><th></th></tr></thead>
      <tbody>${orders.map(o => `
        <tr>
          <td><strong>${o.code}</strong><br><span style="color:var(--muted);font-size:12px">${new Date(o.createdAt).toLocaleString("en-US")}</span></td>
          <td>${esc(o.customerName)}<br><span style="color:var(--muted);font-size:12px">${esc(o.email)}</span></td>
          <td>${o.items.length}</td>
          <td><strong>${money(o.total)}</strong></td>
          <td>${o.payment
            ? `<span class="pill ${o.payment.status === "Approved" ? "paid" : "cancelled"}">${o.payment.status === "Approved" ? "Approved" : "Declined"}</span>
               <br><span style="color:var(--muted);font-size:11px">${esc(o.payment.method)}</span>`
            : "—"}</td>
          <td><span class="pill ${o.status.toLowerCase()}">${ORDER_STATUS[o.status] ?? o.status}</span></td>
          <td><button class="btn sm" onclick="openOrder(${o.id})">Details</button></td>
        </tr>`).join("")}
      </tbody></table></div>`;
}

async function openOrder(id) {
  const o = await api(`/api/orders/${id}`);
  $("orderModalTitle").textContent = `Order ${o.code}`;
  $("orderModalBody").innerHTML = `
    <div class="o-meta">
      <div><b>Customer</b>${esc(o.customerName)}</div>
      <div><b>Status</b><span class="pill ${o.status.toLowerCase()}">${ORDER_STATUS[o.status] ?? o.status}</span></div>
      <div><b>Address</b>${esc(o.address)}, ${esc(o.city)} — ${esc(o.zip)}</div>
      <div><b>Payment</b>${o.payment
        ? (o.payment.status === "Approved"
            ? `Approved · ${esc(o.payment.transactionId)}`
            : `Declined · ${esc(o.payment.reason ?? "")}`)
        : "—"}</div>
    </div>
    <div>${o.items.map(i => `
      <div class="o-item">
        <span>${i.emoji}</span>
        <span>${esc(i.name)}<br><small style="color:var(--muted)">${money(i.unitPrice)} × ${i.quantity}</small></span>
        <span class="q">${money(i.unitPrice * i.quantity)}</span>
      </div>`).join("")}
    </div>
    <div class="total-row grand"><span>Total</span><span>${money(o.total)}</span></div>`;
  $("orderModal").classList.add("show");
}
window.openOrder = openOrder;

// ---------------------------------------------------------------- inventory
async function renderInventory() {
  $("view").innerHTML = `
    <div class="page-head"><div><h1>Inventory</h1><p>Inventory Service — the only writer of stock quantities.</p></div></div>
    <div class="stat-grid" id="stats"></div>
    <div id="invArea"></div>`;

  const items = await api("/api/inventory");
  state.inventory = items;

  const totalUnits = items.reduce((s, i) => s + i.quantity, 0);
  const low = items.filter(i => i.quantity <= i.reorderLevel).length;
  const out = items.filter(i => i.quantity === 0).length;

  $("stats").innerHTML = `
    <div class="stat"><div class="k">Products</div><div class="v">${items.length}</div></div>
    <div class="stat"><div class="k">Total units</div><div class="v good">${totalUnits}</div></div>
    <div class="stat"><div class="k">Low stock</div><div class="v ${low ? "warn" : ""}">${low}</div></div>
    <div class="stat"><div class="k">Out of stock</div><div class="v ${out ? "warn" : ""}">${out}</div></div>`;

  $("invArea").innerHTML = `
    <div class="table-wrap"><table class="table">
      <thead><tr><th>Product</th><th>SKU</th><th>Quantity</th><th>Reorder level</th><th>Actions</th></tr></thead>
      <tbody>${items.map(i => `
        <tr>
          <td>${i.product.emoji} <strong>${esc(i.product.name)}</strong></td>
          <td>${esc(i.product.sku)}</td>
          <td>
            <span class="stock-tag ${i.quantity === 0 ? "out" : i.quantity <= i.reorderLevel ? "low" : ""}">${i.quantity} units</span>
          </td>
          <td>
            <div class="stepper">
              <input type="number" min="0" id="qty-${i.productId}" value="${i.quantity}" />
              <span style="color:var(--muted);font-size:12px">min. ${i.reorderLevel}</span>
            </div>
          </td>
          <td>
            <div style="display:flex;gap:6px;flex-wrap:wrap">
              <button class="btn sm primary" onclick="saveQty(${i.productId})">Save</button>
              <button class="btn sm" onclick="restock(${i.productId}, 10)">+10</button>
              <button class="btn sm" onclick="restock(${i.productId}, 50)">+50</button>
              <button class="btn sm danger" onclick="restock(${i.productId}, -10)">−10</button>
            </div>
          </td>
        </tr>`).join("")}
      </tbody></table></div>`;
}

async function saveQty(productId) {
  const quantity = Number($(`qty-${productId}`).value);
  try {
    await api(`/api/inventory/${productId}/quantity`, { method: "PUT", body: { quantity } });
    toast("Stock updated ✓", "ok");
    renderInventory();
  } catch (e) { toast(e.message, "err"); }
}

async function restock(productId, delta) {
  try {
    await api(`/api/inventory/${productId}/restock`, { method: "POST", body: { delta } });
    toast(delta > 0 ? `+${delta} units ✓` : `${delta} units ✓`, "ok");
    renderInventory();
  } catch (e) { toast(e.message, "err"); }
}
window.saveQty = saveQty;
window.restock = restock;

// ---------------------------------------------------------------- router
async function route() {
  const hash = location.hash.replace("#/", "") || "catalog";
  state.route = ["catalog", "orders", "inventory"].includes(hash) ? hash : "catalog";

  document.querySelectorAll(".nav a").forEach(a =>
    a.classList.toggle("active", a.dataset.nav === state.route));

  try {
    if (state.route === "catalog") await renderCatalog();
    else if (state.route === "orders") await renderOrders();
    else await renderInventory();
  } catch (e) {
    $("view").innerHTML = `<div class="empty"><span class="big">⚠️</span>${esc(e.message)}</div>`;
  }
}

// ---------------------------------------------------------------- wiring
$("cartBtn").addEventListener("click", openCart);
$("closeCart").addEventListener("click", closeCart);
$("overlay").addEventListener("click", closeCart);
document.querySelectorAll("[data-close-modal]").forEach(btn =>
  btn.addEventListener("click", () => $(btn.dataset.closeModal).classList.remove("show")));
document.querySelectorAll(".modal-wrap").forEach(m =>
  m.addEventListener("click", (e) => { if (e.target === m) m.classList.remove("show"); }));
window.addEventListener("hashchange", route);

// expose for inline handlers
Object.assign(window, { addToCart, setQty, removeLine, clearCart });

loadCart().catch(() => {}).finally(route);
