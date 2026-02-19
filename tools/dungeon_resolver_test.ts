import { BranchTemplate, resolve } from "./dungeon_resolver";

const OptionalCrypt: BranchTemplate = {
    id: "optional_crypt",
    name: "Optional Crypt",
    dir: "down",
    color: "gray",
    linear: [
        { template: { id: "crypt", variants: ["crypt_a", "crypt_b"] }, count: 1 },
    ],
};

const SerpentsSkull: BranchTemplate = {
    id: "serpents_skull",
    name: "Serpent's Skull",
    dir: "down",
    color: "dark_yellow",
    default_behaviour: "ss_jungle",
    linear: [
        { template: { id: "ss_shore", variants: ["ss_shore_beached", "ss_shore_debris"], outdoors: true, has_portal_to_parent: true, has_stairs_down: true }, count: 1 },
        { template: { id: "ss_jungle", algorithm: "outdoor_ca_open", outdoors: true }, count: [2, 3], branch_id: "optional_crypt" },
        { template: { id: "ss_saventh_yhi", variants: ["ss_saventh_yhi_a", "ss_saventh_yhi_b"], outdoors: true, behaviour_id: "ss_city" }, count: 1 },
        { template: { id: "ss_vaults", algorithm: ["worley", "worley_warren", "ca"], behaviour_id: "ss_serpentfolk" }, count: [2, 3], branch_id: "optional_crypt" },
        { template: { id: "ss_sanctum", variants: ["ss_sanctum"], behaviour_id: "ss_serpentfolk_boss", no_branch_entrance: true }, count: 1 },
    ],
};

const Dungeon: BranchTemplate = {
    id: "dungeon",
    name: "Dungeon",
    dir: "down",
    color: "yellow",
    depth_range: [20, 20],
    constraints: [
        { branch_id: "serpents_skull", depth: [3, 7] },
    ],
};

const results = resolve([OptionalCrypt, SerpentsSkull, Dungeon]);

for (const branch of results) {
    console.log(`\n=== ${branch.name} (${branch.levels.length} floors, entry=${branch.entry}) ===`);
    let i = 0;
    while (i < branch.levels.length) {
        const l = branch.levels[i];
        if (l.template_id === "default" && !l.branch_id) {
            let j = i;
            while (j < branch.levels.length && branch.levels[j].template_id === "default" && !branch.levels[j].branch_id) j++;
            console.log(`  ${i}${j - 1 > i ? `-${j - 1}` : ""}: default (${j - i})`);
            i = j;
        } else {
            const parts = [l.template_id];
            if (l.behaviour_id) parts.push(`beh:${l.behaviour_id}`);
            if (l.algorithm) parts.push(`algo:${l.algorithm}`);
            if (l.branch_id) parts.push(`-> ${l.branch_id}`);
            if (l.outdoors) parts.push("outdoor");
            console.log(`  ${i}: ${parts.join(", ")}`);
            i++;
        }
    }
}
