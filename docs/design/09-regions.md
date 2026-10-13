# 09 — Region Shapes: borders that look like geography

Status: **AGREED direction (Greg, 2026-10-13)** — layer 1 in M1, layers 2–3 in M2 · Last updated 2026-10-13

## The problem
Voronoi cells over a jittered grid give convex polygons with straight edges and similar sizes. The result looks like a grid, not a map. Real regions, and Genshin's nations, are shaped by geography: **borders follow mountain ridges, rivers, coastlines and straits**. Mondstadt and Liyue are separated by mountains and a river valley, Inazuma by sea. Crossing a border is an *event*: you crest a pass, look down, and a new land opens up.

## Why not regions from Perlin noise alone?
Thresholding a noise map gives organic blobs, but:
- **No identity:** a region needs a center, a name, a faction and a theme. Blobs have none of these.
- **Fragmentation:** you get islands of region A scattered inside B, and slivers along edges.
- **No size control:** you can't ask for "a big highland nation next to a small coastal one."

Noise is great for **biomes** (continuous climate: grassland fades into forest) and that's how biomes already work (07). **Regions** (nations or territories with identity) need structure.

## Proposal: three layers

### 1. Layout: warped, weighted cells (cheap, now)
- Region centers come from seeded points with **variable weights**, so sizes vary from small valleys to vast plateaus.
- **Domain warping** distorts the lookup so borders wander like coastlines.
- This is already requested from the World stream for M1, behind an `IRegionField` interface.

### 2. Borders that follow geography (rule-based, next)
Each point belongs to the region center it's **"closest" to by travel effort, not by straight-line distance**:
- Distance is measured as a **cost to walk there**: climbing, crossing ridges and fording rivers cost more.
- So a region's border naturally lands **on the ridge line or the river** between two centers, the way watersheds and real political borders form.
- **Rules on top:**
  - Every border must be a natural barrier: a mountain ridge, a river gorge, a strait or a dense forest. If no barrier exists between two centers, the terrain generator **raises one**, such as a ridge along the border with a few low **passes**.
  - Each border gets **1–3 passes or crossings** (a mountain pass, a ford, a bridge site, a narrow strait). These are natural spots for gatehouses, ruins and ambushes, and the "crest the pass, see the new land" moment.
  - Regions are contiguous: no fragments.
- **Implementation:** a coarse grid (about 32 m cells) per macro-tile (about 4 km) with a few cells of overlap. Run a multi-source Dijkstra from nearby region centers over the coarse terrain cost, then cache it. It stays deterministic and infinite (tiles computed on demand), and it runs off the main thread.

### 3. Identity drives terrain style
A region isn't just different numbers. Its theme picks **feature generators**:
- **Karst:** stone pillars and terraces (Liyue-like)
- **Rolling meadow:** soft hills, lone trees, windmill sites (Mondstadt-like)
- **Archipelago:** island chains and straits (Inazuma-like)
- **Badlands:** mesas and canyons
- **Highland:** plateaus and cliffs
- **Wetland:** marsh and stilt-villages
- **Old forest:** giant trees and deep valleys

Biomes from climate still vary *inside* a region (beach, grassland, snowcap). The region sets the *character*.

## How it fits the pillars
- **Secrets all the way down:** passes, gorges and straits are natural places for ruins, landmarks and the first view of a new land.
- **Nothing is locked:** barriers have passes. They shape the route without walling anyone out, and a bold climber can go over the ridge instead.
- **Generated, never random:** borders explain themselves ("the river is the border"), and the world graph can say "the Vel'Maren coast ends at the Greyspine ridge."

## Plan
- **M1 (now):** layer 1, warped and weighted cells behind `IRegionField`.
- **M2:** layer 2, terrain-cost borders, barrier rules and passes, plus layer 3 with the first 3 feature generators. Written up as a proper spec after the M1 review.

## Question for Greg
Does the M2 direction match what you meant: **borders snap to mountains, rivers and seas, with a few passes**, plus each region having a recognizable terrain character?
