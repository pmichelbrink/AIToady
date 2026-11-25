"use strict";

const $ = (id) => document.getElementById(id);
const files = { first: null, reverse: null };
let sessionId = null;
let timerHandle = null;

function setupDrop(kind, onChange) {
  const drop = $(`drop-${kind}`);
  const input = $(`file-${kind}`);
  const select = (file) => {
    if (!file || !file.type.startsWith("image/")) return;
    files[kind] = file;
    const preview = $(`preview-${kind}`);
    preview.src = URL.createObjectURL(file);
    preview.hidden = false;
    $(`hint-${kind}`).hidden = true;
    onChange();
  };
  input.addEventListener("change", () => select(input.files[0]));
  drop.addEventListener("dragover", (e) => { e.preventDefault(); drop.classList.add("over"); });
  drop.addEventListener("dragleave", () => drop.classList.remove("over"));
  drop.addEventListener("drop", (e) => {
    e.preventDefault();
    drop.classList.remove("over");
    select(e.dataTransfer.files[0]);
  });
}

function setBusy(busy) {
  $("busy").hidden = !busy;
  for (const id of ["btn-identify", "btn-reverse", "btn-skip"]) $(id).disabled = busy;
  if (!busy) {
    $("btn-identify").disabled = !files.first;
    $("btn-reverse").disabled = !files.reverse;
  }
  clearInterval(timerHandle);
  if (busy) {
    const start = Date.now();
    $("timer").textContent = "0";
    timerHandle = setInterval(() => { $("timer").textContent = Math.round((Date.now() - start) / 1000); }, 1000);
  }
}

function showError(message) {
  $("error").textContent = message;
  $("error").hidden = !message;
}

async function post(url, formData) {
  setBusy(true);
  showError("");
  try {
    const response = await fetch(url, { method: "POST", body: formData });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.detail || `Request failed (${response.status})`);
    return body;
  } finally {
    setBusy(false);
  }
}

function addRow(table, label, value) {
  if (value === undefined || value === null || value === "" || (Array.isArray(value) && !value.length)) return;
  const tr = table.insertRow();
  tr.insertCell().textContent = label;
  tr.insertCell().textContent = Array.isArray(value) ? value.join("; ") : value;
}

function render(data) {
  $("result").hidden = false;
  $("result-summary").textContent = data.summary || "Result";
  $("result-message").textContent = `${data.message} (${data.elapsed_seconds}s, ${data.model})`;

  const table = $("result-table");
  table.replaceChildren();
  const coin = data.coin;
  if (coin) {
    addRow(table, "Coin", coin.name);
    addRow(table, "Person on obverse (heads)", coin.obverse_person);
    addRow(table, "Country / issuer", coin.country_or_issuer);
    addRow(table, "Denomination", coin.denomination);
    addRow(table, "Year", coin.year);
    addRow(table, "Mint mark", coin.mint_mark);
    addRow(table, "Obverse", coin.obverse_description);
    addRow(table, "Reverse", coin.reverse_description);
    addRow(table, "Inscriptions", coin.inscriptions);
    addRow(table, "Composition", coin.composition);
    addRow(table, "Series / catalogue", coin.series_or_catalog_reference);
    addRow(table, "Notes", coin.historical_notes);
    addRow(table, "Confidence", coin.confidence);
    addRow(table, "Uncertainties", coin.uncertainties);
  } else {
    const g = data.general;
    addRow(table, "Identification", g.identification);
    if (data.front_side) addRow(table, "Portrait", data.front_side.portrait_person);
    if (data.front_side) addRow(table, "Inscriptions", data.front_side.legend_text);
    addRow(table, "Category", g.category);
    addRow(table, "Description", g.description);
    addRow(table, "People", g.people_depicted);
    addRow(table, "Visible text", g.visible_text);
    addRow(table, "Features", g.notable_features);
    addRow(table, "Confidence", g.confidence);
  }

  const links = $("result-links");
  links.replaceChildren();
  for (const link of data.links) {
    const a = document.createElement("a");
    a.href = link.url;
    a.textContent = link.title;
    a.target = "_blank";
    a.rel = "noopener noreferrer";
    const li = document.createElement("li");
    li.appendChild(a);
    links.appendChild(li);
  }
  $("result-raw").textContent = JSON.stringify(data, null, 2);
}

function handle(data) {
  render(data);
  if (data.status === "needs_reverse") {
    sessionId = data.session_id;
    $("reverse-message").textContent = data.message;
    $("step-reverse").hidden = false;
    $("step-reverse").scrollIntoView({ behavior: "smooth" });
  } else {
    sessionId = null;
    $("step-reverse").hidden = true;
  }
}

async function identifyFirst() {
  const form = new FormData();
  form.append("image", files.first);
  try { handle(await post("/api/identify", form)); } catch (e) { showError(e.message); }
}

async function identifyReverse() {
  const form = new FormData();
  form.append("image", files.reverse);
  try { handle(await post(`/api/identify/${encodeURIComponent(sessionId)}/reverse`, form)); }
  catch (e) { showError(e.message); }
}

async function skipReverse() {
  try { handle(await post(`/api/identify/${encodeURIComponent(sessionId)}/finish`, new FormData())); }
  catch (e) { showError(e.message); }
}

async function checkHealth() {
  try {
    const h = await (await fetch("/api/health")).json();
    if (!h.ollama_reachable) $("health").textContent = h.error;
    else if (!h.model_installed) $("health").textContent = `Model ${h.model} is not installed. Run: ollama pull ${h.model}`;
    else $("health").textContent = `Using local model ${h.model}`;
  } catch {
    $("health").textContent = "Could not check model status.";
  }
}

setupDrop("first", () => { $("btn-identify").disabled = false; });
setupDrop("reverse", () => { $("btn-reverse").disabled = false; });
$("btn-identify").addEventListener("click", identifyFirst);
$("btn-reverse").addEventListener("click", identifyReverse);
$("btn-skip").addEventListener("click", skipReverse);
$("btn-reset").addEventListener("click", () => window.location.reload());
checkHealth();
