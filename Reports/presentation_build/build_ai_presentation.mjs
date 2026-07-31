import fs from "node:fs/promises";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const TMP = "/Users/vapronix/Documents/GitHub/MOBA/Csharp-HighPerf-MOBA-Core/Reports/presentation_build";
const STARTER = `${TMP}/template-starter.pptx`;
const FINAL = "/Users/vapronix/Documents/GitHub/MOBA/Csharp-HighPerf-MOBA-Core/Reports/AdvancedGameAI_FinalPresentation_AA.SC.U3BCA2307092.pptx";
const RENDER_DIR = `${TMP}/final-render`;
const LAYOUT_DIR = `${TMP}/final-layout`;
const MONTAGE = `${TMP}/final-montage.webp`;

const SCREENSHOTS = {
  gemGrab: `${TMP}/screenshots/gem-grab-countdown.png`,
  brawlBall: `${TMP}/screenshots/brawl-ball-objective.png`,
  knockout: `${TMP}/screenshots/knockout-camera-round.png`,
  debugWide: `${TMP}/screenshots/ai-debug-map-wide.png`,
  debugCombat: `${TMP}/screenshots/ai-debug-combat.png`,
  matchResults: `${TMP}/screenshots/match-results.png`,
  brawlerSelect: `${TMP}/screenshots/brawler-select.png`,
};

const W = 1280;
const H = 720;
const C = {
  bg: "#07111f",
  bg2: "#0b1730",
  panel: "#101d36",
  panel2: "#132746",
  panelDark: "#050b14",
  text: "#f7fbff",
  muted: "#aebbd0",
  dim: "#718096",
  blue: "#2d7ff9",
  cyan: "#35c9ff",
  magenta: "#c01855",
  purple: "#7d3cff",
  amber: "#f5a819",
  green: "#39d98a",
  red: "#ff4d5f",
  line: "#233757",
  white: "#ffffff",
};

let slideCursor = 0;

async function writeBlob(path, blob) {
  await fs.writeFile(path, new Uint8Array(await blob.arrayBuffer()));
}

function addShape(slide, geometry, position, fill, line = "none", opts = {}) {
  return slide.shapes.add({
    geometry,
    position,
    fill,
    line:
      line === "none"
        ? { style: "solid", fill: "none", width: 0 }
        : line,
    ...opts,
  });
}

function addText(slide, text, position, style = {}) {
  const box = addShape(slide, "textbox", position, "none", "none");
  box.text = text;
  box.text.style = {
    fontSize: style.fontSize ?? 20,
    bold: style.bold ?? false,
    color: style.color ?? C.text,
    alignment: style.alignment ?? "left",
    verticalAlignment: style.verticalAlignment ?? "top",
    typeface: style.typeface ?? "Aptos",
    lineSpacing: style.lineSpacing ?? 1.05,
    insets: style.insets ?? { top: 2, right: 4, bottom: 2, left: 4 },
    wrap: "square",
  };
  return box;
}

function addCard(slide, position, fill = C.panel, accent = C.cyan, opts = {}) {
  const card = addShape(slide, "roundRect", position, fill, {
    style: "solid",
    fill: opts.line ?? C.line,
    width: opts.lineWidth ?? 1,
  }, {
    borderRadius: opts.radius ?? 8,
    shadow: opts.shadow ?? "shadow-sm",
  });
  if (accent) {
    addShape(slide, "rect", {
      left: position.left,
      top: position.top,
      width: 8,
      height: position.height,
    }, accent, "none");
  }
  return card;
}

function addHeader(slide, section, title) {
  addShape(slide, "rect", { left: 0, top: 0, width: W, height: 720 }, C.bg, "none");
  addShape(slide, "rect", { left: 0, top: 0, width: W, height: 12 }, C.magenta, "none");
  addText(slide, section.toUpperCase(), { left: 58, top: 38, width: 360, height: 26 }, {
    fontSize: 12,
    bold: true,
    color: C.cyan,
  });
  addText(slide, title, { left: 56, top: 63, width: 1040, height: 58 }, {
    fontSize: 30,
    bold: true,
    color: C.text,
  });
  addText(slide, "Advanced Game AI System | Akash Dhyani | AA.SC.U3BCA2307092", {
    left: 56,
    top: 662,
    width: 720,
    height: 24,
  }, {
    fontSize: 11,
    color: C.dim,
  });
}

function addFooterNumber(slide, number) {
  addText(slide, String(number).padStart(2, "0"), {
    left: 1166,
    top: 658,
    width: 52,
    height: 26,
  }, {
    fontSize: 14,
    bold: true,
    color: C.amber,
    alignment: "right",
  });
}

function addPill(slide, label, x, y, w, fill, color = C.text) {
  addShape(slide, "roundRect", { left: x, top: y, width: w, height: 28 }, fill, "none", {
    borderRadius: 8,
  });
  addText(slide, label, { left: x + 8, top: y + 5, width: w - 16, height: 18 }, {
    fontSize: 11,
    bold: true,
    color,
    alignment: "center",
    verticalAlignment: "middle",
  });
}

function addBullets(slide, items, x, y, w, fontSize = 16, color = C.text, gap = 38) {
  items.forEach((item, index) => {
    const top = y + index * gap;
    addShape(slide, "ellipse", { left: x, top: top + 7, width: 9, height: 9 }, C.amber, "none");
    addText(slide, item, { left: x + 20, top, width: w - 20, height: gap - 2 }, {
      fontSize,
      color,
      lineSpacing: 1.02,
    });
  });
}

async function addImage(slide, path, position, alt, fit = "cover", opts = {}) {
  const bytes = await fs.readFile(path);
  const image = slide.images.add({
    blob: bytes,
    contentType: "image/png",
    alt,
    fit,
    position,
    geometry: opts.geometry ?? "roundRect",
    borderRadius: opts.radius ?? 8,
    ...(opts.crop ? { crop: opts.crop } : {}),
  });
  if (opts.line !== false) {
    addShape(slide, "roundRect", position, "none", {
      style: "solid",
      fill: opts.lineColor ?? C.line,
      width: opts.lineWidth ?? 2,
    }, {
      borderRadius: opts.radius ?? 8,
    });
  }
  return image;
}

function addCaption(slide, text, position, accent = C.cyan) {
  addCard(slide, position, "#07111fcc", accent, { line: "#162541", radius: 6 });
  addText(slide, text, {
    left: position.left + 18,
    top: position.top + 12,
    width: position.width - 28,
    height: position.height - 18,
  }, {
    fontSize: 14,
    color: C.text,
    bold: true,
  });
}

function addFlowNode(slide, label, x, y, w, h, fill, accent = C.cyan, size = 15) {
  addCard(slide, { left: x, top: y, width: w, height: h }, fill, accent, { radius: 8 });
  addText(slide, label, { left: x + 16, top: y + 13, width: w - 26, height: h - 18 }, {
    fontSize: size,
    bold: true,
    color: C.text,
    alignment: "center",
    verticalAlignment: "middle",
  });
}

function addArrow(slide, x, y, w, h, color = C.amber) {
  addShape(slide, "rightArrow", { left: x, top: y, width: w, height: h }, color, "none");
}

function addCodePanel(slide, title, code, position, accent = C.green) {
  addCard(slide, position, "#07101d", accent, { radius: 6, line: "#263858" });
  addText(slide, title, {
    left: position.left + 18,
    top: position.top + 14,
    width: position.width - 36,
    height: 24,
  }, {
    fontSize: 13,
    bold: true,
    color: accent,
  });
  addText(slide, code, {
    left: position.left + 22,
    top: position.top + 48,
    width: position.width - 44,
    height: position.height - 58,
  }, {
    fontSize: 11,
    color: "#d8ffe7",
    typeface: "Menlo",
    lineSpacing: 0.95,
  });
}

function addMetric(slide, value, label, x, y, color) {
  addCard(slide, { left: x, top: y, width: 184, height: 96 }, "#091628", color, {
    radius: 8,
    line: "#1c2f4d",
  });
  addText(slide, value, { left: x + 18, top: y + 17, width: 146, height: 34 }, {
    fontSize: 27,
    bold: true,
    color,
    alignment: "center",
  });
  addText(slide, label, { left: x + 16, top: y + 55, width: 152, height: 28 }, {
    fontSize: 11,
    bold: true,
    color: C.muted,
    alignment: "center",
  });
}

function addSlide(presentation, section, title, notes) {
  const slide = nextSlide(presentation);
  addHeader(slide, section, title);
  slide.speakerNotes.textFrame.setText(notes);
  addFooterNumber(slide, slideCursor);
  return slide;
}

function nextSlide(presentation) {
  const slide = presentation.slides.items[slideCursor];
  if (!slide) {
    throw new Error(`Template starter has only ${presentation.slides.items.length} slides.`);
  }
  for (const element of [...slide.elements.items]) {
    element.delete();
  }
  slideCursor++;
  return slide;
}

function addTitleSlide(presentation) {
  const slide = nextSlide(presentation);
  addShape(slide, "rect", { left: 0, top: 0, width: W, height: H }, "#f7f8fb", "none");
  addShape(slide, "rect", { left: 820, top: 0, width: 460, height: H }, C.bg2, "none");
  addShape(slide, "rect", { left: 744, top: -82, width: 54, height: 880, rotation: 34 }, C.magenta, "none");
  addShape(slide, "roundRect", { left: 948, top: 96, width: 170, height: 170, rotation: 45 }, "#263b58", {
    style: "solid",
    fill: "#ffffff66",
    width: 1,
  }, { borderRadius: 12, shadow: "shadow-md" });
  addShape(slide, "roundRect", { left: 979, top: 475, width: 152, height: 152, rotation: 45 }, C.magenta, "none", {
    borderRadius: 12,
    shadow: "shadow-md",
  });
  addText(slide, "AMRITA | Online", { left: 72, top: 70, width: 440, height: 50 }, {
    fontSize: 31,
    bold: true,
    color: C.magenta,
  });
  addText(slide, "21CSA699A - Major Project", { left: 72, top: 214, width: 690, height: 70 }, {
    fontSize: 43,
    bold: true,
    color: C.magenta,
  });
  addText(slide, "Advanced Game AI System\nUsing Blackboard Architecture", {
    left: 74,
    top: 316,
    width: 720,
    height: 92,
  }, {
    fontSize: 27,
    bold: true,
    color: "#15263a",
    lineSpacing: 1.04,
  });
  addText(slide, "Unity Engine | C# | MOBA/Brawler AI Prototype", {
    left: 76,
    top: 436,
    width: 600,
    height: 34,
  }, {
    fontSize: 19,
    color: "#1f2937",
  });
  addText(slide, "Akash Dhyani\nAA.SC.U3BCA2307092\nProject Guide: Deepa Sreedhar\nCoordinator: Amrita Sindhu\nJuly 2026", {
    left: 76,
    top: 520,
    width: 468,
    height: 120,
  }, {
    fontSize: 17,
    color: "#1f2937",
    lineSpacing: 1.15,
  });
  slide.speakerNotes.textFrame.setText([
    "Source: Final major project report title page and project details.",
    "Opening focus: the project is presented as an AI architecture project, not only a Unity gameplay prototype.",
  ]);
}

function addProjectContext(presentation) {
  const slide = addSlide(
    presentation,
    "Context",
    "Project Identity and Development Goal",
    "Source: final report front matter, proposal, and interim report. This slide establishes the formal project identity and the AI-first goal."
  );
  addCard(slide, { left: 64, top: 144, width: 530, height: 402 }, C.panel, C.magenta);
  addText(slide, "Student and Submission Details", { left: 92, top: 174, width: 450, height: 32 }, {
    fontSize: 23,
    bold: true,
    color: C.text,
  });
  addBullets(slide, [
    "Name: Akash Dhyani",
    "Register No: AA.SC.U3BCA2307092",
    "Program: Bachelor of Computer Applications",
    "Project Guide: Deepa Sreedhar",
    "Project Coordinator: Amrita Sindhu",
    "Technology: Unity Engine with C# gameplay AI",
  ], 96, 236, 450, 15, C.muted, 42);
  addCard(slide, { left: 646, top: 144, width: 568, height: 402 }, C.panel2, C.cyan);
  addText(slide, "Core Aim", { left: 674, top: 174, width: 420, height: 32 }, {
    fontSize: 23,
    bold: true,
  });
  addText(slide,
    "To build a scalable, explainable, and game-mode-aware AI stack for a real-time brawler/MOBA prototype. The project evolved through iterative playtesting: every observed weakness became a system-level improvement rather than a one-off patch.",
    { left: 674, top: 228, width: 488, height: 128 },
    { fontSize: 19, color: C.text, lineSpacing: 1.1 }
  );
  addPill(slide, "AAA mindset", 674, 394, 138, C.amber, "#111827");
  addPill(slide, "Reusable systems", 832, 394, 156, C.green, "#07111f");
  addPill(slide, "Debuggable AI", 1008, 394, 140, C.purple);
}

function addProblem(presentation) {
  const slide = addSlide(
    presentation,
    "Problem",
    "Why Simple Game Bots Were Not Enough",
    "Source: proposal and final report problem statement. This slide frames why the architecture had to move beyond simple nearest-target scripts."
  );
  addText(slide, "Early bot behavior looked playable in isolation, but failed when objectives, obstacles, team pressure, and brawler roles interacted.", {
    left: 72,
    top: 135,
    width: 1136,
    height: 54,
  }, {
    fontSize: 22,
    color: C.muted,
    alignment: "center",
  });
  const problems = [
    ["Flicker", "rapid action switching when scores were close"],
    ["Clumping", "multiple allies crowding one objective or target"],
    ["Objective neglect", "combat decisions overriding gems or ball control"],
    ["Path stalls", "bots stopping on boundaries or obstacles"],
    ["Generic behavior", "every brawler acting like the same shooter"],
    ["Low observability", "hard to prove why a bot moved or stopped"],
  ];
  problems.forEach(([title, body], index) => {
    const col = index % 3;
    const row = Math.floor(index / 3);
    const x = 86 + col * 382;
    const y = 236 + row * 150;
    addCard(slide, { left: x, top: y, width: 328, height: 112 }, "#0b1730", index % 2 ? C.red : C.amber);
    addText(slide, title, { left: x + 28, top: y + 20, width: 268, height: 24 }, {
      fontSize: 21,
      bold: true,
      color: C.text,
    });
    addText(slide, body, { left: x + 28, top: y + 55, width: 272, height: 40 }, {
      fontSize: 14,
      color: C.muted,
    });
  });
}

function addWrongFlow(presentation) {
  const slide = addSlide(
    presentation,
    "Comparison",
    "Common Bot Flow vs MOBA Core AI Flow",
    "Source: final report architecture and observed playtest problems. The comparison summarizes what was wrong and how the project resolves it."
  );
  addText(slide, "Typical makeshift bot", { left: 82, top: 142, width: 460, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.red,
  });
  addText(slide, "Our AAA-style architecture", { left: 704, top: 142, width: 460, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.green,
  });
  const bad = [
    "Find nearest enemy",
    "Move in direct line",
    "Shoot on cooldown",
    "Ignore objective context",
    "Patch every failure case",
  ];
  const good = [
    "Sense world + team state",
    "Score win-condition utility",
    "Commit with hysteresis",
    "Route with map safety",
    "Record debug telemetry",
  ];
  bad.forEach((label, i) => {
    const y = 208 + i * 72;
    addFlowNode(slide, label, 82, y, 300, 42, "#240c15", C.red, 14);
    if (i < bad.length - 1) addArrow(slide, 395, y + 12, 52, 18, "#7d1e2c");
  });
  good.forEach((label, i) => {
    const y = 208 + i * 72;
    addFlowNode(slide, label, 704, y, 320, 42, "#092013", C.green, 14);
    if (i < good.length - 1) addArrow(slide, 1038, y + 12, 52, 18, "#247a4b");
  });
  addCard(slide, { left: 462, top: 238, width: 188, height: 236 }, "#08111d", C.amber, { radius: 8 });
  addText(slide, "Main difference", { left: 488, top: 260, width: 134, height: 24 }, {
    fontSize: 17,
    bold: true,
    color: C.amber,
    alignment: "center",
  });
  addText(slide, "The bot is not a script reacting to one target. It is a decision system balancing combat, objective, team, role, and survival context every tick.", {
    left: 488,
    top: 306,
    width: 136,
    height: 138,
  }, {
    fontSize: 14,
    color: C.muted,
    alignment: "center",
  });
}

function addArchitecture(presentation) {
  const slide = addSlide(
    presentation,
    "Architecture",
    "Layered AI System Architecture",
    "Source: final report system design and implementation modules. Diagram shows the runtime data flow and support systems."
  );
  const y1 = 176;
  const nodes = [
    ["Perception\n& Memory", 74, y1, C.cyan],
    ["Target\nScoring", 254, y1, C.amber],
    ["Utility\nScoring", 434, y1, C.green],
    ["Commitment\nHysteresis", 614, y1, C.purple],
    ["Action\nExecutor", 794, y1, C.blue],
    ["Brawler\nSimulation", 974, y1, C.magenta],
  ];
  nodes.forEach(([label, x, y, accent]) => addFlowNode(slide, label, x, y, 146, 72, C.panel, accent, 15));
  for (let i = 0; i < nodes.length - 1; i++) {
    addArrow(slide, 222 + i * 180, y1 + 24, 42, 24, C.amber);
  }
  addCard(slide, { left: 94, top: 338, width: 316, height: 152 }, "#081528", C.cyan);
  addText(slide, "Team Blackboard", { left: 120, top: 366, width: 260, height: 28 }, {
    fontSize: 20,
    bold: true,
    color: C.cyan,
  });
  addText(slide, "Focus counts, ally pressure, lane ownership, threat center, carrier protection, and shared playbook calls.", {
    left: 120,
    top: 410,
    width: 252,
    height: 58,
  }, {
    fontSize: 14,
    color: C.muted,
  });
  addCard(slide, { left: 482, top: 338, width: 316, height: 152 }, "#081528", C.green);
  addText(slide, "Mode Macro Strategy", { left: 508, top: 366, width: 260, height: 28 }, {
    fontSize: 20,
    bold: true,
    color: C.green,
  });
  addText(slide, "Gem Grab, Knockout, Brawl Ball, Solo Showdown, countdown, final pressure, push, hold, reset.", {
    left: 508,
    top: 410,
    width: 252,
    height: 58,
  }, {
    fontSize: 14,
    color: C.muted,
  });
  addCard(slide, { left: 870, top: 338, width: 316, height: 152 }, "#081528", C.purple);
  addText(slide, "Validation & Telemetry", { left: 896, top: 366, width: 260, height: 28 }, {
    fontSize: 20,
    bold: true,
    color: C.purple,
  });
  addText(slide, "Debug overlay, inspector state, report cards, incidents, outlier review, and movement liveness checks.", {
    left: 896,
    top: 410,
    width: 252,
    height: 58,
  }, {
    fontSize: 14,
    color: C.muted,
  });
}

function addTickLoop(presentation) {
  const slide = addSlide(
    presentation,
    "Runtime",
    "One AI Tick: From World State to Command",
    "Source: final report methodology and implementation notes. This explains how the bot chooses a stable action during gameplay."
  );
  const steps = [
    ["1", "Sense", "Visible enemies, allies, gems, ball, cover, poison, obstacles"],
    ["2", "Remember", "Target memory, recent threats, lane ownership, stuck history"],
    ["3", "Score", "Comparable 0-100 action utilities with objective floors"],
    ["4", "Commit", "Hysteresis prevents jitter; emergency overrides remain active"],
    ["5", "Move + Act", "Route, aim, ability, super, fallback, and debug snapshot"],
  ];
  steps.forEach(([n, title, body], index) => {
    const x = 80 + index * 232;
    addShape(slide, "ellipse", { left: x, top: 185, width: 76, height: 76 }, index % 2 ? C.purple : C.cyan, "none");
    addText(slide, n, { left: x, top: 202, width: 76, height: 38 }, {
      fontSize: 30,
      bold: true,
      alignment: "center",
      verticalAlignment: "middle",
    });
    addText(slide, title, { left: x - 38, top: 284, width: 152, height: 30 }, {
      fontSize: 20,
      bold: true,
      alignment: "center",
    });
    addText(slide, body, { left: x - 58, top: 325, width: 192, height: 100 }, {
      fontSize: 13,
      color: C.muted,
      alignment: "center",
    });
    if (index < steps.length - 1) addArrow(slide, x + 91, 212, 70, 26, C.amber);
  });
  addCard(slide, { left: 196, top: 502, width: 888, height: 82 }, "#081528", C.amber);
  addText(slide, "Design principle", { left: 232, top: 524, width: 160, height: 24 }, {
    fontSize: 16,
    bold: true,
    color: C.amber,
  });
  addText(slide, "Every layer has one responsibility, so improvements to movement, combat, brawler identity, or mode strategy do not collapse into a single fragile script.", {
    left: 402,
    top: 520,
    width: 636,
    height: 40,
  }, {
    fontSize: 16,
    color: C.text,
  });
}

function addModeStrategy(presentation) {
  const slide = addSlide(
    presentation,
    "Mode AI",
    "Game Mode Strategy Is the Highest Priority",
    "Source: implementation of AIGameModeMacroStrategy and recent playtest fixes. Modes produce macro calls that guide lower-level tactics."
  );
  const modes = [
    ["Gem Grab", "Collect gems, protect carrier, contest mine, pressure enemy carrier", C.magenta],
    ["Knockout", "No respawn survival, center pressure, threat spacing, poison avoidance", C.green],
    ["Brawl Ball", "Claim loose ball, pressure carrier, route to goal, pass or shoot", C.amber],
    ["Solo Showdown", "Survive poison, collect cubes, avoid bad early fights, finish weak targets", C.purple],
  ];
  modes.forEach(([mode, body, color], i) => {
    const x = i % 2 === 0 ? 82 : 676;
    const y = i < 2 ? 164 : 380;
    addCard(slide, { left: x, top: y, width: 510, height: 144 }, C.panel, color);
    addText(slide, mode, { left: x + 32, top: y + 24, width: 420, height: 28 }, {
      fontSize: 24,
      bold: true,
      color,
    });
    addText(slide, body, { left: x + 32, top: y + 68, width: 430, height: 52 }, {
      fontSize: 15,
      color: C.muted,
    });
  });
  addText(slide, "Mode strategy sets the win condition. Brawler personality changes how the bot pursues it, not whether it cares about it.", {
    left: 190,
    top: 560,
    width: 900,
    height: 44,
  }, {
    fontSize: 20,
    bold: true,
    color: C.text,
    alignment: "center",
  });
}

function addBrawlerRoles(presentation) {
  const slide = addSlide(
    presentation,
    "Brawler AI",
    "Role-Specific Tactical Intelligence",
    "Source: final report brawler-specific intelligence section and implemented brawler tactical identity packs."
  );
  const roles = [
    ["Colt", "Long-range damage. Holds line pressure and punishes bad angles.", C.red],
    ["Jessie", "Controller/support. Values clumps, bounce shots, and turret control.", C.amber],
    ["Byron", "Support sniper. Heals allies below safety threshold and chips enemies.", C.green],
    ["Barley", "Thrower. Plays behind walls and layers area denial.", C.purple],
    ["El Primo", "Tank. Uses cover/bush approach and commits when close or super-ready.", C.blue],
    ["Bo", "Controller. Uses ranged chip and mines around lanes, goals, and objectives.", C.cyan],
    ["Piper", "Sniper. Wants distance, burst damage, and leap reposition.", C.magenta],
    ["Leon", "Assassin. Uses stealth windows and close pressure without blind chasing.", "#8ef05e"],
  ];
  roles.forEach(([name, body, color], index) => {
    const col = index % 4;
    const row = Math.floor(index / 4);
    const x = 66 + col * 302;
    const y = 160 + row * 205;
    addCard(slide, { left: x, top: y, width: 262, height: 154 }, "#091628", color);
    addText(slide, name, { left: x + 26, top: y + 22, width: 210, height: 28 }, {
      fontSize: 21,
      bold: true,
      color,
    });
    addText(slide, body, { left: x + 26, top: y + 64, width: 210, height: 64 }, {
      fontSize: 13,
      color: C.muted,
    });
  });
  addText(slide, "Generic logic decides the common question: what helps the team win now? Role packs decide how this brawler should do it.", {
    left: 154,
    top: 586,
    width: 972,
    height: 36,
  }, {
    fontSize: 17,
    color: C.text,
    alignment: "center",
  });
}

function addNavigation(presentation) {
  const slide = addSlide(
    presentation,
    "Movement",
    "Map-Aware Navigation and Failure Recovery",
    "Source: AIMapNavigationUtility, NavigationAgent, movement liveness watchdog, and playtest fixes for obstacle stalls."
  );
  addCard(slide, { left: 72, top: 150, width: 472, height: 398 }, C.panel, C.cyan);
  addText(slide, "Movement standards", { left: 104, top: 182, width: 340, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.cyan,
  });
  addBullets(slide, [
    "Grounded grid cells only: no boundary wandering",
    "A* routes with route intent and safety evaluation",
    "Destination abandonment when progress stalls",
    "Anti-starvation budget so one bot cannot consume all path work",
    "Smoothing to reduce jitter and sudden unnatural turns",
  ], 108, 244, 380, 15, C.muted, 45);
  addCard(slide, { left: 612, top: 150, width: 596, height: 398 }, C.panel2, C.amber);
  addText(slide, "Recovery loop", { left: 648, top: 182, width: 360, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.amber,
  });
  const x0 = 654;
  const flow = [
    ["Detect stall", 212],
    ["Invalidate bad route", 286],
    ["Choose fallback cell", 360],
    ["Resume tactical intent", 434],
  ];
  flow.forEach(([label, y], index) => {
    addFlowNode(slide, label, x0 + index * 126, y, 112, 44, "#081528", index % 2 ? C.green : C.amber, 12);
    if (index < flow.length - 1) addArrow(slide, x0 + index * 126 + 116, y + 13, 36, 18, C.cyan);
  });
  addText(slide, "The bot does not stand forever when a path becomes invalid. It detects low movement progress, abandons stale commands, and selects a safer reachable alternative.", {
    left: 650,
    top: 506,
    width: 500,
    height: 50,
  }, {
    fontSize: 15,
    color: C.muted,
  });
}

function addAbilityIntelligence(presentation) {
  const slide = addSlide(
    presentation,
    "Combat",
    "Predictive Combat and Ability Intelligence",
    "Source: ability intelligence packs, predictive combat utilities, shot obstacle checks, and brawler definitions."
  );
  addMetric(slide, "Aim", "lead targets, avoid bad fire lanes", 90, 172, C.cyan);
  addMetric(slide, "Ammo", "discipline instead of spam", 318, 172, C.amber);
  addMetric(slide, "Super", "setup windows and objective value", 546, 172, C.purple);
  addMetric(slide, "Team", "heal, peel, pass, escort, collapse", 774, 172, C.green);
  addMetric(slide, "Mode", "objective floor over blind fighting", 1002, 172, C.magenta);
  addCard(slide, { left: 88, top: 336, width: 504, height: 172 }, "#081528", C.green);
  addText(slide, "Examples", { left: 120, top: 366, width: 220, height: 28 }, {
    fontSize: 22,
    bold: true,
    color: C.green,
  });
  addBullets(slide, [
    "Byron chooses heal-vs-damage based on ally health and fight pressure",
    "Barley prefers indirect throw lanes instead of standing in line of fire",
    "Bo places mines in high-value traffic zones",
  ], 126, 414, 410, 14, C.muted, 36);
  addCard(slide, { left: 670, top: 336, width: 504, height: 172 }, "#081528", C.amber);
  addText(slide, "Fairness", { left: 702, top: 366, width: 220, height: 28 }, {
    fontSize: 22,
    bold: true,
    color: C.amber,
  });
  addBullets(slide, [
    "Difficulty and tier adjust reaction rhythm, commitment, aim confidence, and mistakes",
    "Hard/elite bots are smarter, not secretly immune or overpowered",
    "Behavior remains inspectable through debug telemetry",
  ], 708, 414, 410, 14, C.muted, 36);
}

async function addEvidenceGemGrab(presentation) {
  const slide = addSlide(
    presentation,
    "Evidence",
    "Gem Grab Objective Evidence",
    "Source: user-provided gameplay screenshot. Shows the gem mine area, countdown state, and objective-driven combat context."
  );
  await addImage(slide, SCREENSHOTS.gemGrab, { left: 64, top: 136, width: 774, height: 456 }, "Gem Grab gameplay screenshot", "cover", {
    crop: { left: 0.02, top: 0.01, right: 0.02, bottom: 0.02 },
  });
  addCaption(slide, "Gem Grab evidence: bots must value the central mine, carrier safety, and countdown pressure over random duels.", {
    left: 870,
    top: 146,
    width: 320,
    height: 118,
  }, C.magenta);
  addCard(slide, { left: 870, top: 298, width: 320, height: 248 }, "#081528", C.green);
  addText(slide, "AI requirements", { left: 900, top: 324, width: 250, height: 28 }, {
    fontSize: 21,
    bold: true,
    color: C.green,
  });
  addBullets(slide, [
    "Collect loose gems quickly",
    "Protect own carrier",
    "Pressure enemy carrier",
    "Contest mine when behind",
    "Retreat during countdown",
  ], 904, 372, 250, 14, C.muted, 32);
}

async function addEvidenceModes(presentation) {
  const slide = addSlide(
    presentation,
    "Evidence",
    "Brawl Ball and Knockout Playtest Evidence",
    "Source: user-provided screenshots plus latest gameplay fix. Both issues were converted into system-level corrections."
  );
  await addImage(slide, SCREENSHOTS.brawlBall, { left: 62, top: 142, width: 548, height: 324 }, "Brawl Ball gameplay screenshot", "cover", {
    crop: { left: 0.02, top: 0.0, right: 0.02, bottom: 0.0 },
  });
  await addImage(slide, SCREENSHOTS.knockout, { left: 670, top: 142, width: 548, height: 324 }, "Knockout gameplay screenshot", "cover", {
    crop: { left: 0.02, top: 0.0, right: 0.02, bottom: 0.0 },
  });
  addCaption(slide, "Brawl Ball fix: loose ball now creates a push macro call so bots claim the objective before drifting into generic combat.", {
    left: 82,
    top: 492,
    width: 500,
    height: 82,
  }, C.amber);
  addCaption(slide, "Knockout fix: local-player respawn/camera ownership now rebinds after the round reset, restoring control cleanly.", {
    left: 690,
    top: 492,
    width: 500,
    height: 82,
  }, C.green);
}

async function addDebugEvidence(presentation) {
  const slide = addSlide(
    presentation,
    "Debugging",
    "Scene View AI Debug Visibility",
    "Source: user-provided scene/debug screenshots. Debug overlays expose the exact reason a bot is moving, fighting, retreating, or recovering."
  );
  await addImage(slide, SCREENSHOTS.debugWide, { left: 64, top: 132, width: 540, height: 340 }, "AI debug wide screenshot", "cover", {
    crop: { left: 0.02, top: 0.0, right: 0.02, bottom: 0.02 },
  });
  await addImage(slide, SCREENSHOTS.debugCombat, { left: 676, top: 132, width: 540, height: 340 }, "AI debug combat screenshot", "cover", {
    crop: { left: 0.02, top: 0.0, right: 0.02, bottom: 0.02 },
  });
  addCard(slide, { left: 148, top: 504, width: 984, height: 82 }, "#081528", C.cyan);
  addText(slide, "Debug data shown in the editor: current action, intent, target, role, tier, confidence score, path length, route mode, stuck counter, destination, rays, and objective context.", {
    left: 184,
    top: 526,
    width: 912,
    height: 32,
  }, {
    fontSize: 17,
    bold: true,
    color: C.text,
    alignment: "center",
  });
}

async function addUiEvidence(presentation) {
  const slide = addSlide(
    presentation,
    "Evidence",
    "Playable Systems Around the AI",
    "Source: user-provided brawler select and match result screenshots. These screens support tuning, validation, and game-mode feedback."
  );
  await addImage(slide, SCREENSHOTS.brawlerSelect, { left: 66, top: 138, width: 548, height: 338 }, "Brawler select screenshot", "cover", {
    crop: { left: 0.01, top: 0.02, right: 0.01, bottom: 0.02 },
  });
  await addImage(slide, SCREENSHOTS.matchResults, { left: 666, top: 138, width: 548, height: 338 }, "Match result screenshot", "cover", {
    crop: { left: 0.0, top: 0.02, right: 0.0, bottom: 0.02 },
  });
  addCaption(slide, "Brawler select: role, power, gear, gadget, star power, hypercharge, and nanopower data are surfaced before play.", {
    left: 82,
    top: 502,
    width: 500,
    height: 74,
  }, C.amber);
  addCaption(slide, "Match end: kills, damage, assists, gems, rating, winner, and star player help validate whether AI behavior is useful.", {
    left: 682,
    top: 502,
    width: 500,
    height: 74,
  }, C.green);
}

function addCodeEvidence(presentation) {
  const slide = addSlide(
    presentation,
    "Implementation",
    "Source Excerpts From Recent Fixes",
    "Source: local C# implementation. These snippets document how reported issues were fixed at the system boundary."
  );
  addCodePanel(slide, "Brawl Ball objective fix", `else
{
    call = AIGameModeMacroCall.Push;
    reason = "loose_ball_claim";
}

return new AIGameModeMacroState(
    GameModeId.BrawlBall,
    call,
    phase,
    ownGoals,
    enemyGoals,
    goalsToWin,
    0f,
    matchTimeRemainingSeconds,
    isLeading,
    isBehind,
    false,
    false,
    reason);`, { left: 64, top: 146, width: 548, height: 392 }, C.amber);
  addCodePanel(slide, "Camera restoration after round reset", `private void CompleteRespawn(
    BrawlerController brawler,
    Vector3 position)
{
    if (brawler == null)
        return;

    brawler.gameObject.SetActive(true);
    brawler.Respawn(position);

    if (brawler.GetComponent<PlayerCommandSource>() != null)
        SetPlayerTarget(brawler.PresentationFollowTarget);

    OnBrawlerRespawned?.Invoke(brawler);
}`, { left: 668, top: 146, width: 548, height: 392 }, C.green);
  addText(slide, "Both changes avoid makeshift one-off checks: the mode layer now expresses objective priority, and respawn ownership emits a reusable lifecycle signal.", {
    left: 128,
    top: 572,
    width: 1024,
    height: 36,
  }, {
    fontSize: 17,
    color: C.text,
    alignment: "center",
  });
}

function addValidation(presentation) {
  const slide = addSlide(
    presentation,
    "Validation",
    "How AI Quality Is Checked",
    "Source: final report testing section and debug/telemetry implementation. Validation combines playtest feel with measurable telemetry."
  );
  const tracks = [
    ["Gameplay Gauntlet", "Retreat, peel, objective pickup, ability usage, path recovery, mode awareness"],
    ["Runtime Telemetry", "Decision confidence, role adherence, objective value, stuck count, route failures"],
    ["Debug Overlay", "Live intent, target, path, rays, score gap, tier, role, and current mode"],
    ["Regression Review", "Playtest issues become targeted fixes and committed evidence"],
  ];
  tracks.forEach(([title, body], index) => {
    const x = 110 + index * 286;
    addCard(slide, { left: x, top: 168, width: 236, height: 292 }, "#091628", [C.cyan, C.green, C.purple, C.amber][index]);
    addText(slide, title, { left: x + 22, top: 196, width: 192, height: 52 }, {
      fontSize: 19,
      bold: true,
      color: [C.cyan, C.green, C.purple, C.amber][index],
      alignment: "center",
    });
    addText(slide, body, { left: x + 26, top: 284, width: 184, height: 104 }, {
      fontSize: 14,
      color: C.muted,
      alignment: "center",
    });
  });
  addShape(slide, "rect", { left: 226, top: 520, width: 828, height: 3 }, C.line, "none");
  addText(slide, "Manual Unity playtesting is still required because game AI is judged by visible behavior, not only by passing code checks.", {
    left: 202,
    top: 546,
    width: 876,
    height: 42,
  }, {
    fontSize: 18,
    bold: true,
    color: C.text,
    alignment: "center",
  });
}

function addScalability(presentation) {
  const slide = addSlide(
    presentation,
    "Scalability",
    "Why the Architecture Scales",
    "Source: final report scalability discussion and implemented tuning assets."
  );
  const rows = [
    ["New brawler", "Add definitions + tactical identity + ability pack", "No rewrite of core scoring loop"],
    ["New mode", "Add macro provider + objective utility weights", "Bot roles keep working under new goal"],
    ["New map", "Add semantic lanes, cover, chokes, hotspots", "Navigation and positioning reuse shared map data"],
    ["New difficulty", "Tune profile assets and runtime tier", "Behavior changes without cheating"],
    ["New bug report", "Add telemetry signal + targeted recovery rule", "Issue becomes visible and testable"],
  ];
  rows.forEach(([change, extension, benefit], index) => {
    const y = 156 + index * 82;
    addCard(slide, { left: 88, top: y, width: 1104, height: 58 }, index % 2 ? "#0b1730" : "#101d36", null, {
      radius: 6,
      line: "#1e3354",
    });
    addText(slide, change, { left: 118, top: y + 16, width: 190, height: 22 }, {
      fontSize: 16,
      bold: true,
      color: C.amber,
    });
    addText(slide, extension, { left: 328, top: y + 16, width: 430, height: 22 }, {
      fontSize: 15,
      color: C.text,
    });
    addText(slide, benefit, { left: 790, top: y + 16, width: 360, height: 22 }, {
      fontSize: 15,
      color: C.green,
    });
  });
}

function addResults(presentation) {
  const slide = addSlide(
    presentation,
    "Results",
    "Current State of the AI",
    "Source: final report result section and roadmap completion history through production hardening and later gameplay work."
  );
  addMetric(slide, "Objective", "mode priorities and win-condition pressure", 92, 156, C.magenta);
  addMetric(slide, "Team", "blackboard, focus counts, escort, peel", 318, 156, C.green);
  addMetric(slide, "Combat", "aim, ability timing, role identity", 544, 156, C.amber);
  addMetric(slide, "Movement", "lanes, cover, routes, liveness recovery", 770, 156, C.cyan);
  addMetric(slide, "Debug", "overlay, report cards, incidents", 996, 156, C.purple);
  addCard(slide, { left: 140, top: 334, width: 1000, height: 176 }, "#081528", C.green);
  addText(slide, "Outcome", { left: 176, top: 364, width: 180, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.green,
  });
  addText(slide, "The AI is no longer a single behavior script. It is a layered decision stack with objective priorities, brawler roles, team signals, map navigation, failure recovery, and validation hooks. That is the direction used in production-quality game AI: modular, inspectable, tunable, and extensible.", {
    left: 176,
    top: 410,
    width: 916,
    height: 64,
  }, {
    fontSize: 17,
    color: C.text,
  });
}

function addFuture(presentation) {
  const slide = addSlide(
    presentation,
    "Future Work",
    "Next Improvements After the Report",
    "Source: final report future work plus latest game roadmap items."
  );
  addBullets(slide, [
    "Automated Unity playtest scenarios for mode objectives and AI regression",
    "Richer semantic maps with designer-authored lanes, cover clusters, and danger corridors",
    "More specialist brawler packs, gadgets, star powers, supers, and counters",
    "Optional ML-Agents experiments later for training movement or combat micro policies",
    "Network-ready authority boundaries if multiplayer support becomes the next phase",
  ], 120, 176, 1000, 19, C.text, 60);
  addCard(slide, { left: 214, top: 552, width: 852, height: 58 }, "#081528", C.amber);
  addText(slide, "The current architecture is designed so these additions plug into existing layers instead of replacing the whole AI.", {
    left: 244,
    top: 570,
    width: 792,
    height: 24,
  }, {
    fontSize: 16,
    bold: true,
    color: C.text,
    alignment: "center",
  });
}

function addReferences(presentation) {
  const slide = addSlide(
    presentation,
    "References",
    "References and Source Material",
    "Source: final report references, proposal, interim report, and local implementation files."
  );
  addCard(slide, { left: 84, top: 152, width: 1112, height: 392 }, C.panel, C.magenta);
  addBullets(slide, [
    "Akash Dhyani, Advanced Game AI System using Blackboard Architecture in Unity Engine, proposal, interim report, and final report, 2026.",
    "Ian Millington and John Funge, Artificial Intelligence for Games.",
    "Georgios N. Yannakakis and Julian Togelius, Artificial Intelligence and Games.",
    "Hart, Nilsson, and Raphael, A Formal Basis for the Heuristic Determination of Minimum Cost Paths, 1968.",
    "Unity documentation: ScriptableObject workflow, Unity AI/ML-Agents concepts, and Unity gameplay architecture references.",
    "Project source code: AI utility scoring, blackboard, navigation, mode macro strategy, debug telemetry, and brawler intelligence packs.",
  ], 128, 188, 1030, 15, C.muted, 50);
}

function addThankYou(presentation) {
  const slide = nextSlide(presentation);
  addShape(slide, "rect", { left: 0, top: 0, width: W, height: H }, C.bg, "none");
  addShape(slide, "rect", { left: 0, top: 0, width: W, height: 16 }, C.magenta, "none");
  addShape(slide, "ellipse", { left: 806, top: 108, width: 280, height: 280 }, "#0e2849", "none");
  addShape(slide, "roundRect", { left: 902, top: 284, width: 178, height: 178, rotation: 45 }, C.magenta, "none", {
    borderRadius: 12,
  });
  addText(slide, "THANK YOU", { left: 98, top: 210, width: 700, height: 78 }, {
    fontSize: 56,
    bold: true,
    color: C.text,
  });
  addText(slide, "Questions?", { left: 104, top: 312, width: 420, height: 42 }, {
    fontSize: 30,
    color: C.amber,
    bold: true,
  });
  addText(slide, "Advanced Game AI System\nAkash Dhyani | AA.SC.U3BCA2307092", {
    left: 106,
    top: 470,
    width: 550,
    height: 74,
  }, {
    fontSize: 19,
    color: C.muted,
    lineSpacing: 1.1,
  });
  slide.speakerNotes.textFrame.setText("Closing slide.");
  addFooterNumber(slide, slideCursor);
}

function addSummaryClose(presentation) {
  const slide = addSlide(
    presentation,
    "Conclusion",
    "Validation, Results, and Future Work",
    "Source: final report testing, result, conclusion, and future work sections. This closing slide summarizes the project outcome and next direction."
  );
  addCard(slide, { left: 70, top: 140, width: 350, height: 398 }, C.panel, C.green);
  addText(slide, "Validation", { left: 100, top: 172, width: 260, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.green,
  });
  addBullets(slide, [
    "Unity playtests for mode objectives and combat feel",
    "Debug overlay for decisions, paths, targets, and score gaps",
    "Telemetry for stuck behavior, objective neglect, and confidence",
    "Regression fixes are committed as separate reviewable changes",
  ], 104, 228, 278, 13, C.muted, 44);

  addCard(slide, { left: 466, top: 140, width: 350, height: 398 }, C.panel2, C.amber);
  addText(slide, "Result", { left: 496, top: 172, width: 260, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.amber,
  });
  addText(slide,
    "The AI is a layered, objective-aware system with team blackboard coordination, brawler-specific tactics, game-mode macro strategy, map-aware navigation, recovery hardening, and visible validation tools.",
    { left: 502, top: 232, width: 262, height: 150 },
    { fontSize: 16, color: C.text, alignment: "center", lineSpacing: 1.08 }
  );
  addPill(slide, "Modular", 520, 418, 96, C.cyan, "#06111f");
  addPill(slide, "Tunable", 636, 418, 96, C.green, "#06111f");
  addPill(slide, "Inspectable", 558, 462, 136, C.purple);

  addCard(slide, { left: 862, top: 140, width: 350, height: 398 }, C.panel, C.magenta);
  addText(slide, "Future Work", { left: 892, top: 172, width: 260, height: 30 }, {
    fontSize: 23,
    bold: true,
    color: C.magenta,
  });
  addBullets(slide, [
    "Automated AI gauntlets",
    "Richer semantic maps",
    "More brawler packs and counters",
    "Optional ML-Agents experiments later",
    "Network-ready authority boundaries",
  ], 896, 228, 276, 13, C.muted, 40);

  addShape(slide, "rect", { left: 182, top: 586, width: 916, height: 2 }, C.line, "none");
  addText(slide, "References: project proposal, interim report, final report, Unity documentation, A* pathfinding, Artificial Intelligence for Games, and Artificial Intelligence and Games.", {
    left: 166,
    top: 606,
    width: 948,
    height: 34,
  }, {
    fontSize: 13,
    color: C.dim,
    alignment: "center",
  });
}

async function main() {
  await fs.rm(RENDER_DIR, { recursive: true, force: true });
  await fs.rm(LAYOUT_DIR, { recursive: true, force: true });
  await fs.mkdir(RENDER_DIR, { recursive: true });
  await fs.mkdir(LAYOUT_DIR, { recursive: true });

  slideCursor = 0;
  const presentation = await PresentationFile.importPptx(await FileBlob.load(STARTER));

  addTitleSlide(presentation);
  addProjectContext(presentation);
  addProblem(presentation);
  addWrongFlow(presentation);
  addArchitecture(presentation);
  addTickLoop(presentation);
  addModeStrategy(presentation);
  addBrawlerRoles(presentation);
  addNavigation(presentation);
  addAbilityIntelligence(presentation);
  await addEvidenceGemGrab(presentation);
  await addEvidenceModes(presentation);
  await addDebugEvidence(presentation);
  await addUiEvidence(presentation);
  addCodeEvidence(presentation);
  addSummaryClose(presentation);

  const inspect = await presentation.inspect({
    kind: "slide,textbox,shape,image,notes",
    maxChars: 30000,
  });
  await fs.writeFile(`${TMP}/final-inspect.ndjson`, inspect.ndjson);

  for (const [index, slide] of presentation.slides.items.entries()) {
    const stem = `slide-${String(index + 1).padStart(2, "0")}`;
    await writeBlob(`${RENDER_DIR}/${stem}.png`, await presentation.export({ slide, format: "png", scale: 1 }));
    await fs.writeFile(`${LAYOUT_DIR}/${stem}.layout.json`, await (await slide.export({ format: "layout" })).text());
  }

  await writeBlob(MONTAGE, await presentation.export({ format: "webp", montage: true, scale: 1 }));

  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(FINAL);

  await fs.writeFile(
    "/Users/vapronix/Documents/GitHub/MOBA/Csharp-HighPerf-MOBA-Core/Reports/AdvancedGameAI_FinalPresentation_AA.SC.U3BCA2307092.pptx.inspect.ndjson",
    inspect.ndjson
  );
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
