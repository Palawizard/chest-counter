"use strict";

const STRINGS = {
  fr: { label: "Total estimé", last: (n) => `Partie d'avant : ${n}` },
  en: { label: "Estimated total", last: (n) => `Last match: ${n}` },
};

const element = (id) => document.getElementById(id);

async function refresh() {
  try {
    const state = await (await fetch("state", { cache: "no-store" })).json();
    const lang = state.lang === "fr" ? "fr" : "en";
    const t = STRINGS[lang];
    // French groups digits with a narrow no-break space, which the figure face does not draw.
    const whole = (value) => new Intl.NumberFormat(lang === "fr" ? "fr-FR" : "en-US", { maximumFractionDigits: 0 }).format(value).replace(/ /g, " ");
    document.documentElement.lang = lang;
    element("label").textContent = t.label;
    element("total").textContent = whole(Math.round(state.total));
    element("last").textContent = state.last === null ? "" : t.last(whole(state.last));
  } catch {
    // The app is closed or restarting: keep the last figures until it answers again.
  }
}

refresh();
setInterval(refresh, 3000);
