// Shared texts for the Workshop description images. Open a variant as  variant.html#<card>/<lang>
// cards: header | pasture | guards ; langs: en | ru
const TEXT = {
  en: {
    contents: "Contents",
    tagline: "Pastures, shepherds & herding dogs",
    pastureTitle: "Pasture zone",
    pastureKicker: "Zones menu",
    pastureBody: "Draw a pasture on open grassland and link it to a pen. When the herd gets hungry, a handler ropes up every animal and leads them out.",
    pastureBullets: ["Herd stays inside the pasture", "Grazes until everyone is fed", "Several pastures rotate to regrow"],
    guardsTitle: "Shepherd & herding dog",
    guardsKicker: "Pen marker · Grazing tab",
    guardsBody: "Guards stay with the grazing herd. Fewer animals wander off, predators think twice, and the dog chases them away.",
    wander: "Wander-off chance",
    predator: "Predator interest",
    none: "No guards", dog: "Dog", shepherd: "Shepherd", both: "Both",
    training: "New training: Herding",
    tabTitle: "Grazing tab",
    tabKicker: "Pen marker",
    tabBody: "Every pen gets its own grazing plan: when to go out, when to come home and who stays behind.",
    tabOut: "When to go out",
    tabOutList: ["Fullness below X%", "Pen food running out", "Grazing hours"],
    tabBack: "When to come back",
    tabBackList: ["Everyone is fed", "Hours end or nightfall", "Danger"],
    tabHerd: "Herd",
    tabHerdList: ["Keep sick, pregnant or young home", "Exclude any animal"],
    leadTitle: "Leading the herd",
    leadKicker: "Handling work",
    leadBody: "One handler ropes up the whole herd at once and leads it out, then brings everyone home the same way. Vanilla roping, no teleports.",
    goLabel: "Go graze",
    goDesc: "Take the herd out now",
    backLabel: "Bring herd back",
    backDesc: "Bring it home now",
    leadSide: "Manual control",
    dangerTitle: "Danger",
    dangerKicker: "Return conditions",
    dangerBody: "Real threats bring the herd home on their own. When enemies show up, you decide whether the herd keeps grazing.",
    dangerSide: "What happens",
    dangerRows: [["Predator hunting the herd", "Back home"], ["Toxic fallout or haze", "Back home"], ["Unsafe temperature", "Back home"], ["Enemies on the map", "You choose"]],
    compatTitle: "Compatibility",
    compatRows: [["Game versions", "1.6 · 1.7"], ["Requires", "Harmony"], ["DLC", "Not required"], ["Existing saves", "Safe to add and remove"], ["Modded dogs", "race / trainableTags: TTG_Herding"], ["Languages", "English · Russian"]],
  },
  ru: {
    contents: "Содержание",
    tagline: "Пастбища, пастухи и пастушьи собаки",
    pastureTitle: "Зона «Пастбище»",
    pastureKicker: "Меню «Зоны»",
    pastureBody: "Нарисуйте пастбище на лугу и привяжите к загону. Когда стадо проголодается, животновод возьмёт всех на верёвки и выведет на выпас.",
    pastureBullets: ["Стадо не уходит с пастбища", "Пасётся, пока все не наедятся", "Несколько пастбищ чередуются"],
    guardsTitle: "Пастух и собака",
    guardsKicker: "Метка загона · вкладка «Выпас»",
    guardsBody: "Охрана держится рядом со стадом: животные реже разбредаются, хищники реже нападают, а собака их прогоняет.",
    wander: "Шанс разбрестись",
    predator: "Интерес хищников",
    none: "Без охраны", dog: "Собака", shepherd: "Пастух", both: "Оба",
    training: "Новая дрессировка: «Пастьба»",
    tabTitle: "Вкладка «Выпас»",
    tabKicker: "Метка загона",
    tabBody: "У каждого загона свой план выпаса: когда выводить, когда возвращать и кто остаётся дома.",
    tabOut: "Когда выводить",
    tabOutList: ["Сытость ниже X%", "В загоне кончается корм", "Часы выпаса"],
    tabBack: "Когда возвращать",
    tabBackList: ["Все наелись", "Кончились часы или стемнело", "Опасность"],
    tabHerd: "Стадо",
    tabHerdList: ["Больных, беременных, молодняк — дома", "Исключить любое животное"],
    leadTitle: "Перегон стада",
    leadKicker: "Работа «Уход за животными»",
    leadBody: "Один животновод берёт на верёвки всё стадо разом и ведёт на пастбище, а потом так же возвращает. Ванильные верёвки, без телепортов.",
    goLabel: "На выпас",
    goDesc: "Вывести стадо сейчас",
    backLabel: "Вернуть стадо",
    backDesc: "Вернуть в загон сейчас",
    leadSide: "Ручное управление",
    dangerTitle: "Опасность",
    dangerKicker: "Условия возврата",
    dangerBody: "При настоящей угрозе стадо возвращают само. Если на карте враги — решаете вы: вернуть стадо или пусть пасётся.",
    dangerSide: "Что произойдёт",
    dangerRows: [["Хищник охотится на стадо", "Домой"], ["Токсичные осадки, дымка", "Домой"], ["Опасная температура", "Домой"], ["Враги на карте", "Ваш выбор"]],
    compatTitle: "Совместимость",
    compatRows: [["Версии игры", "1.6 · 1.7"], ["Требуется", "Harmony"], ["Дополнения", "Не нужны"], ["Сохранения", "Можно добавить и удалить"], ["Модовые собаки", "race / trainableTags: TTG_Herding"], ["Языки", "Английский · русский"]],
  },
};

// Default mod settings (multipliers on vanilla odds).
const ODDS = { wander: [0.5, 0.25, 0.15, 0.05], predator: [1, 0.6, 0.5, 0.3] };

const ART = {
  cover: "../../About/Preview.png",
  lead: "../art/lead_out.png",
  guards: "../art/shepherd_dog.png",
  predator: "../art/predator.png",
  home: "../art/return_home.png",
  zoneIcon: "../../Common/Textures/UI/Designators/TTG_ZoneCreate_Pasture.png",
  goIcon: "../../Common/Textures/UI/Commands/TTG_StartGrazing.png",
  backIcon: "../../Common/Textures/UI/Commands/TTG_ReturnHerd.png",
};

function route() {
  const [card, lang] = (location.hash.slice(1) || "header/en").split("/");
  document.documentElement.lang = lang;
  document.querySelectorAll("[data-card]").forEach(el => el.hidden = el.dataset.card !== card);
  const t = TEXT[lang] || TEXT.en;
  document.querySelectorAll("[data-t]").forEach(el => el.textContent = t[el.dataset.t]);
  document.querySelectorAll("[data-list]").forEach(el => {
    el.innerHTML = "";
    t[el.dataset.list].forEach(s => { const li = document.createElement("li"); li.textContent = s; el.appendChild(li); });
  });
  document.querySelectorAll("[data-rows]").forEach(el => {
    el.innerHTML = "";
    t[el.dataset.rows].forEach(([k, v], i) => {
      const row = document.createElement("div");
      row.className = "kv" + (el.dataset.rows === "dangerRows" && i === 3 ? " kv-hl" : "");
      row.innerHTML = `<span class="kv-k"></span><span class="kv-v"></span>`;
      row.children[0].textContent = k;
      row.children[1].textContent = v;
      el.appendChild(row);
    });
  });
  document.querySelectorAll("[data-art]").forEach(el => el.src = ART[el.dataset.art]);
  document.querySelectorAll("[data-odds]").forEach(el => {
    const kind = el.dataset.odds;
    el.innerHTML = "";
    ["none", "dog", "shepherd", "both"].forEach((k, i) => {
      const v = ODDS[kind][i];
      const row = document.createElement("div");
      row.className = "odds-row";
      row.innerHTML = `<span class="odds-label">${t[k]}</span><span class="odds-track"><span class="odds-fill" style="width:${v * 100}%"></span></span><span class="odds-val">×${v}</span>`;
      el.appendChild(row);
    });
  });
}
window.addEventListener("DOMContentLoaded", route);
window.addEventListener("hashchange", route);
