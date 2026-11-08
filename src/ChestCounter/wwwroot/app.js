"use strict";

const MIN_DRUMS = 5;
const MAX_GAMES = 100000;
const ROLL_MS = 420;
const STAGGER_MS = 40;
const ISOLATED_AFTER_MS = 5000;

const whole = new Intl.NumberFormat("fr-FR", { maximumFractionDigits: 0 });
const oneDecimal = new Intl.NumberFormat("fr-FR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const element = (id) => document.getElementById(id);
const plural = (count, word) => `${word}${count >= 2 ? "s" : ""}`;

const odometer = element("odometer");
const games = element("games");
const webview = window.chrome.webview;

let digits = [];
let previous = null;
let lastCountChange = -Infinity;
let settle = 0;
let inputError = null;

function buildDrums(count) {
  odometer.style.setProperty("--n", count);
  odometer.replaceChildren(...Array.from({ length: count }, (_, index) => {
    const drum = document.createElement("div");
    drum.className = "drum";
    drum.style.setProperty("--from-right", count - 1 - index);
    drum.setAttribute("aria-hidden", "true");
    const strip = document.createElement("span");
    strip.className = "strip";
    for (let cell = 0; cell < 20; cell += 1) {
      const span = document.createElement("span");
      span.className = "cell";
      span.textContent = String(cell % 10);
      strip.append(span);
    }
    drum.append(strip);
    return drum;
  }));
  digits = Array(count).fill(0);
}

function stripAt(index) {
  return odometer.children[index].firstChild;
}

function snapDrums() {
  delete odometer.dataset.rolling;
  digits.forEach((digit, index) => stripAt(index).style.setProperty("--pos", digit));
}

function showTotal(total, roll) {
  const text = String(Math.round(total)).padStart(MIN_DRUMS, "0");
  if (text.length !== digits.length) {
    buildDrums(text.length);
    roll = false;
  }

  clearTimeout(settle);
  snapDrums();
  void odometer.offsetHeight;
  if (roll) {
    odometer.dataset.rolling = "";
  }

  [...text].forEach((character, index) => {
    const next = Number(character);
    stripAt(index).style.setProperty("--pos", roll && next < digits[index] ? next + 10 : next);
    digits[index] = next;
  });

  if (roll) {
    settle = setTimeout(snapDrums, ROLL_MS + STAGGER_MS * digits.length);
  }

  odometer.setAttribute("aria-label", `${whole.format(total)} ${plural(total, "coffre")} au total estimé`);
}

function statusFor(state) {
  if (state.error) {
    return state.error;
  }

  const parts = [state.scanning ? "Analyse en cours." : "À jour."];
  if (state.inProgress > 0) {
    parts.push("Une partie en cours sera comptée à la fin.");
  }
  if (state.failed > 0) {
    parts.push(`Replays illisibles : ${whole.format(state.failed)}.`);
  }
  return parts.join(" ");
}

function paintStatus(state) {
  const status = element("status");
  status.textContent = inputError ?? statusFor(state);
  status.dataset.tone = inputError || state.error ? "error" : "normal";
}

function render(state) {
  const countChanged = previous !== null && state.gamesAnalyzed !== previous.gamesAnalyzed;
  const now = performance.now();
  const roll = countChanged && state.gamesAnalyzed === previous.gamesAnalyzed + 1 && now - lastCountChange > ISOLATED_AFTER_MS;
  if (countChanged || previous === null) {
    lastCountChange = now;
  }

  showTotal(state.total, roll);
  element("measured").textContent = whole.format(state.chests);
  element("measured-detail").textContent = state.gamesAnalyzed === 0
    ? "Aucune partie lue pour l'instant"
    : `${plural(state.chests, "coffre")} ${state.chests >= 2 ? "comptés" : "compté"} sur ${whole.format(state.gamesAnalyzed)} ${plural(state.gamesAnalyzed, "partie")}`;
  element("average").textContent = state.gamesAnalyzed === 0 ? "" : `Moyenne : ${oneDecimal.format(state.average)}`;
  element("estimated").textContent = whole.format(state.estimated + state.broken);
  element("estimated-detail").textContent = `${whole.format(state.estimated)} sur ${whole.format(state.gamesWithoutReplay)} ${plural(state.gamesWithoutReplay, "partie")} sans replay`;
  element("broken").textContent = `${whole.format(state.broken)} ${plural(state.broken, "coffre")} ${plural(state.broken, "cassé")}`;
  element("broken-average").textContent = state.gamesAnalyzed === 0 ? "" : `Moyenne : ${oneDecimal.format(state.brokenPerGame)} cassés par partie`;

  paintStatus(state);

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
    inputError = `Entre un nombre entre 0 et ${whole.format(MAX_GAMES)}.`;
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
webview.addEventListener("message", (event) => render(event.data));

buildDrums(MIN_DRUMS);
snapDrums();
webview.postMessage({ type: "ready" });
