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
  $("result-extra").replaceChildren();
  const coin = data.coin;
  const roman = data.roman_coin;
  if (roman) {
    addRow(table, "Ruler", roman.ruler);
    addRow(table, "Reign", roman.ruler_reign);
    addRow(table, "Catalogue reference", roman.catalog_reference);
    addRow(table, "Denomination", roman.denomination);
    addRow(table, "Metal", roman.metal);
    addRow(table, "Mint", roman.mint);
    addRow(table, "Mint mark", roman.mint_mark);
    addRow(table, "Date", roman.date_range);
    addRow(table, "Obverse legend", roman.obverse_legend);
    addRow(table, "Obverse expanded", roman.obverse_legend_expanded);
    addRow(table, "Obverse translation", roman.obverse_translation);
    addRow(table, "Obverse design", roman.obverse_description);
    addRow(table, "Reverse legend", roman.reverse_legend);
    addRow(table, "Reverse expanded", roman.reverse_legend_expanded);
    addRow(table, "Reverse translation", roman.reverse_translation);
    addRow(table, "Reverse design", roman.reverse_description);
    addRow(table, "Notes", roman.historical_notes);
    addRow(table, "Confidence", roman.confidence);
    addRow(table, "Uncertainties", roman.uncertainties);
    renderCandidates(data);
  } else if (data.roman_front) {
    const s = data.roman_front;
    addRow(table, "Side shown", s.side);
    addRow(table, "Portrait", s.portrait_person);
    addRow(table, "Legend as read", s.legend_fragments);
    addRow(table, "Legend", s.legend_reconstructed);
    addRow(table, "Expanded", s.legend_expanded);
    addRow(table, "Translation", s.legend_translation);
    addRow(table, "Design", s.design_description);
    addRow(table, "Reverse type", s.reverse_type);
    addRow(table, "Denomination", s.denomination);
  } else if (coin) {
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

function renderCandidates(data) {
  const extra = $("result-extra");
  if (data.catalog_note) {
    const note = document.createElement("p");
    note.className = "muted";
    note.textContent = data.catalog_note;
    extra.appendChild(note);
  }
  if (!data.catalog_candidates || !data.catalog_candidates.length) return;
  const heading = document.createElement("h3");
  heading.textContent = "OCRE catalogue candidates";
  extra.appendChild(heading);
  const matchedId = data.roman_coin ? data.roman_coin.matched_catalog_id : "";
  const list = document.createElement("ol");
  for (const c of data.catalog_candidates) {
    const li = document.createElement("li");
    if (c.id === matchedId) li.className = "matched";
    const a = document.createElement("a");
    a.href = c.url;
    a.textContent = c.label;
    a.target = "_blank";
    a.rel = "noopener noreferrer";
    li.appendChild(a);
    const details = [c.denomination, c.mint, c.date_range].filter(Boolean).join(", ");
    li.appendChild(document.createTextNode(
      `${c.id === matchedId ? " (match)" : ""} ${details}: ${c.obverse_legend} / ${c.reverse_legend}. ${c.reverse_description}`));
    list.appendChild(li);
  }
  extra.appendChild(list);
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
