#!/usr/bin/env python3
"""
generate-workflow-diagrams.py
=============================================================================
Generates PlantUML state-machine diagrams from workflow-definition.json.

Each diagram is written to ../docs/workflow/generated/ relative to this file.

Usage:
    cd workflow/
    python generate-workflow-diagrams.py                   # generate all
    python generate-workflow-diagrams.py simulation-saga   # generate one

Available diagram names:
    case-state-machine
    simulation-saga
    contouring-saga
    planning-saga
    treatment-saga
    compensation-flow
=============================================================================
"""

import json
import sys
from pathlib import Path
from typing import Generator

# --------------------------------------------------------------------------- #
# Paths
# --------------------------------------------------------------------------- #

DEFINITION_FILE = Path(__file__).parent / "workflow-definition.json"
OUTPUT_DIR = Path(__file__).parent.parent / "docs" / "workflow" / "generated"

# --------------------------------------------------------------------------- #
# Phase grouping for each saga diagram
# --------------------------------------------------------------------------- #

SAGA_PHASES: dict[str, list[str]] = {
    "simulation-saga": ["SIM"],
    "contouring-saga": ["IMG", "CON", "REV"],
    "planning-saga":   ["PLN", "RX", "QA"],
    "treatment-saga":  ["TRT", "POST"],
}

# Human-readable trigger type labels used on arrows
TRIGGER_BADGE: dict[str, str] = {
    "User":          "User",
    "System":        "Sys",
    "ExternalEvent": "Ext",
    "Timer":         "Timer",
}

# --------------------------------------------------------------------------- #
# Data helpers
# --------------------------------------------------------------------------- #

def load_definition() -> dict:
    """Load and return the parsed workflow-definition.json."""
    with DEFINITION_FILE.open(encoding="utf-8") as fh:
        return json.load(fh)


def safe_id(name: str) -> str:
    """Convert a human name into a valid PlantUML state identifier."""
    return (
        name.replace("-", "_")
            .replace(" ", "_")
            .replace("&", "and")
            .replace("/", "_")
            .replace(".", "_")
    )


def phases_by_id(defn: dict) -> dict[str, dict]:
    return {p["id"]: p for p in defn["phases"]}


def statuses_by_phase(defn: dict) -> dict[str, list[str]]:
    """Return mapping of phase_id → [status_name, ...] in definition order."""
    result: dict[str, list[str]] = {}
    for s in defn["statuses"]:
        result.setdefault(s["phase"], []).append(s["name"])
    return result


# --------------------------------------------------------------------------- #
# PlantUML building blocks
# --------------------------------------------------------------------------- #

def _skinparam() -> list[str]:
    return [
        "skinparam DefaultFontName Arial",
        "skinparam Shadowing false",
        "skinparam ArrowColor #444444",
        "skinparam state {",
        "  BackgroundColor #F4F6F8",
        "  BorderColor #5A6473",
        "  FontSize 11",
        "  ArrowFontSize 9",
        "  ArrowColor #444444",
        "}",
    ]


def _header(title: str, direction: str = "top to bottom") -> list[str]:
    lines = ["@startuml", f"title {title}", ""]
    if direction == "left to right":
        lines.append("left to right direction")
    lines += ["hide empty description", ""] + _skinparam() + [""]
    return lines


def _footer() -> list[str]:
    return ["", "@enduml"]


def _arrow_label(t: dict) -> str:
    """
    Build a compact multi-line label for a state-machine arrow.
    Format:  CODE TriggerName\\n«Role» [TriggerType]
    """
    badge = TRIGGER_BADGE.get(t["triggerType"], t["triggerType"])
    first = f"{t['code']} {t['triggerName']} [{badge}]"
    if t.get("requiredRole"):
        return f"{first}\\n\u00ab{t['requiredRole']}\u00bb"
    return first


def _transitions_for(defn: dict, phase_ids: set[str]) -> list[dict]:
    return [t for t in defn["transitions"] if t["phase"] in phase_ids]


def _emit_phase_block(
    phase: dict,
    status_names: list[str],
    lines: list[str],
) -> None:
    """Append a compound state block for one phase."""
    pid = phase["id"]
    label = f"{phase['number']}. {phase['name']}"
    lines.append(f'state "{label}" as {pid} {{')
    for s in status_names:
        lines.append(f"  state {s}")
    lines.append("}")
    lines.append("")


def _emit_arrows(transitions: list[dict], lines: list[str]) -> None:
    """
    Emit one PlantUML arrow per (fromStatus, toStatus) pair.
    Multi-fromStatus transitions generate multiple arrows sharing the same label.
    Self-transitions (A → A) are rendered with a note suffix.
    """
    for t in transitions:
        label = _arrow_label(t)
        for from_s in t["fromStatuses"]:
            if from_s == t["toStatus"]:
                # Self-transition: PlantUML renders these as a loop arrow
                lines.append(f"{from_s} --> {from_s} : {label}")
            else:
                lines.append(f"{from_s} --> {t['toStatus']} : {label}")


# --------------------------------------------------------------------------- #
# Generator: full case state machine
# --------------------------------------------------------------------------- #

def gen_case_state_machine(defn: dict) -> str:
    """
    Full state machine with all 40 statuses and 53 transitions,
    grouped into nine compound phase blocks.
    """
    phase_map = phases_by_id(defn)
    status_map = statuses_by_phase(defn)

    lines = _header("Radiotherapy Case \u2013 Full State Machine", "left to right")

    lines.append("[*] --> Draft")
    lines.append("")

    # --- Phase compound state blocks ---
    for pid, phase in phase_map.items():
        statuses = status_map.get(pid, [])
        if statuses:
            _emit_phase_block(phase, statuses, lines)

    # Terminal state (not inside any phase block)
    lines.append("state Cancelled #FFC0CB : \u25ce Cancelled \u2013 terminal")
    lines.append("Archived --> [*]")
    lines.append("")

    # --- Transitions grouped by phase ---
    for pid, phase in phase_map.items():
        phase_transitions = [t for t in defn["transitions"] if t["phase"] == pid]
        if not phase_transitions:
            continue
        lines.append(
            f"' \u2500\u2500\u2500 Phase {phase['number']}: {phase['name']} "
            f"\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500"
        )
        _emit_arrows(phase_transitions, lines)
        lines.append("")

    lines += _footer()
    return "\n".join(lines)


# --------------------------------------------------------------------------- #
# Generator: saga (phase-filtered state machine)
# --------------------------------------------------------------------------- #

def gen_saga(defn: dict, saga_name: str, phase_ids: list[str]) -> str:
    """
    Phase-filtered state machine for a named saga.
    Statuses belonging to the selected phases are grouped into compound state blocks.
    Statuses that border the saga (entry / exit points from other phases) are shown
    as standalone external states.
    """
    phase_id_set = set(phase_ids)
    phase_map = phases_by_id(defn)
    status_map = statuses_by_phase(defn)
    transitions = _transitions_for(defn, phase_id_set)

    # Collect every status name referenced by these transitions
    referenced: set[str] = set()
    for t in transitions:
        for s in t["fromStatuses"]:
            referenced.add(s)
        referenced.add(t["toStatus"])

    # Statuses that belong to the selected phases (shown inside compound blocks)
    internal: set[str] = {
        s
        for pid in phase_ids
        for s in status_map.get(pid, [])
    }

    # Statuses referenced by transitions but belonging to other phases
    external: set[str] = referenced - internal

    title = saga_name.replace("-", " ").title()
    lines = _header(f"Radiotherapy Workflow \u2013 {title}")

    # Initial pseudo-arrow to first fromStatus of first transition
    first_from = transitions[0]["fromStatuses"][0] if transitions else "Draft"
    lines.append(f"[*] --> {first_from}")
    lines.append("")

    # Compound phase blocks (only for phases in this saga)
    for pid in phase_ids:
        phase = phase_map.get(pid)
        statuses = status_map.get(pid, [])
        if phase and statuses:
            _emit_phase_block(phase, statuses, lines)

    # Known terminal statuses — rendered separately, not as generic externals
    _terminal = {"Cancelled", "Archived"}

    # External boundary states (entry/exit points from other phases, non-terminal)
    for ext in sorted(external - _terminal):
        lines.append(f"state {ext} #DDEEFF : \u00ab external \u00bb")
    if external - _terminal:
        lines.append("")

    # Terminal states referenced by these transitions
    if "Cancelled" in referenced:
        lines.append("state Cancelled #FFC0CB : \u25ce terminal")
    if "Archived" in referenced:
        lines.append("state Archived #D0F0D0 : \u25ce terminal")
    if _terminal & referenced:
        lines.append("")

    # Arrows
    lines.append(f"' \u2500\u2500\u2500 Transitions \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500")
    _emit_arrows(transitions, lines)
    lines.append("")

    lines += _footer()
    return "\n".join(lines)


# --------------------------------------------------------------------------- #
# Generator: compensation flow
# --------------------------------------------------------------------------- #

def gen_compensation_flow(defn: dict) -> str:
    """
    Compensation routing diagram.

    Each compensation definition is shown as a state node labelled with its
    code and failure condition.  An arrow leads to the target recovery status
    (or to a [*] terminal if there is no status change).  Nodes are grouped
    by the category field.
    """
    compensations = defn["compensations"]

    # Group compensations by category (preserving first-seen order)
    categories: dict[str, list[dict]] = {}
    for c in compensations:
        categories.setdefault(c["category"], []).append(c)

    lines = _header("Workflow Compensation Flows")

    # --- Category compound state blocks ---
    for cat, items in categories.items():
        cat_id = safe_id(cat)
        lines.append(f'state "{cat}" as {cat_id} {{')
        for c in items:
            cid = safe_id(c["code"])
            # Truncate long conditions to keep node labels readable
            cond = c["failureCondition"]
            if len(cond) > 55:
                cond = cond[:52] + "\u2026"
            lines.append(f'  state "{c["code"]}\\n{cond}" as {cid}')
        lines.append("}")
        lines.append("")

    # --- Compensation arrows ---
    lines.append(
        "' \u2500\u2500\u2500 Compensation links "
        "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500"
    )

    for c in compensations:
        cid = safe_id(c["code"])
        target = c.get("targetStatus")
        action = c["compensationAction"]
        # Compact the action label
        if len(action) > 55:
            action = action[:52] + "\u2026"
        retry = ""
        if c.get("retryPolicy"):
            retry = f"\\n[{c['retryPolicy']['strategy']}]"
        manual = ""
        if c.get("manualInterventionRequired"):
            manual = "\\n\u26a0 manual"

        label = f"{action}{retry}{manual}"

        if target:
            lines.append(f"{cid} --> {target} : {label}")
        else:
            lines.append(f"{cid} --> [*] : {label}")

    lines.append("")
    lines += _footer()
    return "\n".join(lines)


# --------------------------------------------------------------------------- #
# Output
# --------------------------------------------------------------------------- #

def write_puml(diagram_name: str, content: str) -> None:
    """Write diagram content to the output directory."""
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    dest = OUTPUT_DIR / f"{diagram_name}.puml"
    dest.write_text(content, encoding="utf-8")
    try:
        rel = dest.relative_to(Path.cwd())
    except ValueError:
        rel = dest
    print(f"  \u2713  {rel}")


# --------------------------------------------------------------------------- #
# Diagram registry
# --------------------------------------------------------------------------- #

def _build_generators(defn: dict) -> dict[str, str]:
    """Evaluate all generators and return a name → content mapping."""
    return {
        "case-state-machine": gen_case_state_machine(defn),
        "simulation-saga":    gen_saga(defn, "simulation-saga",  SAGA_PHASES["simulation-saga"]),
        "contouring-saga":    gen_saga(defn, "contouring-saga",  SAGA_PHASES["contouring-saga"]),
        "planning-saga":      gen_saga(defn, "planning-saga",    SAGA_PHASES["planning-saga"]),
        "treatment-saga":     gen_saga(defn, "treatment-saga",   SAGA_PHASES["treatment-saga"]),
        "compensation-flow":  gen_compensation_flow(defn),
    }


# --------------------------------------------------------------------------- #
# Entry point
# --------------------------------------------------------------------------- #

def main() -> None:
    defn = load_definition()
    all_generators = _build_generators(defn)

    requested = sys.argv[1:] or list(all_generators.keys())
    unknown = [n for n in requested if n not in all_generators]
    if unknown:
        available = ", ".join(all_generators)
        print(
            f"Unknown diagram(s): {', '.join(unknown)}\n"
            f"Available: {available}",
            file=sys.stderr,
        )
        sys.exit(1)

    print(f"Generating {len(requested)} diagram(s) → {OUTPUT_DIR}")
    for name in requested:
        write_puml(name, all_generators[name])
    print("Done.")


if __name__ == "__main__":
    main()
