"use strict";

const MAX_GAMES = 100000;
const ISOLATED_AFTER_MS = 5000;

// French uses the singular for 0 and 1; numbers arrive already formatted.
const frPlural = (formatted, word) => (/^[01]$/.test(formatted) ? word : `${word}s`);

const STRINGS = {
  fr: {
    appName: "Compteur de coffres",
    total: "Total estimé",
    inChests: "en coffres",
    upToDate: "À jour",
    measured: "Lu dans les replays",
    estimated: "Estimé",
    gamesLabel: "Parties sans replay",
    gamesHint: "Jouées avant l'installation, ou dont le replay a été supprimé.",
    less: "Retirer une partie",
    more: "Ajouter une partie",
    restart: "Redémarrer",
    noGames: "Aucune partie lue pour l'instant.",
    measuredDetail: (games, average) => `Sur ${games} ${frPlural(games, "partie")}, ${average} en moyenne.`,
    estimatedDetail: (missed, games, broken, basis, perMatch) => {
      const pace = basis === null ? "" : basis === "1" ? ", au rythme de ta première partie" : `, au rythme de tes ${basis} premières parties`;
      const average = perMatch === null ? "" : `, environ ${perMatch} par partie`;
      return `${missed} pour ${games} ${frPlural(games, "partie")} sans replay${pace}. ${broken} ${frPlural(broken, "cassé")} sans être ${frPlural(broken, "ouvert")}${average}.`;
    },
    scanning: "Analyse des replays en cours.",
    inProgress: "La partie en cours sera comptée à la fin.",
    failed: (n) => `Replays illisibles\u00a0: ${n}.`,
    errorRead: "La lecture du dossier a échoué. Nouvel essai dans 30 secondes.",
    errorSave: "L'enregistrement a échoué. Réessaie dans un instant.",
    errorInput: (max) => `Entre un nombre entre 0 et ${max}.`,
    update: (version) => `La version ${version} est prête.`,
    loading: "Chargement.",
    cut: (n) => (n === "1" ? "1 partie interrompue, non comptée." : `${n} parties interrompues, non comptées.`),
    restored: "Tes résultats ont été restaurés depuis la sauvegarde.",
    totalAverage: (n) => `Environ ${n} par partie`,
    lastMatch: "Partie d'avant",
    lastDetail: (when, opened, broken) => `Finie ${when}. ${opened} ${frPlural(opened, "ouvert")}${broken === "0" ? "" : ` et environ ${broken} ${frPlural(broken, "cassé")} sans être ${frPlural(broken, "ouvert")}`}.`,
    at: (time) => `à ${time}`,
    onDay: (day, time) => `le ${day} à ${time}`,
    lastDay: (today, games, average, best) => `Aujourd'hui : ${today}${games === "0" ? "" : ` en ${games} ${frPlural(games, "partie")}, ${average} par partie`} · Meilleure partie : ${best}`,
    openMatches: "Voir les parties",
    back: "Retour",
    matchesTitle: "Parties",
    notify: "Me prévenir à la fin de chaque partie",
    notifyHint: "Une notification Windows avec les coffres de la partie.",
    settingsTitle: "Réglages",
    overlay: "Overlay pendant Fortnite",
    overlayHint: "Le total et la partie d'avant par-dessus le jeu, en plein écran fenêtré. Ctrl+Maj+O le cache, Ctrl+Maj+L le déplace.",
    hotKeysTaken: "Un autre logiciel utilise déjà Ctrl+Maj+O ou Ctrl+Maj+L : l'overlay s'affiche, mais ces raccourcis ne marchent pas.",
    obs: "Page pour OBS",
    obsHint: "Donne une adresse à ajouter comme source Navigateur dans OBS, sur ce PC uniquement.",
    obsFailed: "La page n'a pas pu démarrer : un autre logiciel utilise les ports 28470 à 28479.",
    overlayShort: "Overlay",
    move: "Déplacer",
    lockHere: "Fixer ici",
    copy: "Copier",
    copied: "Copiée",
    startWithFortnite: "S'ouvrir avec Fortnite",
    startHint: "Démarre discrètement avec Windows, puis ouvre Chest Counter dans la zone de notification quand Fortnite se lance.",
    kinds: { normal: ["normal", "normaux"], rare: ["rare", "rares"], buried: ["enterré", "enterrés"], relic: ["relique", "reliques"], poster: ["avis de recherche", "avis de recherche"] },
    matchBroken: (n) => `environ ${n} ${frPlural(n, "cassé")} sans être ${frPlural(n, "ouvert")}`,
    notice: (before, after, moved) => `Comptage corrigé : les coffres revus étaient comptés deux fois. Lu dans les replays : ${after} au lieu de ${before}.${moved === "0" ? "" : moved === "1" ? " 1 partie sans replay est maintenant estimée." : ` ${moved} parties sans replay sont maintenant estimées.`}`,
    gotIt: "Compris",
  },
  en: {
    appName: "Chest Counter",
    total: "Estimated total",
    inChests: "in chests",
    upToDate: "Up to date",
    measured: "Read from replays",
    estimated: "Estimated",
    gamesLabel: "Matches without a replay",
    gamesHint: "Played before installing, or whose replay was deleted.",
    less: "Remove a match",
    more: "Add a match",
    restart: "Restart",
    noGames: "No match read yet.",
    measuredDetail: (games, average) => `Over ${games} ${games === "1" ? "match" : "matches"}, ${average} on average.`,
    estimatedDetail: (missed, games, broken, basis, perMatch) => {
      const pace = basis === null ? "" : basis === "1" ? ", at the pace of your first match" : `, at the pace of your first ${basis} matches`;
      const average = perMatch === null ? "" : `, about ${perMatch} per match`;
      return `${missed} for ${games} ${games === "1" ? "match" : "matches"} without a replay${pace}. ${broken} broken unopened${average}.`;
    },
    scanning: "Reading replays.",
    inProgress: "The match in progress will be counted when it ends.",
    failed: (n) => `Unreadable replays: ${n}.`,
    errorRead: "Reading the folder failed. Trying again in 30 seconds.",
    errorSave: "Saving failed. Try again in a moment.",
    errorInput: (max) => `Enter a number between 0 and ${max}.`,
    update: (version) => `Version ${version} is ready.`,
    loading: "Loading.",
    cut: (n) => (n === "1" ? "1 match cut short, not counted." : `${n} matches cut short, not counted.`),
    restored: "Your results were restored from the backup.",
    totalAverage: (n) => `About ${n} per match`,
    lastMatch: "Last match",
    lastDetail: (when, opened, broken) => `Ended ${when}. ${opened} opened${broken === "0" ? "" : ` and about ${broken} broken unopened`}.`,
    at: (time) => `at ${time}`,
    onDay: (day, time) => `on ${day} at ${time}`,
    lastDay: (today, games, average, best) => `Today: ${today}${games === "0" ? "" : ` in ${games} ${games === "1" ? "match" : "matches"}, ${average} per match`} · Best match: ${best}`,
    openMatches: "See all matches",
    back: "Back",
    matchesTitle: "Matches",
    notify: "Notify me when each match ends",
    notifyHint: "A Windows notification with the match's chests.",
    settingsTitle: "Settings",
    overlay: "Overlay while Fortnite runs",
    overlayHint: "The total and the last match over the game, in windowed fullscreen. Ctrl+Shift+O hides it, Ctrl+Shift+L moves it.",
    hotKeysTaken: "Another app already uses Ctrl+Shift+O or Ctrl+Shift+L: the overlay shows, but these shortcuts do not work.",
    obs: "Page for OBS",
    obsHint: "Gives an address to add as a Browser source in OBS, on this PC only.",
    obsFailed: "The page could not start: another app uses ports 28470 to 28479.",
    overlayShort: "Overlay",
    move: "Move",
    lockHere: "Lock here",
    copy: "Copy",
    copied: "Copied",
    startWithFortnite: "Open with Fortnite",
    startHint: "Starts quietly with Windows, then opens Chest Counter in the notification area when Fortnite starts.",
    kinds: { normal: ["normal", "normal"], rare: ["rare", "rare"], buried: ["buried", "buried"], relic: ["relic", "relics"], poster: ["wanted poster", "wanted posters"] },
    matchBroken: (n) => `about ${n} broken unopened`,
    notice: (before, after, moved) => `Count corrected: chests seen again were counted twice. Read from replays: ${after} instead of ${before}.${moved === "0" ? "" : moved === "1" ? " 1 match without a replay is now estimated." : ` ${moved} matches without a replay are now estimated.`}`,
    gotIt: "Got it",
  },
};

const requested = new URLSearchParams(location.search).get("lang") ?? navigator.language;
const lang = requested.toLowerCase().startsWith("fr") ? "fr" : "en";
const t = STRINGS[lang];
const locale = lang === "fr" ? "fr-FR" : "en-US";
const grouped = new Intl.NumberFormat(locale, { maximumFractionDigits: 0 });
// French groups digits with a narrow no-break space, which the figure face does not draw.
const whole = { format: (value) => grouped.format(value).replace(/\u202f/g, "\u00a0") };
const oneDecimal = new Intl.NumberFormat(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const stampTime = new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", hour: "numeric", minute: "2-digit" });
const clock = new Intl.DateTimeFormat(locale, { hour: "numeric", minute: "2-digit" });
const shortDay = new Intl.DateTimeFormat(locale, { day: "numeric", month: "short" });
const KIND_ORDER = ["normal", "rare", "buried", "relic", "poster"];

const element = (id) => document.getElementById(id);
// Live regions announce every write: write only what changed.
const setText = (node, text) => { if (node.textContent !== text) node.textContent = text; };
const games = element("games");
const stamp = element("stamp");
const medal = document.querySelector(".medal");
const webview = window.chrome.webview;
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");

let previous = null;
let lastCompleted = null;
let lastCountChange = -Infinity;
let inputError = null;

function applyStaticText() {
  document.documentElement.lang = lang;
  document.title = t.appName;
  document.querySelectorAll("[data-t]").forEach((node) => { node.textContent = t[node.dataset.t]; });
  document.querySelectorAll("[data-t-label]").forEach((node) => {
    node.setAttribute("aria-label", t[node.dataset.tLabel]);
    if (node.classList.contains("top-button")) node.title = t[node.dataset.tLabel];
  });
  element("status").textContent = t.loading;
}

// The compass swings once and settles, like a needle finding north.
function swingMedallion() {
  const keyframes = reducedMotion.matches
    ? [{ opacity: 0.55 }, { opacity: 1 }]
    : [
        { transform: "rotate(0deg)", easing: "cubic-bezier(0.33, 1, 0.68, 1)" },
        { transform: "rotate(-18deg)", offset: 0.35, easing: "cubic-bezier(0.23, 1, 0.32, 1)" },
        { transform: "rotate(0deg)" },
      ];
  medal.animate(keyframes, { duration: 300, easing: "linear" });
}

function statusFor(state) {
  if (state.error === "read") {
    return t.errorRead;
  }
  if (state.error === "save") {
    return t.errorSave;
  }

  const parts = state.scanning ? [t.scanning] : [];
  if (state.restored) {
    parts.push(t.restored);
  }
  if (state.inProgress > 0) {
    parts.push(t.inProgress);
  }
  if (state.cut > 0) {
    parts.push(t.cut(whole.format(state.cut)));
  }
  if (state.failed > 0) {
    parts.push(t.failed(whole.format(state.failed)));
  }
  return parts.join(" ");
}

function paintStatus(state) {
  const status = element("status");
  status.textContent = inputError ?? statusFor(state);
  status.dataset.tone = inputError || state.error ? "error" : "normal";
}

// Judged on completed scans only: the watcher rescans the folder often,
// and the window must stay still unless exactly one isolated match was added.
function paintStamp(state) {
  if (state.scanning) {
    stamp.hidden = lastCompleted === null || Boolean(state.error);
    return;
  }

  const now = performance.now();
  const added = lastCompleted === null ? 0 : state.gamesAnalyzed - lastCompleted.gamesAnalyzed;
  const isolated = added === 1 && now - lastCountChange > ISOLATED_AFTER_MS;
  if (lastCompleted === null || added !== 0) {
    element("stamp-time").textContent = stampTime.format(new Date());
    lastCountChange = now;
  }
  lastCompleted = state;
  stamp.hidden = Boolean(state.error);
  if (isolated && !stamp.hidden) {
    swingMedallion();
  }
}

function render(state) {
  const unread = lastCompleted === null && state.scanning && state.gamesAnalyzed === 0;
  const figure = (value) => (unread ? "–" : whole.format(Math.round(value)));

  const totalText = figure(state.total);
  element("total").textContent = totalText;
  element("total").style.setProperty("--chars", Math.max(totalText.length, 4));

  element("measured").textContent = figure(state.chests);
  element("measured-detail").textContent = state.gamesAnalyzed === 0
    ? t.noGames
    : t.measuredDetail(whole.format(state.gamesAnalyzed), oneDecimal.format(state.average));

  element("estimated").textContent = figure(state.estimated + state.broken);
  element("estimated-detail").textContent = unread ? "" : t.estimatedDetail(
    whole.format(Math.round(state.estimated)),
    whole.format(state.gamesWithoutReplay),
    whole.format(Math.round(state.broken)),
    state.gamesWithoutReplay > 0 && state.basisGames > 0 ? whole.format(state.basisGames) : null,
    brokenPerMatch(state));

  const allGames = state.gamesAnalyzed + state.gamesWithoutReplay;
  element("total-average").hidden = unread || allGames === 0;
  element("total-average").textContent = allGames === 0 ? "" : t.totalAverage(oneDecimal.format(Math.round(state.total) / allGames));
  paintLast(state, unread);
  paintStamp(state);
  paintStatus(state);

  paintSettings(state.settings);
  element("notice").hidden = !state.notice;
  if (state.notice) {
    element("notice-text").textContent = t.notice(whole.format(state.notice.before), whole.format(state.notice.after), whole.format(state.notice.moved));
  }
  if (!element("matches").hidden && previous !== null && previous.gamesAnalyzed !== state.gamesAnalyzed) {
    webview.postMessage({ type: "matches" });
  }

  element("update").hidden = !state.update;
  if (state.update) {
    element("update-text").textContent = t.update(state.update);
  }

  if (document.activeElement !== games) {
    games.value = state.gamesWithoutReplay;
    games.removeAttribute("aria-invalid");
  }
  previous = state;
}

// Over every match, read or estimated, like the broken figure it divides.
function brokenPerMatch(state) {
  const games = state.gamesAnalyzed + state.gamesWithoutReplay;
  return games === 0 ? null : oneDecimal.format(Math.round(state.broken) / games);
}

function ended(date) {
  const sameDay = date.toDateString() === new Date().toDateString();
  return sameDay ? t.at(clock.format(date)) : t.onDay(shortDay.format(date), clock.format(date));
}

function paintLast(state, unread) {
  const last = state.last;
  element("last").hidden = unread || !last;
  element("open-matches").hidden = unread || !last;
  if (!last || unread) {
    return;
  }

  element("last-chests").textContent = whole.format(Math.round(last.chests + last.broken));
  element("last-detail").textContent = t.lastDetail(ended(new Date(last.endedAt)), whole.format(last.chests), whole.format(Math.round(last.broken)));
  const todayAverage = state.todayGames === 0 ? "" : oneDecimal.format(state.today / state.todayGames);
  element("last-day").textContent = t.lastDay(whole.format(state.today), whole.format(state.todayGames), todayAverage, whole.format(state.best));
}

function kindsText(kinds) {
  return KIND_ORDER.filter((kind) => kinds[kind] > 0)
    .map((kind) => `${whole.format(kinds[kind])} ${t.kinds[kind][kinds[kind] > 1 ? 1 : 0]}`)
    .join(", ");
}

function renderMatches(data) {
  const list = element("match-list");
  list.replaceChildren(...data.matches.map((match) => {
    const item = document.createElement("li");
    item.className = "match";
    const when = document.createElement("p");
    when.className = "match-when";
    when.textContent = stampTime.format(new Date(match.playedAt));
    const figure = document.createElement("p");
    figure.className = "figure";
    figure.textContent = whole.format(Math.round(match.chests + match.broken));
    const detail = document.createElement("p");
    detail.className = "detail";
    const broken = Math.round(match.broken);
    detail.textContent = [kindsText(match.kinds), broken > 0 ? t.matchBroken(whole.format(broken)) : ""].filter(Boolean).join(" · ");
    item.append(when, figure, detail);
    return item;
  }));
  element("matches-empty").hidden = data.matches.length > 0;
}

function paintSettings(settings) {
  if (!settings) {
    return;
  }

  element("notify").checked = settings.notify;
  element("overlay").checked = settings.overlay;
  element("obs").checked = settings.obs;
  element("start-with-fortnite").checked = settings.startWithFortnite;
  const overlayHint = element("overlay-hint");
  setText(overlayHint, settings.overlay && settings.hotKeysTaken ? t.hotKeysTaken : t.overlayHint);
  overlayHint.dataset.tone = settings.overlay && settings.hotKeysTaken ? "error" : "normal";
  const obsHint = element("obs-hint");
  setText(obsHint, settings.obsFailed ? t.obsFailed : t.obsHint);
  obsHint.dataset.tone = settings.obsFailed ? "error" : "normal";
  element("overlay-move").hidden = !settings.overlay;
  element("toggle-overlay").setAttribute("aria-pressed", String(settings.overlay));
  const move = element("move-overlay");
  move.textContent = settings.overlayMoving ? t.lockHere : t.move;
  move.setAttribute("aria-pressed", String(Boolean(settings.overlayMoving)));
  element("obs-address").hidden = !settings.obsAddress;
  element("obs-url").textContent = settings.obsAddress ?? "";
}

function showView(name, from) {
  for (const view of ["home", "matches", "settings"]) {
    element(view).hidden = view !== name;
  }
  if (name === "matches") {
    webview.postMessage({ type: "matches" });
    element("back").focus();
  } else if (name === "settings") {
    element("settings-back").focus();
  } else {
    element(from).focus();
  }
}


function isValid(value) {
  return Number.isInteger(value) && value >= 0 && value <= MAX_GAMES;
}

function submit(value) {
  if (isValid(value)) {
    inputError = null;
    games.removeAttribute("aria-invalid");
    webview.postMessage({ type: "setGamesWithoutReplay", value });
  } else {
    inputError = t.errorInput(whole.format(MAX_GAMES));
    games.setAttribute("aria-invalid", "true");
  }

  if (previous !== null) {
    paintStatus(previous);
  }
}

function step(delta) {
  const current = Number(games.value);
  const next = Math.min(MAX_GAMES, Math.max(0, (Number.isInteger(current) ? current : 0) + delta));
  games.value = next;
  submit(next);
}

games.addEventListener("input", () => {
  if (games.value !== "") {
    submit(Number(games.value));
    return;
  }

  inputError = null;
  games.removeAttribute("aria-invalid");
  if (previous !== null) {
    paintStatus(previous);
  }
});
games.addEventListener("blur", () => {
  if (previous !== null) {
    inputError = null;
    games.value = previous.gamesWithoutReplay;
    games.removeAttribute("aria-invalid");
    paintStatus(previous);
  }
});
element("less").addEventListener("click", () => step(-1));
element("more").addEventListener("click", () => step(1));
element("apply-update").addEventListener("click", () => webview.postMessage({ type: "applyUpdate" }));
element("dismiss-notice").addEventListener("click", () => webview.postMessage({ type: "dismissNotice" }));
element("open-matches").addEventListener("click", () => showView("matches"));
element("back").addEventListener("click", () => showView("home", "open-matches"));
element("open-settings").addEventListener("click", () => showView("settings"));
element("settings-back").addEventListener("click", () => showView("home", "open-settings"));
element("notify").addEventListener("change", (event) => webview.postMessage({ type: "setNotify", value: event.target.checked }));
element("overlay").addEventListener("change", (event) => webview.postMessage({ type: "setOverlay", value: event.target.checked }));
element("obs").addEventListener("change", (event) => webview.postMessage({ type: "setObs", value: event.target.checked }));
element("start-with-fortnite").addEventListener("change", (event) => webview.postMessage({ type: "setStartWithFortnite", value: event.target.checked }));
element("move-overlay").addEventListener("click", () => webview.postMessage({ type: "moveOverlay" }));
element("toggle-overlay").addEventListener("click", () => webview.postMessage({ type: "setOverlay", value: !element("overlay").checked }));
element("copy-obs").addEventListener("click", () => {
  webview.postMessage({ type: "copyObs" });
  element("copy-obs").textContent = t.copied;
  setTimeout(() => { element("copy-obs").textContent = t.copy; }, 1500);
});
document.addEventListener("keydown", (event) => {
  if (event.key !== "Escape") {
    return;
  }
  if (!element("matches").hidden) {
    showView("home", "open-matches");
  } else if (!element("settings").hidden) {
    showView("home", "open-settings");
  }
});
webview.addEventListener("message", (event) => (event.data.matches ? renderMatches(event.data) : render(event.data)));

applyStaticText();
webview.postMessage({ type: "ready" });
