import fs from "node:fs/promises";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const TMP = "/Users/vapronix/Documents/GitHub/MOBA/Csharp-HighPerf-MOBA-Core/Reports/presentation_build";
const STARTER = `${TMP}/template-starter.pptx`;
const FINAL = "/Users/vapronix/Documents/GitHub/MOBA/Csharp-HighPerf-MOBA-Core/Reports/AdvancedGameAI_FinalPresentation_AA.SC.U3BCA2307092.pptx";
const FINAL_INSPECT = `${FINAL}.inspect.ndjson`;
const RENDER_DIR = `${TMP}/final-render`;
const LAYOUT_DIR = `${TMP}/final-layout`;
const MONTAGE = `${TMP}/final-montage.webp`;
const SOURCE_NOTES = `${TMP}/source-notes.txt`;

const SCREENSHOTS = {
  gemGrab: `${TMP}/screenshots/gem-grab-countdown.png`,
  brawlBall: `${TMP}/screenshots/brawl-ball-objective.png`,
  knockout: `${TMP}/screenshots/knockout-camera-round.png`,
  debugWide: `${TMP}/screenshots/ai-debug-map-wide.png`,
  debugCombat: `${TMP}/screenshots/ai-debug-combat.png`,
  matchResults: `${TMP}/screenshots/match-results.png`,
  brawlerSelect: `${TMP}/screenshots/brawler-select.png`,
};

const C = {
  magenta: "#c01855",
  navy: "#20384f",
  blue: "#2b5faa",
  cyan: "#0f8fb3",
  green: "#16824a",
  amber: "#f4a51c",
  red: "#c83232",
  purple: "#6a36bf",
  black: "#111111",
  gray: "#444444",
  lightGray: "#f2f4f7",
  midGray: "#d8dde7",
  white: "#ffffff",
};

async function writeBlob(path, blob) {
  await fs.writeFile(path, new Uint8Array(await blob.arrayBuffer()));
}

function slideAt(presentation, oneBased) {
  const slide = presentation.slides.items[oneBased - 1];
  if (!slide) {
    throw new Error(`Missing template slide ${oneBased}`);
  }
  return slide;
}

function byName(slide, name) {
  const element = slide.elements.items.find((item) => item.name === name);
  if (!element) {
    throw new Error(`Missing element "${name}" on slide`);
  }
  return element;
}

function setText(slide, name, text) {
  const element = byName(slide, name);
  if (!element.text) {
    throw new Error(`Element "${name}" has no editable text`);
  }
  element.text.set(text);
  return element;
}

function removeElement(slide, name) {
  const element = slide.elements.items.find((item) => item.name === name);
  if (element) {
    element.delete();
  }
}

function notes(slide, lines) {
  slide.speakerNotes.textFrame.setText(lines.join("\n"));
}

function line(fill = "none", width = 0) {
  return { style: "solid", fill, width };
}

function shape(slide, geometry, position, fill, outline = "none", options = {}) {
  return slide.shapes.add({
    geometry,
    position,
    fill,
    line: outline === "none" ? line("none", 0) : outline,
    ...options,
  });
}

function text(slide, value, position, options = {}) {
  const box = shape(slide, "textbox", position, "none", "none");
  box.text = value;
  box.text.style = {
    typeface: "Calibri",
    fontSize: options.fontSize ?? 18,
    bold: options.bold ?? false,
    italic: options.italic ?? false,
    color: options.color ?? C.black,
    alignment: options.alignment ?? "left",
    verticalAlignment: options.verticalAlignment ?? "top",
    lineSpacing: options.lineSpacing ?? 1.05,
    insets: options.insets ?? { top: 2, right: 4, bottom: 2, left: 4 },
    wrap: "square",
  };
  return box;
}

function pill(slide, value, x, y, w, color) {
  shape(slide, "roundRect", { left: x, top: y, width: w, height: 30 }, color, "none", {
    borderRadius: 6,
  });
  text(slide, value, { left: x + 8, top: y + 6, width: w - 16, height: 18 }, {
    fontSize: 12,
    bold: true,
    color: C.white,
    alignment: "center",
    verticalAlignment: "middle",
  });
}

function card(slide, position, color = C.lightGray, accent = C.magenta) {
  shape(slide, "rect", position, color, line(C.midGray, 1));
  if (accent) {
    shape(slide, "rect", {
      left: position.left,
      top: position.top,
      width: 7,
      height: position.height,
    }, accent, "none");
  }
}

function node(slide, value, x, y, w, h, fill, color = C.white) {
  shape(slide, "roundRect", { left: x, top: y, width: w, height: h }, fill, line(fill, 1), {
    borderRadius: 6,
  });
  text(slide, value, { left: x + 8, top: y + 10, width: w - 16, height: h - 18 }, {
    fontSize: 13,
    bold: true,
    color,
    alignment: "center",
    verticalAlignment: "middle",
  });
}

function arrow(slide, x, y, w = 42) {
  shape(slide, "rightArrow", { left: x, top: y, width: w, height: 18 }, C.amber, "none");
}

async function addImage(slide, path, position, alt, fit = "cover", crop) {
  const bytes = await fs.readFile(path);
  slide.images.add({
    blob: bytes,
    contentType: "image/png",
    alt,
    fit,
    position,
    geometry: "rect",
    ...(crop ? { crop } : {}),
  });
  shape(slide, "rect", position, "none", line(C.navy, 2));
}

function caption(slide, value, position, accent = C.magenta) {
  card(slide, position, "#f8fafc", accent);
  text(slide, value, {
    left: position.left + 18,
    top: position.top + 10,
    width: position.width - 28,
    height: position.height - 14,
  }, {
    fontSize: 12,
    bold: true,
    color: C.gray,
    lineSpacing: 1.08,
  });
}

function codeBox(slide, title, code, position) {
  card(slide, position, "#101827", C.green);
  text(slide, title, {
    left: position.left + 18,
    top: position.top + 12,
    width: position.width - 36,
    height: 22,
  }, {
    fontSize: 12,
    bold: true,
    color: "#8ce6b5",
  });
  text(slide, code, {
    left: position.left + 18,
    top: position.top + 42,
    width: position.width - 36,
    height: position.height - 48,
  }, {
    typeface: "Courier New",
    fontSize: 10.6,
    color: C.white,
    lineSpacing: 0.98,
  });
}

function infoCard(slide, title, body, position, accent, titleSize = 13, bodySize = 10.8) {
  card(slide, position, "#f8fafc", accent);
  text(slide, title, {
    left: position.left + 18,
    top: position.top + 12,
    width: position.width - 32,
    height: 22,
  }, {
    fontSize: titleSize,
    bold: true,
    color: accent,
  });
  text(slide, body, {
    left: position.left + 18,
    top: position.top + 40,
    width: position.width - 32,
    height: position.height - 46,
  }, {
    fontSize: bodySize,
    color: C.gray,
    lineSpacing: 1.02,
  });
}

function addArchitectureDiagram(slide) {
  text(slide, "Runtime AI is organized as a data pipeline: each layer owns one decision responsibility, writes explainable state, and feeds the next layer.", {
    left: 94,
    top: 162,
    width: 1060,
    height: 36,
  }, {
    fontSize: 15,
    color: C.gray,
    lineSpacing: 1.05,
  });

  const y = 226;
  const items = [
    ["World\nPerception", 78, C.cyan],
    ["Shared\nMemory", 255, C.navy],
    ["Utility\nScoring", 432, C.green],
    ["Action\nCommitment", 609, C.purple],
    ["Planner\nNavigation", 786, C.blue],
    ["Command\nOutput", 963, C.magenta],
  ];

  for (const [label, x, color] of items) {
    node(slide, label, x, y, 138, 58, color);
  }

  for (let i = 0; i < items.length - 1; i += 1) {
    arrow(slide, 222 + i * 177, y + 20, 22);
  }

  infoCard(slide, "Blackboard coordination", "Shared focus counts, ally pressure, lane claims, threat center, and objective ownership.", {
    left: 92,
    top: 392,
    width: 250,
    height: 104,
  }, C.magenta);
  infoCard(slide, "Game-mode strategy", "Objective floors keep the current win condition above generic combat preference.", {
    left: 374,
    top: 392,
    width: 250,
    height: 104,
  }, C.green);
  infoCard(slide, "Navigation safety", "A* pathing, walkable-cell validation, route budgets, and stuck recovery prevent stalls.", {
    left: 656,
    top: 392,
    width: 250,
    height: 104,
  }, C.blue);
  infoCard(slide, "Telemetry & tuning", "Debug gizmos, incident logs, performance tiers, and ScriptableObject presets support tuning.", {
    left: 938,
    top: 392,
    width: 250,
    height: 104,
  }, C.purple);

  card(slide, { left: 174, top: 548, width: 930, height: 50 }, "#eef6ff", C.cyan);
  text(slide, "Industry-style design goal: explainable decisions, modular expansion, data-driven tuning, and predictable runtime cost.", {
    left: 198,
    top: 563,
    width: 880,
    height: 20,
  }, {
    fontSize: 13.5,
    bold: true,
    color: C.navy,
    alignment: "center",
  });
}

function addComparisonFlow(slide) {
  text(slide, "Typical quick bot", { left: 96, top: 445, width: 310, height: 28 }, {
    fontSize: 18,
    bold: true,
    color: C.red,
  });
  text(slide, "MOBA Core approach", { left: 620, top: 445, width: 330, height: 28 }, {
    fontSize: 18,
    bold: true,
    color: C.green,
  });

  const bad = ["Nearest target", "Direct chase", "Shoot cooldown", "Patch issue"];
  const good = ["Sense context", "Score options", "Commit safely", "Log evidence"];
  bad.forEach((item, index) => {
    const x = 90 + index * 118;
    node(slide, item, x, 494, 96, 44, C.red);
    if (index < bad.length - 1) {
      arrow(slide, x + 99, 507, 22);
    }
  });
  good.forEach((item, index) => {
    const x = 608 + index * 124;
    node(slide, item, x, 494, 104, 44, C.green);
    if (index < good.length - 1) {
      arrow(slide, x + 107, 507, 22);
    }
  });
}

async function build() {
  await fs.mkdir(TMP, { recursive: true });
  await fs.rm(RENDER_DIR, { recursive: true, force: true });
  await fs.rm(LAYOUT_DIR, { recursive: true, force: true });
  await fs.mkdir(RENDER_DIR, { recursive: true });
  await fs.mkdir(LAYOUT_DIR, { recursive: true });

  const presentation = await PresentationFile.importPptx(await FileBlob.load(STARTER));

  const s1 = slideAt(presentation, 1);
  setText(s1, "TextBox 3", "");
  setText(s1, "TextBox 4", "21CSA699A - Major Project\n\nDATE : July 2026");
  text(s1, "Advanced Game AI System\nusing Blackboard Architecture", {
    left: 62,
    top: 420,
    width: 600,
    height: 88,
  }, {
    fontSize: 30,
    bold: true,
    color: C.navy,
    lineSpacing: 1.02,
  });
  text(s1, "Akash Dhyani | AA.SC.U3BCA2307092\nProject Guide: Deepa Sreedhar\nProject Coordinator: Amrita Sindhu", {
    left: 64,
    top: 552,
    width: 560,
    height: 64,
  }, {
    fontSize: 16,
    color: C.black,
    lineSpacing: 1.08,
  });
  notes(s1, [
    "[Sources]",
    "- Major project final report title page and project details.",
    "- Uploaded BCA presentation template, reused without changing the required cover structure.",
  ]);

  const s2 = slideAt(presentation, 2);
  setText(s2, "Content Placeholder 2", "Project Title: Advanced Game AI System Using Blackboard Architecture in Unity Engine\nStudent Name: Akash Dhyani\nRegister Number: AA.SC.U3BCA2307092\nProgram: Bachelors of Computer Applications\nSemester: Final Year\nDepartment & Institution: Amrita Online CS\nProject Guide: Deepa Sreedhar\nProject Coordinator: Amrita Sindhu");
  notes(s2, [
    "[Sources]",
    "- Major project final report front matter.",
    "- Proposal and interim report student metadata.",
  ]);

  const s3 = slideAt(presentation, 3);
  setText(s3, "Title 1", "Introduction");
  setText(s3, "Content Placeholder 2", "Unity MOBA prototype focused on AI architecture.\nBots evaluate objectives, team state, map safety, and role policies.\nCore approach: utility scoring, blackboard sharing, tactical movement, and tuning data.");
  notes(s3, [
    "[Sources]",
    "- Major project final report abstract and introduction.",
  ]);

  const s4 = slideAt(presentation, 4);
  setText(s4, "Title 1", "Problem Statement");
  setText(s4, "Rectangle 1", "Problem: basic combat bots could fight,\nbut ignored objectives, team pressure,\nmap safety, and brawler identity.\nRequired: a scalable AI system that\nis objective-aware and explainable.");
  addComparisonFlow(s4);
  notes(s4, [
    "[Sources]",
    "- Major project final report problem definition.",
    "- Playtest issues recorded during development: objective neglect, clumping, stalls, target lock, and weak mode awareness.",
  ]);

  const s5 = slideAt(presentation, 5);
  setText(s5, "Title 1", "Objectives");
  setText(s5, "Rectangle 1", "1. Build utility-based decisions.\n2. Coordinate teams through blackboard data.\n3. Prioritize active game-mode objectives.\n4. Navigate safely around map obstacles.\n5. Add role-specific tactical behavior.\n6. Expose debug and validation tooling.");
  notes(s5, [
    "[Sources]",
    "- Major project final report objectives section.",
  ]);

  const s6 = slideAt(presentation, 6);
  setText(s6, "Title 1", "Scope of the Project");
  setText(s6, "Rectangle 1", "Included:\n- AI decisions, team coordination, mode strategy.\n- Tactical movement, navigation, and recovery.\n- Role intelligence and debug visibility.\nNot included:\n- Online multiplayer, monetization, commercial art,\n  and full ML training.");
  pill(s6, "Utility AI", 725, 585, 120, C.magenta);
  pill(s6, "Blackboard", 870, 585, 130, C.navy);
  pill(s6, "A* routes", 1025, 585, 110, C.green);
  pill(s6, "Mode AI", 725, 630, 120, C.blue);
  pill(s6, "Role AI", 870, 630, 130, C.amber);
  pill(s6, "Telemetry", 1025, 630, 110, C.purple);
  notes(s6, [
    "[Sources]",
    "- Major project final report scope section.",
  ]);

  const s7 = slideAt(presentation, 7);
  setText(s7, "Title 1", "Literature Review / Existing System");
  removeElement(s7, "Rectangle 1");
  text(s7, "The project compares common bot-authoring patterns and chooses an explainable hybrid architecture suitable for real-time games.", {
    left: 96,
    top: 156,
    width: 1000,
    height: 34,
  }, {
    fontSize: 15,
    color: C.gray,
  });
  infoCard(s7, "Finite State Machines", "Clear and easy to debug, but transitions become hard to scale when objectives, hazards, roles, and teamplay interact.", {
    left: 86,
    top: 218,
    width: 310,
    height: 126,
  }, C.navy);
  infoCard(s7, "Behavior Trees", "Useful for structured behavior, but large trees often repeat checks and can hide why one action won over another.", {
    left: 446,
    top: 218,
    width: 310,
    height: 126,
  }, C.amber);
  infoCard(s7, "Utility AI", "Actions are scored from context. This fits dynamic combat because targeting, safety, and objectives can compete numerically.", {
    left: 806,
    top: 218,
    width: 310,
    height: 126,
  }, C.green);
  infoCard(s7, "Selected direction", "Utility scoring + team blackboard + A* navigation + validation telemetry. The system remains authored, inspectable, and tunable; ML can be explored later without replacing the core.", {
    left: 190,
    top: 394,
    width: 820,
    height: 118,
  }, C.magenta, 14, 11.2);
  notes(s7, [
    "[Sources]",
    "- Major project final report literature review.",
    "- Project report references to game AI, A* pathfinding, utility AI, blackboard systems, and Unity ScriptableObjects.",
  ]);

  const s8 = slideAt(presentation, 8);
  setText(s8, "Title 1", "Proposed System");
  removeElement(s8, "Rectangle 1");
  text(s8, "The proposed system treats every bot as a modular decision agent. Game rules, map data, tuning assets, and team state feed the same runtime pipeline.", {
    left: 96,
    top: 166,
    width: 1010,
    height: 42,
  }, {
    fontSize: 15.5,
    color: C.gray,
    lineSpacing: 1.05,
  });
  const proposed = [
    ["Perception", 88, 300, C.cyan],
    ["Utility\nScoring", 288, 300, C.green],
    ["Team\nBlackboard", 488, 300, C.navy],
    ["Mode\nStrategy", 688, 300, C.magenta],
    ["Planner +\nExecutor", 888, 300, C.blue],
  ];
  proposed.forEach(([label, x, y, color]) => node(s8, label, x, y, 154, 58, color));
  for (let i = 0; i < proposed.length - 1; i += 1) {
    arrow(s8, 248 + i * 200, 320, 34);
  }
  infoCard(s8, "Scalable by data", "Difficulty, personality, ability tuning, map semantics, and mode priorities are configured outside the main controller logic.", {
    left: 92,
    top: 420,
    width: 305,
    height: 112,
  }, C.purple);
  infoCard(s8, "Explainable decisions", "Debug labels show current intent, target, score gap, route state, confidence rank, and recovery reason.", {
    left: 452,
    top: 420,
    width: 305,
    height: 112,
  }, C.blue);
  infoCard(s8, "Production hardening", "Lifecycle cleanup, objective fallback, budget fairness, and incident logging reduce stalls and frame spikes.", {
    left: 812,
    top: 420,
    width: 305,
    height: 112,
  }, C.green);
  notes(s8, [
    "[Sources]",
    "- Major project final report proposed system section.",
  ]);

  const s9 = slideAt(presentation, 9);
  setText(s9, "Title 1", "Architecture");
  removeElement(s9, "Rectangle 1");
  addArchitectureDiagram(s9);
  notes(s9, [
    "[Sources]",
    "- Major project final report system design.",
    "- Local AI modules: perception, target scoring, utility scoring, commitment, team blackboard, tactical movement, and validation telemetry.",
  ]);

  const s10 = slideAt(presentation, 10);
  setText(s10, "Title 1", "Methodology / Algorithms");
  removeElement(s10, "Content Placeholder 2");
  text(s10, "Methodology focused on small, verifiable AI systems: implement one behavior layer, expose debug evidence, playtest it, then tune the scoring data.", {
    left: 92,
    top: 154,
    width: 1040,
    height: 36,
  }, {
    fontSize: 15,
    color: C.gray,
  });
  const algorithmCards = [
    ["Utility scoring", "Scores actions from objective value, target value, range, safety, team pressure, and risk.", C.green],
    ["A* grid routing", "Builds walkable-cell paths around obstacles with fallback goals and route budgets.", C.blue],
    ["Spatial queries", "Detects nearby targets, gems, hazards, line-of-fire, bush proximity, and area effects.", C.cyan],
    ["Blackboard", "Stores shared team knowledge such as focus counts, lanes, carriers, and threat centers.", C.navy],
    ["Hysteresis", "Keeps actions stable long enough to avoid visible flicker and rapid direction changes.", C.purple],
    ["Mode strategy", "Adds objective floors for Gem Grab, Knockout, Brawl Ball, and Solo Showdown.", C.magenta],
    ["Failure recovery", "Detects stalls, stale commands, blocked routes, and repeated failed casts.", C.red],
    ["Tuning assets", "ScriptableObject-style presets adjust difficulty, personality, and role behavior without rewriting code.", C.amber],
  ];
  algorithmCards.forEach(([title, body, color], index) => {
    const col = index % 4;
    const row = Math.floor(index / 4);
    infoCard(s10, title, body, {
      left: 68 + col * 295,
      top: 214 + row * 132,
      width: 260,
      height: 106,
    }, color, 12.2, 9.9);
  });
  const steps = ["Sense", "Score", "Share", "Commit", "Act", "Validate"];
  steps.forEach((item, index) => {
    const x = 252 + index * 130;
    node(s10, item, x, 568, 92, 38, index % 2 ? C.navy : C.magenta);
    if (index < steps.length - 1) {
      arrow(s10, x + 94, 578, 24);
    }
  });
  notes(s10, [
    "[Sources]",
    "- Major project final report methodology and algorithms section.",
    "- Algorithms and concepts: utility scoring, A* pathfinding, spatial queries, blackboard coordination, and ScriptableObject tuning.",
  ]);

  const s11 = slideAt(presentation, 11);
  setText(s11, "Title 1", "Implementation Details");
  removeElement(s11, "Content Placeholder 2");
  text(s11, "Representative code patterns from the implementation show the AI architecture in practical software terms: score, share state, route safely, and enforce mode objectives.", {
    left: 92,
    top: 156,
    width: 1050,
    height: 40,
  }, {
    fontSize: 15,
    color: C.black,
    lineSpacing: 1.08,
  });
  codeBox(s11, "Utility score contract", `float score = baseScore + objective + safety - risk;\nscore -= teamFocus * focusPenalty;\nreturn Mathf.Clamp(score, 0f, 100f);`, {
    left: 76,
    top: 224,
    width: 520,
    height: 126,
  });
  codeBox(s11, "Team blackboard signal", `blackboard.ReportIntent(selfId, targetId, objectiveId);\nfocusCount = blackboard.GetFocusCount(targetId);\nallyPressure = blackboard.GetObjectivePressure(slotId);`, {
    left: 654,
    top: 224,
    width: 520,
    height: 126,
  });
  codeBox(s11, "Navigation liveness", `if (!nav.TryBuildPath(start, goal, out path))\n    goal = nav.FindNearbyWalkable(goal);\n\nif (watchdog.IsStalled(self))\n    planner.AbandonDestination();`, {
    left: 76,
    top: 392,
    width: 520,
    height: 142,
  });
  codeBox(s11, "Game-mode objective floor", `objectiveFloor = mode switch {\n    GemGrab => ScoreGemIntent(state),\n    BrawlBall => ScoreBallIntent(state),\n    Knockout => ScoreSurvivalPressure(state),\n};\nscore = Mathf.Max(score, objectiveFloor);`, {
    left: 654,
    top: 392,
    width: 520,
    height: 142,
  });
  infoCard(s11, "Software architecture takeaway", "The code avoids one large bot script. Each module has a narrow responsibility, which makes the system testable, explainable, and easier to extend for new modes.", {
    left: 196,
    top: 568,
    width: 850,
    height: 72,
  }, C.amber, 12.5, 10.3);
  notes(s11, [
    "[Sources]",
    "- Local Unity C# implementation files.",
    "- Code snippets are concise presentation extracts from the implemented AI pipeline.",
  ]);

  const s12 = slideAt(presentation, 12);
  setText(s12, "Title 1", "Results & Output");
  setText(s12, "Content Placeholder 2", "Sample playable outputs:");
  await addImage(s12, SCREENSHOTS.gemGrab, {
    left: 76,
    top: 168,
    width: 540,
    height: 310,
  }, "Gem Grab gameplay screenshot", "cover", { left: 0.02, top: 0.01, right: 0.02, bottom: 0.02 });
  await addImage(s12, SCREENSHOTS.matchResults, {
    left: 664,
    top: 168,
    width: 540,
    height: 310,
  }, "Match result screenshot", "cover", { left: 0.02, top: 0.02, right: 0.02, bottom: 0.02 });
  caption(s12, "Gem Grab now exposes countdown pressure, gem counts, objective area control, and tactical combat context.", {
    left: 76,
    top: 500,
    width: 540,
    height: 58,
  }, C.magenta);
  caption(s12, "Match result UI records kills, damage, assists, gems, rating, winner, and star player for validation.", {
    left: 664,
    top: 500,
    width: 540,
    height: 58,
  }, C.green);
  notes(s12, [
    "[Sources]",
    "- User-provided Gem Grab gameplay screenshot.",
    "- User-provided match results screenshot.",
  ]);

  const s13 = slideAt(presentation, 13);
  setText(s13, "Title 1", "Testing & Validation");
  removeElement(s13, "Content Placeholder 2");
  text(s13, "Validation combines Unity playtests with live AI telemetry so each movement, target, route, and objective choice can be inspected.", {
    left: 96,
    top: 180,
    width: 1010,
    height: 44,
  }, {
    fontSize: 22,
    color: C.black,
  });
  await addImage(s13, SCREENSHOTS.debugWide, {
    left: 76,
    top: 220,
    width: 540,
    height: 310,
  }, "Scene view AI debug screenshot", "cover", { left: 0.02, top: 0.0, right: 0.02, bottom: 0.02 });
  await addImage(s13, SCREENSHOTS.debugCombat, {
    left: 664,
    top: 220,
    width: 540,
    height: 310,
  }, "Combat debug screenshot", "cover", { left: 0.02, top: 0.0, right: 0.02, bottom: 0.02 });
  caption(s13, "Debug overlay: action, intent, role, tier, target, confidence, path length, stuck count, and map rays.", {
    left: 285,
    top: 550,
    width: 710,
    height: 48,
  }, C.blue);
  notes(s13, [
    "[Sources]",
    "- User-provided scene/debug screenshots from playtesting.",
    "- Major project final report testing and validation section.",
  ]);

  const s14 = slideAt(presentation, 14);
  setText(s14, "Title 1", "Conclusion and Future enhancements");
  removeElement(s14, "Content Placeholder 2");
  card(s14, { left: 96, top: 205, width: 310, height: 230 }, "#f8fafc", C.magenta);
  text(s14, "Conclusion", { left: 124, top: 232, width: 250, height: 28 }, {
    fontSize: 22,
    bold: true,
    color: C.magenta,
  });
  text(s14, "Layered, objective-aware AI for a Unity real-time game prototype.", {
    left: 124,
    top: 286,
    width: 240,
    height: 78,
  }, { fontSize: 15.5, color: C.black, lineSpacing: 1.05 });
  card(s14, { left: 486, top: 205, width: 310, height: 230 }, "#f8fafc", C.green);
  text(s14, "Achievements", { left: 514, top: 232, width: 250, height: 28 }, {
    fontSize: 22,
    bold: true,
    color: C.green,
  });
  text(s14, "Utility scoring, team blackboard, role policies, navigation recovery, and debug telemetry.", {
    left: 514,
    top: 286,
    width: 240,
    height: 94,
  }, { fontSize: 15.5, color: C.black, lineSpacing: 1.05 });
  card(s14, { left: 876, top: 205, width: 310, height: 230 }, "#f8fafc", C.blue);
  text(s14, "Future Work", { left: 904, top: 232, width: 250, height: 28 }, {
    fontSize: 22,
    bold: true,
    color: C.blue,
  });
  text(s14, "Automated gauntlets, richer semantic maps, broader role packs, and optional ML experiments.", {
    left: 904,
    top: 286,
    width: 240,
    height: 94,
  }, { fontSize: 15.5, color: C.black, lineSpacing: 1.05 });
  notes(s14, [
    "[Sources]",
    "- Major project final report conclusion and future work sections.",
  ]);

  const s15 = slideAt(presentation, 15);
  setText(s15, "Title 1", "References");
  removeElement(s15, "Content Placeholder 2");
  text(s15, "1. Akash Dhyani, Advanced Game AI System using Blackboard Architecture in Unity Engine, proposal, interim report, and final report, 2026.\n2. Ian Millington and John Funge, Artificial Intelligence for Games.\n3. Georgios N. Yannakakis and Julian Togelius, Artificial Intelligence and Games.\n4. Hart, Nilsson, and Raphael, A Formal Basis for the Heuristic Determination of Minimum Cost Paths, 1968.\n5. Unity documentation: ScriptableObject workflow, gameplay architecture, and Unity AI / ML-Agents concepts.\n6. Project source code: AI scoring, blackboard, navigation, mode strategy, role intelligence, and debug telemetry.", {
    left: 98,
    top: 176,
    width: 1020,
    height: 386,
  }, {
    fontSize: 20,
    color: C.black,
    lineSpacing: 1.12,
  });
  notes(s15, [
    "[Sources]",
    "- Major project final report references.",
    "- Local project source code and submitted report artifacts.",
  ]);

  const s16 = slideAt(presentation, 16);
  setText(s16, "Google Shape;275;p45", "THANK YOU");
  setText(s16, "Google Shape;256;p43", "Questions?");
  notes(s16, [
    "[Sources]",
    "- Uploaded BCA presentation template closing slide reused with project closing text.",
  ]);

  const sourceNotes = [
    "Presentation source notes",
    "- Visual template: /Users/vapronix/Downloads/BCA Project Presentation Template.pptx",
    "- Report source: Reports/Major_Project_Final_Report_AI_Akash_Dhyani.docx",
    "- Gameplay evidence: user-provided screenshots stored under Reports/presentation_build/screenshots",
    "- Code evidence: local Unity C# implementation modules in Assets/Scripts/Core",
  ].join("\n");
  await fs.writeFile(SOURCE_NOTES, sourceNotes);

  for (const [index, slide] of presentation.slides.items.entries()) {
    const stem = `slide-${String(index + 1).padStart(2, "0")}`;
    await writeBlob(`${RENDER_DIR}/${stem}.png`, await presentation.export({ slide, format: "png", scale: 1 }));
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(`${LAYOUT_DIR}/${stem}.layout.json`, await layout.text());
  }

  await writeBlob(MONTAGE, await presentation.export({ format: "webp", montage: true, scale: 1 }));
  const inspect = await presentation.inspect({
    kind: "slide,textbox,shape,image,notes,layout",
    maxChars: 30000,
  });
  await fs.writeFile(FINAL_INSPECT, inspect.ndjson);

  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(FINAL);
}

build().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
