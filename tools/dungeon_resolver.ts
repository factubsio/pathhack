// --- types ---

export interface BranchTemplate {
    id: string;
    name: string;
    dir: "up" | "down";
    color: string;

    entry?: string;                     // incoming edge: template id of entry floor (default: first)

    depth_range?: [number, number];     // constraint mode only

    // defaults inherited by rules that don't override
    default_algorithm_pool?: string[];
    default_wall_color?: string;
    default_floor_color?: string;
    default_behaviour?: string;

    // exactly one of these
    linear?: LinearPlacementRule[];
    constraints?: ConstraintPlacementRule[];

    // resolution state
    unresolved_children?: number;
}

export interface LevelTemplate {
    id: string;
    behaviour_id?: string;              // runtime behaviour, inherited from branch if unset
    algorithm?: string | string[];      // fixed, pick-one, or null (inherit from branch)
    variants?: string[];                // hand-painted level ids, pick one
    wall_color?: string;
    floor_color?: string;
    outdoors?: boolean;
    no_branch_entrance?: boolean;
    has_stairs_up?: boolean;            // hand-painted level manages its own stairs
    has_stairs_down?: boolean;
    has_portal_to_parent?: boolean;
    has_portal_to_child?: boolean;
}

export interface LinearPlacementRule {
    template: LevelTemplate;
    count: number | [number, number];
    branch_id?: string;                 // outgoing edge: child branch that exits from this segment
}

export interface ConstraintPlacementRule {
    template?: LevelTemplate;
    branch_id?: string;                 // outgoing edge: child branch that exits from this floor
    depth: [number, number];            // [shallow, deep], negative = from bottom
    relative_to?: string;               // if set, depth is offset from this rule's resolved position
    probability?: number;               // 100 = always (default), 0 = try but don't fail
}

export interface ResolvedLevel {
    template_id: string;
    behaviour_id?: string;
    algorithm?: string;
    variants?: string[];
    wall_color?: string;
    floor_color?: string;
    outdoors?: boolean;
    no_branch_entrance?: boolean;
    branch_id?: string;                 // outgoing edge
}

export interface ResolvedBranch {
    id: string;
    name: string;
    dir: "up" | "down";
    color: string;
    entry: number;                      // index of entry floor
    levels: ResolvedLevel[];
}

// --- resolution ---

function collectChildBranchIds(branch: BranchTemplate): string[] {
    const ids = new Set<string>();
    for (const rule of branch.linear ?? [])
        if (rule.branch_id) ids.add(rule.branch_id);
    for (const rule of branch.constraints ?? [])
        if (rule.branch_id) ids.add(rule.branch_id);
    return [...ids];
}

export function resolve(templates: BranchTemplate[]): ResolvedBranch[] {
    const parentOf = new Map<string, BranchTemplate>();
    for (const t of templates) {
        t.unresolved_children = collectChildBranchIds(t).length;
        for (const c of collectChildBranchIds(t))
            parentOf.set(c, t);
    }

    const remaining = [...templates];
    const results: ResolvedBranch[] = [];

    while (remaining.length > 0) {
        const idx = remaining.findIndex(t => t.unresolved_children === 0);
        if (idx < 0) throw new Error("cycle or unresolvable");

        const next = remaining.splice(idx, 1)[0];
        results.push(resolveBranch(next));

        const parent = parentOf.get(next.id);
        if (parent) parent.unresolved_children!--;
    }

    return results;
}

function resolveBranch(branch: BranchTemplate): ResolvedBranch {
    const levels: ResolvedLevel[] = branch.linear
        ? resolveLinear(branch)
        : resolveConstraint(branch);

    const entry = branch.entry
        ? levels.findIndex(l => l.template_id === branch.entry)
        : 0;

    return {
        id: branch.id,
        name: branch.name,
        dir: branch.dir,
        color: branch.color,
        entry,
        levels,
    };
}

function resolveLevel(rule: { template?: LevelTemplate, branch_id?: string }, branch: BranchTemplate): ResolvedLevel {
    const t = rule.template;
    return {
        template_id: t?.id ?? "default",
        behaviour_id: t?.behaviour_id ?? branch.default_behaviour,
        algorithm: pickOne(t?.algorithm) ?? pickOne(branch.default_algorithm_pool),
        variants: t?.variants,
        wall_color: t?.wall_color ?? branch.default_wall_color,
        floor_color: t?.floor_color ?? branch.default_floor_color,
        outdoors: t?.outdoors,
        no_branch_entrance: t?.no_branch_entrance,
        branch_id: rule.branch_id,
    };
}

function resolveLinear(branch: BranchTemplate): ResolvedLevel[] {
    const levels: ResolvedLevel[] = [];
    const candidates = new Map<string, number[]>();

    for (const rule of branch.linear!) {
        const count = typeof rule.count === "number" ? rule.count : rngRange(rule.count[0], rule.count[1]);
        const startIdx = levels.length;
        for (let i = 0; i < count; i++)
            levels.push(resolveLevel({ template: rule.template }, branch));

        if (rule.branch_id) {
            const existing = candidates.get(rule.branch_id) ?? [];
            for (let i = startIdx; i < levels.length; i++)
                existing.push(i);
            candidates.set(rule.branch_id, existing);
        }
    }

    const used = new Set<number>();
    for (const [branchId, floors] of candidates) {
        const available = floors.filter(f => !used.has(f));
        if (available.length === 0) throw new Error(`no available floor for branch '${branchId}' in '${branch.id}'`);
        const pick = available[rngRange(0, available.length - 1)];
        levels[pick].branch_id = branchId;
        used.add(pick);
    }

    return levels;
}

function resolveConstraint(branch: BranchTemplate): ResolvedLevel[] {
    const depth = rngRange(branch.depth_range![0], branch.depth_range![1]);
    const levels: ResolvedLevel[] = Array.from({ length: depth }, () => resolveLevel({}, branch));

    const rules = (branch.constraints ?? []).map(r => ({
        ...r,
        depth: [
            r.depth[0] < 0 ? depth + r.depth[0] : r.depth[0],
            r.depth[1] < 0 ? depth + r.depth[1] : r.depth[1],
        ] as [number, number],
    }));

    const placed = new Map<string, number>();

    function place(idx: number): boolean {
        if (idx >= rules.length) return true;

        const rule = rules[idx];

        if (rule.probability !== undefined && rule.probability < 100) {
            if (rngRange(1, 100) > rule.probability)
                return place(idx + 1);
        }

        let [min, max] = rule.depth;

        if (rule.relative_to) {
            const base = placed.get(rule.relative_to);
            if (base === undefined) return false;
            min += base;
            max += base;
        }

        min = Math.max(0, min);
        max = Math.min(depth - 1, max);

        const valid: number[] = [];
        for (let d = min; d <= max; d++)
            if (levels[d].template_id === "default") valid.push(d);

        for (let i = valid.length - 1; i > 0; i--) {
            const j = rngRange(0, i);
            [valid[i], valid[j]] = [valid[j], valid[i]];
        }

        for (const d of valid) {
            levels[d] = resolveLevel(rule, branch);
            if (rule.template) placed.set(rule.template.id, d);

            if (place(idx + 1)) return true;

            levels[d] = resolveLevel({}, branch);
            if (rule.template) placed.delete(rule.template.id);
        }

        return rule.probability === 0;
    }

    if (!place(0)) throw new Error(`constraint solver failed for '${branch.id}'`);

    return levels;
}

// --- helpers ---

function pickOne<T>(v: T | T[] | undefined): T | undefined {
    if (v === undefined) return undefined;
    if (Array.isArray(v)) return v[Math.floor(Math.random() * v.length)];
    return v;
}

function rngRange(min: number, max: number): number {
    return min + Math.floor(Math.random() * (max - min + 1));
}
