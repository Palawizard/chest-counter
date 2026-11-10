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
    estimatedDetail: (missed, games, broken, basis) => {
      const pace = basis === null ? "" : basis === "1" ? ", au rythme de ta première partie" : `, au rythme de tes ${basis} premières parties`;
      return `${missed} pour ${games} ${frPlural(games, "partie")} sans replay${pace}. ${broken} ${frPlural(broken, "cassé")} sans être ${frPlural(broken, "ouvert")}.`;
    },
    scanning: "Analyse des replays en cours.",
    inProgress: "La partie en cours sera comptée à la fin.",
    failed: (n) => `Replays illisibles\u00a0: ${n}.`,
    errorRead: "La lecture du dossier a échoué. Nouvel essai dans 30 secondes.",
    errorSave: "L'enregistrement a échoué. Réessaie dans un instant.",
    errorInput: (max) => `Entre un nombre entre 0 et ${max}.`,
    update: (version) => `La version ${version} est prête.`,
    loading: "Chargement.",
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
    estimatedDetail: (missed, games, broken, basis) => {
      const pace = basis === null ? "" : basis === "1" ? ", at the pace of your first match" : `, at the pace of your first ${basis} matches`;
      return `${missed} for ${games} ${games === "1" ? "match" : "matches"} without a replay${pace}. ${broken} broken unopened.`;
    },
    scanning: "Reading replays.",
    inProgress: "The match in progress will be counted when it ends.",
    failed: (n) => `Unreadable replays: ${n}.`,
    errorRead: "Reading the folder failed. Trying again in 30 seconds.",
    errorSave: "Saving failed. Try again in a moment.",
    errorInput: (max) => `Enter a number between 0 and ${max}.`,
    update: (version) => `Version ${version} is ready.`,
    loading: "Loading.",
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

const element = (id) => document.getElementById(id);
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
  document.querySelectorAll("[data-t-label]").forEach((node) => node.setAttribute("aria-label", t[node.dataset.tLabel]));
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
  if (state.inProgress > 0) {
    parts.push(t.inProgress);
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
    state.gamesWithoutReplay > 0 && state.basisGames > 0 ? whole.format(state.basisGames) : null);

  paintStamp(state);
  paintStatus(state);

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
webview.addEventListener("message", (event) => render(event.data));

applyStaticText();
webview.postMessage({ type: "ready" });
