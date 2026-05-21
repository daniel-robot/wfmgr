import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  SimpleChanges
} from '@angular/core';
import {
  WorkflowMetaItem,
  WorkflowTransition
} from '../../core/models/workflow.models';

interface GraphNode {
  code: string;
  x: number;
  y: number;
  width: number;
  height: number;
  reachable: boolean;
}

type EdgeKind = 'saved' | 'kept' | 'added' | 'removed';

interface GraphEdge {
  from: string;
  to: string;
  kind: EdgeKind;
  codes: string[];
  /** Multi-edge fan-out index between same (from,to) pair. */
  index: number;
  pathD: string;
  labelX: number;
  labelY: number;
}

const NODE_W = 132;
const NODE_H = 30;
const COL_GAP = 80;
const ROW_GAP = 16;
const PAD = 24;

/**
 * Live workflow-transition graph. Pure SVG, no dependencies. Re-lays out
 * automatically whenever its inputs change — driven by the parent's form
 * values so authors see immediate impact while editing.
 */
@Component({
  selector: 'app-workflow-graph',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="legend">
      <span><span class="swatch saved"></span> existing</span>
      <span><span class="swatch kept"></span> editing — kept</span>
      <span><span class="swatch added"></span> added</span>
      <span><span class="swatch removed"></span> removed</span>
    </div>
    <div class="scroll" *ngIf="nodes.length > 0; else emptyState">
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height"
           [attr.width]="width"
           [attr.height]="height"
           role="img"
           aria-label="Workflow transitions diagram">
        <defs>
          <marker id="arrow-saved" viewBox="0 0 10 10" refX="9" refY="5"
                  markerWidth="6" markerHeight="6" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 Z" fill="#90a4ae" />
          </marker>
          <marker id="arrow-kept" viewBox="0 0 10 10" refX="9" refY="5"
                  markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 Z" fill="#1976d2" />
          </marker>
          <marker id="arrow-added" viewBox="0 0 10 10" refX="9" refY="5"
                  markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 Z" fill="#2e7d32" />
          </marker>
          <marker id="arrow-removed" viewBox="0 0 10 10" refX="9" refY="5"
                  markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 Z" fill="#b71c1c" />
          </marker>
        </defs>

        <!-- Edges first so nodes render above them -->
        <g class="edges">
          <g *ngFor="let e of edges; trackBy: trackEdge"
             [class]="'edge edge-' + e.kind">
            <path [attr.d]="e.pathD"
                  fill="none"
                  [attr.marker-end]="'url(#arrow-' + e.kind + ')'">
              <title>{{ e.from }} → {{ e.to }}{{ e.codes.length ? ' (' + e.codes.join(', ') + ')' : '' }}</title>
            </path>
          </g>
        </g>

        <!-- Nodes -->
        <g class="nodes">
          <g *ngFor="let n of nodes; trackBy: trackNode"
             class="node"
             [class.unreachable]="!n.reachable"
             [class.touched]="touchedStatuses.has(n.code)"
             [attr.transform]="'translate(' + n.x + ',' + n.y + ')'">
            <rect [attr.width]="n.width" [attr.height]="n.height"
                  rx="6" ry="6" />
            <text [attr.x]="n.width / 2" [attr.y]="n.height / 2 + 4"
                  text-anchor="middle">{{ n.code }}</text>
          </g>
        </g>
      </svg>
    </div>
    <ng-template #emptyState>
      <p class="muted small">No transitions to graph yet.</p>
    </ng-template>
  `,
  styles: [`
    :host { display: block; }
    .legend {
      display: flex; gap: 0.75rem; flex-wrap: wrap;
      font-size: 0.72rem; color: #555; margin-bottom: 0.4rem;
    }
    .legend .swatch {
      display: inline-block; width: 0.9rem; height: 0.18rem;
      vertical-align: middle; margin-right: 0.25rem;
    }
    .legend .swatch.saved { background: #90a4ae; }
    .legend .swatch.kept { background: #1976d2; height: 0.22rem; }
    .legend .swatch.added { background: #2e7d32; height: 0.22rem; }
    .legend .swatch.removed { background: #b71c1c; height: 0.22rem; outline: 1px dashed #b71c1c; outline-offset: -1px; }

    .scroll { overflow: auto; max-height: 420px; border: 1px solid #e6e9ee; border-radius: 6px; background: #fcfcfd; padding: 4px; }
    svg { display: block; }

    .node rect { fill: #f0f4f8; stroke: #b0bec5; stroke-width: 1; }
    .node text { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; fill: #263238; pointer-events: none; }
    .node.unreachable rect { fill: #fafafa; stroke-dasharray: 3 3; stroke: #cfd8dc; }
    .node.unreachable text { fill: #90a4ae; }
    .node.touched rect { stroke: #1976d2; stroke-width: 2; fill: #e3f2fd; }

    .edge-saved path { stroke: #cfd8dc; stroke-width: 1.2; opacity: 0.85; }
    .edge-kept path { stroke: #1976d2; stroke-width: 2; }
    .edge-added path { stroke: #2e7d32; stroke-width: 2.2; }
    .edge-removed path { stroke: #b71c1c; stroke-width: 1.6; stroke-dasharray: 4 3; }
    .muted { color: #888; }
    .small { font-size: 0.78rem; }
  `]
})
export class WorkflowGraphComponent implements OnChanges {
  @Input() allTransitions: WorkflowTransition[] = [];
  @Input() statuses: WorkflowMetaItem[] = [];
  /** Id of the transition currently being edited; null when creating new. */
  @Input() editingId: string | null = null;
  /** Code of the in-progress edit (display only). */
  @Input() editingCode = '';
  @Input() editingFromStatuses: string[] = [];
  @Input() editingToStatus = '';
  /** True when the editor is open; controls whether pending edges are overlaid. */
  @Input() editingActive = false;

  nodes: GraphNode[] = [];
  edges: GraphEdge[] = [];
  width = 0;
  height = 0;
  touchedStatuses = new Set<string>();

  trackNode = (_: number, n: GraphNode) => n.code;
  trackEdge = (_: number, e: GraphEdge) => `${e.from}→${e.to}#${e.index}`;

  ngOnChanges(_: SimpleChanges): void {
    this.rebuild();
  }

  // ── Build ──────────────────────────────────────────────────────────────
  private rebuild(): void {
    const editing = this.editingActive;
    const editingId = this.editingId;

    // 1. Collect all statuses we need to show.
    const statusSet = new Set<string>();
    for (const s of this.statuses) statusSet.add(s.code);
    for (const t of this.allTransitions) {
      statusSet.add(t.toStatus);
      for (const f of t.fromStatuses) statusSet.add(f);
    }
    if (editing) {
      if (this.editingToStatus) statusSet.add(this.editingToStatus);
      for (const f of this.editingFromStatuses) statusSet.add(f);
    }

    // 2. Build edge list, tagging by kind.
    //    "Saved" edges = all transitions except the one currently being edited.
    //    For the edited transition we compare its saved edges against the form.
    const edges: Omit<GraphEdge, 'index' | 'pathD' | 'labelX' | 'labelY'>[] = [];

    const editingTrans =
      editing && editingId ? this.allTransitions.find((t) => t.id === editingId) : null;

    // Saved edges (everyone else, plus the editing one if not active).
    for (const t of this.allTransitions) {
      if (editing && editingId && t.id === editingId) continue;
      for (const f of t.fromStatuses) {
        edges.push({ from: f, to: t.toStatus, kind: 'saved', codes: [t.code] });
      }
    }

    // Editing overlay.
    this.touchedStatuses.clear();
    if (editing) {
      const pendingPairs = new Set<string>();
      const pendingTo = this.editingToStatus;
      if (pendingTo) {
        for (const f of this.editingFromStatuses) {
          pendingPairs.add(`${f}→${pendingTo}`);
          this.touchedStatuses.add(f);
          this.touchedStatuses.add(pendingTo);
        }
      }
      const savedPairs = new Set<string>();
      if (editingTrans) {
        for (const f of editingTrans.fromStatuses) {
          savedPairs.add(`${f}→${editingTrans.toStatus}`);
        }
      }

      // Kept (in both) and Added (in pending only).
      if (pendingTo) {
        for (const f of this.editingFromStatuses) {
          const key = `${f}→${pendingTo}`;
          const kind: EdgeKind = savedPairs.has(key) ? 'kept' : 'added';
          edges.push({
            from: f,
            to: pendingTo,
            kind,
            codes: [this.editingCode || '(new)']
          });
        }
      }
      // Removed (in saved only).
      if (editingTrans) {
        for (const f of editingTrans.fromStatuses) {
          const key = `${f}→${editingTrans.toStatus}`;
          if (!pendingPairs.has(key)) {
            edges.push({
              from: f,
              to: editingTrans.toStatus,
              kind: 'removed',
              codes: [editingTrans.code]
            });
            this.touchedStatuses.add(f);
            this.touchedStatuses.add(editingTrans.toStatus);
          }
        }
      }
    }

    // 3. Compute level (column) for each status via BFS from "Submitted".
    const level = this.computeLevels(statusSet, edges);

    // 4. Group by level → columns; sort within column.
    const byLevel = new Map<number, string[]>();
    for (const code of statusSet) {
      const lv = level.get(code) ?? -1; // -1 = unreachable bucket
      let col = byLevel.get(lv);
      if (!col) {
        col = [];
        byLevel.set(lv, col);
      }
      col.push(code);
    }
    for (const arr of byLevel.values()) arr.sort();

    // 5. Place nodes. Unreachable column goes last.
    const sortedLevels = Array.from(byLevel.keys()).sort((a, b) => {
      if (a === -1) return 1;
      if (b === -1) return -1;
      return a - b;
    });
    const xByLevel = new Map<number, number>();
    sortedLevels.forEach((lv, i) => xByLevel.set(lv, PAD + i * (NODE_W + COL_GAP)));

    const nodes: GraphNode[] = [];
    const yByCode = new Map<string, number>();
    const xByCode = new Map<string, number>();
    let maxRowCount = 0;
    for (const lv of sortedLevels) {
      const col = byLevel.get(lv)!;
      maxRowCount = Math.max(maxRowCount, col.length);
      col.forEach((code, idx) => {
        const x = xByLevel.get(lv)!;
        const y = PAD + idx * (NODE_H + ROW_GAP);
        xByCode.set(code, x);
        yByCode.set(code, y);
        nodes.push({
          code,
          x,
          y,
          width: NODE_W,
          height: NODE_H,
          reachable: lv >= 0
        });
      });
    }

    // 6. Index parallel edges and compute paths.
    const fanCounts = new Map<string, number>();
    for (const e of edges) {
      const k = `${e.from}→${e.to}`;
      fanCounts.set(k, (fanCounts.get(k) ?? 0) + 1);
    }
    const fanSeen = new Map<string, number>();
    const finalEdges: GraphEdge[] = edges.map((e) => {
      const k = `${e.from}→${e.to}`;
      const total = fanCounts.get(k) ?? 1;
      const idx = fanSeen.get(k) ?? 0;
      fanSeen.set(k, idx + 1);
      const offset = (idx - (total - 1) / 2) * 14;
      const fx = xByCode.get(e.from);
      const fy = yByCode.get(e.from);
      const tx = xByCode.get(e.to);
      const ty = yByCode.get(e.to);
      const pathD =
        fx === undefined || fy === undefined || tx === undefined || ty === undefined
          ? ''
          : pathFor(
              fx,
              fy,
              tx,
              ty,
              offset,
              e.from === e.to
            );
      return {
        ...e,
        index: idx,
        pathD,
        labelX: fx ?? 0,
        labelY: fy ?? 0
      };
    });

    // 7. Compute SVG dimensions.
    this.width = sortedLevels.length === 0
      ? 0
      : PAD + sortedLevels.length * (NODE_W + COL_GAP);
    this.height = maxRowCount === 0 ? 0 : PAD * 2 + maxRowCount * (NODE_H + ROW_GAP);
    this.nodes = nodes;
    this.edges = finalEdges;
  }

  /** BFS levels from "Submitted" using only forward edges; unreachable → -1. */
  private computeLevels(
    statuses: Set<string>,
    edges: { from: string; to: string }[]
  ): Map<string, number> {
    const adjacency = new Map<string, Set<string>>();
    for (const code of statuses) adjacency.set(code, new Set());
    for (const e of edges) {
      if (!adjacency.has(e.from)) adjacency.set(e.from, new Set());
      adjacency.get(e.from)!.add(e.to);
    }
    const level = new Map<string, number>();
    const start = 'Submitted';
    if (!statuses.has(start)) return level;
    level.set(start, 0);
    const queue: string[] = [start];
    while (queue.length > 0) {
      const cur = queue.shift()!;
      const lv = level.get(cur)!;
      const neighbors = adjacency.get(cur);
      if (!neighbors) continue;
      for (const n of neighbors) {
        if (n === cur) continue; // ignore self for layout depth
        if (!level.has(n)) {
          level.set(n, lv + 1);
          queue.push(n);
        }
      }
    }
    return level;
  }
}

function pathFor(
  fx: number,
  fy: number,
  tx: number,
  ty: number,
  offset: number,
  selfLoop: boolean
): string {
  const sx = fx + NODE_W;
  const sy = fy + NODE_H / 2;
  const ex = tx;
  const ey = ty + NODE_H / 2;

  if (selfLoop) {
    // Small loop above the node.
    const cx = fx + NODE_W / 2;
    const top = fy;
    const r = 18;
    return `M ${cx - 8} ${top}
            C ${cx - r} ${top - r * 1.6},
              ${cx + r} ${top - r * 1.6},
              ${cx + 8} ${top}`;
  }

  // Forward edge — cubic bezier with horizontal control points + vertical offset.
  if (ex >= sx) {
    const dx = Math.max(40, (ex - sx) / 2);
    const c1x = sx + dx;
    const c1y = sy + offset;
    const c2x = ex - dx;
    const c2y = ey + offset;
    return `M ${sx} ${sy} C ${c1x} ${c1y}, ${c2x} ${c2y}, ${ex} ${ey}`;
  }

  // Backward edge — route around the top with extra vertical clearance.
  const lift = 36 + Math.abs(offset);
  const c1x = sx + 40;
  const c1y = sy - lift;
  const c2x = ex - 40;
  const c2y = ey - lift;
  return `M ${sx} ${sy} C ${c1x} ${c1y}, ${c2x} ${c2y}, ${ex} ${ey}`;
}
